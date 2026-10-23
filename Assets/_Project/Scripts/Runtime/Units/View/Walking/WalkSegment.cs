using UnityEngine;

namespace FriendshipWarriors.Units.View.Walking
{
    /// <summary>
    /// One leg of a walk, between two tile centres, in grid-local world space (as <see cref="Grid.GridMetrics"/>).
    /// Walks follow the surface through <see cref="Edge"/> (the middle of the shared tile edge), so slopes are walked
    /// smoothly; hops, leaps and pushes follow an arc that peaks <see cref="Arc"/> above the straight line.
    /// </summary>
    public readonly struct WalkSegment
    {
        public readonly WalkMotion Motion;
        public readonly Vector3 From;
        public readonly Vector3 Edge;
        public readonly Vector3 To;
        public readonly float Arc;
        public readonly float Duration;

        /// <summary>The way the unit faces on this leg (pushes keep whatever the unit faced).</summary>
        public readonly Direction Facing;

        public WalkSegment(WalkMotion motion, Vector3 from, Vector3 edge, Vector3 to, float arc, float duration, Direction facing)
        {
            Motion = motion;
            From = from;
            Edge = edge;
            To = to;
            Arc = arc;
            Duration = duration;
            Facing = facing;
        }

        public bool TurnsUnit => Motion != WalkMotion.Pushed;

        /// <summary>Position a fraction <paramref name="t"/> (0–1) of the way along.</summary>
        public Vector3 Sample(float t)
        {
            t = Mathf.Clamp01(t);
            if (Motion == WalkMotion.Walk)
                return t < 0.5f ? Vector3.Lerp(From, Edge, t * 2f) : Vector3.Lerp(Edge, To, t * 2f - 1f);
            var position = Vector3.Lerp(From, To, t);
            position.y += 4f * Arc * t * (1f - t);
            return position;
        }
    }
}
