using System;
using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;
using FriendshipWarriors.Units.Equipment;

namespace FriendshipWarriors.QuickPlay
{
    /// <summary>
    /// Builds a random quick-play team: random jobs (advanced jobs come with their unlock requirements), random legal
    /// gear in every slot, every ability the job and its base jobs teach learned, random reaction / support / movement
    /// abilities equipped, a secondary command when one is available, and unique names. Everything is drawn from one
    /// <see cref="Random"/>, so the same seed always builds the same team. Pure C#, no scene dependencies.
    /// </summary>
    public static class RandomRoster
    {
        /// <summary>Military-steampunk surnames for generic soldiers.</summary>
        public static readonly IReadOnlyList<string> DefaultNames = new[]
        {
            "Ashford", "Brennan", "Calloway", "Draven", "Ellery", "Fairweather", "Garrick", "Halloway", "Ingram",
            "Jessup", "Kestrel", "Lockhart", "Marlowe", "Northcott", "Orsini", "Pembroke", "Quill", "Ravensworth",
            "Sterling", "Thorne", "Underhill", "Vance", "Whitlock", "Yardley", "Brass", "Cogburn", "Ferris", "Gage",
        };

        public static List<Unit> Build(Team team, int size, int level, IReadOnlyList<JobDefinition> jobs,
            IReadOnlyList<EquipmentDefinition> equipment, Random rng, IReadOnlyList<string> names = null)
        {
            if (jobs == null || jobs.Count == 0) throw new ArgumentException("Need at least one job.", nameof(jobs));
            names ??= DefaultNames;

            var namePool = names.ToList();
            var units = new List<Unit>(size);
            for (int i = 0; i < size; i++)
            {
                var job = jobs[rng.Next(jobs.Count)];
                string name = TakeName(namePool, rng, i);
                var unit = UnitFactory.CreateGeneric(name, team, job, level, rng);
                LearnAbilities(unit);
                EquipPassives(unit, rng);
                ChooseSecondary(unit, rng);
                EquipGear(unit, equipment, rng);
                unit.RestoreFully();
                units.Add(unit);
            }
            return units;
        }

        /// <summary>Removes a random name from the pool; falls back to numbered names if the pool runs out.</summary>
        private static string TakeName(List<string> pool, Random rng, int index)
        {
            if (pool.Count == 0)
                return $"Recruit {index + 1}";
            int pick = rng.Next(pool.Count);
            string name = pool[pick];
            pool.RemoveAt(pick);
            return name;
        }

        /// <summary>The unit's job followed by every job its unlock requirements lead back to, without repeats.</summary>
        public static List<JobDefinition> JobLine(JobDefinition job)
        {
            var line = new List<JobDefinition>();
            void Visit(JobDefinition j)
            {
                if (j == null || line.Contains(j))
                    return;
                line.Add(j);
                foreach (var requirement in j.Requirements)
                    Visit(requirement.Job);
            }
            Visit(job);
            return line;
        }

        /// <summary>Grants the Prof for, and learns, every ability the job line teaches.</summary>
        private static void LearnAbilities(Unit unit)
        {
            foreach (var job in JobLine(unit.Job))
            {
                foreach (var ability in job.Abilities)
                {
                    if (ability == null || unit.HasLearned(ability))
                        continue;
                    unit.GainProficiency(ability.LearnCost, job);
                    unit.TryLearn(ability, job);
                }
            }
        }

        private static void EquipPassives(Unit unit, Random rng)
        {
            foreach (var kind in new[] { AbilityKind.Reaction, AbilityKind.Support, AbilityKind.Movement })
            {
                var options = unit.LearnedAbilities.Where(a => a.Kind == kind).OrderBy(a => a.name, StringComparer.Ordinal).ToList();
                if (options.Count > 0)
                    unit.TryEquip(options[rng.Next(options.Count)]);
            }
        }

        /// <summary>Another job in the unit's line with a learned action ability becomes its secondary command, if any.</summary>
        private static void ChooseSecondary(Unit unit, Random rng)
        {
            var options = JobLine(unit.Job).Skip(1)
                .Where(j => j.Abilities.Any(a => a != null && a.Kind == AbilityKind.Action && unit.HasLearned(a)))
                .ToList();
            if (options.Count > 0)
                unit.TrySetSecondary(options[rng.Next(options.Count)]);
        }

        /// <summary>
        /// A random item the job can use in each slot, weapon first so a two-handed weapon leaves the off hand empty.
        /// </summary>
        private static void EquipGear(Unit unit, IReadOnlyList<EquipmentDefinition> equipment, Random rng)
        {
            if (equipment == null)
                return;
            foreach (var category in new[] { EquipmentCategory.Weapon, EquipmentCategory.Shield, EquipmentCategory.Head, EquipmentCategory.Body, EquipmentCategory.Accessory })
            {
                if (category == EquipmentCategory.Shield && unit.Weapon != null && unit.Weapon.TwoHanded)
                    continue;
                var options = equipment.Where(e => e != null && e.Category == category && unit.Job.CanEquip(e)).ToList();
                if (options.Count > 0)
                    unit.TryEquipItem(options[rng.Next(options.Count)]);
            }
        }
    }
}
