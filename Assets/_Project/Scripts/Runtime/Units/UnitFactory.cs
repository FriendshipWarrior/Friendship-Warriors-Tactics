using System;

namespace FriendshipWarriors.Units
{
    /// <summary>Creates generic (non-story) units the way FFT does: random starting raw stats, then levelled up.</summary>
    public static class UnitFactory
    {
        // FFT's generic starting raw stats (the male ranges; we have no gender split). Recalled, not re-verified.
        private const int HpMin = 491520, HpMax = 524287;
        private const int MpMin = 229376, MpMax = 245759;
        private const int StartingSpeed = 98304;
        private const int StartingPhysicalAttack = 81920;
        private const int StartingMagicAttack = 65536;

        /// <summary>Range generic units roll Morale and Resonance in.</summary>
        public const int SpiritMin = 45, SpiritMax = 74;

        /// <summary>
        /// Builds a unit at <paramref name="level"/> in <paramref name="job"/>, granting whatever job levels the job's
        /// unlock requirements need. Deterministic for a given <paramref name="rng"/> state.
        /// </summary>
        public static Unit CreateGeneric(string name, Team team, JobDefinition job, int level, Random rng)
        {
            var raw = new RawStats(
                rng.Next(HpMin, HpMax + 1),
                rng.Next(MpMin, MpMax + 1),
                StartingSpeed,
                StartingPhysicalAttack,
                StartingMagicAttack);

            // Morale and Resonance (our Brave and Faith) roll like FFT generics' Brave and Faith, in a middling band.
            int morale = rng.Next(SpiritMin, SpiritMax + 1);
            int resonance = rng.Next(SpiritMin, SpiritMax + 1);
            var unit = new Unit(name, team, job, raw, morale, resonance);
            unit.GainExperience(ExperienceCurve.TotalFor(Math.Clamp(level, 1, Unit.MaxLevel)));
            GrantRequirements(unit, job);
            return unit;
        }

        private static void GrantRequirements(Unit unit, JobDefinition job)
        {
            foreach (var requirement in job.Requirements)
            {
                if (requirement.Job == null)
                    continue;
                GrantRequirements(unit, requirement.Job);
                int needed = JobLevels.ProficiencyFor(requirement.Level) - unit.Proficiency(requirement.Job);
                if (needed > 0)
                    unit.GainProficiency(needed, requirement.Job);
            }
        }
    }
}
