namespace FriendshipWarriors.Grid
{
    /// <summary>
    /// One battlefield tile, modeled on FFT's tile data: a base height, an optional slope, water depth and surface
    /// type. Heights are in half-h steps (FFT shows heights like "2.5h"); see <see cref="GridMetrics"/>.
    /// </summary>
    public readonly struct Tile
    {
        /// <summary>Height of the tile's lowest corner, in half-h steps.</summary>
        public readonly int Height;
        public readonly TerrainType Terrain;

        /// <summary>Water depth level (0 = dry). FFT stores 0–7; see <see cref="AllowsActions"/>.</summary>
        public readonly int Depth;

        /// <summary>Water this deep or deeper stops a unit standing in it from acting (FFT).</summary>
        public const int DeepWaterDepth = 2;

        public readonly SlopeType Slope;

        /// <summary>Rise from the slope's low corners to its high corners, in half-h steps. Ignored when flat.</summary>
        public readonly int SlopeHeight;

        /// <summary>Something on the tile (a tree, a crate) prevents units entering it. Set from the map's props.</summary>
        public readonly bool Blocked;

        public Tile(int height, TerrainType terrain, int depth = 0, SlopeType slope = SlopeType.Flat, int slopeHeight = 0,
            bool blocked = false)
        {
            Height = height;
            Terrain = terrain;
            Depth = depth;
            Slope = slope;
            SlopeHeight = slope == SlopeType.Flat ? 0 : slopeHeight;
            Blocked = blocked;
        }

        public bool IsWalkable => Terrain != TerrainType.Void && !Blocked;

        /// <summary>
        /// Height a unit stands at, in half-h steps: the middle of the slope, as in FFT (a ramp from 1h to 2h counts
        /// as 1.5h). Heights only come in halves, so a slope whose midpoint would fall between half steps (an odd
        /// slope height) rounds down. Movement compares these values.
        /// </summary>
        public int StandingHeight => Height + SlopeHeight / 2;

        /// <summary>Whether a unit standing here can act. False in deep water (no Swim-type abilities exist yet).</summary>
        public bool AllowsActions => Depth < DeepWaterDepth;

        public Tile WithBlocked(bool blocked) => new(Height, Terrain, Depth, Slope, SlopeHeight, blocked);

        public override string ToString() =>
            Slope == SlopeType.Flat ? $"{Terrain} h{Height}" : $"{Terrain} h{Height} {Slope}+{SlopeHeight}";
    }
}
