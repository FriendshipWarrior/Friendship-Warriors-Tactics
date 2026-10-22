namespace FriendshipWarriors.Units
{
    /// <summary>
    /// FFT's stat math (per the Battle Mechanics Guide, recalled rather than re-verified):
    /// shown stat = raw × job multiplier% / 1,638,400, and each level-up adds raw / (growth constant + level),
    /// so a lower growth constant means faster growth.
    /// </summary>
    public static class StatFormulas
    {
        public const int Divisor = 1638400;
        public const int MaxHpOrMp = 999;
        public const int MaxOtherStat = 99;

        public static int Shown(int raw, int multiplierPercent, int cap) =>
            System.Math.Min(cap, (int)((long)raw * multiplierPercent / Divisor));

        /// <param name="level">The unit's level before this level-up.</param>
        public static int Grow(int raw, int growthConstant, int level) => raw + raw / (growthConstant + level);
    }
}
