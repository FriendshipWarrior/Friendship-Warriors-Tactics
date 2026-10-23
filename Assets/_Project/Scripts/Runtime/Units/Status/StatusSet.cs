using System;
using System.Collections.Generic;

namespace FriendshipWarriors.Units.Status
{
    /// <summary>
    /// The statuses on one unit and how long each has left: clock ticks for most, or the unit's own turns for statuses
    /// with a <see cref="StatusRules.TurnDuration"/> (counted down by <see cref="TickTurn"/>). Re-applying a status
    /// refreshes it to the longer of the two durations; applying Haste or Slow cancels the other. Deterministic, no
    /// Unity dependency.
    /// </summary>
    public sealed class StatusSet
    {
        // Kept sorted by status so iteration order (and therefore everything downstream) is deterministic.
        private readonly SortedDictionary<StatusEffect, int> _remaining = new();
        private readonly List<StatusEffect> _expired = new();
        private readonly HashSet<StatusEffect> _turnBased = new();

        public bool Has(StatusEffect status) => _remaining.ContainsKey(status);

        public IEnumerable<StatusEffect> Active => _remaining.Keys;

        public int Count => _remaining.Count;

        /// <summary>Whether this status's <see cref="Remaining"/> counts the unit's turns rather than clock ticks.</summary>
        public bool IsTurnBased(StatusEffect status) => _turnBased.Contains(status);

        /// <summary>Ticks (or turns, see <see cref="IsTurnBased"/>) left, <see cref="StatusRules.UntilCured"/> for
        /// open-ended statuses, or 0 if absent.</summary>
        public int Remaining(StatusEffect status) => _remaining.TryGetValue(status, out int ticks) ? ticks : 0;

        /// <param name="duration">Clock ticks, or null for the status's default (which may be counted in turns).</param>
        public void Add(StatusEffect status, int? duration = null)
        {
            if (StatusRules.Opposite(status) is { } opposite && Remove(opposite))
                return; // FFT: Haste on a Slowed unit just cancels the Slow (and vice versa).

            var turns = duration == null ? StatusRules.TurnDuration(status) : null;
            if (turns != null)
                _turnBased.Add(status);
            else
                _turnBased.Remove(status);

            int ticks = turns ?? duration ?? StatusRules.DefaultDuration(status);
            if (_remaining.TryGetValue(status, out int current))
            {
                if (current == StatusRules.UntilCured || ticks == StatusRules.UntilCured)
                    ticks = StatusRules.UntilCured;
                else
                    ticks = Math.Max(current, ticks);
            }
            _remaining[status] = ticks;
        }

        public bool Remove(StatusEffect status)
        {
            _turnBased.Remove(status);
            return _remaining.Remove(status);
        }

        public void Clear()
        {
            _remaining.Clear();
            _turnBased.Clear();
        }

        /// <summary>Counts down clock-timed statuses by <paramref name="ticks"/>; returns those that ran out.</summary>
        public IReadOnlyList<StatusEffect> Tick(int ticks = 1) => CountDown(ticks, turnBased: false);

        /// <summary>Counts down turn-timed statuses at the end of the unit's own turn; returns those that ran out.</summary>
        public IReadOnlyList<StatusEffect> TickTurn() => CountDown(1, turnBased: true);

        private IReadOnlyList<StatusEffect> CountDown(int amount, bool turnBased)
        {
            int ticks = amount;
            _expired.Clear();
            var keys = new List<StatusEffect>(_remaining.Keys);
            foreach (var status in keys)
            {
                int left = _remaining[status];
                if (left == StatusRules.UntilCured || _turnBased.Contains(status) != turnBased)
                    continue;
                left -= ticks;
                if (left <= 0)
                {
                    _remaining.Remove(status);
                    _turnBased.Remove(status);
                    _expired.Add(status);
                }
                else
                {
                    _remaining[status] = left;
                }
            }
            return _expired;
        }

        public bool Any(Func<StatusEffect, bool> predicate)
        {
            foreach (var status in _remaining.Keys)
            {
                if (predicate(status))
                    return true;
            }
            return false;
        }
    }
}
