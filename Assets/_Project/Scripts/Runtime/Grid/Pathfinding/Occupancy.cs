namespace FriendshipWarriors.Grid.Pathfinding
{
    /// <summary>Who stands on a tile, from the point of view of the unit that is moving.</summary>
    public enum Occupancy
    {
        Empty,

        /// <summary>Can be passed through but not stopped on.</summary>
        Ally,

        /// <summary>Blocks movement entirely.</summary>
        Enemy,
    }
}
