namespace FriendshipWarriors.Grid
{
    /// <summary>Corner heights of a tile's top surface, in half-h steps.</summary>
    public readonly struct TileCorners
    {
        public readonly int SouthWest;
        public readonly int SouthEast;
        public readonly int NorthEast;
        public readonly int NorthWest;

        public TileCorners(int southWest, int southEast, int northEast, int northWest)
        {
            SouthWest = southWest;
            SouthEast = southEast;
            NorthEast = northEast;
            NorthWest = northWest;
        }
    }
}
