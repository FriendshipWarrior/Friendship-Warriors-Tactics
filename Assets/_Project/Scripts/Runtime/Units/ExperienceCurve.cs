using System;

namespace FriendshipWarriors.Units
{
    /// <summary>
    /// EXP needed to reach the next level. Unlike FFT's flat 100 per level, each level costs more than the last:
    /// <c>Base × Growth^(level − 1)</c>, rounded. Tune the two constants to taste.
    /// </summary>
    public static class ExperienceCurve
    {
        public const int BaseExperience = 100;
        public const double GrowthPerLevel = 1.06;

        /// <param name="level">The unit's current level (1–98).</param>
        public static int ToNextLevel(int level)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level), level, "Levels start at 1.");
            return (int)Math.Round(BaseExperience * Math.Pow(GrowthPerLevel, level - 1));
        }

        /// <summary>Total EXP from level 1 to <paramref name="level"/>.</summary>
        public static int TotalFor(int level)
        {
            int total = 0;
            for (int l = 1; l < level; l++)
                total += ToNextLevel(l);
            return total;
        }
    }
}
