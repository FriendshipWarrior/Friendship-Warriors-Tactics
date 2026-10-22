using System;
using System.Collections.Generic;

namespace FriendshipWarriors.Grid
{
    /// <summary>
    /// The logical battlefield: a rectangle of tiles, each with a height and terrain.
    /// Pure data with no Unity dependency so battle rules can be tested in EditMode.
    /// </summary>
    public sealed class BattleGrid
    {
        private readonly Tile[] _tiles;

        public int Width { get; }
        public int Depth { get; }

        public BattleGrid(int width, int depth)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be positive.");
            if (depth <= 0) throw new ArgumentOutOfRangeException(nameof(depth), depth, "Depth must be positive.");

            Width = width;
            Depth = depth;
            _tiles = new Tile[width * depth];
        }

        public Tile this[GridCoord coord]
        {
            get => _tiles[IndexOf(coord)];
            set => _tiles[IndexOf(coord)] = value;
        }

        public bool Contains(GridCoord coord) =>
            coord.X >= 0 && coord.X < Width && coord.Y >= 0 && coord.Y < Depth;

        public bool TryGetTile(GridCoord coord, out Tile tile)
        {
            if (!Contains(coord))
            {
                tile = default;
                return false;
            }

            tile = _tiles[coord.Y * Width + coord.X];
            return true;
        }

        /// <summary>Every coordinate in row-major order (Y outer, X inner).</summary>
        public IEnumerable<GridCoord> AllCoords()
        {
            for (int y = 0; y < Depth; y++)
            for (int x = 0; x < Width; x++)
                yield return new GridCoord(x, y);
        }

        private int IndexOf(GridCoord coord)
        {
            if (!Contains(coord))
                throw new ArgumentOutOfRangeException(nameof(coord), coord, $"Outside the {Width}x{Depth} grid.");
            return coord.Y * Width + coord.X;
        }
    }
}
