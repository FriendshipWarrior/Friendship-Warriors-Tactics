namespace FriendshipWarriors.Grid
{
    public readonly struct Tile
    {
        /// <summary>Height in steps; see <see cref="GridMetrics.StepHeight"/> for the world-space size of a step.</summary>
        public readonly int Height;
        public readonly TerrainType Terrain;

        public Tile(int height, TerrainType terrain)
        {
            Height = height;
            Terrain = terrain;
        }

        public bool IsWalkable => Terrain.IsWalkable();

        public override string ToString() => $"{Terrain} h{Height}";
    }
}
