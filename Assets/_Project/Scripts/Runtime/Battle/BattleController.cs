using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Battle.Abilities;
using FriendshipWarriors.Battle.Combat;
using FriendshipWarriors.Battle.Objectives;
using FriendshipWarriors.Battle.Magic;
using FriendshipWarriors.Battle.Reactions;
using FriendshipWarriors.Cameras;
using FriendshipWarriors.Controls;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Pathfinding;
using FriendshipWarriors.Grid.View;
using FriendshipWarriors.UI;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;
using FriendshipWarriors.Units.Status;
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
    /// from the team's <see cref="Inventory"/>. Spells (Evocation, Warding) aim at a tile: charged ones start charging
    /// and end the caster's turn, then resolve on the CT clock before unit turns (see <see cref="TurnOrder.NextEvent"/>).
    /// Knocked-out units count down 3 turns before they are lost. Every team
    /// is player-controlled until enemy AI exists, but only the Player team gets timed presses (Attack only).
    /// </summary>
    public sealed partial class BattleController : MonoBehaviour
    {
        private enum Phase { Idle, Command, ActMenu, ChooseDestination, ChooseTarget, TimedPress, ChooseFacing, BattleOver, AiTurn, AbilityMenu, ChooseAbilityTarget, SpellResolving }
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

        [Header("Magic")]
        [Tooltip("Pause after a charged spell resolves, in seconds, so its result can be read.")]
        [SerializeField, Min(0f)] private float _spellResolvePause = 1.1f;

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

        // Magic.
        private readonly List<GridCoord> _spellArea = new();
        private float _spellContinueAt;
        // Battle time for presses, result holds and AI steps; disabling this component (pausing) stops it.
        private readonly BattleClock _clock = new();
        private float BattleNow => _clock.Now(Time.unscaledTime);

        /// <summary>The battle is over; true when the Player team won. Raised once, after the result banner is shown.</summary>
        public event System.Action<bool> BattleEnded;

        public Unit ActiveUnit => _active != null ? _active.Unit : null;
        public TurnState Turn => _turn;
        public TurnOrder Order => _order;

        private IEnumerable<Unit> Units => _views.Select(v => v.Unit);

        private readonly List<Unit> _lost = new();
        private BattleObjective _objective = BattleObjective.Standard;

        /// <summary>What this battle asks of the player (defeat all enemies unless the setup says otherwise).</summary>
        public BattleObjective Objective => _objective;

        /// <summary>Units whose death counter ran out this battle.</summary>
        public IReadOnlyList<Unit> LostUnits => _lost;

        /// <summary>The current verdict on the objective.</summary>
        public BattleOutcome Outcome => ObjectiveEvaluator.Evaluate(_objective, Units, _lost);

        /// <summary>Sets the item stock a team draws from in this battle.</summary>
        public void SetInventory(Team team, Inventory inventory) => _inventories[team] = inventory;

        public Inventory InventoryOf(Team team) => _inventories.TryGetValue(team, out var inventory) ? inventory : null;

        private void OnEnable()
        {
            _clock.Resume(Time.unscaledTime);
            _hud.SetClock(_clock);
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
            _clock.Pause(Time.unscaledTime);
        }

        /// <summary>
        /// Starts a battle with these units; everyone's CT starts at 0, as in FFT, and last battle's boosts, Morale
        /// swings and statuses are cleared. <paramref name="prepare"/> runs after that and before the first turn, for
        /// anything a scenario starts with (statuses, Morale).
        /// </summary>
        /// <param name="objective">How this battle is won; null for the standard objective (defeat all enemies).</param>
        public void Begin(IReadOnlyList<UnitView> views, System.Action<IReadOnlyList<Unit>> prepare = null,
            BattleObjective objective = null)
        {
            _views = views.ToList();
            _lost.Clear();
            _objective = objective ?? BattleObjective.Standard;
            _phase = Phase.Idle; // a previous battle may have ended in BattleOver
            foreach (var unit in Units)
            {
                unit.ChargeTime = 0;
                unit.ClearBattleModifiers();
            }
            prepare?.Invoke(Units.ToList());
            _order = new TurnOrder(Units);
            int seed = _battleSeed != 0 ? _battleSeed : System.Environment.TickCount;
            _rng = new System.Random(seed);
            Debug.Log($"[Battle] Seed {seed}.", this);
            _hud.HideTimingPrompt();
            ResetAiTurn();
            ShowObjective();
            StartNextTurn();
        }

        /// <summary>Announces the objective, keeps it on screen, and tags the target and protected units.</summary>
        private void ShowObjective()
        {
            string text = ObjectiveEvaluator.Describe(_objective, Units);
            _hud.SetObjective(text);
            _hud.ShowBanner($"Objective: {text}", seconds: 4f);
            foreach (var view in _views)
            {
                if (_objective.IsTarget(view.Unit.Key) && view.Unit.Team == Team.Enemy)
                    view.SetRole("TARGET", new Color(1f, 0.35f, 0.3f));
                else if (_objective.IsProtected(view.Unit.Key) && view.Unit.Team == Team.Player)
                    view.SetRole("PROTECT", new Color(0.45f, 1f, 0.55f));
                else
                    view.SetRole(null, Color.white);
            }
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
                case Phase.SpellResolving:
                    if (BattleNow >= _spellContinueAt)
                        StartNextTurn();
                    break;
            }
        }

        // ------------------------------------------------------------------ Turn flow

        private void StartNextTurn()
        {
            if (CheckBattleOver())
                return;

            _spellArea.Clear();
            Unit next = null;
            while (true)
            {
                var e = _order.NextEvent();
                if (e.Kind == TurnEventKind.None)
                    break;
                if (e.Kind == TurnEventKind.SpellInterrupted)
                {
                    _hud.ShowBanner($"{e.Charge.Caster.Name}'s {e.Charge.Spell.DisplayName} is interrupted.");
                    Debug.Log($"[Battle] {e.Charge} interrupted.", this);
                    continue;
                }
                if (e.Kind == TurnEventKind.SpellResolves)
                {
                    ResolveCharged(e.Charge);
                    return;
                }
                // Knocked-out units still take their place in the turn order, but only to count down (FFT).
                if (e.Unit.IsKnockedOut)
                {
                    _order.EndTurn(e.Unit, moved: false, acted: false);
                    if (e.Unit.TickDeathCounter())
                    {
                        RemoveLostUnit(e.Unit);
                        // Losing a protected unit can end the battle right here.
                        if (CheckBattleOver())
                            return;
                    }
                    continue;
                }
                next = e.Unit;
                break;
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

            // FFT Chicken: a panicking unit regains a little Morale each turn and recovers at 10.
            if (unit.HasStatus(StatusEffect.Panic))
            {
                unit.AdjustMorale(MoraleRules.PanicRecoveryPerTurn);
                if (!unit.HasStatus(StatusEffect.Panic))
                    _hud.ShowBanner($"{unit.Name} steadies (Morale {unit.Morale}).");
            }

            _turn = new TurnState(unit.HasTurnTrade(TurnTrade.MoveForAct), unit.HasTurnTrade(TurnTrade.ActForMove),
                moveBlocked: !unit.CanMove, actBlocked: !unit.CanAct);
            _cursor.SetCoord(unit.Coord, followCamera: true);
            _commandMenu.SetItems(System.Array.Empty<MenuItem>());

            // Asleep (or otherwise unable to do anything): the turn passes, costing what a Wait costs.
            if (!_turn.CanMove && !_turn.CanAct)
            {
                _hud.ShowBanner($"{unit.Name} can't act ({string.Join(", ", unit.Statuses.Active)}).");
                Debug.Log($"[Battle] {unit.Name} loses its turn to {string.Join(", ", unit.Statuses.Active)}.", this);
                EndTurn();
                return;
            }

            if (IsAiDriven(unit))
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
            if (_ability.IsSpell)
            {
                // Spells aim at a tile, occupied or not; revives still need someone down on it.
                if (_abilityTiles.Contains(coord) && (_ability.Targeting != AbilityTargeting.KnockedOut || AbilityTargetAt(coord) != null))
                    CastSpell(_ability, coord);
                return;
            }
            var target = AbilityTargetAt(coord);
            if (target == null)
                return;
            var user = _active.Unit;
            if (target.Unit != user)
                user.Facing = DirectionExtensions.Toward(user.Coord, coord);

            bool wasDown = target.Unit.IsKnockedOut;
            var result = AbilityCommand.Execute(_ability, user, target.Unit, _gridView.Grid, Units, InventoryOf(user.Team), _rng);
            if (result.KnockedTo != null)
                target.SyncPosition();
            string message = result.Message;
            if (result.Outcome.Damage > 0)
                message += AfterDamage(target.Unit, user, wasDown, result.Outcome.Damage);
            Debug.Log($"[Battle] {message} ({result.Outcome.HitChance}% to hit).", this);
            _hud.ShowBanner(message);
            _turn.RecordAct();
            if (CheckBattleOver())
                return;
            AfterAction();
        }

        // ------------------------------------------------------------------ Magic

        /// <summary>
        /// Casts a spell at a tile. Instant spells resolve now; charged ones start charging (the caster gains Charging,
        /// its turn's options end without extra CT) and resolve later on the CT clock. MP is paid on casting.
        /// </summary>
        private void CastSpell(AbilityDefinition spell, GridCoord tile)
        {
            var caster = _active.Unit;
            if (tile != caster.Coord)
                caster.Facing = DirectionExtensions.Toward(caster.Coord, tile);
            _turn.RecordAct();

            var result = SpellCasting.Begin(caster, spell, tile, _order, _gridView.Grid, Units.ToList(), _rng, out var charge);
            if (charge == null)
            {
                string message = result.Message + SpellAftermath(result);
                Debug.Log($"[Battle] {message}", this);
                _hud.ShowBanner(message);
                if (CheckBattleOver())
                    return;
                AfterAction();
                return;
            }

            _turn.Conclude();
            string ticks = charge.TicksLeft == 1 ? "1 tick" : $"{charge.TicksLeft} ticks";
            _hud.ShowBanner($"{caster.Name} begins {spell.DisplayName} (resolves in {ticks}).");
            Debug.Log($"[Battle] {charge} begins charging ({ticks}).", this);
            AfterAction();
        }

        /// <summary>A charged spell comes due: resolve it, show the result for a moment, then carry on with the clock.</summary>
        private void ResolveCharged(ChargingAction charge)
        {
            _phase = Phase.SpellResolving;
            _hud.HideMenu();
            _cursor.DirectionalInputLocked = true;
            var result = SpellResolver.Resolve(charge.Caster, charge.Spell, charge.Target, _gridView.Grid, Units.ToList(), _rng);
            string message = result.Message + SpellAftermath(result);
            Debug.Log($"[Battle] {message}", this);
            _hud.ShowBanner(message);
            _hud.SetHint($"{charge.Spell.DisplayName} resolves");
            _spellArea.Clear();
            _spellArea.AddRange(result.Area);
            _cursor.SetCoord(charge.Target, followCamera: true);
            _spellContinueAt = BattleNow + _spellResolvePause;
            Refresh();
        }

        /// <summary>
        /// What follows a spell: Morale loss for knock-outs, the damaged units' own reactions (non-physical, so no counter
        /// strikes), then Counter Magic from those it hit. Returns text for the banner.
        /// </summary>
        private string SpellAftermath(SpellResult result)
        {
            string extra = "";
            var units = Units.ToList();
            foreach (var hit in result.Targets.Where(t => t.Hit))
            {
                if (hit.WasKnockedOut)
                    OnKnockedOut(hit.Target);
                if (hit.Damage > 0)
                {
                    var reaction = ReactionResolver.React(hit.Target, result.Caster, physical: false, hit.Damage, _gridView.Grid,
                        units, InventoryOf(hit.Target.Team), _rng);
                    if (reaction.Triggered)
                        extra += $"   |   {reaction.Message}";
                }
                var counter = SpellResolver.TryCounterMagic(hit.Target, result, _gridView.Grid, units, _rng);
                if (counter != null)
                {
                    extra += $"   |   {hit.Target.Name} counters: {counter.Message}";
                    foreach (var countered in counter.Targets.Where(t => t.WasKnockedOut))
                        OnKnockedOut(countered.Target);
                }
            }
            return extra;
        }

        private string SpellPreview(GridCoord tile)
        {
            var caster = _active.Unit;
            var area = new HashSet<GridCoord>(SpellArea.Tiles(_gridView.Grid, tile, _ability.AreaRadius, _ability.AreaVertical));
            var targets = Units.Where(u => area.Contains(u.Coord) && AbilityRules.IsValidTarget(_ability, caster, u)).ToList();
            int ticks = _ability.ChargeSpeed > 0 ? (TurnOrder.TurnThreshold + _ability.ChargeSpeed - 1) / _ability.ChargeSpeed : 0;
            string timing = ticks > 0 ? $"resolves in {ticks} tick{(ticks == 1 ? "" : "s")}" : "instant";
            if (targets.Count == 0)
                return $"{_ability.DisplayName}: no one here now ({timing})";
            var parts = targets.Select(t => _ability.Effect switch
            {
                AbilityEffect.Damage => $"{t.Name} ~{MagicFormulas.Damage(caster, t, _ability.Power)}",
                AbilityEffect.RestoreHp => $"{t.Name} +{MagicFormulas.Heal(caster, t, _ability.Power)}",
                _ => $"{t.Name} {MagicFormulas.HitChance(caster, t, _ability.Power)}%",
            });
            return $"{_ability.DisplayName}: {string.Join(", ", parts)} ({timing})";
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
            _pressStart = BattleNow;
            _hud.ShowTimingPrompt(_pressRole == PressRole.Offense ? "Strike!" : "Brace!", _timedPress);
            _hud.SetHint("Press A / Enter / Space as the marker crosses the zone");
            Refresh();
        }

        private void UpdateTimedPress()
        {
            float elapsed = BattleNow - _pressStart;
            if (_pendingResult == null && elapsed > _timedPress.Duration)
                ResolveAttack(0f, pressed: false);
            else if (_pendingResult != null && BattleNow >= _resolveAt)
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
            _resolveAt = BattleNow + (_pressRole != PressRole.None ? 0.6f : 0f);
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
                bool wasDown = target.IsKnockedOut;
                int dealt = target.TakeDamage(result.Damage);
                outcome = $"{result.Damage} damage{(result.Critical ? " - critical!" : "")}{(target.IsKnockedOut ? $"  {target.Name} is down!" : "")}";
                outcome += AfterDamage(target, attacker, wasDown, dealt);
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

        /// <summary>Ends the battle if the objective is won or lost (see <see cref="ObjectiveEvaluator"/>).</summary>
        private bool CheckBattleOver()
        {
            if (_phase == Phase.BattleOver)
                return true;
            var outcome = Outcome;
            if (!outcome.IsOver)
                return false;

            _phase = Phase.BattleOver;
            // In-battle boosts end with the battle.
            foreach (var unit in Units)
                unit.ClearBattleModifiers();
            _hud.HideMenu();
            _hud.HideTimingPrompt();
            Refresh(); // bring the turn order and unit panel up to date (a just-lost unit leaves the list)
            _gridView.ClearHighlights();
            _hud.ShowBanner(outcome.Kind == BattleOutcomeKind.Victory ? $"Victory! {outcome.Reason}" : $"Defeat... {outcome.Reason}", seconds: 0f);
            Debug.Log($"[Battle] {outcome}.", this);
            _hud.SetHint("R / Select: new map");
            BattleEnded?.Invoke(playersStanding);
            return true;
        }

        /// <summary>A knocked-out unit whose counter ran out leaves the battle (FFT turns it into a crystal or chest).</summary>
        private void RemoveLostUnit(Unit unit)
        {
            var view = _views.FirstOrDefault(v => v.Unit == unit);
            _order.Remove(unit);
            _lost.Add(unit);
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
            // A counter-strike can knock out the unit whose turn it is: the turn ends there.
            if (_active.Unit.IsKnockedOut)
            {
                ResetAiTurn();
                EndTurn();
                return;
            }
            if (IsAiDriven(_active.Unit))
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
            var unit = _active.Unit;
            // FFT: Poison and Regen take effect at the end of the unit's turn (Poison max HP / 16, Regen / 8).
            if (unit.HasStatus(StatusEffect.Poison) && !unit.IsKnockedOut)
            {
                int lost = unit.TakeDamage(System.Math.Max(1, unit.Stats.MaxHp / StatusRules.PoisonDivisor));
                _hud.ShowBanner($"{unit.Name}: Poison, -{lost} HP{(unit.IsKnockedOut ? $"  {unit.Name} is down!" : "")}");
                if (unit.IsKnockedOut)
                    OnKnockedOut(unit);
            }
            if (unit.HasStatus(StatusEffect.Regen) && !unit.IsKnockedOut)
            {
                int healed = unit.RestoreHp(System.Math.Max(1, unit.Stats.MaxHp / StatusRules.RegenDivisor));
                if (healed > 0)
                    _hud.ShowBanner($"{unit.Name}: Regen, +{healed} HP");
            }
            // Turn-counted statuses (Silence) tick down as the unit's own turn ends.
            unit.Statuses.TickTurn();
            _order.EndTurn(unit, _turn.MoveSpent, _turn.ActSpent);
            StartNextTurn();
        }

        /// <summary>
        /// Everything that follows damage from an action: the target's reaction, then Morale loss across the team if
        /// the target (or a counter-struck attacker) went down. Returns text to add to the result banner.
        /// </summary>
        private string AfterDamage(Unit target, Unit attacker, bool targetWasDown, int damageTaken)
        {
            string extra = "";
            if (!targetWasDown && target.IsKnockedOut)
                OnKnockedOut(target);

            bool attackerWasDown = attacker.IsKnockedOut;
            var reaction = ReactionResolver.React(target, attacker, physical: true, damageTaken, _gridView.Grid,
                Units.ToList(), InventoryOf(target.Team), _rng);
            if (reaction.Triggered)
            {
                extra = $"   |   {reaction.Message}";
                Debug.Log($"[Battle] Reaction: {reaction.Message}.", this);
                var attackerView = _views.FirstOrDefault(v => v.Unit == attacker);
                if (attackerView != null)
                    attackerView.SyncPosition();
                if (!attackerWasDown && attacker.IsKnockedOut)
                    OnKnockedOut(attacker);
            }
            return extra;
        }

        /// <summary>A unit going down shakes its standing teammates (the user's Morale design).</summary>
        private void OnKnockedOut(Unit unit)
        {
            foreach (var ally in Units.Where(u => u != unit && u.Team == unit.Team && !u.IsKnockedOut))
                ally.AdjustMorale(-MoraleRules.AllyKnockedOutLoss);
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
                case Phase.TimedPress when _pendingResult == null && !_clock.IsPaused:
                    float elapsed = BattleNow - _pressStart;
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
                case Phase.ChooseAbilityTarget when _ability.IsSpell:
                    foreach (var coord in _abilityTiles)
                        _gridView.SetHighlight(coord, TileHighlight.MoveRange);
                    if (_abilityTiles.Contains(_cursor.Coord))
                    {
                        foreach (var coord in SpellArea.Tiles(_gridView.Grid, _cursor.Coord, _ability.AreaRadius, _ability.AreaVertical))
                            _gridView.SetHighlight(coord, _ability.IsSupportAction ? TileHighlight.Path : TileHighlight.EnemyDeployment);
                        _hud.SetHint($"{SpellPreview(_cursor.Coord)}   A / click: cast   B / Esc: back");
                    }
                    else
                    {
                        _hud.SetHint($"{_ability.DisplayName}: choose a tile in range   A / click: cast   B / Esc: back");
                    }
                    break;
                case Phase.SpellResolving:
                    foreach (var coord in _spellArea)
                        _gridView.SetHighlight(coord, TileHighlight.EnemyDeployment);
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
            {
                var entries = new List<TurnOrderEntry>();
                if (ActiveUnit != null && _phase != Phase.SpellResolving)
                    entries.Add(new TurnOrderEntry(ActiveUnit));
                entries.AddRange(_order.PreviewEntries(8, _phase != Phase.SpellResolving ? ActiveUnit : null));
                _hud.SetTurnOrder(entries.Take(8).ToList(), ActiveUnit);
            }
        }
    }
}
