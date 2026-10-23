namespace FriendshipWarriors.Units.Abilities
{
    /// <summary>Which units an action ability may be aimed at.</summary>
    public enum AbilityTargeting
    {
        /// <summary>Only the user.</summary>
        Self = 0,

        /// <summary>Any unit that is standing, friend or foe (FFT lets you heal enemies and hit allies).</summary>
        Standing = 1,

        /// <summary>Only knocked-out units whose death counter hasn't run out.</summary>
        KnockedOut = 2,
    }
}
