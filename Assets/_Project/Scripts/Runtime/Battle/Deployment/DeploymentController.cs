using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Battle.Objectives;
using FriendshipWarriors.Cameras;
using FriendshipWarriors.Controls;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.View;
using FriendshipWarriors.UI;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.View;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FriendshipWarriors.Battle.Deployment
{
    /// <summary>
    /// The formation phase before turn 1 (FFT's formation screen). Each human-controlled side arranges its units on its
    /// deployment zone (highlighted in the team colour) while the enemy stands where it will start, there to be
    /// inspected. Select a unit, pick a zone tile (an occupied tile swaps the two), then choose its facing. The
    /// Formation menu (opened with Cancel, or Confirm on a tile without one of your units) begins the battle. Rules
    /// live in <see cref="DeploymentPlan"/>; this only reads input and draws. Disable it (as pausing does) to freeze it.
    /// </summary>
    public sealed class DeploymentController : MonoBehaviour
    {
        private enum Step { Menu, Browse, Place, Facing }

        private const string ArrangeId = "arrange", BeginId = "begin";

        [SerializeField] private BattleInput _input;
        [SerializeField] private GridView _gridView;
        [SerializeField] private TileCursor _cursor;
        [SerializeField] private IsoCameraRig _cameraRig;
        [SerializeField] private BattleHud _hud;

        [Header("Menu Repeat")]
        [SerializeField, Min(0.05f)] private float _menuRepeatDelay = 0.35f;
        [SerializeField, Min(0.02f)] private float _menuRepeatInterval = 0.12f;

        private readonly MenuModel _menu = new("Formation");
        private readonly Queue<Team> _teams = new();
        private readonly List<UnitPlacement> _placements = new();
        private List<UnitView> _views = new();
        private BattleObjective _objective;
        private System.Action<IReadOnlyList<UnitPlacement>> _onFinished;
        private DeploymentPlan _plan;
        private Team _team;
        private Step _step;
        private Unit _selected;
        private bool _running;
        private bool _finishRequested;
        private int _heldMenuDirection;
        private float _nextMenuRepeat;
        private GridCoord _heldFacingStep;

        public bool IsRunning => _running;

        /// <summary>The side arranging its units right now.</summary>
        public Team CurrentTeam => _team;

        /// <summary>The current side's formation (null when not running).</summary>
        public DeploymentPlan Plan => _running ? _plan : null;

        private void OnEnable()
        {
            _input.Confirm.performed += OnConfirm;
            _input.Cancel.performed += OnCancel;
            _input.Details.performed += OnDetails;
            _cursor.Moved += OnCursorMoved;
            _cursor.Clicked += OnTileClicked;
            _hud.MenuItemClicked += OnMenuClicked;
        }

        private void OnDisable()
        {
            _input.Confirm.performed -= OnConfirm;
            _input.Cancel.performed -= OnCancel;
            _input.Details.performed -= OnDetails;
            _cursor.Moved -= OnCursorMoved;
            _cursor.Clicked -= OnTileClicked;
            _hud.MenuItemClicked -= OnMenuClicked;
        }

        /// <summary>
        /// Runs the formation phase for <paramref name="teams"/> in order (sides the AI controls keep their spawn
        /// positions and are left out), then calls <paramref name="onFinished"/> with every placement made. With no
        /// teams it finishes at once, so the battle starts exactly as spawned.
        /// </summary>
        public void Run(IReadOnlyList<UnitView> views, IEnumerable<Team> teams, BattleObjective objective,
            System.Action<IReadOnlyList<UnitPlacement>> onFinished)
        {
            Cancel();
            _views = views.ToList();
            _objective = objective ?? BattleObjective.Standard;
            _onFinished = onFinished;
            _placements.Clear();
            foreach (var team in teams.Distinct())
                _teams.Enqueue(team);

            string text = ObjectiveEvaluator.Describe(_objective, _views.Select(v => v.Unit));
            _hud.SetObjective(text);
            _hud.SetTurnOrder(System.Array.Empty<TurnOrderEntry>(), null);
            ObjectiveTags.Apply(_views, _objective);
            if (_teams.Count == 0)
            {
                Finish();
                return;
            }
            _hud.ShowBanner($"Formation   |   Objective: {text}", seconds: 4f);
            _running = true;
            StartTeam(_teams.Dequeue());
        }

        /// <summary>Abandons the formation phase without calling back (for example when the map is rerolled).</summary>
        public void Cancel()
        {
            _teams.Clear();
            _finishRequested = false;
            if (!_running)
                return;
            _running = false;
            _plan = null;
            _selected = null;
            _hud.HideMenu();
            _gridView.ClearHighlights();
            _cursor.DirectionalInputLocked = false;
        }

        private void StartTeam(Team team)
        {
            _team = team;
            var map = _gridView.Map;
            var zone = team == Team.Player ? map.PlayerDeployment : map.EnemyDeployment;
            var units = _views.Select(v => v.Unit).Where(u => u.Team == team).ToList();
            _plan = new DeploymentPlan(_gridView.Grid, zone, units, capacity: units.Count);
            ApplyPlan();
            _selected = null;
            var first = _plan.Placements.Count > 0 ? _plan.Placements[0].Coord : (zone.Count > 0 ? zone[0] : _cursor.Coord);
            _cursor.SetCoord(first, followCamera: true);
            _menu.SetItems(System.Array.Empty<MenuItem>()); // a new formation opens on Arrange Units, not Begin Battle
            OpenMenu();
        }

        private void Update()
        {
            if (_finishRequested)
            {
                // Finishing waits a frame so the Confirm that chose Begin Battle can't also reach the battle's first menu.
                _finishRequested = false;
                FinishTeam();
                return;
            }
            if (!_running)
                return;
            if (_step == Step.Menu)
                ReadMenuNavigation();
            else if (_step == Step.Facing)
                ReadFacingInput();
        }

        // ------------------------------------------------------------------ Steps

        private void OpenMenu()
        {
            _step = Step.Menu;
            _cursor.DirectionalInputLocked = true;
            _menu.Title = _teams.Count > 0 ? $"Formation ({_team})" : "Formation";
            _menu.SetItems(new[]
            {
                new MenuItem(ArrangeId, "Arrange Units"),
                new MenuItem(BeginId, _teams.Count > 0 ? "Done" : "Begin Battle"),
            }, _menu.Selected?.Id);
            _hud.ShowMenu(_menu);
            _hud.SetHint("D-pad / W S: choose   A / Enter: select   B / Esc: back to the field");
            Refresh();
        }

        private void Browse()
        {
            _step = Step.Browse;
            _selected = null;
            _hud.HideMenu();
            _cursor.DirectionalInputLocked = false;
            _hud.SetHint("Formation   A / click: pick up a unit   B / Esc / right-click: menu (Begin Battle)");
            Refresh();
        }

        private void Select(Unit unit)
        {
            _step = Step.Place;
            _selected = unit;
            _cursor.DirectionalInputLocked = false;
            _hud.SetHint($"Move {unit.Name}: choose a highlighted tile (a unit there swaps)   A / click: place   B / Esc: back");
            Refresh();
        }

        private void BeginFacing(Unit unit)
        {
            _step = Step.Facing;
            _selected = unit;
            _heldFacingStep = default;
            _cursor.DirectionalInputLocked = true;
            _cursor.SetCoord(_plan.CoordOf(unit) ?? unit.Coord, followCamera: true);
            _hud.SetHint($"{unit.Name}: choose a facing   D-pad / mouse: turn   A / Enter / click: done");
            Refresh();
        }

        private void TryPlace(GridCoord coord)
        {
            var from = _plan.CoordOf(_selected);
            if (from == coord)
            {
                BeginFacing(_selected);
                return;
            }
            if (!_plan.Place(_selected, coord))
                return;
            ApplyPlan();
            BeginFacing(_selected);
        }

        private void FinishTeam()
        {
            if (_plan == null)
                return;
            _plan.Apply();
            _placements.AddRange(_plan.Placements);
            Debug.Log($"[Deployment] {_team}: {string.Join("; ", _plan.Placements)}.", this);
            if (_teams.Count > 0)
            {
                StartTeam(_teams.Dequeue());
                return;
            }
            _running = false;
            _plan = null;
            _selected = null;
            _hud.HideMenu();
            _gridView.ClearHighlights();
            _cursor.DirectionalInputLocked = false;
            Finish();
        }

        /// <summary>X / Tab: the full details (Status) of the unit under the cursor, or back to the compact panel.</summary>
        private void OnDetails(InputAction.CallbackContext _)
        {
            if (!_running)
                return;
            _hud.SetDetailsExpanded(!_hud.DetailsExpanded);
            Refresh();
        }

        private void Finish()
        {
            _hud.SetDetailsExpanded(false);
            var callback = _onFinished;
            _onFinished = null;
            callback?.Invoke(_placements.ToList());
        }

        /// <summary>Writes the formation onto the units and moves their figures there.</summary>
        private void ApplyPlan()
        {
            _plan.Apply();
            foreach (var view in _views)
            {
                if (_plan.IsDeployed(view.Unit))
                    view.SyncPosition();
            }
        }

        // ------------------------------------------------------------------ Input

        private void OnConfirm(InputAction.CallbackContext _)
        {
            if (!_running || _finishRequested)
                return;
            switch (_step)
            {
                case Step.Menu when _menu.Selected is { Enabled: true } item: Activate(item.Id); break;
                case Step.Browse: PickAt(_cursor.Coord); break;
                case Step.Place: TryPlace(_cursor.Coord); break;
                case Step.Facing: Browse(); break;
            }
        }

        private void OnCancel(InputAction.CallbackContext _)
        {
            if (!_running || _finishRequested)
                return;
            switch (_step)
            {
                case Step.Menu: Browse(); break;
                case Step.Browse: OpenMenu(); break;
                case Step.Place:
                case Step.Facing:
                    var unit = _selected;
                    Browse();
                    if (unit != null)
                        _cursor.SetCoord(_plan.CoordOf(unit) ?? unit.Coord, followCamera: true);
                    break;
            }
        }

        private void OnTileClicked(GridCoord coord)
        {
            if (!_running || _finishRequested)
                return;
            switch (_step)
            {
                case Step.Browse: PickAt(coord); break;
                case Step.Place: TryPlace(coord); break;
                case Step.Facing: Browse(); break;
            }
        }

        private void OnMenuClicked(int index)
        {
            if (!_running || _finishRequested || _step != Step.Menu || index >= _menu.Items.Count)
                return;
            if (_menu.Select(index) && _menu.Items[index].Enabled)
                Activate(_menu.Items[index].Id);
        }

        private void Activate(string id)
        {
            if (id == ArrangeId)
                Browse();
            else if (id == BeginId)
                _finishRequested = true;
        }

        /// <summary>Picks up one of this side's units; anywhere else opens the Formation menu.</summary>
        private void PickAt(GridCoord coord)
        {
            var unit = _plan.UnitAt(coord);
            if (unit != null)
                Select(unit);
            else
                OpenMenu();
        }

        private void OnCursorMoved(GridCoord coord)
        {
            if (!_running)
                return;
            // With the mouse, pointing at a tile turns the unit toward it.
            if (_step == Step.Facing && _selected != null && _plan.CoordOf(_selected) is { } at && coord != at)
            {
                _plan.SetFacing(_selected, DirectionExtensions.Toward(at, coord));
                _plan.Apply();
            }
            Refresh();
        }

        private void ReadMenuNavigation()
        {
            float y = _input.MoveCursor.ReadValue<Vector2>().y;
            int direction = y > 0.5f ? -1 : y < -0.5f ? 1 : 0;
            float now = Time.unscaledTime;
            if (direction == 0)
            {
                _heldMenuDirection = 0;
                return;
            }
            if (direction != _heldMenuDirection)
            {
                _heldMenuDirection = direction;
                _nextMenuRepeat = now + _menuRepeatDelay;
                _menu.MoveSelection(direction);
            }
            else if (now >= _nextMenuRepeat)
            {
                _nextMenuRepeat = now + _menuRepeatInterval;
                _menu.MoveSelection(direction);
            }
        }

        /// <summary>D-pad/stick/keys turn the unit, mapped to grid directions the same way the cursor is.</summary>
        private void ReadFacingInput()
        {
            var input = _input.MoveCursor.ReadValue<Vector2>();
            var step = input.magnitude >= 0.5f ? CursorMath.ToGridStep(input, _cameraRig.ViewYaw) : default;
            if (step != default && step != _heldFacingStep && _selected != null)
            {
                _plan.SetFacing(_selected, DirectionExtensions.FromStep(step));
                _plan.Apply();
                Refresh();
            }
            _heldFacingStep = step;
        }

        // ------------------------------------------------------------------ Presentation

        private void Refresh()
        {
            if (!_running)
                return;
            _gridView.ClearHighlights();
            // FFT draws the formation squares blue (the move-range colour); a second human side gets its zone colour.
            var zoneHighlight = _team == Team.Player ? TileHighlight.MoveRange : TileHighlight.EnemyDeployment;
            foreach (var coord in _plan.Tiles)
                _gridView.SetHighlight(coord, zoneHighlight);

            if (_selected != null && _plan.CoordOf(_selected) is { } at)
            {
                _gridView.SetHighlight(at, TileHighlight.Selected);
                if (_step == Step.Facing)
                    _gridView.SetHighlight(at + _plan.FacingOf(_selected).ToStep(), TileHighlight.Path);
            }
            if (_step is Step.Browse or Step.Place)
                _gridView.SetHighlight(_cursor.Coord, TileHighlight.Hover);

            // The unit panel shows whoever is under the cursor (either side), else the unit being placed.
            var hovered = _views.FirstOrDefault(v => v.Unit.Coord == _cursor.Coord);
            _hud.SetUnitInfo(hovered != null ? hovered.Unit : _selected);
        }
    }
}
