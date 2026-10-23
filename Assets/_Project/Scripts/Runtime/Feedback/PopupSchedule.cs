using System.Collections.Generic;

namespace FriendshipWarriors.Feedback
{
    /// <summary>
    /// Keeps popups over one unit readable when several arrive together (an area spell plus a status, damage and a
    /// Self-Dose heal, a counter-strike): each starts at least <see cref="Stagger"/> after the previous one on the
    /// same unit, and one that starts while others still show takes the lowest free row above them. Different
    /// units are independent, so an area spell shows on everyone at once, as in FFT. Pure; times are battle seconds.
    /// </summary>
    public sealed class PopupSchedule
    {
        public const float Stagger = 0.3f;

        private readonly float _duration;
        private readonly Dictionary<int, List<(float start, int row)>> _byKey = new();

        public PopupSchedule(float duration = PopupMotion.Duration)
        {
            _duration = duration;
        }

        /// <summary>When a popup for <paramref name="key"/> (a unit) requested at <paramref name="now"/> starts, and its row.</summary>
        public (float start, int row) Add(int key, float now)
        {
            if (!_byKey.TryGetValue(key, out var list))
                _byKey[key] = list = new List<(float, int)>();

            float start = now;
            foreach (var (s, _) in list)
            {
                if (s + Stagger > start)
                    start = s + Stagger;
            }

            list.RemoveAll(p => p.start + _duration <= start);
            int row = 0;
            while (list.Exists(p => p.row == row))
                row++;
            list.Add((start, row));
            return (start, row);
        }

        public void Clear() => _byKey.Clear();
    }
}
