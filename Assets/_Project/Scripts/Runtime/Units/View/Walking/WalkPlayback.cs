using FriendshipWarriors.Units.View.PaperDoll;
using UnityEngine;

namespace FriendshipWarriors.Units.View.Walking
{
    /// <summary>
    /// A walk in progress. It only moves when advanced, by battle time (see <see cref="Battle.BattleClock"/>), so a
    /// paused battle freezes it mid-step and resuming carries on from there.
    /// </summary>
    public sealed class WalkPlayback
    {
        public WalkPath Path { get; }

        /// <summary>Seconds of the walk played so far, at normal speed.</summary>
        public float Elapsed { get; private set; }

        public bool IsFinished => Elapsed >= Path.Duration;

        public WalkPlayback(WalkPath path) => Path = path;

        /// <summary>Plays <paramref name="seconds"/> of battle time, sped up by <paramref name="rate"/> (hurrying).</summary>
        public void Advance(float seconds, float rate = 1f)
        {
            if (seconds > 0f && rate > 0f)
                Elapsed = Mathf.Min(Path.Duration, Elapsed + seconds * rate);
        }

        public void Finish() => Elapsed = Path.Duration;

        public Vector3 Position => Path.PositionAt(Elapsed);

        public WalkSegment Current => Path.SegmentAt(Elapsed, out _);

        /// <summary>
        /// The walk-cycle frame to show, or -1 for none (pushes use the unit's usual animation). Frames follow walk
        /// time, so they freeze with the walk; hops and leaps hold the stride frame in the air.
        /// </summary>
        public int WalkFrame
        {
            get
            {
                if (Path.Segments.Count == 0)
                    return -1;
                return Current.Motion switch
                {
                    WalkMotion.Walk => (int)(Elapsed / PaperDollLayout.FrameDuration(DollAnimation.Walk)) %
                                       PaperDollLayout.FrameCount(DollAnimation.Walk),
                    WalkMotion.Pushed => -1,
                    _ => 1,
                };
            }
        }
    }
}
