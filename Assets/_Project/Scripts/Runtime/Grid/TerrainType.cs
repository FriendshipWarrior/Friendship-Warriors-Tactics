namespace FriendshipWarriors.Grid
{
    /// <summary>
    /// Surface material of a tile. Drives visuals now and terrain-dependent rules later (FFT's Geomancy, for one).
    /// Values are serialized into map assets, so append new types rather than reordering.
    /// </summary>
    public enum TerrainType
    {
        Grass = 0,
        Dirt = 1,
        Stone = 2,
        Sand = 3,
        Water = 4,

        /// <summary>No tile at this coordinate (a hole or the edge of an irregular map).</summary>
        Void = 5,
    }
}
