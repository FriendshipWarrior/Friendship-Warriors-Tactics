namespace FriendshipWarriors.Battle
{
    /// <summary>In-battle Morale swings (the user's Morale design: heavy losses shake a unit's grit).</summary>
    public static class MoraleRules
    {
        /// <summary>Morale lost, for this battle, by every standing teammate when a unit is knocked out. Our addition; tune freely.</summary>
        public const int AllyKnockedOutLoss = 5;

        /// <summary>Morale a panicking unit regains at the start of each of its turns (FFT Chicken: +1 Brave per turn).</summary>
        public const int PanicRecoveryPerTurn = 1;
    }
}
