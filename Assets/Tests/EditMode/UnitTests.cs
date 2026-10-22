using System.Collections.Generic;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Pathfinding;
using FriendshipWarriors.Units;
using NUnit.Framework;
using UnityEngine;

namespace FriendshipWarriors.Tests
{
    public class UnitTests
    {
        private readonly List<Object> _created = new();
        private JobDefinition _cadet;
        private JobDefinition _vanguard;
        private JobDefinition _evoker;

        [SetUp]
        public void SetUp()
        {
            _cadet = Job("Cadet", new StatFactors(100, 75, 100, 90, 80), new StatFactors(11, 15, 100, 60, 50), move: 4);
            _vanguard = Job("Vanguard", new StatFactors(120, 80, 100, 120, 80), new StatFactors(10, 15, 100, 40, 50), move: 3,
                classEvasion: 10, requirements: new JobRequirement(_cadet, 2));
            _evoker = Job("Evoker", new StatFactors(75, 120, 100, 60, 150), new StatFactors(12, 9, 100, 60, 50), move: 3);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                Object.DestroyImmediate(obj);
            _created.Clear();
        }

        private JobDefinition Job(string name, StatFactors multipliers, StatFactors growth, int move, int classEvasion = 5,
            params JobRequirement[] requirements)
        {
            var job = ScriptableObject.CreateInstance<JobDefinition>();
            job.Define(name, "", multipliers, growth, move, 3, classEvasion, requirements, Color.gray);
            _created.Add(job);
            return job;
        }

        private static RawStats StandardRaw => new(491520, 229376, 98304, 81920, 65536);

        [Test]
        public void ShownStats_AreRawTimesJobMultiplier()
        {
            var unit = new Unit("Test", Team.Player, _cadet, StandardRaw);
            var stats = unit.Stats;

            // 491520 * 100 / 1638400 = 30, 98304 * 100 / 1638400 = 6, and so on.
            Assert.That(stats.MaxHp, Is.EqualTo(30));
            Assert.That(stats.MaxMp, Is.EqualTo(10));
            Assert.That(stats.Speed, Is.EqualTo(6));
            Assert.That(stats.PhysicalAttack, Is.EqualTo(4));
            Assert.That(stats.MagicAttack, Is.EqualTo(3));
            Assert.That(stats.Move, Is.EqualTo(4));
            Assert.That(unit.CurrentHp, Is.EqualTo(30));
        }

        [Test]
        public void ShownStats_AreCapped()
        {
            Assert.That(StatFormulas.Shown(int.MaxValue, 150, StatFormulas.MaxOtherStat), Is.EqualTo(99));
            Assert.That(StatFormulas.Shown(int.MaxValue, 150, StatFormulas.MaxHpOrMp), Is.EqualTo(999));
        }

        [Test]
        public void LevelUp_GrowsRawStatsByRawOverConstantPlusLevel()
        {
            var unit = new Unit("Test", Team.Player, _cadet, StandardRaw);

            int levels = unit.GainExperience(100);

            Assert.That(levels, Is.EqualTo(1));
            Assert.That(unit.Level, Is.EqualTo(2));
            Assert.That(unit.Raw.Hp, Is.EqualTo(491520 + 491520 / (11 + 1)));
            Assert.That(unit.Raw.PhysicalAttack, Is.EqualTo(81920 + 81920 / (60 + 1)));
        }

        [Test]
        public void GrowthDependsOnTheJobYouLevelIn()
        {
            var cadet = new Unit("A", Team.Player, _cadet, StandardRaw);
            var evoker = new Unit("B", Team.Player, _evoker, StandardRaw);

            cadet.GainExperience(1000);
            evoker.GainExperience(1000);

            Assert.That(evoker.Raw.Mp, Is.GreaterThan(cadet.Raw.Mp), "Evoker's MP growth constant (9) beats Cadet's (15)");
            Assert.That(cadet.Raw.Hp, Is.GreaterThan(evoker.Raw.Hp), "Cadet's HP growth constant (11) beats Evoker's (12)");
        }

        [Test]
        public void LevelUp_AddsTheMaxHpGainToCurrentHp()
        {
            var unit = new Unit("Test", Team.Player, _cadet, StandardRaw);
            unit.GainExperience(500);

            Assert.That(unit.CurrentHp, Is.EqualTo(unit.Stats.MaxHp));
        }

        [Test]
        public void Experience_CarriesOverAndStopsAtTheLevelCap()
        {
            var unit = new Unit("Test", Team.Player, _cadet, StandardRaw);

            // Level 1→2 costs 100 and 2→3 costs 106, leaving 44 toward level 4.
            unit.GainExperience(250);
            Assert.That(unit.Level, Is.EqualTo(3));
            Assert.That(unit.Experience, Is.EqualTo(44));

            unit.GainExperience(ExperienceCurve.TotalFor(Unit.MaxLevel) + 5000);
            Assert.That(unit.Level, Is.EqualTo(Unit.MaxLevel));
            Assert.That(unit.Experience, Is.Zero);
        }

        [TestCase(0, 1)]
        [TestCase(199, 1)]
        [TestCase(200, 2)]
        [TestCase(699, 3)]
        [TestCase(3000, 8)]
        [TestCase(99999, 8)]
        public void JobLevels_FollowFftThresholds(int proficiency, int expectedLevel)
        {
            Assert.That(JobLevels.ForProficiency(proficiency), Is.EqualTo(expectedLevel));
        }

        [Test]
        public void ExperienceCurve_GrowsEveryLevel()
        {
            Assert.That(ExperienceCurve.ToNextLevel(1), Is.EqualTo(100));
            for (int level = 2; level < Unit.MaxLevel; level++)
                Assert.That(ExperienceCurve.ToNextLevel(level), Is.GreaterThan(ExperienceCurve.ToNextLevel(level - 1)), $"level {level}");
            Assert.That(ExperienceCurve.TotalFor(1), Is.Zero);
            Assert.That(ExperienceCurve.TotalFor(3), Is.EqualTo(206));
        }

        [Test]
        public void GainingTheExactTotal_ReachesTheLevel()
        {
            var unit = new Unit("Test", Team.Player, _cadet, StandardRaw);

            unit.GainExperience(ExperienceCurve.TotalFor(40));

            Assert.That(unit.Level, Is.EqualTo(40));
            Assert.That(unit.Experience, Is.Zero);
        }

        [Test]
        public void MasteringAJob_AwardsOneCommendation()
        {
            var unit = new Unit("Test", Team.Player, _cadet, StandardRaw);

            unit.GainProficiency(2999);
            Assert.That(unit.HasMastered(_cadet), Is.False);
            Assert.That(unit.Commendations, Is.Empty);

            unit.GainProficiency(1);
            unit.GainProficiency(500);

            Assert.That(unit.HasMastered(_cadet), Is.True);
            Assert.That(unit.Commendations.Count, Is.EqualTo(1));
            Assert.That(unit.Commendations[0].Source, Is.EqualTo(CommendationSource.JobMastery));
            Assert.That(unit.Commendations[0].Job, Is.SameAs(_cadet));
        }

        [Test]
        public void FieldChallenges_AwardCommendations()
        {
            var unit = new Unit("Test", Team.Player, _cadet, StandardRaw);

            unit.AwardCommendation("Held the Bridge");

            Assert.That(unit.Commendations.Count, Is.EqualTo(1));
            Assert.That(unit.Commendations[0].Source, Is.EqualTo(CommendationSource.FieldChallenge));
        }

        [Test]
        public void AdvancedJobs_UnlockAtJobLevelTwoInTheirBaseJob()
        {
            var unit = new Unit("Test", Team.Player, _cadet, StandardRaw);

            Assert.That(unit.TryChangeJob(_vanguard), Is.False);
            unit.GainProficiency(199);
            Assert.That(unit.TryChangeJob(_vanguard), Is.False);
            unit.GainProficiency(1);
            Assert.That(unit.TryChangeJob(_vanguard), Is.True);
            Assert.That(unit.Job, Is.SameAs(_vanguard));
        }

        [Test]
        public void ChangingJob_ChangesShownStatsButKeepsRawStats()
        {
            var unit = new Unit("Test", Team.Player, _cadet, StandardRaw);
            unit.GainProficiency(200);
            var raw = unit.Raw;

            unit.TryChangeJob(_vanguard);

            Assert.That(unit.Raw.Hp, Is.EqualTo(raw.Hp));
            Assert.That(unit.Stats.MaxHp, Is.EqualTo(36), "491520 * 120 / 1638400");
            Assert.That(unit.Stats.Move, Is.EqualTo(3));
            Assert.That(unit.Stats.ClassEvasion, Is.EqualTo(10));
            Assert.That(unit.CurrentHp, Is.EqualTo(30), "current HP carries over");
        }

        [Test]
        public void ChangingToASmallerMaximum_CapsCurrentHp()
        {
            var unit = new Unit("Test", Team.Player, _cadet, StandardRaw);

            unit.TryChangeJob(_evoker);

            Assert.That(unit.CurrentHp, Is.EqualTo(unit.Stats.MaxHp));
            Assert.That(unit.Stats.MaxHp, Is.LessThan(30));
        }

        [Test]
        public void Factory_IsDeterministicAndGrantsUnlockRequirements()
        {
            var a = UnitFactory.CreateGeneric("A", Team.Enemy, _vanguard, 10, new System.Random(5));
            var b = UnitFactory.CreateGeneric("B", Team.Enemy, _vanguard, 10, new System.Random(5));

            Assert.That(a.Level, Is.EqualTo(10));
            Assert.That(a.Raw.Hp, Is.EqualTo(b.Raw.Hp));
            Assert.That(a.Raw.Mp, Is.EqualTo(b.Raw.Mp));
            Assert.That(a.JobLevel(_cadet), Is.GreaterThanOrEqualTo(2));
            Assert.That(a.MeetsRequirements(_vanguard), Is.True);
        }

        [Test]
        public void Occupancy_TreatsTeammatesAsAlliesAndOthersAsEnemies()
        {
            var mover = new Unit("Mover", Team.Player, _cadet, StandardRaw) { Coord = new GridCoord(0, 0) };
            var friend = new Unit("Friend", Team.Player, _cadet, StandardRaw) { Coord = new GridCoord(1, 0) };
            var foe = new Unit("Foe", Team.Enemy, _cadet, StandardRaw) { Coord = new GridCoord(2, 0) };

            var occupancy = UnitOccupancy.For(mover, new[] { mover, friend, foe });

            Assert.That(occupancy(new GridCoord(0, 0)), Is.EqualTo(Occupancy.Empty));
            Assert.That(occupancy(new GridCoord(1, 0)), Is.EqualTo(Occupancy.Ally));
            Assert.That(occupancy(new GridCoord(2, 0)), Is.EqualTo(Occupancy.Enemy));
            Assert.That(occupancy(new GridCoord(3, 0)), Is.EqualTo(Occupancy.Empty));
        }
    }
}
