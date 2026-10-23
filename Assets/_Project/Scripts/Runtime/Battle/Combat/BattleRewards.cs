using System;
using FriendshipWarriors.Units;

namespace FriendshipWarriors.Battle.Combat
{
    /// <summary>FFT's per-action rewards.</summary>
    public static class BattleRewards
    {
        /// <summary>Percent of a level earned for acting on a same-level target (FFT's 10 out of 100).</summary>
        public const int BasePercentOfLevel = 10;

        /// <summary>
        /// EXP for an action on <paramref name="target"/>. FFT gives 10 + (target level − actor level) out of the 100 EXP
        /// a level costs; we keep that shape but take it as a percentage of what the actor's next level costs on our
        /// exponential curve. So a same-level target is worth a tenth of a level at any level, each level the target
        /// is above the actor adds a point and each level below takes one away. At least 1 EXP, at most one full level.
        /// </summary>
        public static int Experience(Unit actor, Unit target)
        {
            if (actor.Level >= Unit.MaxLevel)
                return 0;
            int toNext = ExperienceCurve.ToNextLevel(actor.Level);
            int percent = BasePercentOfLevel + target.Level - actor.Level;
            int experience = (int)Math.Round(toNext * percent / 100.0);
            return Math.Clamp(experience, 1, toNext);
        }

        /// <summary>Prof for a successful action: 8 + 2 × job level + level / 4 (FFT's JP formula).</summary>
        public static int Proficiency(Unit actor) => 8 + 2 * actor.JobLevel(actor.Job) + actor.Level / 4;

        /// <summary>Allies on the field get a quarter of an action's Prof in their own current job (FFT).</summary>
        public static int SpilloverProficiency(int earned) => earned / 4;
    }
}
