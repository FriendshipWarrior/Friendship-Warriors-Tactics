namespace FriendshipWarriors.Grid
{
    /// <summary>
    /// Shape of a tile's top surface, after FFT's tile slope types. Inclines rise toward the named edge; convex tiles
    /// raise only the named corner; concave tiles raise every corner except the named one.
    /// </summary>
    public enum SlopeType
    {
        Flat = 0,
        RiseNorth = 1,
        RiseEast = 2,
        RiseSouth = 3,
        RiseWest = 4,
        ConvexNorthEast = 5,
        ConvexSouthEast = 6,
        ConvexSouthWest = 7,
        ConvexNorthWest = 8,
        ConcaveNorthEast = 9,
        ConcaveSouthEast = 10,
        ConcaveSouthWest = 11,
        ConcaveNorthWest = 12,
    }
}
