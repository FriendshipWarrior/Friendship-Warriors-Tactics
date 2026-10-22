using System;
using System.Collections.Generic;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.View;
using UnityEditor;
using UnityEngine;

namespace FriendshipWarriors.Editor.Maps
{
    /// <summary>
    /// Renders a live 3D view of a map inside an editor window, using the same <see cref="GridView"/> the game uses,
    /// in a private preview scene so nothing leaks into the open scene. Camera angles match the battle camera.
    /// </summary>
    internal sealed class MapPreview : IDisposable
    {
        private const float LowPitch = 30f;
        private const float HighPitch = 45f;

        private readonly PreviewRenderUtility _utility;
        private readonly GameObject _root;
        private readonly GridView _view;

        public int QuarterTurns { get; set; }
        public bool HighTilt { get; set; }
        public float Zoom { get; set; } = 1f;

        public MapPreview()
        {
            _utility = new PreviewRenderUtility();
            var camera = _utility.camera;
            camera.orthographic = true;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 500f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.17f, 0.20f);

            _utility.lights[0].intensity = 1.1f;
            _utility.lights[0].transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            _utility.lights[1].intensity = 0.5f;
            _utility.ambientColor = new Color(0.45f, 0.45f, 0.5f);

            _root = new GameObject("Map Preview") { hideFlags = HideFlags.HideAndDontSave };
            _view = _root.AddComponent<GridView>();
            _utility.AddSingleGO(_root);
        }

        public void Rebuild(BattleMapData map, TerrainPalette palette, IEnumerable<(GridCoord coord, TileHighlight highlight)> highlights)
        {
            _view.Configure(map, palette, ProjectAssets.TerrainMaterial, ProjectAssets.HighlightMaterial);
            _view.Build();
            foreach (var (coord, highlight) in highlights)
                _view.SetHighlight(coord, highlight);
            foreach (var t in _root.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.HideAndDontSave;
        }

        public void Draw(Rect rect)
        {
            if (Event.current.type != EventType.Repaint || _view.Grid == null)
                return;

            var grid = _view.Grid;
            var center = GridMetrics.GridCenter(grid);
            var rotation = Quaternion.Euler(HighTilt ? HighPitch : LowPitch, 45f + 90f * QuarterTurns, 0f);
            var camera = _utility.camera;
            camera.transform.SetPositionAndRotation(center - rotation * Vector3.forward * 100f, rotation);
            camera.orthographicSize = Mathf.Max(grid.Width, grid.Depth) * 0.6f / Mathf.Max(0.1f, Zoom);

            _utility.BeginPreview(rect, GUIStyle.none);
            _utility.Render(allowScriptableRenderPipeline: true);
            _utility.EndAndDrawPreview(rect);
        }

        public void Dispose()
        {
            _view.Release();
            _utility.Cleanup();
            UnityEngine.Object.DestroyImmediate(_root);
        }
    }
}
