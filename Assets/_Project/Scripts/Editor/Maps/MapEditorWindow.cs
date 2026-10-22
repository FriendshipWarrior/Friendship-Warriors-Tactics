using System;
using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Props;
using FriendshipWarriors.Grid.Validation;
using FriendshipWarriors.Grid.View;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace FriendshipWarriors.Editor.Maps
{
    /// <summary>
    /// Paints <see cref="BattleMapData"/> assets: heights, terrain, slopes, water, props and deployment zones on a
    /// top-down grid, with a live 3D preview and validation. Left-click paints; right-click picks up the tile's
    /// values (or erases, for props and deployment). Every stroke is one undo step.
    /// </summary>
    public sealed class MapEditorWindow : EditorWindow
    {
        private enum Tool { Height, Terrain, Slope, Water, Props, Deploy }
        private enum HeightMode { Set, Raise, Lower }

        private static readonly string[] ToolNames = { "Height", "Terrain", "Slope", "Water", "Props", "Deploy" };
        private static readonly Color VoidColor = new(0.12f, 0.12f, 0.14f);
        private static readonly Color PlayerColor = new(0.30f, 0.90f, 0.40f);
        private static readonly Color EnemyColor = new(0.95f, 0.30f, 0.60f);
        private static readonly Color SelectionColor = new(1f, 0.85f, 0.2f);

        [SerializeField] private BattleMapData _map;
        [SerializeField] private TerrainPalette _palette;
        [SerializeField] private Tool _tool;
        [SerializeField] private HeightMode _heightMode = HeightMode.Raise;
        [SerializeField] private int _heightValue = 2;
        [SerializeField] private TerrainType _terrain = TerrainType.Grass;
        [SerializeField] private SlopeType _slope = SlopeType.RiseNorth;
        [SerializeField] private int _slopeHeight = 2;
        [SerializeField] private int _waterDepth = 1;
        [SerializeField] private PropDefinition _prop;
        [SerializeField] private DeploymentSide _deploySide = DeploymentSide.Player;
        [SerializeField] private int _brushSize = 1;
        [SerializeField] private float _cellSize = 32f;
        [SerializeField] private int _referenceJump = MapValidator.DefaultReferenceJump;

        private readonly HashSet<GridCoord> _strokeCells = new();
        private PropDefinition[] _props = Array.Empty<PropDefinition>();
        private List<MapIssue> _issues = new();
        private MapPreview _preview;
        private bool _previewDirty = true;
        private GridCoord? _hovered;
        private GridCoord? _selected;
        private int _strokeUndoGroup = -1;
        private bool _strokeRightButton;
        private Vector2 _gridScroll;
        private Vector2 _issueScroll;
        private int _newWidth;
        private int _newDepth;

        [MenuItem("Friendship Warriors/Map Editor")]
        public static void Open() => GetWindow<MapEditorWindow>("Map Editor").minSize = new Vector2(960f, 620f);

        public static void Open(BattleMapData map)
        {
            Open();
            GetWindow<MapEditorWindow>().SetMap(map);
        }

        /// <summary>Double-clicking a map asset opens it here instead of the Inspector.</summary>
        [OnOpenAsset]
        private static bool OnOpenAsset(EntityId entityId, int line)
        {
            if (EditorUtility.EntityIdToObject(entityId) is not BattleMapData map)
                return false;
            Open(map);
            return true;
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
            if (_palette == null)
                _palette = ProjectAssets.DefaultPalette;
            _props = ProjectAssets.FindAll<PropDefinition>();
            Undo.undoRedoPerformed += OnUndoRedo;
            if (_map != null)
                SetMap(_map);
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            _preview?.Dispose();
            _preview = null;
        }

        private void OnProjectChange()
        {
            _props = ProjectAssets.FindAll<PropDefinition>();
            Repaint();
        }

        private void SetMap(BattleMapData map)
        {
            _map = map;
            _selected = null;
            if (_map == null)
                return;
            _newWidth = _map.Width;
            _newDepth = _map.Depth;
            OnMapChanged();
        }

        private void OnUndoRedo()
        {
            if (_map == null)
                return;
            OnMapChanged();
            Repaint();
        }

        private void OnMapChanged()
        {
            _issues = MapValidator.Validate(_map, _referenceJump);
            _previewDirty = true;
        }

        // ------------------------------------------------------------------ GUI

        private void OnGUI()
        {
            DrawHeader();
            if (_map == null)
            {
                EditorGUILayout.HelpBox("Pick a Battle Map asset, or create one with New Map.", MessageType.Info);
                return;
            }

            DrawToolOptions();
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawGridPanel();
                DrawPreviewPanel();
            }
            DrawIssues();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var map = (BattleMapData)EditorGUILayout.ObjectField(_map, typeof(BattleMapData), false, GUILayout.Width(260f));
                if (map != _map)
                    SetMap(map);
                if (GUILayout.Button("New Map", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                    CreateMap();
                GUILayout.FlexibleSpace();
                EditorGUI.BeginChangeCheck();
                _palette = (TerrainPalette)EditorGUILayout.ObjectField("Palette", _palette, typeof(TerrainPalette), false, GUILayout.Width(300f));
                if (EditorGUI.EndChangeCheck())
                    _previewDirty = true;
            }

            if (_map == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                string mapName = EditorGUILayout.TextField("Name", _map.DisplayName);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_map, "Rename Map");
                    _map.DisplayName = mapName;
                    EditorUtility.SetDirty(_map);
                }

                GUILayout.Space(16f);
                EditorGUIUtility.labelWidth = 40f;
                _newWidth = Mathf.Clamp(EditorGUILayout.IntField("Width", _newWidth, GUILayout.Width(90f)), 1, 64);
                _newDepth = Mathf.Clamp(EditorGUILayout.IntField("Depth", _newDepth, GUILayout.Width(90f)), 1, 64);
                EditorGUIUtility.labelWidth = 0f;
                using (new EditorGUI.DisabledScope(_newWidth == _map.Width && _newDepth == _map.Depth))
                {
                    if (GUILayout.Button("Resize", GUILayout.Width(70f)))
                    {
                        Undo.RecordObject(_map, "Resize Map");
                        _map.Resize(_newWidth, _newDepth, fillHeight: 2);
                        EditorUtility.SetDirty(_map);
                        OnMapChanged();
                    }
                }
            }
        }

        private void DrawToolOptions()
        {
            _tool = (Tool)GUILayout.Toolbar((int)_tool, ToolNames);
            using (new EditorGUILayout.HorizontalScope())
            {
                switch (_tool)
                {
                    case Tool.Height:
                        _heightMode = (HeightMode)GUILayout.Toolbar((int)_heightMode, new[] { "Set", "Raise", "Lower" }, GUILayout.Width(210f));
                        _heightValue = HalfStepSlider(_heightMode == HeightMode.Set ? "Height" : "Amount", _heightValue,
                            _heightMode == HeightMode.Set ? 0 : 1, 40);
                        break;
                    case Tool.Terrain:
                        _terrain = (TerrainType)EditorGUILayout.EnumPopup("Terrain", _terrain, GUILayout.Width(300f));
                        break;
                    case Tool.Slope:
                        _slope = (SlopeType)EditorGUILayout.EnumPopup("Slope", _slope, GUILayout.Width(300f));
                        using (new EditorGUI.DisabledScope(_slope == SlopeType.Flat))
                            _slopeHeight = HalfStepSlider("Rise", _slopeHeight, 1, 12);
                        break;
                    case Tool.Water:
                        _waterDepth = EditorGUILayout.IntSlider("Depth", _waterDepth, 0, 7, GUILayout.Width(300f));
                        GUILayout.Label(_waterDepth >= Tile.DeepWaterDepth ? "Units here can't act." : "Units here can act.", EditorStyles.miniLabel);
                        break;
                    case Tool.Props:
                        DrawPropPicker();
                        break;
                    case Tool.Deploy:
                        _deploySide = (DeploymentSide)GUILayout.Toolbar((int)_deploySide - 1, new[] { "Player", "Enemy" }, GUILayout.Width(200f)) + 1;
                        break;
                }

                GUILayout.FlexibleSpace();
                _brushSize = EditorGUILayout.IntSlider("Brush", _brushSize, 1, 5, GUILayout.Width(260f));
            }
            EditorGUILayout.LabelField(ToolHint(), EditorStyles.miniLabel);
        }

        private string ToolHint() => _tool switch
        {
            Tool.Props => "Left-click places the prop. Right-click removes props.",
            Tool.Deploy => "Left-click adds tiles to the zone. Right-click removes them from any zone.",
            Tool.Height when _heightMode != HeightMode.Set => "Left-click to raise or lower by the amount. Right-click picks up the tile's height.",
            _ => "Left-click paints. Right-click picks up the tile's value.",
        };

        private void DrawPropPicker()
        {
            var props = _props;
            if (props.Length == 0)
            {
                GUILayout.Label("No Prop assets yet (Create > Friendship Warriors > Prop).", EditorStyles.miniLabel);
                return;
            }
            int index = Mathf.Max(0, Array.IndexOf(props, _prop));
            index = EditorGUILayout.Popup("Prop", index, props.Select(p => p.DisplayName).ToArray(), GUILayout.Width(300f));
            _prop = props[index];
        }

        /// <summary>A slider over half-h steps that shows the value the way FFT does (1.5h).</summary>
        private static int HalfStepSlider(string label, int steps, int min, int max)
        {
            float h = EditorGUILayout.Slider(label, steps / (float)GridMetrics.StepsPerH, min / 2f, max / 2f, GUILayout.Width(300f));
            return Mathf.RoundToInt(h * GridMetrics.StepsPerH);
        }

        private void DrawGridPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                _cellSize = EditorGUILayout.Slider("Cell Size", _cellSize, 16f, 56f);
                _gridScroll = EditorGUILayout.BeginScrollView(_gridScroll);
                var gridRect = GUILayoutUtility.GetRect(_map.Width * _cellSize, _map.Depth * _cellSize, GUILayout.ExpandWidth(false));
                DrawGrid(gridRect);
                HandleGridInput(gridRect);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.LabelField(HoverDescription(), EditorStyles.miniLabel);
            }
        }

        private void DrawPreviewPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width * 0.45f)))
            {
                _preview ??= new MapPreview();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("◀ Rotate", GUILayout.Width(80f)))
                        _preview.QuarterTurns = (_preview.QuarterTurns + 3) % 4;
                    if (GUILayout.Button("Rotate ▶", GUILayout.Width(80f)))
                        _preview.QuarterTurns = (_preview.QuarterTurns + 1) % 4;
                    _preview.HighTilt = GUILayout.Toggle(_preview.HighTilt, "High tilt", GUILayout.Width(80f));
                    _preview.Zoom = GUILayout.HorizontalSlider(_preview.Zoom, 0.5f, 3f);
                }

                if (_previewDirty && Event.current.type == EventType.Layout)
                {
                    _preview.Rebuild(_map, _palette, PreviewHighlights());
                    _previewDirty = false;
                }

                var rect = GUILayoutUtility.GetRect(10f, 10f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                _preview.Draw(rect);
            }
        }

        private IEnumerable<(GridCoord, TileHighlight)> PreviewHighlights()
        {
            foreach (var coord in _map.PlayerDeployment)
                yield return (coord, TileHighlight.PlayerDeployment);
            foreach (var coord in _map.EnemyDeployment)
                yield return (coord, TileHighlight.EnemyDeployment);
            if (_selected is { } selected && _map.Contains(selected))
                yield return (selected, TileHighlight.Selected);
        }

        private void DrawIssues()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                int errors = _issues.Count(i => i.Severity == IssueSeverity.Error);
                int warnings = _issues.Count - errors;
                EditorGUILayout.LabelField($"Validation: {errors} error(s), {warnings} warning(s)", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                EditorGUI.BeginChangeCheck();
                _referenceJump = EditorGUILayout.IntSlider("Reachable with Jump", _referenceJump, 1, 7, GUILayout.Width(320f));
                if (EditorGUI.EndChangeCheck())
                    OnMapChanged();
            }

            _issueScroll = EditorGUILayout.BeginScrollView(_issueScroll, GUILayout.Height(120f));
            if (_issues.Count == 0)
                EditorGUILayout.LabelField("No problems found.");
            foreach (var issue in _issues)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var icon = EditorGUIUtility.IconContent(issue.Severity == IssueSeverity.Error ? "console.erroricon.sml" : "console.warnicon.sml");
                    GUILayout.Label(icon, GUILayout.Width(20f));
                    if (issue.Coord is { } coord)
                    {
                        if (GUILayout.Button(coord.ToString(), EditorStyles.miniButton, GUILayout.Width(70f)))
                        {
                            _selected = coord;
                            _previewDirty = true;
                        }
                    }
                    GUILayout.Label(issue.Message);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------ Grid drawing

        private void DrawGrid(Rect area)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            int maxHeight = 1;
            for (int y = 0; y < _map.Depth; y++)
            for (int x = 0; x < _map.Width; x++)
                maxHeight = Mathf.Max(maxHeight, _map.GetTile(new GridCoord(x, y)).StandingHeight);

            var label = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            var corner = new GUIStyle(EditorStyles.miniLabel) { fontSize = 9, padding = new RectOffset(2, 2, 0, 0) };

            for (int y = 0; y < _map.Depth; y++)
            for (int x = 0; x < _map.Width; x++)
            {
                var coord = new GridCoord(x, y);
                var tile = _map.GetTile(coord);
                var cell = CellRect(area, coord);
                var fill = tile.Terrain == TerrainType.Void
                    ? VoidColor
                    : Shade(_palette != null ? _palette.Get(tile.Terrain).TopColor : Color.gray, tile.StandingHeight / (float)maxHeight);
                EditorGUI.DrawRect(cell, fill);

                var text = fill.grayscale > 0.55f ? Color.black : Color.white;
                label.normal.textColor = corner.normal.textColor = text;
                if (tile.Terrain != TerrainType.Void)
                    GUI.Label(cell, GridMetrics.FormatHeight(tile.StandingHeight).TrimEnd('h'), label);

                if (_cellSize >= 28f)
                {
                    if (tile.Slope != SlopeType.Flat)
                        GUI.Label(cell, SlopeGlyph(tile.Slope), corner);
                    if (tile.Depth > 0)
                        GUI.Label(new Rect(cell.x, cell.yMax - 12f, cell.width, 12f), "~" + tile.Depth, corner);
                    var prop = _map.GetPropAt(coord);
                    if (prop != null)
                    {
                        var propStyle = new GUIStyle(corner) { alignment = TextAnchor.UpperRight };
                        GUI.Label(cell, prop.DisplayName.Substring(0, 1), propStyle);
                    }
                }

                switch (_map.GetDeploymentAt(coord))
                {
                    case DeploymentSide.Player: DrawBorder(cell, PlayerColor, 3f); break;
                    case DeploymentSide.Enemy: DrawBorder(cell, EnemyColor, 3f); break;
                }
                if (_selected == coord)
                    DrawBorder(cell, SelectionColor, 2f);
                if (_hovered == coord)
                    DrawBorder(cell, Color.white, 1f);
            }
        }

        private Rect CellRect(Rect area, GridCoord coord) =>
            // North (+Y) is drawn at the top.
            new(area.x + coord.X * _cellSize, area.y + (_map.Depth - 1 - coord.Y) * _cellSize, _cellSize - 1f, _cellSize - 1f);

        private bool TryGetCell(Rect area, Vector2 mouse, out GridCoord coord)
        {
            int x = Mathf.FloorToInt((mouse.x - area.x) / _cellSize);
            int y = _map.Depth - 1 - Mathf.FloorToInt((mouse.y - area.y) / _cellSize);
            coord = new GridCoord(x, y);
            return area.Contains(mouse) && _map.Contains(coord);
        }

        private static Color Shade(Color baseColor, float normalizedHeight) =>
            Color.Lerp(baseColor * 0.55f, Color.Lerp(baseColor, Color.white, 0.25f), Mathf.Clamp01(normalizedHeight));

        private static string SlopeGlyph(SlopeType slope) => slope switch
        {
            SlopeType.RiseNorth => "↑",
            SlopeType.RiseEast => "→",
            SlopeType.RiseSouth => "↓",
            SlopeType.RiseWest => "←",
            SlopeType.ConvexNorthEast => "NE+",
            SlopeType.ConvexSouthEast => "SE+",
            SlopeType.ConvexSouthWest => "SW+",
            SlopeType.ConvexNorthWest => "NW+",
            SlopeType.ConcaveNorthEast => "NE-",
            SlopeType.ConcaveSouthEast => "SE-",
            SlopeType.ConcaveSouthWest => "SW-",
            SlopeType.ConcaveNorthWest => "NW-",
            _ => "",
        };

        private static void DrawBorder(Rect rect, Color color, float width)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, width, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }

        private string HoverDescription()
        {
            if (_hovered is not { } coord || !_map.Contains(coord))
                return " ";
            var tile = _map.GetTile(coord);
            var parts = new List<string> { coord.ToString(), tile.Terrain.ToString() };
            if (tile.Terrain != TerrainType.Void)
                parts.Add(GridMetrics.FormatHeight(tile.StandingHeight));
            if (tile.Slope != SlopeType.Flat)
                parts.Add($"{tile.Slope} +{GridMetrics.FormatHeight(tile.SlopeHeight)}");
            if (tile.Depth > 0)
                parts.Add($"depth {tile.Depth}{(tile.AllowsActions ? "" : " (no actions)")}");
            var prop = _map.GetPropAt(coord);
            if (prop != null)
                parts.Add(prop.DisplayName);
            var side = _map.GetDeploymentAt(coord);
            if (side != DeploymentSide.None)
                parts.Add($"{side} deployment");
            return string.Join("  ·  ", parts);
        }

        // ------------------------------------------------------------------ Painting

        private void HandleGridInput(Rect area)
        {
            var e = Event.current;
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            {
                GridCoord? hovered = TryGetCell(area, e.mousePosition, out var c) ? c : null;
                if (hovered != _hovered)
                {
                    _hovered = hovered;
                    Repaint();
                }
            }

            switch (e.type)
            {
                case EventType.MouseDown when (e.button == 0 || e.button == 1) && TryGetCell(area, e.mousePosition, out var start):
                    _strokeRightButton = e.button == 1;
                    if (_strokeRightButton && _tool is not (Tool.Props or Tool.Deploy))
                    {
                        PickUp(start);
                        e.Use();
                        break;
                    }
                    Undo.IncrementCurrentGroup();
                    _strokeUndoGroup = Undo.GetCurrentGroup();
                    _strokeCells.Clear();
                    PaintBrush(start);
                    e.Use();
                    break;

                case EventType.MouseDrag when _strokeUndoGroup >= 0:
                    if (TryGetCell(area, e.mousePosition, out var dragged))
                        PaintBrush(dragged);
                    e.Use();
                    break;

                case EventType.MouseUp when _strokeUndoGroup >= 0:
                    Undo.CollapseUndoOperations(_strokeUndoGroup);
                    _strokeUndoGroup = -1;
                    OnMapChanged();
                    e.Use();
                    break;
            }
        }

        private void PaintBrush(GridCoord center)
        {
            int low = -(_brushSize - 1) / 2;
            int high = _brushSize / 2;
            bool changed = false;
            for (int dy = low; dy <= high; dy++)
            for (int dx = low; dx <= high; dx++)
            {
                var coord = new GridCoord(center.X + dx, center.Y + dy);
                // Each tile is painted once per stroke, so dragging back and forth doesn't keep raising it.
                if (_map.Contains(coord) && _strokeCells.Add(coord))
                {
                    Undo.RecordObject(_map, "Paint Map");
                    Paint(coord);
                    changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(_map);
                _previewDirty = true;
                Repaint();
            }
        }

        private void Paint(GridCoord coord)
        {
            var t = _map.GetTile(coord);
            switch (_tool)
            {
                case Tool.Height:
                    int height = _heightMode switch
                    {
                        HeightMode.Set => _heightValue,
                        HeightMode.Raise => t.Height + _heightValue,
                        _ => t.Height - _heightValue,
                    };
                    _map.SetTile(coord, new Tile(Mathf.Clamp(height, 0, 60), t.Terrain, t.Depth, t.Slope, t.SlopeHeight));
                    break;
                case Tool.Terrain:
                    _map.SetTile(coord, new Tile(t.Height, _terrain, _terrain == TerrainType.Water ? t.Depth : 0, t.Slope, t.SlopeHeight));
                    break;
                case Tool.Slope:
                    _map.SetTile(coord, new Tile(t.Height, t.Terrain, t.Depth, _slope, _slopeHeight));
                    break;
                case Tool.Water:
                    var terrain = _waterDepth > 0 ? TerrainType.Water : t.Terrain;
                    _map.SetTile(coord, new Tile(t.Height, terrain, _waterDepth, t.Slope, t.SlopeHeight));
                    break;
                case Tool.Props:
                    _map.SetPropAt(coord, _strokeRightButton ? null : _prop);
                    break;
                case Tool.Deploy:
                    _map.SetDeploymentAt(coord, _strokeRightButton ? DeploymentSide.None : _deploySide);
                    break;
            }
        }

        /// <summary>Eyedropper: copies the tile's value for the current tool into the tool settings.</summary>
        private void PickUp(GridCoord coord)
        {
            var t = _map.GetTile(coord);
            // A void tile's height, slope and depth are meaningless; only its terrain is worth copying.
            if (t.Terrain == TerrainType.Void && _tool != Tool.Terrain)
                return;
            switch (_tool)
            {
                case Tool.Height:
                    _heightMode = HeightMode.Set;
                    _heightValue = t.Height;
                    break;
                case Tool.Terrain:
                    _terrain = t.Terrain;
                    break;
                case Tool.Slope:
                    _slope = t.Slope;
                    if (t.Slope != SlopeType.Flat)
                        _slopeHeight = t.SlopeHeight;
                    break;
                case Tool.Water:
                    _waterDepth = t.Depth;
                    break;
            }
            Repaint();
        }

        private void CreateMap()
        {
            string path = EditorUtility.SaveFilePanelInProject("New Battle Map", "BattleMap", "asset", "Choose where to save the map.", ProjectAssets.MapsFolder);
            if (string.IsNullOrEmpty(path))
                return;

            var map = CreateInstance<BattleMapData>();
            var grid = new BattleGrid(12, 12);
            foreach (var coord in grid.AllCoords())
                grid[coord] = new Tile(2, TerrainType.Grass);
            map.CopyTilesFrom(grid);
            map.DisplayName = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(map, path);
            AssetDatabase.SaveAssets();
            SetMap(map);
        }
    }
}
