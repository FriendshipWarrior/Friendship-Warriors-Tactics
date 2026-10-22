namespace FriendshipWarriors.Grid
{
    public enum TerrainType
    {
        Grass = 0,
        Dirt = 1,
        Stone = 2,
        Sand = 3,
        ShallowWater = 4,
        DeepWater = 5,

        /// <summary>No tile at this coordinate (a hole or the edge of an irregular map).</summary>
        Void = 6,
    }
}
