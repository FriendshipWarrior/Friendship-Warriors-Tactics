namespace FriendshipWarriors.Units.Abilities
{
    /// <summary>What an action ability does to its target. Values are serialized into assets; append only.</summary>
    public enum AbilityEffect
    {
        None = 0,

        /// <summary>Physical damage: P.Atk × a random whole number in the ability's random range.</summary>
        Damage = 1,

        /// <summary>Raises P.Atk by the ability's amount until the battle ends.</summary>
        BoostPhysicalAttack = 2,

        RestoreHp = 3,
        RestoreMp = 4,

        /// <summary>Restores HP and MP to full.</summary>
        RestoreAll = 5,

        /// <summary>Brings a knocked-out unit back while its death counter is still running.</summary>
        Revive = 6,
    }
}
