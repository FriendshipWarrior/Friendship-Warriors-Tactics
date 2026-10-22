using System.Collections.Generic;
using FriendshipWarriors.Cameras;
using FriendshipWarriors.Controls;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Pathfinding;
using FriendshipWarriors.Grid.View;
using UnityEngine;

namespace FriendshipWarriors.Dev
{
    /// <summary>
    /// Test-scene driver for the battlefield: shows a unit's movement range, previews the path to the cursor, and
    /// moves the unit on Confirm. Cancel toggles a view of the map's deployment zones. Stands in for the battle
    /// state machine until that exists.
    /// </summary>
    public sealed class GridSandbox : MonoBehaviour
    {
        [SerializeField] private GridView _gridView;
        [SerializeField] private TileCursor _cursor;
        [SerializeField] private IsoCameraRig _cameraRig;
        [SerializeField] private Transform _unit;

        [Header("Unit (FFT Squire baseline)")]
        [SerializeField] private Vector2Int _startCoord = new(1, 1);
        [SerializeField, Min(0)] private int _move = 4;
        [SerializeField, Range(0, 7)] private int _jump = 3;

        [Header("Rules")]
        [Tooltip("How many h higher a unit must be to leap over an enemy.")]
        [SerializeField, Min(0)] private int _enemyLeapClearance = 2;

        private readonly List<GridCoord> _path = new();
        private GridCoord _unitCoord;
        private MovementRange _range;
        private bool _showDeployment;

        private void OnEnable()
        {
            _cursor.Moved += OnCursorMoved;
            _cursor.Confirmed += OnConfirmed;
            _cursor.Cancelled += OnCancelled;
        }

        private void OnDisable()
        {
            _cursor.Moved -= OnCursorMoved;
            _cursor.Confirmed -= OnConfirmed;
            _cursor.Cancelled -= OnCancelled;
        }

        private void Start()
        {
            var grid = _gridView.Grid;
            var center = GridMetrics.GridCenter(grid);
            _cameraRig.SetPanBounds(new Bounds(center, new Vector3(grid.Width, 0f, grid.Depth)));

            var start = new GridCoord(_startCoord.x, _startCoord.y);
            PlaceUnit(start);
            _cursor.SetCoord(start, followCamera: true);
            _cameraRig.Focus(_gridView.SurfaceCenter(start), snap: true);
        }

        private void OnCursorMoved(GridCoord _) => RefreshHighlights();

        private void OnConfirmed(GridCoord target)
        {
            if (_range.CanMoveTo(target))
                PlaceUnit(target);
        }

        private void OnCancelled()
        {
            _showDeployment = !_showDeployment;
            RefreshHighlights();
        }

        private void PlaceUnit(GridCoord coord)
        {
            _unitCoord = coord;
            _unit.position = _gridView.SurfaceCenter(coord);
            _range = Pathfinder.FindMovementRange(_gridView.Grid, coord, new MovementProfile(_move, _jump),
                rules: new MovementRules(_enemyLeapClearance));
            RefreshHighlights();
        }

        private void RefreshHighlights()
        {
            _gridView.ClearHighlights();

            if (_showDeployment)
            {
                foreach (var coord in _gridView.Map.PlayerDeployment)
                    _gridView.SetHighlight(coord, TileHighlight.PlayerDeployment);
                foreach (var coord in _gridView.Map.EnemyDeployment)
                    _gridView.SetHighlight(coord, TileHighlight.EnemyDeployment);
            }
            else
            {
                foreach (var coord in _range.Destinations)
                    _gridView.SetHighlight(coord, TileHighlight.MoveRange);
                if (_range.TryGetPath(_cursor.Coord, _path))
                {
                    foreach (var step in _path)
                        _gridView.SetHighlight(step, TileHighlight.Path);
                }
            }

            _gridView.SetHighlight(_unitCoord, TileHighlight.Selected);
            _gridView.SetHighlight(_cursor.Coord, TileHighlight.Hover);
        }
    }
}
