namespace FriendshipWarriors.Units
{
    /// <summary>
    /// Total Proficiency (Prof) needed for each job level, using FFT's job point thresholds (recalled, not
    /// re-verified): level 2 at 200 up to level 8 at 3000. Reaching level 8 masters the job.
    /// </summary>
    public static class JobLevels
    {
        public const int MaxLevel = 8;

        private static readonly int[] Thresholds = { 0, 200, 400, 700, 1100, 1600, 2200, 3000 };

        public static int ForProficiency(int totalProficiency)
        {
            int level = 1;
            while (level < MaxLevel && totalProficiency >= Thresholds[level])
                level++;
            return level;
        }

        public static int ProficiencyFor(int level) => Thresholds[System.Math.Clamp(level, 1, MaxLevel) - 1];
    }
}
