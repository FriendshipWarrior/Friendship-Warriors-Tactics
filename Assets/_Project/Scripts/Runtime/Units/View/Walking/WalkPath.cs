using System.Collections.Generic;
using FriendshipWarriors.Grid;
using UnityEngine;

namespace FriendshipWarriors.Units.View.Walking
{
    /// <summary>
    /// How a move plays out on screen, built from the route the rules already chose: one <see cref="WalkSegment"/>
    /// per step, FFT-style. Steps whose shared edge meets are walked along the surface (slopes included); steps up or
    /// down a ledge hop; leaps across gaps or over lower enemies arc over everything in between. Pure, and never read
    /// by the rules: the unit's tile changes the moment it moves, only the picture catches up.
    /// </summary>
    public sealed class WalkPath
    {
        private readonly List<WalkSegment> _segments;

        public IReadOnlyList<WalkSegment> Segments => _segments;

        /// <summary>Seconds the whole walk takes at normal speed.</summary>
        public float Duration { get; }

        public Vector3 End => _segments.Count > 0 ? _segments[^1].To : Vector3.zero;

        private WalkPath(List<WalkSegment> segments)
        {
            _segments = segments;
            foreach (var segment in segments)
                Duration += segment.Duration;
        }

        /// <summary>
        /// The walk from <paramref name="origin"/> along <paramref name="path"/> (as <see cref="Grid.Pathfinding.MovementRange.TryGetPath"/>
        /// fills it: every tile after the origin, up to the destination).
        /// </summary>
        public static WalkPath ForRoute(BattleGrid grid, GridCoord origin, IReadOnlyList<GridCoord> path, WalkSettings settings)
        {
            var segments = new List<WalkSegment>(path.Count);
            var from = origin;
            foreach (var to in path)
            {
                segments.Add(Step(grid, from, to, settings));
                from = to;
            }
            return new WalkPath(segments);
        }

        /// <summary>A push from one tile to another (knockback), keeping <paramref name="facing"/>.</summary>
        public static WalkPath ForPush(BattleGrid grid, GridCoord from, GridCoord to, Direction facing, WalkSettings settings)
        {
            var a = Centre(grid, from);
            var b = Centre(grid, to);
            float drop = Mathf.Abs(a.y - b.y);
            // Level pushes slide; pushes off a ledge drop with a small hop.
            float arc = drop > 0.001f ? Mathf.Max(a.y, b.y) + settings.HopHeight * 0.5f - (a.y + b.y) * 0.5f : 0f;
            float seconds = settings.PushSeconds * (1f + settings.HopTimePerH * HeightInH(drop));
            var segment = new WalkSegment(WalkMotion.Pushed, a, Vector3.Lerp(a, b, 0.5f), b, arc, seconds, facing);
            return new WalkPath(new List<WalkSegment> { segment });
        }

        /// <summary>The segment playing at <paramref name="time"/> seconds in, and how far through it (0–1).</summary>
        public WalkSegment SegmentAt(float time, out float t)
        {
            if (_segments.Count == 0)
            {
                t = 1f;
                return default;
            }
            float start = 0f;
            for (int i = 0; i < _segments.Count; i++)
            {
                var segment = _segments[i];
                if (time < start + segment.Duration || i == _segments.Count - 1)
                {
                    t = segment.Duration > 0f ? Mathf.Clamp01((time - start) / segment.Duration) : 1f;
                    return segment;
                }
                start += segment.Duration;
            }
            t = 1f;
            return _segments[^1];
        }

        public Vector3 PositionAt(float time) => _segments.Count == 0 ? Vector3.zero : SegmentAt(time, out float t).Sample(t);

        private static WalkSegment Step(BattleGrid grid, GridCoord from, GridCoord to, WalkSettings settings)
        {
            var a = Centre(grid, from);
            var b = Centre(grid, to);
            var facing = DirectionExtensions.Toward(from, to);
            float step = settings.StepSeconds;
            float mid = (a.y + b.y) * 0.5f;

            int distance = from.ManhattanDistance(to);
            if (distance > 1)
            {
                // A leap: clear the highest tile between take-off and landing (a lower enemy stands on one of them).
                float top = Mathf.Max(a.y, b.y);
                var direction = facing.ToStep();
                var c = from;
                for (int i = 1; i < distance; i++)
                {
                    c += direction;
                    if (grid.TryGetTile(c, out var under) && under.Terrain != TerrainType.Void)
                        top = Mathf.Max(top, under.StandingHeight * GridMetrics.StepHeight);
                }
                return new WalkSegment(WalkMotion.Leap, a, Vector3.Lerp(a, b, 0.5f), b, top + settings.LeapHeight - mid,
                    step * distance, facing);
            }

            float edgeFrom = EdgeSteps(grid[from], facing);
            float edgeTo = EdgeSteps(grid[to], facing.Opposite());
            if (Mathf.Abs(edgeFrom - edgeTo) < 0.01f)
            {
                // The surfaces meet: walk over the shared edge, which follows slopes up and down.
                var edge = Vector3.Lerp(a, b, 0.5f);
                edge.y = edgeFrom * GridMetrics.StepHeight;
                return new WalkSegment(WalkMotion.Walk, a, edge, b, 0f, step, facing);
            }

            float rise = Mathf.Abs(a.y - b.y);
            return new WalkSegment(WalkMotion.Hop, a, Vector3.Lerp(a, b, 0.5f), b, Mathf.Max(a.y, b.y) + settings.HopHeight - mid,
                step * (1f + settings.HopTimePerH * HeightInH(rise)), facing);
        }

        /// <summary>Where a unit stands on a tile, matching <see cref="GridMetrics.SurfaceCenter"/>.</summary>
        private static Vector3 Centre(BattleGrid grid, GridCoord coord) => GridMetrics.SurfaceCenter(coord, grid[coord]);

        /// <summary>Height of the middle of a tile's edge on the <paramref name="side"/> side, in half-h steps.</summary>
        internal static float EdgeSteps(in Tile tile, Direction side)
        {
            var c = TileShape.Corners(tile);
            return side switch
            {
                Direction.North => (c.NorthWest + c.NorthEast) * 0.5f,
                Direction.East => (c.NorthEast + c.SouthEast) * 0.5f,
                Direction.South => (c.SouthWest + c.SouthEast) * 0.5f,
                _ => (c.SouthWest + c.NorthWest) * 0.5f,
            };
        }

        private static float HeightInH(float world) => world / (GridMetrics.StepHeight * GridMetrics.StepsPerH);
    }
}
