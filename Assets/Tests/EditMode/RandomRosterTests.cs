using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.QuickPlay;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;
using FriendshipWarriors.Units.Equipment;
using NUnit.Framework;
using UnityEditor;

namespace FriendshipWarriors.Tests
{
    public class RandomRosterTests
    {
        private const int TeamSize = 5;
        private const int Level = 5;

        private IReadOnlyList<JobDefinition> _jobs;
        private List<EquipmentDefinition> _equipment;

        [SetUp]
        public void SetUp()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<JobCatalog>("Assets/_Project/Data/Jobs/JobCatalog.asset");
            Assume.That(catalog, Is.Not.Null, "job catalog asset");
            _jobs = catalog.Jobs;
            _equipment = AssetDatabase.FindAssets("t:" + nameof(EquipmentDefinition), new[] { "Assets/_Project/Data/Equipment" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<EquipmentDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .OrderBy(e => e.name)
                .ToList();
            Assume.That(_equipment, Is.Not.Empty, "equipment assets");
        }

        private List<Unit> Build(int seed, Team team = Team.Player) =>
            RandomRoster.Build(team, TeamSize, Level, _jobs, _equipment, new System.Random(seed));

        private static string Describe(Unit u)
        {
            string Name(UnityEngine.Object o) => o != null ? o.name : "-";
            var slots = string.Join(",", System.Enum.GetValues(typeof(EquipmentSlot)).Cast<EquipmentSlot>().Select(s => Name(u.Equipped(s))));
            var learned = string.Join(",", u.LearnedAbilities.Select(a => a.name).OrderBy(n => n));
            return $"{u.Name}|{u.Job.name}|{u.Level}|{u.Raw.Hp}|{u.Raw.Mp}|{u.Morale}|{u.Resonance}|{slots}|" +
                   $"{Name(u.Reaction)}|{Name(u.Support)}|{Name(u.Movement)}|{Name(u.SecondaryJob)}|{learned}";
        }

        [Test]
        public void SameSeed_BuildsTheSameRoster()
        {
            var a = Build(4242).Select(Describe).ToList();
            var b = Build(4242).Select(Describe).ToList();

            Assert.That(b, Is.EqualTo(a));
        }

        [Test]
        public void DifferentSeeds_BuildDifferentRosters()
        {
            var a = Build(1).Select(Describe);
            var b = Build(2).Select(Describe);

            Assert.That(b, Is.Not.EqualTo(a));
        }

        [Test]
        public void Teams_AreFullSize_SameLevel_UniqueNames_FullHealth()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                var team = Build(seed);

                Assert.That(team.Count, Is.EqualTo(TeamSize), $"seed {seed}");
                Assert.That(team.Select(u => u.Name).Distinct().Count(), Is.EqualTo(TeamSize), $"seed {seed}: names");
                Assert.That(team.All(u => u.Level == Level), Is.True, $"seed {seed}: level");
                Assert.That(team.All(u => u.CurrentHp == u.Stats.MaxHp && u.CurrentMp == u.Stats.MaxMp), Is.True, $"seed {seed}: HP/MP");
            }
        }

        [Test]
        public void Gear_IsLegalForTheJob_AndFillsEverySlotItCan()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                foreach (var unit in Build(seed))
                {
                    var context = $"seed {seed} {unit.Name} ({unit.Job.name})";
                    foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
                    {
                        var item = unit.Equipped(slot);
                        if (item != null)
                            Assert.That(unit.Job.CanEquip(item), Is.True, $"{context}: {item.name} in {slot}");
                    }
                    Assert.That(unit.Weapon, Is.Not.Null, $"{context}: every job has a weapon type in the starter gear");
                    if (unit.Weapon.TwoHanded)
                        Assert.That(unit.Equipped(EquipmentSlot.OffHand), Is.Null, $"{context}: two-handed weapon");
                    Assert.That(unit.Head, Is.Not.Null, $"{context}: head");
                    Assert.That(unit.Body, Is.Not.Null, $"{context}: body");
                }
            }
        }

        [Test]
        public void Abilities_AreLearnedFromTheJobLine_AndEquippedLegally()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                foreach (var unit in Build(seed))
                {
                    var context = $"seed {seed} {unit.Name} ({unit.Job.name})";
                    var line = RandomRoster.JobLine(unit.Job);
                    var taught = line.SelectMany(j => j.Abilities).Where(a => a != null).ToHashSet();

                    Assert.That(taught.All(unit.HasLearned), Is.True, $"{context}: everything the job line teaches is learned");
                    Assert.That(unit.LearnedAbilities.All(taught.Contains), Is.True, $"{context}: nothing from outside the job line");
                    Assert.That(unit.MeetsRequirements(unit.Job), Is.True, $"{context}: unlock requirements");

                    foreach (var (equipped, kind) in new[] { (unit.Reaction, AbilityKind.Reaction), (unit.Support, AbilityKind.Support), (unit.Movement, AbilityKind.Movement) })
                    {
                        if (equipped == null)
                            continue;
                        Assert.That(equipped.Kind, Is.EqualTo(kind), $"{context}: {equipped.name} slot");
                        Assert.That(unit.HasLearned(equipped), Is.True, $"{context}: {equipped.name} learned");
                    }

                    if (unit.SecondaryJob != null)
                    {
                        Assert.That(unit.SecondaryJob, Is.Not.SameAs(unit.Job), context);
                        Assert.That(line.Contains(unit.SecondaryJob), Is.True, $"{context}: secondary from the job line");
                    }
                }
            }
        }

        [Test]
        public void ManyMatches_UseEveryJob()
        {
            var seen = new HashSet<JobDefinition>();
            for (int seed = 0; seed < 60; seed++)
            {
                foreach (var unit in Build(seed))
                    seen.Add(unit.Job);
            }

            Assert.That(seen.Count, Is.EqualTo(_jobs.Count));
        }

        [Test]
        public void JobLine_IncludesBaseJobs()
        {
            foreach (var job in _jobs)
            {
                var line = RandomRoster.JobLine(job);
                Assert.That(line[0], Is.SameAs(job));
                foreach (var requirement in job.Requirements)
                    Assert.That(line.Contains(requirement.Job), Is.True, $"{job.name} line includes {requirement.Job.name}");
            }
        }
    }
}
