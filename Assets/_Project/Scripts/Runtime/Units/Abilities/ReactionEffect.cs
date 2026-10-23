namespace FriendshipWarriors.Units.Abilities
{
    /// <summary>
    /// What a reaction ability does. Triggered ones roll against the reactor's Morale (FFT: Brave); passive ones
    /// always apply. Values are serialized into assets, so append only.
    /// </summary>
    public enum ReactionEffect
    {
        None = 0,

        /// <summary>When hit by a physical attack, strike back with the reaction's own damage (FFT Squire Counter Tackle).</summary>
        CounterStrike = 1,

        /// <summary>When damaged, use the team's weakest HP item on yourself (FFT Chemist Auto Potion).</summary>
        AutoHeal = 2,

        /// <summary>Passive: weapon evasion counts against front and side physical attacks (FFT Knight Parry / Weapon Guard).</summary>
        Parry = 3,

        /// <summary>When damaged, Speed +1 for the battle (FFT Archer Speed Save).</summary>
        SpeedUp = 4,

        /// <summary>When damaged, gain Regen (FFT White Mage Regenerator).</summary>
        GainRegen = 5,

        /// <summary>When hit by an enemy's offensive spell, cast the same spell back at the caster (FFT Black Mage Counter Magic).</summary>
        CounterMagic = 6,
    }
}
