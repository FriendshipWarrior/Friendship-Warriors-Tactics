using System;
using System.Collections.Generic;

namespace FriendshipWarriors.Units.Status
{
    /// <summary>
    /// The statuses on one unit and the clock ticks each has left. Re-applying a status refreshes it to the longer
    /// of the two durations; applying Haste or Slow cancels the other. Deterministic, no Unity dependency.
    /// </summary>
    public sealed class StatusSet
    {
        // Kept sorted by status so iteration order (and therefore everything downstream) is deterministic.
        private readonly SortedDictionary<StatusEffect, int> _remaining = new();
        private readonly List<StatusEffect> _expired = new();

        public bool Has(StatusEffect status) => _remaining.ContainsKey(status);

        public IEnumerable<StatusEffect> Active => _remaining.Keys;

        public int Count => _remaining.Count;

        /// <summary>Ticks left, <see cref="StatusRules.UntilCured"/> for open-ended statuses, or 0 if absent.</summary>
        public int Remaining(StatusEffect status) => _remaining.TryGetValue(status, out int ticks) ? ticks : 0;

        /// <param name="duration">Clock ticks, or null for the status's FFT default.</param>
        public void Add(StatusEffect status, int? duration = null)
        {
            if (StatusRules.Opposite(status) is { } opposite && _remaining.Remove(opposite))
                return; // FFT: Haste on a Slowed unit just cancels the Slow (and vice versa).

            int ticks = duration ?? StatusRules.DefaultDuration(status);
            if (_remaining.TryGetValue(status, out int current))
            {
                if (current == StatusRules.UntilCured || ticks == StatusRules.UntilCured)
                    ticks = StatusRules.UntilCured;
                else
                    ticks = Math.Max(current, ticks);
            }
            _remaining[status] = ticks;
        }

        public bool Remove(StatusEffect status) => _remaining.Remove(status);

        public void Clear() => _remaining.Clear();

        /// <summary>Counts down timed statuses by <paramref name="ticks"/>; returns those that ran out.</summary>
        public IReadOnlyList<StatusEffect> Tick(int ticks = 1)
        {
            _expired.Clear();
            var keys = new List<StatusEffect>(_remaining.Keys);
            foreach (var status in keys)
            {
                int left = _remaining[status];
                if (left == StatusRules.UntilCured)
                    continue;
                left -= ticks;
                if (left <= 0)
                {
                    _remaining.Remove(status);
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
