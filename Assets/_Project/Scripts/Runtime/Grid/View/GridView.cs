using System.Collections.Generic;
using UnityEngine;

namespace FriendshipWarriors.Grid.View
{
    /// <summary>
    /// Builds placeholder geometry for a <see cref="BattleMapData"/>: one column per tile plus a colored cap on top,
    /// and tints caps to show highlights. Holds no rules; the <see cref="Grid"/> it exposes is the source of truth.
    /// </summary>
    public sealed class GridView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private BattleMapData _map;
        [SerializeField] private Material _tileMaterial;

        [Tooltip("How far tile columns extend below height 0.")]
        [SerializeField, Min(0.1f)] private float _baseDepth = 1f;
        [SerializeField, Range(0.02f, 0.5f)] private float _capThickness = 0.08f;
        [Tooltip("Cap width relative to the tile; the gap leaves visible grid lines.")]
        [SerializeField, Range(0.5f, 1f)] private float _capInset = 0.92f;
        [SerializeField, Range(0f, 1f)] private float _sideDarkening = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _highlightStrength = 0.65f;

        [Header("Terrain Colors")]
        [SerializeField] private Color _grass = new(0.42f, 0.62f, 0.30f);
        [SerializeField] private Color _dirt = new(0.55f, 0.42f, 0.28f);
        [SerializeField] private Color _stone = new(0.55f, 0.55f, 0.58f);
        [SerializeField] private Color _sand = new(0.85f, 0.78f, 0.55f);
        [SerializeField] private Color _shallowWater = new(0.35f, 0.60f, 0.80f);
        [SerializeField] private Color _deepWater = new(0.15f, 0.30f, 0.60f);

        [Header("Highlight Colors")]
        [SerializeField] private Color _moveRange = new(0.30f, 0.55f, 1.00f);
        [SerializeField] private Color _path = new(1.00f, 0.85f, 0.25f);
        [SerializeField] private Color _hover = new(1.00f, 1.00f, 1.00f);
        [SerializeField] private Color _selected = new(1.00f, 0.40f, 0.30f);

        private readonly Dictionary<GridCoord, TileView> _tiles = new();
        private MaterialPropertyBlock _block;

        public BattleGrid Grid { get; private set; }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            Build();
        }

        public void Build()
        {
            foreach (var tile in _tiles.Values)
                Destroy(tile.gameObject);
            _tiles.Clear();

            Grid = _map.CreateGrid();
            foreach (var coord in Grid.AllCoords())
            {
                var tile = Grid[coord];
                if (tile.Terrain != TerrainType.Void)
                    _tiles.Add(coord, CreateTile(coord, tile));
            }
        }

        public bool TryGetTileView(GridCoord coord, out TileView view) => _tiles.TryGetValue(coord, out view);

        public void SetHighlight(GridCoord coord, TileHighlight highlight)
        {
            if (!_tiles.TryGetValue(coord, out var view))
                return;

            var color = highlight == TileHighlight.None
                ? view.BaseColor
                : Color.Lerp(view.BaseColor, HighlightColor(highlight), _highlightStrength);
            SetColor(view.Cap, color);
        }

        public void ClearHighlights()
        {
            foreach (var view in _tiles.Values)
                SetColor(view.Cap, view.BaseColor);
        }

        private TileView CreateTile(GridCoord coord, Tile tile)
        {
            var top = GridMetrics.TileTopCenter(coord, tile.Height);
            var color = TerrainColor(tile.Terrain);

            // The column keeps its collider so pointer picks hit the tile from any side.
            var column = GameObject.CreatePrimitive(PrimitiveType.Cube);
            column.name = $"Tile {coord}";
            column.transform.SetParent(transform, false);
            float columnHeight = top.y + _baseDepth;
            column.transform.localPosition = new Vector3(top.x, top.y - columnHeight * 0.5f, top.z);
            column.transform.localScale = new Vector3(GridMetrics.TileSize, columnHeight, GridMetrics.TileSize);
            var columnRenderer = column.GetComponent<Renderer>();
            columnRenderer.sharedMaterial = _tileMaterial;
            SetColor(columnRenderer, Color.Lerp(color, Color.black, _sideDarkening));

            // The cap sits flush with the column top, raised a hair to avoid z-fighting.
            var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cap.name = "Cap";
            Destroy(cap.GetComponent<Collider>());
            cap.transform.SetParent(column.transform, true);
            cap.transform.position = top + Vector3.up * (0.002f - _capThickness * 0.5f);
            cap.transform.localScale = new Vector3(_capInset, _capThickness / columnHeight, _capInset);
            var capRenderer = cap.GetComponent<Renderer>();
            capRenderer.sharedMaterial = _tileMaterial;
            SetColor(capRenderer, color);

            var view = column.AddComponent<TileView>();
            view.Initialize(coord, capRenderer, color);
            return view;
        }

        private void SetColor(Renderer target, Color color)
        {
            _block.SetColor(BaseColorId, color);
            target.SetPropertyBlock(_block);
        }

        private Color TerrainColor(TerrainType terrain) => terrain switch
        {
            TerrainType.Dirt => _dirt,
            TerrainType.Stone => _stone,
            TerrainType.Sand => _sand,
            TerrainType.ShallowWater => _shallowWater,
            TerrainType.DeepWater => _deepWater,
            _ => _grass,
        };

        private Color HighlightColor(TileHighlight highlight) => highlight switch
        {
            TileHighlight.MoveRange => _moveRange,
            TileHighlight.Path => _path,
            TileHighlight.Hover => _hover,
            TileHighlight.Selected => _selected,
            _ => Color.clear,
        };
    }
}
