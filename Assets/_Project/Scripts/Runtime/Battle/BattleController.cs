using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Battle.Abilities;
using FriendshipWarriors.Battle.Combat;
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
    /// what was used. Act → Attack uses FFT's physical attack rules plus the timed press: the player presses Confirm
    /// as the marker crosses the zone to try for a critical (attacking) or a block (an enemy attacking a player
    /// unit). Act → job command lists the job's learned action abilities (see <see cref="AbilityCommand"/>); items draw
    /// from the team's <see cref="Inventory"/>. Knocked-out units count down 3 turns before they are lost. Every team
    /// is player-controlled until enemy AI exists, but only the Player team gets timed presses (Attack only).
    /// </summary>
    public sealed partial class BattleController : MonoBehaviour
    {
        private enum Phase { Idle, Command, ActMenu, ChooseDestination, ChooseTarget, TimedPress, ChooseFacing, BattleOver, AiTurn, AbilityMenu, ChooseAbilityTarget }
        private enum PressRole { None, Offense, Defense }

        private const string MoveId = "move", ActId = "act", WaitId = "wait", AttackId = "attack", PrimaryId = "primary", SecondaryId = "secondary";

        [SerializeField] private BattleInput _input;
        [SerializeField] private GridView _gridView;
        [SerializeField] private TileCursor _cursor;
        [SerializeField] private IsoCameraRig _cameraRig;
        [SerializeField] private BattleHud _hud;

        [Header("Rules")]
        [Tooltip("How many h higher a unit must be to leap over an enemy.")]
        [SerializeField, Min(0)] private int _enemyLeapClearance = 2;

        [Header("Combat")]
        [SerializeField] private TimedPressRules _timedPress = TimedPressRules.Default;
        [Tooltip("0 picks a new seed each battle (logged).")]
        [SerializeField] private int _battleSeed;

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
        private System.Random _rng;
        private UnitView _target;
        private PressRole _pressRole;
        private float _pressStart;
        private float _resolveAt;
        private AttackResult? _pendingResult;

        // Job commands (abilities and items).
        private readonly MenuModel _abilityMenu = new("Skills");
        private readonly AbilityCommand _abilities = new();
        private readonly Dictionary<Team, Inventory> _inventories = new();
        private readonly HashSet<GridCoord> _abilityTiles = new();
        private AbilityDefinition _ability;

        public Unit ActiveUnit => _active != null ? _active.Unit : null;
        public TurnState Turn => _turn;
        public TurnOrder Order => _order;

        private IEnumerable<Unit> Units => _views.Select(v => v.Unit);

        /// <summary>Sets the item stock a team draws from in this battle.</summary>
        public void SetInventory(Team team, Inventory inventory) => _inventories[team] = inventory;

        public Inventory InventoryOf(Team team) => _inventories.TryGetValue(team, out var inventory) ? inventory : null;

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
            {
                unit.ChargeTime = 0;
                unit.ClearBattleModifiers();
            }
            _order = new TurnOrder(Units);
            int seed = _battleSeed != 0 ? _battleSeed : System.Environment.TickCount;
            _rng = new System.Random(seed);
            Debug.Log($"[Battle] Seed {seed}.", this);
            _hud.HideTimingPrompt();
            ResetAiTurn();
            StartNextTurn();
        }

        private void Update()
        {
            switch (_phase)
            {
                case Phase.Command:
                case Phase.ActMenu:
                case Phase.AbilityMenu:
                    ReadMenuNavigation();
                    break;
                case Phase.ChooseFacing:
                    ReadFacingInput();
                    break;
                case Phase.TimedPress:
                    UpdateTimedPress();
                    break;
                case Phase.AiTurn:
                    UpdateAiTurn();
                    break;
            }
        }

        // ------------------------------------------------------------------ Turn flow

        private void StartNextTurn()
        {
            if (CheckBattleOver())
                return;

            // Knocked-out units still take their place in the turn order, but only to count down (FFT).
            var next = _order.NextTurn();
            while (next != null && next.IsKnockedOut)
            {
                _order.EndTurn(next, moved: false, acted: false);
                if (next.TickDeathCounter())
                    RemoveLostUnit(next);
                next = _order.NextTurn();
            }

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
            if (IsAiControlled(unit))
                BeginAiTurn();
            else
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
            var items = new List<MenuItem> { new(AttackId, "Attack"), CommandItem(PrimaryId, unit.Job) };
            if (unit.SecondaryJob != null)
                items.Add(CommandItem(SecondaryId, unit.SecondaryJob));
            _actMenu.SetItems(items);
            _hud.ShowMenu(_actMenu);
            _hud.SetHint("A / Enter: select   B / Esc: back");
            Refresh();
        }

        private MenuItem CommandItem(string id, JobDefinition job)
        {
            bool any = AbilityCommand.LearnedActions(_active.Unit, job).Count > 0;
            return new MenuItem(id, any ? job.CommandName : $"{job.CommandName} (none learned)", any);
        }

        private void Activate(string id)
        {
            switch (id)
            {
                case MoveId: BeginDestination(); break;
                case ActId: OpenActMenu(); break;
                case WaitId: BeginFacing(); break;
                case AttackId: BeginTarget(); break;
                case PrimaryId: OpenAbilityMenu(_active.Unit.Job); break;
                case SecondaryId: OpenAbilityMenu(_active.Unit.SecondaryJob); break;
                default:
                    var ability = _abilities.Listed(id);
                    if (ability != null)
                        BeginAbilityTarget(ability);
                    break;
            }
        }

        // ------------------------------------------------------------------ Job commands

        private void OpenAbilityMenu(JobDefinition job)
        {
            _phase = Phase.AbilityMenu;
            _cursor.DirectionalInputLocked = true;
            _abilities.BuildMenu(_abilityMenu, job, _active.Unit, _gridView.Grid, Units, InventoryOf(_active.Unit.Team));
            _hud.ShowMenu(_abilityMenu);
            _hud.SetHint("A / Enter: select   B / Esc: back");
            Refresh();
        }

        private void BeginAbilityTarget(AbilityDefinition ability)
        {
            _ability = ability;
            _abilityTiles.Clear();
            _abilityTiles.UnionWith(AbilityRules.TargetTiles(_gridView.Grid, ability, _active.Unit));
            _phase = Phase.ChooseAbilityTarget;
            _hud.HideMenu();
            _cursor.DirectionalInputLocked = false;
            // Self-only abilities start on the user, ready to confirm.
            if (ability.Targeting == AbilityTargeting.Self)
                _cursor.SetCoord(_active.Unit.Coord, followCamera: true);
            Refresh();
        }

        private UnitView AbilityTargetAt(GridCoord coord)
        {
            if (!_abilityTiles.Contains(coord))
                return null;
            var view = _views.FirstOrDefault(v => v.Unit.Coord == coord && AbilityRules.IsValidTarget(_ability, _active.Unit, v.Unit));
            return view;
        }

        private void TryAbilityTarget(GridCoord coord)
        {
            var target = AbilityTargetAt(coord);
            if (target == null)
                return;
            var user = _active.Unit;
            if (target.Unit != user)
                user.Facing = DirectionExtensions.Toward(user.Coord, coord);

            var result = AbilityCommand.Execute(_ability, user, target.Unit, _gridView.Grid, Units, InventoryOf(user.Team), _rng);
            if (result.KnockedTo != null)
                target.SyncPosition();
            Debug.Log($"[Battle] {result.Message} ({result.Outcome.HitChance}% to hit).", this);
            _hud.ShowBanner(result.Message);
            _turn.RecordAct();
            if (CheckBattleOver())
                return;
            AfterAction();
        }

        private string AbilityPreview(Unit target)
        {
            var user = _active.Unit;
            var outcome = _ability.Effect switch
            {
                AbilityEffect.Damage => $"{CombatFormulas.HitChance(user, target)}% to hit, " +
                                        $"{user.Stats.PhysicalAttack * _ability.RandomMultiplier.x}-{user.Stats.PhysicalAttack * _ability.RandomMultiplier.y} damage" +
                                        (_ability.Knockback ? ", may knock back" : ""),
                AbilityEffect.BoostPhysicalAttack => $"P.Atk +{_ability.Amount} for this battle",
                AbilityEffect.RestoreHp => $"+{_ability.Amount} HP",
                AbilityEffect.RestoreMp => $"+{_ability.Amount} MP",
                AbilityEffect.RestoreAll => "full HP and MP",
                AbilityEffect.Revive => "revive",
                _ => "",
            };
            return $"{_ability.DisplayName} on {target.Name}: {outcome}";
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

        /// <summary>Targets within the equipped weapon's range and vertical reach.</summary>
        private void BeginTarget()
        {
            _targets.Clear();
            _targets.UnionWith(AttackRange.Targets(_gridView.Grid, _active.Unit));
            _phase = Phase.ChooseTarget;
            _hud.HideMenu();
            _cursor.DirectionalInputLocked = false;
            Refresh();
        }

        private UnitView Attackable(GridCoord coord)
        {
            var view = UnitAt(coord);
            return _targets.Contains(coord) && view != null && view != _active && !view.Unit.IsKnockedOut ? view : null;
        }

        private void TryTarget(GridCoord coord)
        {
            var target = Attackable(coord);
            if (target == null)
                return;
            var unit = _active.Unit;
            unit.Facing = DirectionExtensions.Toward(unit.Coord, coord);
            _target = target;

            // A human times a strike when attacking, or a block when the other side attacks one of theirs.
            _pressRole = PressRoleFor(unit, target.Unit);
            if (_pressRole == PressRole.None)
            {
                ResolveAttack(0f, pressed: false);
                return;
            }

            _phase = Phase.TimedPress;
            _pendingResult = null;
            _pressStart = Time.unscaledTime;
            _hud.ShowTimingPrompt(_pressRole == PressRole.Offense ? "Strike!" : "Brace!", _timedPress);
            _hud.SetHint("Press A / Enter / Space as the marker crosses the zone");
            Refresh();
        }

        private void UpdateTimedPress()
        {
            float elapsed = Time.unscaledTime - _pressStart;
            if (_pendingResult == null && elapsed > _timedPress.Duration)
                ResolveAttack(0f, pressed: false);
            else if (_pendingResult != null && Time.unscaledTime >= _resolveAt)
                FinishAttack();
        }

        private void ResolveAttack(float accuracy, bool pressed)
        {
            var attacker = _active.Unit;
            var target = _target.Unit;
            var result = AttackResolver.Resolve(attacker, target, _rng, _timedPress,
                offenseAccuracy: _pressRole == PressRole.Offense ? accuracy : 0f,
                defenseAccuracy: _pressRole == PressRole.Defense ? accuracy : 0f);
            _pendingResult = result;

            if (_pressRole != PressRole.None)
                _hud.StopTimingPrompt(accuracy, pressed);
            // Hold the frozen bar briefly so the player sees how the press went.
            _resolveAt = Time.unscaledTime + (_pressRole != PressRole.None ? 0.6f : 0f);
            if (_pressRole == PressRole.None)
                FinishAttack();
        }

        private void FinishAttack()
        {
            var result = _pendingResult.Value;
            _pendingResult = null;
            _hud.HideTimingPrompt();
            var attacker = _active.Unit;
            var target = _target.Unit;

            string outcome;
            if (!result.Hit)
                outcome = "Miss!";
            else if (result.Blocked)
                outcome = "Blocked!";
            else
            {
                target.TakeDamage(result.Damage);
                outcome = $"{result.Damage} damage{(result.Critical ? " - critical!" : "")}{(target.IsKnockedOut ? $"  {target.Name} is down!" : "")}";
            }

            // FFT rewards a successful action: EXP and Prof for the actor, a quarter of the Prof for allies.
            if (result.Hit && !result.Blocked)
            {
                attacker.GainExperience(BattleRewards.Experience(attacker, target));
                int prof = BattleRewards.Proficiency(attacker);
                attacker.GainProficiency(prof);
                foreach (var ally in Units.Where(u => u != attacker && u.Team == attacker.Team && !u.IsKnockedOut))
                    ally.GainProficiency(BattleRewards.SpilloverProficiency(prof));
            }

            Debug.Log($"[Battle] {attacker.Name} attacks {target.Name} from the {result.Angle.ToString().ToLowerInvariant()} " +
                      $"({result.HitChance}% to hit): {result}.", this);
            _hud.ShowBanner($"{attacker.Name} > {target.Name}: {outcome}");
            _turn.RecordAct();
            if (CheckBattleOver())
                return;
            AfterAction();
        }

        private bool CheckBattleOver()
        {
            bool playersStanding = Units.Any(u => u.Team == Team.Player && !u.IsKnockedOut);
            bool enemiesStanding = Units.Any(u => u.Team == Team.Enemy && !u.IsKnockedOut);
            if (playersStanding && enemiesStanding)
                return false;

            _phase = Phase.BattleOver;
            // In-battle boosts end with the battle.
            foreach (var unit in Units)
                unit.ClearBattleModifiers();
            _hud.HideMenu();
            _hud.HideTimingPrompt();
            _gridView.ClearHighlights();
            _hud.ShowBanner(playersStanding ? "Victory!" : "Defeat...", seconds: 0f);
            _hud.SetHint("R / Select: new map");
            return true;
        }

        /// <summary>A knocked-out unit whose counter ran out leaves the battle (FFT turns it into a crystal or chest).</summary>
        private void RemoveLostUnit(Unit unit)
        {
            var view = _views.FirstOrDefault(v => v.Unit == unit);
            _order.Remove(unit);
            if (view != null)
            {
                _views.Remove(view);
                Destroy(view.gameObject);
            }
            _hud.ShowBanner($"{unit.Name} is lost.");
            Debug.Log($"[Battle] {unit.Name}'s death counter ran out.", this);
        }

        private void AfterAction()
        {
            if (IsAiControlled(_active.Unit))
            {
                ResumeAiTurn();
                return;
            }
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
                case Phase.AbilityMenu when _abilityMenu.Selected is { Enabled: true } item: Activate(item.Id); break;
                case Phase.ChooseAbilityTarget: TryAbilityTarget(_cursor.Coord); break;
                case Phase.ChooseDestination: TryMoveTo(_cursor.Coord); break;
                case Phase.ChooseTarget: TryTarget(_cursor.Coord); break;
                case Phase.TimedPress when _pendingResult == null:
                    float elapsed = Time.unscaledTime - _pressStart;
                    ResolveAttack(_timedPress.Accuracy(elapsed), pressed: true);
                    break;
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
                case Phase.AbilityMenu:
                    OpenActMenu();
                    break;
                case Phase.ChooseAbilityTarget:
                    OpenAbilityMenu(_abilities.Job);
                    break;
            }
        }

        private void OnMenuClicked(int index)
        {
            var menu = OpenMenu;
            if (menu != null && index < menu.Items.Count && menu.Items[index].Enabled)
                Activate(menu.Items[index].Id);
        }

        private void OnTileClicked(GridCoord coord)
        {
            switch (_phase)
            {
                case Phase.ChooseDestination: TryMoveTo(coord); break;
                case Phase.ChooseTarget: TryTarget(coord); break;
                case Phase.ChooseAbilityTarget: TryAbilityTarget(coord); break;
                case Phase.ChooseFacing: EndTurn(); break;
            }
        }

        private MenuModel OpenMenu => _phase switch
        {
            Phase.Command => _commandMenu,
            Phase.ActMenu => _actMenu,
            Phase.AbilityMenu => _abilityMenu,
            _ => null,
        };

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
            var menu = OpenMenu;
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
                    var preview = Attackable(_cursor.Coord);
                    if (preview != null)
                    {
                        int hit = CombatFormulas.HitChance(_active.Unit, preview.Unit);
                        int damage = CombatFormulas.AttackFactors(_active.Unit).Damage;
                        var angle = FacingRules.AngleOfAttack(preview.Unit.Coord, preview.Unit.Facing, _active.Unit.Coord);
                        _hud.SetHint($"{preview.Unit.Name}: {hit}% to hit, {damage} damage ({angle.ToString().ToLowerInvariant()})   A / click: attack   B / Esc: back");
                    }
                    else
                    {
                        _hud.SetHint("Choose a target in range   A / click: attack   B / Esc: back");
                    }
                    break;
                case Phase.TimedPress:
                    _gridView.SetHighlight(_target.Unit.Coord, TileHighlight.EnemyDeployment);
                    break;
                case Phase.ChooseAbilityTarget:
                    foreach (var coord in _abilityTiles)
                        _gridView.SetHighlight(coord, _ability.IsSupportAction ? TileHighlight.MoveRange : TileHighlight.EnemyDeployment);
                    var abilityTarget = AbilityTargetAt(_cursor.Coord);
                    _hud.SetHint(abilityTarget != null
                        ? $"{AbilityPreview(abilityTarget.Unit)}   A / click: use   B / Esc: back"
                        : $"{_ability.DisplayName}: choose a target   A / click: use   B / Esc: back");
                    break;
                case Phase.ChooseFacing:
                    _gridView.SetHighlight(_active.Unit.Coord + _active.Unit.Facing.ToStep(), TileHighlight.Path);
                    break;
                case Phase.AiTurn:
                    RefreshAiHighlights();
                    break;
            }

            if (_active != null)
                _gridView.SetHighlight(_active.Unit.Coord, TileHighlight.Selected);
            if (_phase is Phase.ChooseDestination or Phase.ChooseTarget or Phase.ChooseAbilityTarget)
                _gridView.SetHighlight(_cursor.Coord, TileHighlight.Hover);

            var hovered = UnitAt(_cursor.Coord);
            _hud.SetUnitInfo(hovered != null ? hovered.Unit : ActiveUnit);
            if (_order != null)
                _hud.SetTurnOrder(_order.Preview(8, ActiveUnit).Prepend(ActiveUnit).Where(u => u != null).Take(8).ToList(), ActiveUnit);
        }
    }
}
