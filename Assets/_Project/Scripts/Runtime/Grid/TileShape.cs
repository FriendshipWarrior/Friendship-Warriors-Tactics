namespace FriendshipWarriors.Grid
{
    public static class TileShape
    {
        public static TileCorners Corners(in Tile tile)
        {
            int low = tile.Height;
            int high = tile.Height + tile.SlopeHeight;
            return tile.Slope switch
            {
                SlopeType.RiseNorth => new TileCorners(low, low, high, high),
                SlopeType.RiseEast => new TileCorners(low, high, high, low),
                SlopeType.RiseSouth => new TileCorners(high, high, low, low),
                SlopeType.RiseWest => new TileCorners(high, low, low, high),
                SlopeType.ConvexNorthEast => new TileCorners(low, low, high, low),
                SlopeType.ConvexSouthEast => new TileCorners(low, high, low, low),
                SlopeType.ConvexSouthWest => new TileCorners(high, low, low, low),
                SlopeType.ConvexNorthWest => new TileCorners(low, low, low, high),
                SlopeType.ConcaveNorthEast => new TileCorners(high, high, low, high),
                SlopeType.ConcaveSouthEast => new TileCorners(high, low, high, high),
                SlopeType.ConcaveSouthWest => new TileCorners(low, high, high, high),
                SlopeType.ConcaveNorthWest => new TileCorners(high, high, high, low),
                _ => new TileCorners(low, low, low, low),
            };
        }

        /// <summary>
        /// Whether the top surface should be split into triangles along the NW–SE diagonal rather than SW–NE.
        /// Corner slopes split across the odd corner so it forms its own triangle; everything else uses SW–NE.
        /// </summary>
        public static bool SplitsNorthWestToSouthEast(SlopeType slope) => slope switch
        {
            SlopeType.ConvexNorthEast or SlopeType.ConcaveNorthEast or
            SlopeType.ConvexSouthWest or SlopeType.ConcaveSouthWest => true,
            _ => false,
        };
    }
}
