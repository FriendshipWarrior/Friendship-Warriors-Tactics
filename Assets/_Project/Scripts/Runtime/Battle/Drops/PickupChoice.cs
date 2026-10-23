namespace FriendshipWarriors.Battle.Drops
{
    /// <summary>What the unit taking a drop chooses. Chests have no choice.</summary>
    public enum PickupChoice
    {
        /// <summary>Opening a chest.</summary>
        None,

        /// <summary>Steel rod: fully restore HP and MP.</summary>
        Restore,

        /// <summary>Steel rod: learn the fallen unit's abilities.</summary>
        Inherit,
    }
}
