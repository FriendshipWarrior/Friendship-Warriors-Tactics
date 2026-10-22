namespace FriendshipWarriors.Units.Abilities
{
    /// <summary>
    /// Lets a unit spend one part of its turn as the other. Not in FFT; a deliberate addition.
    /// </summary>
    public enum TurnTrade
    {
        None,

        /// <summary>Give up the Move to Act a second time.</summary>
        MoveForAct,

        /// <summary>Give up the Act to Move a second time.</summary>
        ActForMove,
    }
}
