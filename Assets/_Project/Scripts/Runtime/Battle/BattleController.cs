using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Cameras;
using FriendshipWarriors.Controls;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Pathfinding;
using FriendshipWarriors.Grid.View;
using FriendshipWarriors.UI;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;
using FriendshipWarriors.Units.View;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FriendshipWarriors.Battle
{
    /// <summary>
    /// Runs battle turns FFT-style. <see cref="TurnOrder"/> picks whose turn it is; that unit gets the command menu
    /// (Move, Act, Wait). Move and Act may come in either order, and turn-trade abilities can swap one for a second
    /// use of the other. Wait — or using up both — ends the turn with a facing choice, and CT is spent according to
    /// what was used. Every team is player-controlled until enemy AI exists. Act offers a placeholder Attack until
    /// the combat system lands.
    /// </summary>
    public sealed class BattleController : MonoBehaviour
    {
        private enum Phase { Idle, Command, ActMenu, ChooseDestination, ChooseTarget, ChooseFacing }

        private const string MoveId = "move", ActId = "act", WaitId = "wait", AttackId = "attack";

        [SerializeField] private BattleInput _input;
        [SerializeField] private GridView _gridView;
        [SerializeField] private TileCursor _cursor;
        [SerializeField] private IsoCameraRig _cameraRig;
        [SerializeField] private BattleHud _hud;

        [Header("Rules")]
        [Tooltip("How many h higher a unit must be to leap over an enemy.")]
        [SerializeField, Min(0)] private int _enemyLeapClearance = 2;

        [Header("Menu Repeat")]
        [SerializeField, Min(0.05f)] private float _menuRepeatDelay = 0.35f;
        [SerializeField, Min(0.02f)] private float _menuRepeatInterval = 0.12f;

        private readonly MenuModel _commandMenu = new("Command");
        private readonly MenuModel _actMenu = new("Act");
        private readonly List<GridCoord> _path = new();
        private readonly HashSet<GridCoord> _targets = new();
        private List<UnitView> _views = new();
        private TurnOrder _order;
        private UnitView _active;
        private TurnState _turn;
        private Phase _phase;
        private MovementRange _range;
        private int _heldMenuDirection;
        private float _nextMenuRepeat;
        private GridCoord _heldFacingStep;

        public Unit ActiveUnit => _active != null ? _active.Unit : null;
        public TurnState Turn => _turn;
        public TurnOrder Order => _order;

        private IEnumerable<Unit> Units => _views.Select(v => v.Unit);

        private void OnEnable()
        {
            _input.Confirm.performed += OnConfirm;
            _input.Cancel.performed += OnCancel;
            _cursor.Moved += OnCursorMoved;
            _cursor.Clicked += OnTileClicked;
            _hud.MenuItemClicked += OnMenuClicked;
        }

        private void OnDisable()
        {
            _input.Confirm.performed -= OnConfirm;
            _input.Cancel.performed -= OnCancel;
            _cursor.Moved -= OnCursorMoved;
            _cursor.Clicked -= OnTileClicked;
            _hud.MenuItemClicked -= OnMenuClicked;
        }

        /// <summary>Starts a battle with these units; everyone's CT starts at 0, as in FFT.</summary>
        public void Begin(IReadOnlyList<UnitView> views)
        {
            _views = views.ToList();
            foreach (var unit in Units)
                unit.ChargeTime = 0;
            _order = new TurnOrder(Units);
            StartNextTurn();
        }

        private void Update()
        {
            switch (_phase)
            {
                case Phase.Command:
                case Phase.ActMenu:
                    ReadMenuNavigation();
                    break;
                case Phase.ChooseFacing:
                    ReadFacingInput();
                    break;
            }
        }

        // ------------------------------------------------------------------ Turn flow

        private void StartNextTurn()
        {
            var next = _order.NextTurn();
            _active = _views.FirstOrDefault(v => v.Unit == next);
            if (_active == null)
            {
                _phase = Phase.Idle;
                _hud.HideMenu();
                _hud.SetHint("No unit can act.");
                return;
            }

            var unit = _active.Unit;
            _turn = new TurnState(unit.HasTurnTrade(TurnTrade.MoveForAct), unit.HasTurnTrade(TurnTrade.ActForMove));
            _cursor.SetCoord(unit.Coord, followCamera: true);
            _commandMenu.SetItems(System.Array.Empty<MenuItem>());
            OpenCommandMenu();
        }

        private void OpenCommandMenu()
        {
            _phase = Phase.Command;
            _cursor.DirectionalInputLocked = true;
            _commandMenu.Title = _active.Unit.Name;
            string preferred = _commandMenu.Selected?.Id;
            _commandMenu.SetItems(new[]
            {
                new MenuItem(MoveId, _turn.NextMoveUsesAct ? "Move (uses Act)" : "Move", _turn.CanMove),
                new MenuItem(ActId, _turn.NextActUsesMove ? "Act (uses Move)" : "Act", _turn.CanAct),
                new MenuItem(WaitId, "Wait"),
            }, preferred);
            _hud.ShowMenu(_commandMenu);
            _hud.SetHint("D-pad / W S: choose   A / Enter: select");
            _cursor.SetCoord(_active.Unit.Coord, followCamera: true);
            Refresh();
        }

        private void OpenActMenu()
        {
            _phase = Phase.ActMenu;
            var unit = _active.Unit;
            _actMenu.Title = "Act";
            var items = new List<MenuItem>
            {
                new(AttackId, "Attack"),
                new("primary", $"{unit.Job.CommandName} (no abilities yet)", false),
            };
            if (unit.SecondaryJob != null)
                items.Add(new MenuItem("secondary", $"{unit.SecondaryJob.CommandName} (no abilities yet)", false));
            _actMenu.SetItems(items);
            _hud.ShowMenu(_actMenu);
            _hud.SetHint("A / Enter: select   B / Esc: back");
            Refresh();
        }

        private void Activate(string id)
        {
            switch (id)
            {
                case MoveId: BeginDestination(); break;
                case ActId: OpenActMenu(); break;
                case WaitId: BeginFacing(); break;
                case AttackId: BeginTarget(); break;
            }
        }

        private void BeginDestination()
        {
            var unit = _active.Unit;
            var stats = unit.Stats;
            _range = Pathfinder.FindMovementRange(_gridView.Grid, unit.Coord, new MovementProfile(stats.Move, stats.Jump),
                UnitOccupancy.For(unit, Units), new MovementRules(_enemyLeapClearance));
            _phase = Phase.ChooseDestination;
            _hud.HideMenu();
            _cursor.DirectionalInputLocked = false;
            _hud.SetHint("Choose where to move   A / click: move   B / Esc: back");
            Refresh();
        }

        private void TryMoveTo(GridCoord coord)
        {
            if (!_range.CanMoveTo(coord))
                return;
            var unit = _active.Unit;
            _range.TryGetPath(coord, _path);
            var lastFrom = _path.Count > 1 ? _path[^2] : unit.Coord;
            unit.Coord = coord;
            unit.Facing = DirectionExtensions.Toward(lastFrom, coord);
            _active.SyncPosition();
            _turn.RecordMove();
            AfterAction();
        }

        /// <summary>Placeholder targeting: any unit on one of the four adjacent tiles.</summary>
        private void BeginTarget()
        {
            _targets.Clear();
            foreach (var direction in GridCoord.CardinalDirections)
            {
                var coord = _active.Unit.Coord + direction;
                if (_gridView.Grid.Contains(coord))
                    _targets.Add(coord);
            }
            _phase = Phase.ChooseTarget;
            _hud.HideMenu();
            _cursor.DirectionalInputLocked = false;
            _hud.SetHint("Choose an adjacent unit to attack   A / click: attack   B / Esc: back");
            Refresh();
        }

        private void TryTarget(GridCoord coord)
        {
            var target = UnitAt(coord);
            if (!_targets.Contains(coord) || target == null)
                return;
            var unit = _active.Unit;
            unit.Facing = DirectionExtensions.Toward(unit.Coord, coord);
            var angle = FacingRules.AngleOfAttack(target.Unit.Coord, target.Unit.Facing, unit.Coord);
            Debug.Log($"[Battle] {unit.Name} attacks {target.Unit.Name} from the {angle.ToString().ToLowerInvariant()} " +
                      "(placeholder: combat comes next).", this);
            _turn.RecordAct();
            AfterAction();
        }

        private void AfterAction()
        {
            if (_turn.IsFinished)
                BeginFacing();
            else
                OpenCommandMenu();
        }

        private void BeginFacing()
        {
            _phase = Phase.ChooseFacing;
            _heldFacingStep = default;
            _hud.HideMenu();
            _cursor.DirectionalInputLocked = true;
            _cursor.SetCoord(_active.Unit.Coord, followCamera: true);
            _hud.SetHint(_turn.IsFinished
                ? "Choose a facing   D-pad / mouse: turn   A / Enter: end turn"
                : "Choose a facing   D-pad / mouse: turn   A / Enter: end turn   B / Esc: back");
            Refresh();
        }

        private void EndTurn()
        {
            _order.EndTurn(_active.Unit, _turn.MoveSpent, _turn.ActSpent);
            StartNextTurn();
        }

        // ------------------------------------------------------------------ Input

        private void OnConfirm(InputAction.CallbackContext _)
        {
            switch (_phase)
            {
                case Phase.Command when _commandMenu.Selected is { Enabled: true } item: Activate(item.Id); break;
                case Phase.ActMenu when _actMenu.Selected is { Enabled: true } item: Activate(item.Id); break;
                case Phase.ChooseDestination: TryMoveTo(_cursor.Coord); break;
                case Phase.ChooseTarget: TryTarget(_cursor.Coord); break;
                case Phase.ChooseFacing: EndTurn(); break;
            }
        }

        private void OnCancel(InputAction.CallbackContext _)
        {
            switch (_phase)
            {
                case Phase.ActMenu:
                case Phase.ChooseDestination:
                    OpenCommandMenu();
                    break;
                case Phase.ChooseTarget:
                    OpenActMenu();
                    _cursor.DirectionalInputLocked = true;
                    break;
                case Phase.ChooseFacing when !_turn.IsFinished:
                    OpenCommandMenu();
                    break;
            }
        }

        private void OnMenuClicked(int index)
        {
            var menu = _phase == Phase.Command ? _commandMenu : _phase == Phase.ActMenu ? _actMenu : null;
            if (menu != null && index < menu.Items.Count && menu.Items[index].Enabled)
                Activate(menu.Items[index].Id);
        }

        private void OnTileClicked(GridCoord coord)
        {
            switch (_phase)
            {
                case Phase.ChooseDestination: TryMoveTo(coord); break;
                case Phase.ChooseTarget: TryTarget(coord); break;
                case Phase.ChooseFacing: EndTurn(); break;
            }
        }

        private void OnCursorMoved(GridCoord coord)
        {
            // With the mouse, pointing at a tile turns the unit toward it.
            if (_phase == Phase.ChooseFacing && coord != _active.Unit.Coord)
                _active.Unit.Facing = DirectionExtensions.Toward(_active.Unit.Coord, coord);
            Refresh();
        }

        /// <summary>Up/down moves through the open menu, with key-repeat while held.</summary>
        private void ReadMenuNavigation()
        {
            float y = _input.MoveCursor.ReadValue<Vector2>().y;
            int direction = y > 0.5f ? -1 : y < -0.5f ? 1 : 0;
            var menu = _phase == Phase.Command ? _commandMenu : _actMenu;
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
                menu.MoveSelection(direction);
            }
            else if (now >= _nextMenuRepeat)
            {
                _nextMenuRepeat = now + _menuRepeatInterval;
                menu.MoveSelection(direction);
            }
        }

        /// <summary>D-pad/stick/keys turn the unit, mapped to grid directions the same way the cursor is.</summary>
        private void ReadFacingInput()
        {
            var input = _input.MoveCursor.ReadValue<Vector2>();
            var step = input.magnitude >= 0.5f ? CursorMath.ToGridStep(input, _cameraRig.ViewYaw) : default;
            if (step != default && step != _heldFacingStep)
            {
                _active.Unit.Facing = DirectionExtensions.FromStep(step);
                Refresh();
            }
            _heldFacingStep = step;
        }

        // ------------------------------------------------------------------ Presentation

        private UnitView UnitAt(GridCoord coord) => _views.FirstOrDefault(v => v.Unit.Coord == coord);

        private void Refresh()
        {
            _gridView.ClearHighlights();
            switch (_phase)
            {
                case Phase.ChooseDestination:
                    foreach (var coord in _range.Destinations)
                        _gridView.SetHighlight(coord, TileHighlight.MoveRange);
                    if (_range.TryGetPath(_cursor.Coord, _path))
                    {
                        foreach (var step in _path)
                            _gridView.SetHighlight(step, TileHighlight.Path);
                    }
                    break;
                case Phase.ChooseTarget:
                    foreach (var coord in _targets)
                        _gridView.SetHighlight(coord, TileHighlight.EnemyDeployment);
                    break;
                case Phase.ChooseFacing:
                    _gridView.SetHighlight(_active.Unit.Coord + _active.Unit.Facing.ToStep(), TileHighlight.Path);
                    break;
            }

            if (_active != null)
                _gridView.SetHighlight(_active.Unit.Coord, TileHighlight.Selected);
            if (_phase is Phase.ChooseDestination or Phase.ChooseTarget)
                _gridView.SetHighlight(_cursor.Coord, TileHighlight.Hover);

            var hovered = UnitAt(_cursor.Coord);
            _hud.SetUnitInfo(hovered != null ? hovered.Unit : ActiveUnit);
            if (_order != null)
                _hud.SetTurnOrder(_order.Preview(8, ActiveUnit).Prepend(ActiveUnit).Where(u => u != null).Take(8).ToList(), ActiveUnit);
        }
    }
}
