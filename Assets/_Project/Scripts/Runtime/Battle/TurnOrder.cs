using System;
using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Battle.Magic;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;
using FriendshipWarriors.Units.Status;

namespace FriendshipWarriors.Battle
{
    /// <summary>
    /// FFT's Charge Time (CT) clock. Every clock tick, each unit's CT rises by its Speed; a unit takes its turn once
    /// its CT reaches 100. When several are ready, the highest CT goes first and ties go to the earlier unit in the
    /// roster. Ending a turn spends 60 CT, plus 20 for moving and 20 for acting. Haste fills CT at Speed × 3/2,
    /// Slow at Speed / 2, Stop not at all (FFT). Every status timer counts down one per clock tick.
    /// Charged spells (FFT slow actions) live here too: each tick, every charging spell's CT rises by its charge speed
    /// and those reaching 100 come due, then units' CT rises and their status timers count down — so a spell due on
    /// the same tick as a unit's turn resolves first. Use <see cref="NextEvent"/> to step through both.
    /// </summary>
    public sealed class TurnOrder
    {
        public const int TurnThreshold = 100;
        public const int BaseTurnCost = 60;
        public const int MoveCost = 20;
        public const int ActCost = 20;

        /// <summary>CT left after a turn is capped here (FFT; recalled rather than verified).</summary>
        public const int MaxChargeTimeAfterTurn = 60;

        private readonly List<Unit> _units;
        private readonly List<ChargingAction> _charging = new();
        private readonly Queue<ChargingAction> _resolving = new();
        private readonly Queue<ChargingAction> _interrupted = new();
        private int _sequence;

        public TurnOrder(IEnumerable<Unit> units)
        {
            _units = units.ToList();
        }

        public IReadOnlyList<Unit> Units => _units;

        /// <summary>Spells being charged, not yet resolved.</summary>
        public IReadOnlyList<ChargingAction> Charging => _charging;

        public ChargingAction ChargeOf(Unit caster) => _charging.FirstOrDefault(c => c.Caster == caster);

        /// <summary>Clock ticks elapsed since the battle began.</summary>
        public int ClockTick { get; private set; }

        /// <summary>CT a unit gains per clock tick: its Speed, adjusted by Haste, Slow and Stop.</summary>
        public static int ChargeRate(Unit unit)
        {
            if (unit.HasStatus(StatusEffect.Stop))
                return 0;
            int speed = unit.Stats.Speed;
            if (unit.HasStatus(StatusEffect.Haste))
                return speed * 3 / 2;
            if (unit.HasStatus(StatusEffect.Slow))
                return speed / 2;
            return speed;
        }

        public void Remove(Unit unit)
        {
            _units.Remove(unit);
            CancelCharge(unit);
        }

        /// <summary>The caster starts charging <paramref name="spell"/> at <paramref name="target"/> and gains Charging.</summary>
        public ChargingAction BeginCharge(Unit caster, AbilityDefinition spell, GridCoord target)
        {
            if (spell.ChargeSpeed <= 0)
                throw new ArgumentException($"{spell.DisplayName} is instant; resolve it directly.", nameof(spell));
            CancelCharge(caster);
            var charge = new ChargingAction(caster, spell, target, _sequence++);
            _charging.Add(charge);
            caster.AddStatus(StatusEffect.Charging, StatusRules.UntilCured);
            return charge;
        }

        /// <summary>Drops any spell the unit is charging (without resolving it).</summary>
        public void CancelCharge(Unit caster)
        {
            if (_charging.RemoveAll(c => c.Caster == caster) > 0)
                caster.RemoveStatus(StatusEffect.Charging);
        }

        /// <summary>Advances the clock to the next unit turn and returns it, or null if nobody can ever act. Only for
        /// battles without charged spells: with spells in play, use <see cref="NextEvent"/>.</summary>
        public Unit NextTurn()
        {
            while (true)
            {
                var e = NextEvent();
                switch (e.Kind)
                {
                    case TurnEventKind.UnitTurn: return e.Unit;
                    case TurnEventKind.None: return null;
                    case TurnEventKind.SpellResolves:
                        throw new InvalidOperationException("A charged spell is due; step the clock with NextEvent instead.");
                }
            }
        }

        /// <summary>
        /// Advances the clock to whatever happens next: a spell being cut off, a spell resolving (before any unit turn
        /// that tick), or a unit's turn. The resolving caster loses Charging as the event is returned.
        /// </summary>
        public TurnEvent NextEvent()
        {
            while (true)
            {
                QueueInterrupted();
                if (_interrupted.Count > 0)
                    return TurnEvent.Interrupted(_interrupted.Dequeue());
                if (_resolving.Count > 0)
                {
                    var due = _resolving.Dequeue();
                    if (ChargeOf(due.Caster) == null)
                        due.Caster.RemoveStatus(StatusEffect.Charging);
                    return TurnEvent.Resolves(due);
                }

                var ready = ReadyUnit(_units, u => u.ChargeTime);
                if (ready != null)
                    return TurnEvent.Turn(ready);
                // Speed, not charge rate: a stopped unit's Stop wears off as the clock runs, so keep ticking.
                if (_charging.Count == 0 && (_units.Count == 0 || _units.All(u => u.Stats.Speed <= 0)))
                    return TurnEvent.None;

                // Charged spells advance before units, so one due on the same tick as a unit's turn resolves first.
                AdvanceCharges(_charging, c => c.ChargeTime, (c, ct) => c.ChargeTime = ct, due =>
                {
                    _charging.Remove(due);
                    _resolving.Enqueue(due);
                });
                foreach (var unit in _units)
                {
                    unit.ChargeTime += ChargeRate(unit);
                    unit.Statuses.Tick();
                }
                ClockTick++;
            }
        }

        /// <summary>Moves charges whose caster can no longer finish them (knocked out, stopped, asleep, silenced) to the interrupted queue.</summary>
        private void QueueInterrupted()
        {
            foreach (var charge in _charging.ToList())
            {
                var caster = charge.Caster;
                if (!caster.IsKnockedOut && !caster.Statuses.Any(StatusRules.InterruptsCharging))
                    continue;
                _charging.Remove(charge);
                caster.RemoveStatus(StatusEffect.Charging);
                _interrupted.Enqueue(charge);
            }
        }

        /// <summary>Adds each charge's speed to its CT; those at 100 or more come due, highest CT first, then the earliest cast.</summary>
        private static void AdvanceCharges<T>(List<T> charges, Func<T, int> getCt, Action<T, int> setCt, Action<T> onDue) where T : class
        {
            foreach (var charge in charges)
                setCt(charge, getCt(charge) + SpellOf(charge).ChargeSpeed);
            var due = charges.Where(c => getCt(c) >= TurnThreshold)
                .OrderByDescending(getCt)
                .ThenBy(c => c is ChargingAction a ? a.Sequence : 0)
                .ToList();
            foreach (var charge in due)
                onDue(charge);
        }

        private static AbilityDefinition SpellOf<T>(T charge) => charge switch
        {
            ChargingAction a => a.Spell,
            SimulatedCharge s => s.Charge.Spell,
            _ => throw new ArgumentException(nameof(charge)),
        };

        private sealed class SimulatedCharge
        {
            public ChargingAction Charge;
            public int ChargeTime;
        }

        public static int TurnCost(bool moved, bool acted) => BaseTurnCost + (moved ? MoveCost : 0) + (acted ? ActCost : 0);

        public void EndTurn(Unit unit, bool moved, bool acted) =>
            unit.ChargeTime = Math.Min(unit.ChargeTime - TurnCost(moved, acted), MaxChargeTimeAfterTurn);

        /// <summary>
        /// The next <paramref name="count"/> events, unit turns and charged spells resolving, simulated on copies of the
        /// CT values (status timers aren't simulated). Like <see cref="Preview"/>, it assumes every turn moves and acts.
        /// </summary>
        public List<TurnOrderEntry> PreviewEntries(int count, Unit active = null)
        {
            var result = new List<TurnOrderEntry>(count);
            foreach (var due in _resolving)
                result.Add(new TurnOrderEntry(due.Caster, due.Spell));
            var ct = _units.ToDictionary(u => u, u => u.ChargeTime);
            if (active != null && ct.ContainsKey(active))
                ct[active] = Math.Min(ct[active] - TurnCost(true, true), MaxChargeTimeAfterTurn);
            var charges = _charging.Select(c => new SimulatedCharge { Charge = c, ChargeTime = c.ChargeTime }).ToList();

            int guard = 0;
            while (result.Count < count && guard++ < 10000)
            {
                var ready = ReadyUnit(_units, u => ct[u]);
                if (ready != null)
                {
                    result.Add(new TurnOrderEntry(ready));
                    ct[ready] = Math.Min(ct[ready] - TurnCost(true, true), MaxChargeTimeAfterTurn);
                    continue;
                }
                if (charges.Count == 0 && _units.All(u => ChargeRate(u) <= 0))
                    break;
                AdvanceCharges(charges, c => c.ChargeTime, (c, value) => c.ChargeTime = value, due =>
                {
                    charges.Remove(due);
                    result.Add(new TurnOrderEntry(due.Charge.Caster, due.Charge.Spell));
                });
                foreach (var unit in _units)
                    ct[unit] += ChargeRate(unit);
            }
            return result.Take(count).ToList();
        }

        /// <summary>
        /// The next <paramref name="count"/> turns, simulated on copies of the CT values. Like FFT's turn list, this
        /// assumes every turn moves and acts; <paramref name="active"/> (if given) is mid-turn and is charged that
        /// full cost first.
        /// </summary>
        public List<Unit> Preview(int count, Unit active = null)
        {
            var result = new List<Unit>(count);
            var ct = _units.ToDictionary(u => u, u => u.ChargeTime);
            if (active != null && ct.ContainsKey(active))
                ct[active] = Math.Min(ct[active] - TurnCost(true, true), MaxChargeTimeAfterTurn);
            if (_units.All(u => u.Stats.Speed <= 0))
                return result;

            while (result.Count < count)
            {
                var ready = ReadyUnit(_units, u => ct[u]);
                if (ready == null)
                {
                    // Preview doesn't run status timers, so with everyone stopped CT would never fill: give up.
                    if (_units.All(u => ChargeRate(u) <= 0))
                        break;
                    foreach (var unit in _units)
                        ct[unit] += ChargeRate(unit);
                    continue;
                }
                result.Add(ready);
                ct[ready] = Math.Min(ct[ready] - TurnCost(true, true), MaxChargeTimeAfterTurn);
            }
            return result;
        }

        private static Unit ReadyUnit(List<Unit> units, Func<Unit, int> chargeTime)
        {
            Unit best = null;
            foreach (var unit in units)
            {
                // Strictly greater, so earlier roster entries win ties.
                if (chargeTime(unit) >= TurnThreshold && (best == null || chargeTime(unit) > chargeTime(best)))
                    best = unit;
            }
            return best;
        }
    }
}
