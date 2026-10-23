namespace FriendshipWarriors.Battle.Drops
{
    /// <summary>What a permanently lost unit leaves on its tile (FFT: a crystal or a treasure chest).</summary>
    public enum DropKind
    {
        /// <summary>Our crystal: a glowing steel rod. Teaches the fallen unit's abilities or restores HP and MP.</summary>
        SteelRod,

        /// <summary>A treasure chest: a piece of the fallen unit's gear, or an item for a monster.</summary>
        Chest,
    }
}
