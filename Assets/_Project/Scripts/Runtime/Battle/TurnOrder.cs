using System;
using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Units;

namespace FriendshipWarriors.Battle
{
    /// <summary>
    /// FFT's Charge Time (CT) clock. Every clock tick, each unit's CT rises by its Speed; a unit takes its turn once
    /// its CT reaches 100. When several are ready, the highest CT goes first and ties go to the earlier unit in the
    /// roster. Ending a turn spends 60 CT, plus 20 for moving and 20 for acting.
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

        public TurnOrder(IEnumerable<Unit> units)
        {
            _units = units.ToList();
        }

        public IReadOnlyList<Unit> Units => _units;

        /// <summary>Clock ticks elapsed since the battle began.</summary>
        public int ClockTick { get; private set; }

        public void Remove(Unit unit) => _units.Remove(unit);

        /// <summary>Advances the clock until a unit is ready and returns it, or null if nobody can ever act.</summary>
        public Unit NextTurn()
        {
            if (_units.Count == 0 || _units.All(u => u.Stats.Speed <= 0 && u.ChargeTime < TurnThreshold))
                return null;

            while (true)
            {
                var ready = ReadyUnit(_units, u => u.ChargeTime);
                if (ready != null)
                    return ready;
                foreach (var unit in _units)
                    unit.ChargeTime += unit.Stats.Speed;
                ClockTick++;
            }
        }

        public static int TurnCost(bool moved, bool acted) => BaseTurnCost + (moved ? MoveCost : 0) + (acted ? ActCost : 0);

        public void EndTurn(Unit unit, bool moved, bool acted) =>
            unit.ChargeTime = Math.Min(unit.ChargeTime - TurnCost(moved, acted), MaxChargeTimeAfterTurn);

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
                    foreach (var unit in _units)
                        ct[unit] += unit.Stats.Speed;
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
