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

        /// <summary>
        /// EXP for an action that isn't an attack (healing, buffs, items), whoever it targets: the same-level share of
        /// what the actor's next level costs, so it grows with the actor's level (about 6% more per level on our curve)
        /// and support roles level at the same pace as fighting equal foes. FFT bases this on the target's level; using
        /// the actor's own level is our change. None at level 99.
        /// </summary>
        public static int SupportExperience(Unit actor)
        {
            if (actor.Level >= Unit.MaxLevel)
                return 0;
            int toNext = ExperienceCurve.ToNextLevel(actor.Level);
            return Math.Max(1, (int)Math.Round(toNext * BasePercentOfLevel / 100.0));
        }

        /// <summary>Prof for a successful action: 8 + 2 × job level + level / 4 (FFT's JP formula).</summary>
        public static int Proficiency(Unit actor)
        {
            int prof = 8 + 2 * actor.JobLevel(actor.Job) + actor.Level / 4;
            // A support ability like FFT's Gained JP Up (×1.5) scales it.
            int bonus = actor.Support != null ? actor.Support.ProficiencyBonusPercent : 0;
            return prof * (100 + bonus) / 100;
        }

        /// <summary>Allies on the field get a quarter of an action's Prof in their own current job (FFT).</summary>
        public static int SpilloverProficiency(int earned) => earned / 4;
    }
}
