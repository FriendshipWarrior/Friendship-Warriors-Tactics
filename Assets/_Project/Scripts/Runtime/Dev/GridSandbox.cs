using System.Collections.Generic;
using FriendshipWarriors.Cameras;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Pathfinding;
using FriendshipWarriors.Grid.View;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FriendshipWarriors.Dev
{
    /// <summary>
    /// Test-scene driver for the grid milestone: shows a unit's movement range, previews the path to the hovered
    /// tile, and moves the unit on click. Stands in for the battle state machine until that exists.
    /// </summary>
    public sealed class GridSandbox : MonoBehaviour
    {
        [SerializeField] private GridView _gridView;
        [SerializeField] private Camera _camera;
        [SerializeField] private IsoCameraRig _cameraRig;
        [SerializeField] private Transform _unit;

        [Header("Unit")]
        [SerializeField] private Vector2Int _startCoord = new(1, 1);
        [SerializeField, Min(0)] private int _move = 4;
        [SerializeField, Min(0)] private int _jump = 2;

        private readonly List<GridCoord> _path = new();
        private InputAction _point;
        private InputAction _select;
        private GridCoord _unitCoord;
        private GridCoord? _hovered;
        private MovementRange _range;

        private void Awake()
        {
            _point = new InputAction("Point", InputActionType.Value, "<Pointer>/position");
            _select = new InputAction("Select", InputActionType.Button, "<Pointer>/press");
            _select.performed += _ => OnSelect();
        }

        private void OnEnable()
        {
            _point.Enable();
            _select.Enable();
        }

        private void OnDisable()
        {
            _point.Disable();
            _select.Disable();
        }

        private void OnDestroy()
        {
            _point.Dispose();
            _select.Dispose();
        }

        private void Start()
        {
            var grid = _gridView.Grid;
            var center = GridMetrics.GridCenter(grid);
            _cameraRig.SetPanBounds(new Bounds(center, new Vector3(grid.Width, 0f, grid.Depth)));
            _cameraRig.Focus(center, snap: true);

            PlaceUnit(new GridCoord(_startCoord.x, _startCoord.y));
        }

        private void Update()
        {
            var hovered = PickTile();
            if (hovered != _hovered)
            {
                _hovered = hovered;
                RefreshHighlights();
            }
        }

        private void OnSelect()
        {
            if (_hovered is { } target && _range.CanMoveTo(target))
                PlaceUnit(target);
        }

        private void PlaceUnit(GridCoord coord)
        {
            _unitCoord = coord;
            _unit.position = GridMetrics.TileTopCenter(coord, _gridView.Grid[coord].Height);
            _range = Pathfinder.FindMovementRange(_gridView.Grid, coord, new MovementProfile(_move, _jump));
            RefreshHighlights();
        }

        private GridCoord? PickTile()
        {
            var ray = _camera.ScreenPointToRay(_point.ReadValue<Vector2>());
            if (Physics.Raycast(ray, out var hit, 1000f) && hit.collider.TryGetComponent<TileView>(out var tile))
                return tile.Coord;
            return null;
        }

        private void RefreshHighlights()
        {
            _gridView.ClearHighlights();
            foreach (var coord in _range.Destinations)
                _gridView.SetHighlight(coord, TileHighlight.MoveRange);

            if (_hovered is { } hovered)
            {
                if (_range.TryGetPath(hovered, _path))
                {
                    foreach (var step in _path)
                        _gridView.SetHighlight(step, TileHighlight.Path);
                }
                _gridView.SetHighlight(hovered, TileHighlight.Hover);
            }

            _gridView.SetHighlight(_unitCoord, TileHighlight.Selected);
        }
    }
}
