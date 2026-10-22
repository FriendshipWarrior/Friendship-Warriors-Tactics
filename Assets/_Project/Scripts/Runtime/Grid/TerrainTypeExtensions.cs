namespace FriendshipWarriors.Grid
{
    public static class TerrainTypeExtensions
    {
        public static bool IsWalkable(this TerrainType terrain) =>
            terrain != TerrainType.DeepWater && terrain != TerrainType.Void;
    }
}
