using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Cameras;
using FriendshipWarriors.Controls;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Generation;
using FriendshipWarriors.Grid.Pathfinding;
using FriendshipWarriors.Grid.View;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.View;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FriendshipWarriors.Dev
{
    /// <summary>
    /// Test-scene driver until the battle state machine exists. Spawns both teams on their deployment zones; select
    /// any unit to see its range, confirm a tile to move, then pick a facing (D-pad or mouse) and confirm, as at the
    /// end of an FFT turn. Cancel steps back; with nothing selected it toggles the deployment view. R / Select rolls
    /// a random map. A small overlay shows the stats of the unit under the cursor.
    /// </summary>
    public sealed class BattleSandbox : MonoBehaviour
    {
        private enum Mode { Idle, Moving, Facing }

        [SerializeField] private BattleInput _input;
        [SerializeField] private GridView _gridView;
        [SerializeField] private TileCursor _cursor;
        [SerializeField] private IsoCameraRig _cameraRig;

        [Header("Roster")]
        [SerializeField] private JobDefinition[] _playerJobs = System.Array.Empty<JobDefinition>();
        [SerializeField] private JobDefinition[] _enemyJobs = System.Array.Empty<JobDefinition>();
        [SerializeField, Range(1, 99)] private int _unitLevel = 5;
        [SerializeField] private int _rosterSeed = 7;

        [Header("Rules")]
        [Tooltip("How many h higher a unit must be to leap over an enemy.")]
        [SerializeField, Min(0)] private int _enemyLeapClearance = 2;

        [Header("Random Maps")]
        [SerializeField] private MapGeneratorSettings _generatorSettings;

        private readonly List<UnitView> _views = new();
        private readonly List<GridCoord> _path = new();
        private Transform _unitRoot;
        private BattleMapData _generatedMap;
        private Mode _mode;
        private UnitView _selected;
        private MovementRange _range;
        private GridCoord _moveOrigin;
        private Direction _originalFacing;
        private GridCoord _heldFacingStep;
        private bool _showDeployment;

        private IEnumerable<Unit> Units => _views.Select(v => v.Unit);

        private void OnEnable()
        {
            _cursor.Moved += OnCursorMoved;
            _cursor.Confirmed += OnConfirmed;
            _cursor.Cancelled += OnCancelled;
            _input.DebugRegenerate.performed += OnRegenerate;
        }

        private void OnDisable()
        {
            _cursor.Moved -= OnCursorMoved;
            _cursor.Confirmed -= OnConfirmed;
            _cursor.Cancelled -= OnCancelled;
            _input.DebugRegenerate.performed -= OnRegenerate;
        }

        private void OnDestroy()
        {
            if (_generatedMap != null)
                Destroy(_generatedMap);
        }

        private void Start()
        {
            _unitRoot = new GameObject("Units").transform;
            SetUpBattlefield();
        }

        private void Update()
        {
            if (_mode == Mode.Facing)
                ReadFacingInput();
        }

        // ------------------------------------------------------------------ Setup

        private void SetUpBattlefield()
        {
            foreach (var view in _views)
                Destroy(view.gameObject);
            _views.Clear();
            Deselect();

            var map = _gridView.Map;
            var rng = new System.Random(_rosterSeed);
            Spawn(_playerJobs, Team.Player, map.PlayerDeployment, map.EnemyDeployment, rng);
            Spawn(_enemyJobs, Team.Enemy, map.EnemyDeployment, map.PlayerDeployment, rng);

            var grid = _gridView.Grid;
            var center = GridMetrics.GridCenter(grid);
            _cameraRig.SetPanBounds(new Bounds(center, new Vector3(grid.Width, 0f, grid.Depth)));
            var start = map.PlayerDeployment.Count > 0 ? map.PlayerDeployment[0] : default;
            _cursor.SetCoord(start, followCamera: true);
            _cameraRig.Focus(_gridView.SurfaceCenter(start), snap: true);
            RefreshHighlights();
        }

        private void Spawn(JobDefinition[] jobs, Team team, IReadOnlyList<GridCoord> zone, IReadOnlyList<GridCoord> facingZone, System.Random rng)
        {
            // Face the other side's zone, as units do at the start of an FFT battle.
            var target = facingZone.Count > 0 ? facingZone[0] : default;
            for (int i = 0; i < jobs.Length && i < zone.Count; i++)
            {
                if (jobs[i] == null)
                    continue;
                var unit = UnitFactory.CreateGeneric($"{jobs[i].DisplayName} {(char)('A' + i)}", team, jobs[i], _unitLevel, rng);
                unit.Coord = zone[i];
                unit.Facing = DirectionExtensions.Toward(zone[i], target);
                _views.Add(UnitView.Create(unit, _gridView, _cameraRig, _unitRoot));
            }
        }

        private void OnRegenerate(InputAction.CallbackContext _)
        {
            if (_generatorSettings == null)
            {
                Debug.LogWarning("[BattleSandbox] No Map Generator Settings assigned.", this);
                return;
            }

            int seed = System.Environment.TickCount;
            if (_generatedMap == null)
                _generatedMap = ScriptableObject.CreateInstance<BattleMapData>();
            var result = MapGenerator.Generate(_generatorSettings, seed, _generatedMap);
            Debug.Log($"[BattleSandbox] Random map seed {seed}: {_generatedMap.Width}x{_generatedMap.Depth}, " +
                      $"{result.Attempts} attempt(s), {(result.Succeeded ? "valid" : "INVALID")}.", this);

            _gridView.SetMap(_generatedMap);
            _gridView.Build();
            SetUpBattlefield();
        }

        // ------------------------------------------------------------------ Turn flow

        private void OnConfirmed(GridCoord coord)
        {
            switch (_mode)
            {
                case Mode.Idle:
                    Select(UnitAt(coord));
                    break;

                case Mode.Moving when coord == _selected.Unit.Coord:
                    BeginFacing(_selected.Unit.Facing);
                    break;

                case Mode.Moving when _range.CanMoveTo(coord):
                    _range.TryGetPath(coord, _path);
                    var lastFrom = _path.Count > 1 ? _path[^2] : _moveOrigin;
                    _selected.Unit.Coord = coord;
                    _selected.SyncPosition();
                    BeginFacing(DirectionExtensions.Toward(lastFrom, coord));
                    break;

                case Mode.Moving:
                    Select(UnitAt(coord));
                    break;

                case Mode.Facing:
                    Deselect();
                    break;
            }
            RefreshHighlights();
        }

        private void OnCancelled()
        {
            switch (_mode)
            {
                case Mode.Facing:
                    // Undo the move and go back to choosing a destination.
                    _selected.Unit.Coord = _moveOrigin;
                    _selected.Unit.Facing = _originalFacing;
                    _selected.SyncPosition();
                    _cursor.DirectionalInputLocked = false;
                    _cursor.SetCoord(_moveOrigin, followCamera: true);
                    _mode = Mode.Moving;
                    break;
                case Mode.Moving:
                    Deselect();
                    break;
                default:
                    _showDeployment = !_showDeployment;
                    break;
            }
            RefreshHighlights();
        }

        private void Select(UnitView view)
        {
            if (view == null)
            {
                Deselect();
                return;
            }

            _selected = view;
            var unit = view.Unit;
            _moveOrigin = unit.Coord;
            _originalFacing = unit.Facing;
            var stats = unit.Stats;
            _range = Pathfinder.FindMovementRange(_gridView.Grid, unit.Coord, new MovementProfile(stats.Move, stats.Jump),
                UnitOccupancy.For(unit, Units), new MovementRules(_enemyLeapClearance));
            _mode = Mode.Moving;
        }

        private void Deselect()
        {
            _selected = null;
            _range = null;
            _mode = Mode.Idle;
            if (_cursor != null)
                _cursor.DirectionalInputLocked = false;
        }

        private void BeginFacing(Direction initial)
        {
            _selected.Unit.Facing = initial;
            _mode = Mode.Facing;
            _heldFacingStep = default;
            _cursor.DirectionalInputLocked = true;
            _cursor.SetCoord(_selected.Unit.Coord, followCamera: true);
        }

        /// <summary>D-pad/stick/keys turn the unit, mapped to grid directions the same way the cursor is.</summary>
        private void ReadFacingInput()
        {
            var input = _input.MoveCursor.ReadValue<Vector2>();
            var step = input.magnitude >= 0.5f ? CursorMath.ToGridStep(input, _cameraRig.ViewYaw) : default;
            if (step != default && step != _heldFacingStep)
            {
                _selected.Unit.Facing = DirectionExtensions.FromStep(step);
                RefreshHighlights();
            }
            _heldFacingStep = step;
        }

        private void OnCursorMoved(GridCoord coord)
        {
            // With the mouse, pointing at a tile turns the unit toward it.
            if (_mode == Mode.Facing && coord != _selected.Unit.Coord)
                _selected.Unit.Facing = DirectionExtensions.Toward(_selected.Unit.Coord, coord);
            RefreshHighlights();
        }

        private UnitView UnitAt(GridCoord coord) => _views.FirstOrDefault(v => v.Unit.Coord == coord);

        // ------------------------------------------------------------------ Presentation

        private void RefreshHighlights()
        {
            _gridView.ClearHighlights();

            if (_mode == Mode.Idle && _showDeployment)
            {
                foreach (var coord in _gridView.Map.PlayerDeployment)
                    _gridView.SetHighlight(coord, TileHighlight.PlayerDeployment);
                foreach (var coord in _gridView.Map.EnemyDeployment)
                    _gridView.SetHighlight(coord, TileHighlight.EnemyDeployment);
            }
            else if (_mode == Mode.Moving)
            {
                foreach (var coord in _range.Destinations)
                    _gridView.SetHighlight(coord, TileHighlight.MoveRange);
                if (_range.TryGetPath(_cursor.Coord, _path))
                {
                    foreach (var step in _path)
                        _gridView.SetHighlight(step, TileHighlight.Path);
                }
            }
            else if (_mode == Mode.Facing)
            {
                var unit = _selected.Unit;
                _gridView.SetHighlight(unit.Coord + unit.Facing.ToStep(), TileHighlight.Path);
            }

            if (_selected != null)
                _gridView.SetHighlight(_selected.Unit.Coord, TileHighlight.Selected);
            if (_mode != Mode.Facing)
                _gridView.SetHighlight(_cursor.Coord, TileHighlight.Hover);
        }

        private void OnGUI()
        {
            var view = _selected != null ? _selected : UnitAt(_cursor.Coord);
            var unit = view != null ? view.Unit : null;
            string hint = _mode switch
            {
                Mode.Moving => "Confirm a blue tile to move (or the unit's own tile to stay). Cancel to deselect.",
                Mode.Facing => "Choose a facing with the D-pad or mouse, then Confirm. Cancel undoes the move.",
                _ => "Confirm a unit to select it. Cancel shows deployment zones. R / Select: random map.",
            };

            float scale = Screen.height / 720f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUILayout.BeginArea(new Rect(10f, 10f, 360f, 320f), GUI.skin.box);
            GUILayout.Label(hint);
            if (unit != null)
            {
                var s = unit.Stats;
                GUILayout.Label($"{unit.Name}  ({unit.Team})");
                GUILayout.Label($"{unit.Job.DisplayName}  Lv {unit.Level}  EXP {unit.Experience}/{ExperienceCurve.ToNextLevel(unit.Level)}");
                GUILayout.Label($"Job Lv {unit.JobLevel(unit.Job)}  Prof {unit.Proficiency(unit.Job)}  Commendations {unit.Commendations.Count}");
                GUILayout.Label($"HP {unit.CurrentHp}/{s.MaxHp}   MP {unit.CurrentMp}/{s.MaxMp}");
                GUILayout.Label($"Speed {s.Speed}   P.Atk {s.PhysicalAttack}   M.Atk {s.MagicAttack}");
                GUILayout.Label($"Move {s.Move}   Jump {s.Jump}   C-Ev {s.ClassEvasion}%   Facing {unit.Facing}");
            }
            GUILayout.EndArea();
        }
    }
}
