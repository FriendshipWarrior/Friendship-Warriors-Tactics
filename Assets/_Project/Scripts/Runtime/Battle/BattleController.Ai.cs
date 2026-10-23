using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Battle.AI;
using FriendshipWarriors.Battle.Combat;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Pathfinding;
using FriendshipWarriors.Grid.View;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Status;
using FriendshipWarriors.Units.View.Walking;
using UnityEngine;

namespace FriendshipWarriors.Battle
{
    /// <summary>
    /// AI-controlled turns. <see cref="AiPlanner"/> decides the whole turn up front; this plays it back as a queue of
    /// paced steps (show the move range, move, attack, face, end) so the player can follow along; moves wait for the
    /// unit's walk to finish before the next step. Attacks go through
    /// the same resolution as the player's, so an AI unit attacking a Player-team unit still raises the "Brace!" prompt.
    /// </summary>
    public sealed partial class BattleController
    {
        [Header("Team Control")]
        [SerializeField] private TeamControl _playerControl = TeamControl.Human;
        [SerializeField] private TeamControl _enemyControl = TeamControl.AI;
        [Tooltip("Pause between AI steps, in seconds, so its turn is readable.")]
        [SerializeField, Min(0f)] private float _aiStepDelay = 0.55f;

        private readonly Queue<System.Action> _aiSteps = new();
        private float _aiNextStepAt;
        private AiPlan _aiPlan;
        private MovementRange _aiRange;
        private GridCoord? _aiDestination;

        public TeamControl ControlOf(Team team) => team == Team.Player ? _playerControl : _enemyControl;

        public void SetControl(Team team, TeamControl control)
        {
            if (team == Team.Player)
                _playerControl = control;
            else
                _enemyControl = control;
        }

        /// <summary>The objective, seen from an AI unit: the other side's protected units are priority targets, and a
        /// unit the other side must defeat plays it safe.</summary>
        private AI.AiHints AiHintsFor(Unit unit)
        {
            var priority = Units.Where(u => u.Team != unit.Team && u.Team == Team.Player && _objective.IsProtected(u.Key)).ToList();
            bool cautious = unit.Team == Team.Enemy && _objective.IsTarget(unit.Key);
            return new AI.AiHints(priority, cautious);
        }

        private bool IsAiControlled(Unit unit) => unit != null && ControlOf(unit.Team) == TeamControl.AI;

        /// <summary>Who drives this turn: the AI for AI teams, and for panicking units of any team (FFT Chicken).</summary>
        private bool IsAiDriven(Unit unit) => IsAiControlled(unit) || (unit != null && unit.HasStatus(StatusEffect.Panic));

        /// <summary>Only human-controlled sides get timed presses: a strike for their attacks, a block against the AI's.</summary>
        private PressRole PressRoleFor(Unit attacker, Unit target) =>
            !IsAiControlled(attacker) ? PressRole.Offense
            : !IsAiControlled(target) ? PressRole.Defense
            : PressRole.None;

        private void ResetAiTurn()
        {
            _aiSteps.Clear();
            _aiPlan = null;
            _aiRange = null;
            _aiDestination = null;
        }

        private void BeginAiTurn()
        {
            ResetAiTurn();
            _phase = Phase.AiTurn;
            _hud.HideMenu();
            _cursor.DirectionalInputLocked = true;

            var unit = _active.Unit;
            _aiPlan = AiPlanner.Plan(_gridView.Grid, unit, Units.ToList(), new MovementRules(_enemyLeapClearance), _rng, AiHintsFor(unit));
            Debug.Log($"[Battle] {unit.Name} (AI) plans: {_aiPlan}.", this);
            _hud.SetHint($"{unit.Name}'s turn");

            if (_aiPlan.MoveBefore is { } before)
            {
                _aiSteps.Enqueue(() => AiShowMove(before));
                _aiSteps.Enqueue(() => AiMove(before));
            }
            if (_aiPlan.Spell != null)
            {
                var spell = _aiPlan.Spell;
                var tile = _aiPlan.SpellTile;
                _aiSteps.Enqueue(() => AiCast(spell, tile));
            }
            else if (_aiPlan.Target != null)
            {
                var target = _aiPlan.Target;
                _aiSteps.Enqueue(() => AiAttack(target));
            }
            if (_aiPlan.MoveAfter is { } after)
            {
                _aiSteps.Enqueue(() => AiShowMove(after));
                _aiSteps.Enqueue(() => AiMove(after));
            }
            _aiSteps.Enqueue(AiFace);
            _aiSteps.Enqueue(EndTurn);

            _aiNextStepAt = BattleNow + _aiStepDelay;
            Refresh();
        }

        private void UpdateAiTurn()
        {
            if (_aiSteps.Count == 0 || BattleNow < _aiNextStepAt)
                return;
            _aiNextStepAt = BattleNow + _aiStepDelay;
            _aiSteps.Dequeue().Invoke();
        }

        /// <summary>Picks the AI turn back up after an attack (or its timed prompt) finishes.</summary>
        private void ResumeAiTurn()
        {
            _phase = Phase.AiTurn;
            _aiNextStepAt = BattleNow + _aiStepDelay;
            Refresh();
        }

        private void AiShowMove(GridCoord destination)
        {
            var unit = _active.Unit;
            var stats = unit.Stats;
            _aiRange = Pathfinder.FindMovementRange(_gridView.Grid, unit.Coord, new MovementProfile(stats.Move, stats.Jump),
                UnitOccupancy.For(unit, Units), new MovementRules(_enemyLeapClearance));
            _aiDestination = destination;
            _cursor.SetCoord(destination, followCamera: true);
            Refresh();
        }

        private void AiMove(GridCoord destination)
        {
            var unit = _active.Unit;
            // The board can't change between planning and moving, but check anyway rather than teleport.
            if (_aiRange != null && _turn.CanMove && _aiRange.CanMoveTo(destination))
            {
                _aiRange.TryGetPath(destination, _path);
                var lastFrom = _path.Count > 1 ? _path[^2] : unit.Coord;
                var walk = WalkPath.ForRoute(_gridView.Grid, unit.Coord, _path, _walkSettings);
                unit.Coord = destination;
                unit.Facing = DirectionExtensions.Toward(lastFrom, destination);
                _turn.RecordMove();
                _active.Walk(walk);
            }
            _aiRange = null;
            _aiDestination = null;
            // The next step waits for the walk; a shorter pause after it, since the walk itself was the show.
            WaitForWalks(() =>
            {
                _cursor.SetCoord(unit.Coord, followCamera: true);
                _phase = Phase.AiTurn;
                _aiNextStepAt = BattleNow + _aiStepDelay * 0.5f;
                Refresh();
            }, follow: _active);
        }

        private void AiAttack(Unit target)
        {
            var unit = _active.Unit;
            _targets.Clear();
            _targets.UnionWith(AttackRange.Targets(_gridView.Grid, unit));
            if (!_turn.CanAct || target.IsKnockedOut || !_targets.Contains(target.Coord))
                return;
            _cursor.SetCoord(target.Coord, followCamera: true);
            // Hands over to the shared attack flow; it calls AfterAction → ResumeAiTurn when done.
            TryTarget(target.Coord);
        }

        private void AiCast(FriendshipWarriors.Units.Abilities.AbilityDefinition spell, GridCoord tile)
        {
            var unit = _active.Unit;
            if (!_turn.CanAct || unit.CurrentMp < spell.MpCost || unit.HasStatus(StatusEffect.Silence))
                return;
            _ability = spell;
            _cursor.SetCoord(tile, followCamera: true);
            // Hands over to the shared casting path; it calls AfterAction → ResumeAiTurn when done.
            CastSpell(spell, tile);
        }

        private void AiFace()
        {
            _active.Unit.Facing = _aiPlan.Facing;
            Refresh();
        }

        private void RefreshAiHighlights()
        {
            if (_aiRange == null || _aiDestination is not { } destination)
                return;
            foreach (var coord in _aiRange.Destinations)
                _gridView.SetHighlight(coord, TileHighlight.MoveRange);
            if (_aiRange.TryGetPath(destination, _path))
            {
                foreach (var step in _path)
                    _gridView.SetHighlight(step, TileHighlight.Path);
            }
        }
    }
}
