using System;
using FriendshipWarriors.Grid;

namespace FriendshipWarriors.Units
{
    public static class DirectionExtensions
    {
        public static GridCoord ToStep(this Direction direction) => direction switch
        {
            Direction.North => GridCoord.North,
            Direction.East => GridCoord.East,
            Direction.South => GridCoord.South,
            _ => GridCoord.West,
        };

        /// <summary>Degrees clockwise from north, matching camera yaw.</summary>
        public static float ToYaw(this Direction direction) => (int)direction * 90f;

        public static Direction Opposite(this Direction direction) => (Direction)(((int)direction + 2) % 4);

        /// <summary>The direction of a single orthogonal step.</summary>
        public static Direction FromStep(GridCoord step)
        {
            if (step == GridCoord.North) return Direction.North;
            if (step == GridCoord.East) return Direction.East;
            if (step == GridCoord.South) return Direction.South;
            if (step == GridCoord.West) return Direction.West;
            throw new ArgumentException($"{step} is not an orthogonal unit step.", nameof(step));
        }

        /// <summary>The direction from one tile toward another along the dominant axis (ties favor north/south).</summary>
        public static Direction Toward(GridCoord from, GridCoord to)
        {
            var d = to - from;
            if (Math.Abs(d.X) > Math.Abs(d.Y))
                return d.X > 0 ? Direction.East : Direction.West;
            return d.Y >= 0 ? Direction.North : Direction.South;
        }
    }
}
