using System;
using System.Collections.Generic;

namespace FriendshipWarriors.Grid
{
    /// <summary>
    /// Integer tile position on a battle grid. X runs east (world +X), Y runs north (world +Z).
    /// </summary>
    public readonly struct GridCoord : IEquatable<GridCoord>
    {
        public static readonly GridCoord North = new(0, 1);
        public static readonly GridCoord East = new(1, 0);
        public static readonly GridCoord South = new(0, -1);
        public static readonly GridCoord West = new(-1, 0);

        /// <summary>The four orthogonal steps, in a fixed order so pathfinding is deterministic.</summary>
        public static readonly IReadOnlyList<GridCoord> CardinalDirections = new[] { North, East, South, West };

        public readonly int X;
        public readonly int Y;

        public GridCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int ManhattanDistance(GridCoord other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

        public static GridCoord operator +(GridCoord a, GridCoord b) => new(a.X + b.X, a.Y + b.Y);
        public static GridCoord operator -(GridCoord a, GridCoord b) => new(a.X - b.X, a.Y - b.Y);
        public static GridCoord operator *(GridCoord a, int scale) => new(a.X * scale, a.Y * scale);
        public static bool operator ==(GridCoord a, GridCoord b) => a.Equals(b);
        public static bool operator !=(GridCoord a, GridCoord b) => !a.Equals(b);

        public bool Equals(GridCoord other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridCoord other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X}, {Y})";
    }
}
