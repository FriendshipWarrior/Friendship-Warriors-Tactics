namespace FriendshipWarriors.Battle
{
    /// <summary>
    /// Battle time in seconds: real time minus the time spent paused. Timed presses, result holds, spell pauses and
    /// AI steps read it, so pausing freezes them and they resume exactly where they were. Pure, so it's tested
    /// without Play mode; callers pass in the real time (<c>Time.unscaledTime</c>).
    /// </summary>
    public sealed class BattleClock
    {
        private float _pausedTotal;
        private float _pausedAt;

        public bool IsPaused { get; private set; }

        /// <summary>Battle time at real time <paramref name="realTime"/>. Doesn't move while paused.</summary>
        public float Now(float realTime) => (IsPaused ? _pausedAt : realTime) - _pausedTotal;

        public void Pause(float realTime)
        {
            if (IsPaused)
                return;
            IsPaused = true;
            _pausedAt = realTime;
        }

        public void Resume(float realTime)
        {
            if (!IsPaused)
                return;
            IsPaused = false;
            if (realTime > _pausedAt)
                _pausedTotal += realTime - _pausedAt;
        }
    }
}
