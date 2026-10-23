using System;

namespace FriendshipWarriors.Battle.Drops
{
    /// <summary>Tunable drop rules (see <see cref="DropRules"/>); <see cref="DropTable"/> holds them as an asset.</summary>
    public readonly struct DropSettings
    {
        /// <summary>
        /// Percent chance a lost unit leaves a steel rod rather than a chest. FFT's odds aren't documented anywhere we
        /// found, so this is an even split.
        /// </summary>
        public readonly int RodChance;

        /// <summary>Percent chance each of the fallen unit's abilities is learned from its rod (100 = all of them).</summary>
        public readonly int InheritChance;

        /// <summary>
        /// FFT only teaches abilities from jobs the picker has unlocked (Death needs Black Mage). Off: every ability
        /// is learned, usable once its job is.
        /// </summary>
        public readonly bool RequireUnlockedJob;

        public DropSettings(int rodChance, int inheritChance, bool requireUnlockedJob)
        {
            RodChance = Math.Clamp(rodChance, 0, 100);
            InheritChance = Math.Clamp(inheritChance, 0, 100);
            RequireUnlockedJob = requireUnlockedJob;
        }

        public static DropSettings Default => new(50, 100, true);
    }
}
