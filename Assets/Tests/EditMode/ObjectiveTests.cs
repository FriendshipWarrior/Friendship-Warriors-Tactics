using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Battle.AI;
using FriendshipWarriors.Battle.Objectives;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Pathfinding;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Equipment;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FriendshipWarriors.Tests
{
    public class ObjectiveTests
    {
        private readonly List<Object> _created = new();
        private JobDefinition _job;
        private EquipmentDefinition _sword;

        [SetUp]
        public void SetUp()
        {
            _job = ScriptableObject.CreateInstance<JobDefinition>();
            _job.Define("Soldier", "", new StatFactors(100, 100, 100, 100, 100), new StatFactors(11, 15, 100, 60, 50), 4, 3, 0,
                new JobRequirement[0], Color.gray);
            _job.SetEquipment(new[] { WeaponType.Sword }, canUseShields: false);
            _sword = ScriptableObject.CreateInstance<EquipmentDefinition>();
            _sword.DefineWeapon("Sword", "", WeaponType.Sword, 4, 1, 1, 3);
            _created.Add(_job);
            _created.Add(_sword);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                Object.DestroyImmediate(obj);
            _created.Clear();
        }

        private Unit Soldier(string name, Team team, string key = null, int x = 0, int y = 0)
        {
            var unit = new Unit(name, team, _job, new RawStats(491520, 229376, 6 * 16384, 6 * 16384, 4 * 16384))
            {
                Key = key,
                Coord = new GridCoord(x, y),
            };
            unit.TryEquipItem(_sword);
            return unit;
        }

        private static void KnockOut(Unit unit) => unit.TakeDamage(99999);

        /// <summary>Knocks a unit out and runs its death counter down, as if it had sat through three turns.</summary>
        private static void Lose(Unit unit)
        {
            KnockOut(unit);
            while (!unit.TickDeathCounter()) { }
        }

        private static BattleOutcome Evaluate(BattleObjective objective, IEnumerable<Unit> field, params Unit[] lost) =>
            ObjectiveEvaluator.Evaluate(objective, field, lost);

        // ------------------------------------------------------------------ Standard: defeat all enemies

        [Test]
        public void Standard_IsOngoingWhileBothSidesStand()
        {
            var units = new[] { Soldier("P", Team.Player), Soldier("E", Team.Enemy) };

            Assert.That(Evaluate(BattleObjective.Standard, units).Kind, Is.EqualTo(BattleOutcomeKind.Ongoing));
        }

        [Test]
        public void Standard_IsWonWhenEveryEnemyIsDown()
        {
            var e1 = Soldier("E1", Team.Enemy);
            var e2 = Soldier("E2", Team.Enemy);
            KnockOut(e1);
            Lose(e2);

            var outcome = Evaluate(BattleObjective.Standard, new[] { Soldier("P", Team.Player), e1 }, e2);

            Assert.That(outcome.Kind, Is.EqualTo(BattleOutcomeKind.Victory));
            Assert.That(outcome.Reason, Does.Contain("All enemies"));
        }

        [Test]
        public void AnyObjective_IsLostWhenEveryPlayerUnitIsDown()
        {
            var p = Soldier("P", Team.Player);
            KnockOut(p);
            var target = Soldier("Boss", Team.Enemy, "boss");

            Assert.That(Evaluate(BattleObjective.Standard, new[] { p, Soldier("E", Team.Enemy) }).Kind, Is.EqualTo(BattleOutcomeKind.Defeat));
            Assert.That(Evaluate(new BattleObjective(VictoryCondition.DefeatTarget, "boss"), new[] { p, target }).Kind,
                Is.EqualTo(BattleOutcomeKind.Defeat));
        }

        // ------------------------------------------------------------------ Defeat a target

        [Test]
        public void DefeatTarget_IsWonTheMomentTheTargetIsKnockedOut_EvenWithOthersStanding()
        {
            var target = Soldier("Commander", Team.Enemy, "commander");
            var units = new[] { Soldier("P", Team.Player), target, Soldier("E", Team.Enemy), Soldier("E2", Team.Enemy) };
            var objective = new BattleObjective(VictoryCondition.DefeatTarget, "commander");

            Assert.That(Evaluate(objective, units).Kind, Is.EqualTo(BattleOutcomeKind.Ongoing));
            KnockOut(target);
            var outcome = Evaluate(objective, units);

            Assert.That(outcome.Kind, Is.EqualTo(BattleOutcomeKind.Victory));
            Assert.That(outcome.Reason, Is.EqualTo("Commander defeated"));
        }

        [Test]
        public void DefeatTarget_IgnoresOtherEnemiesFalling()
        {
            var target = Soldier("Commander", Team.Enemy, "commander");
            var grunt = Soldier("Grunt", Team.Enemy);
            KnockOut(grunt);
            var objective = new BattleObjective(VictoryCondition.DefeatTarget, "commander");

            Assert.That(Evaluate(objective, new[] { Soldier("P", Team.Player), target, grunt }).Kind, Is.EqualTo(BattleOutcomeKind.Ongoing));
        }

        [Test]
        public void DefeatTarget_WithTheTargetMissing_FallsBackToDefeatingEveryone()
        {
            var e = Soldier("E", Team.Enemy);
            var objective = new BattleObjective(VictoryCondition.DefeatTarget, "nobody");
            var units = new[] { Soldier("P", Team.Player), e };

            Assert.That(Evaluate(objective, units).Kind, Is.EqualTo(BattleOutcomeKind.Ongoing));
            KnockOut(e);
            Assert.That(Evaluate(objective, units).Kind, Is.EqualTo(BattleOutcomeKind.Victory));
        }

        [Test]
        public void DefeatTarget_OnlyCountsAnEnemyWithThatKey()
        {
            var lookalike = Soldier("Mine", Team.Player, "commander");
            var objective = new BattleObjective(VictoryCondition.DefeatTarget, "commander");

            Assert.That(ObjectiveEvaluator.FindTarget(objective, new[] { lookalike }), Is.Null);
        }

        // ------------------------------------------------------------------ Protect

        [Test]
        public void Protected_KnockedOutOrRevived_IsStillOngoing()
        {
            var escort = Soldier("Escort", Team.Player, "escort");
            var objective = new BattleObjective(VictoryCondition.DefeatAllEnemies, "", "escort");
            var units = new[] { escort, Soldier("P", Team.Player), Soldier("E", Team.Enemy) };

            KnockOut(escort);
            Assert.That(Evaluate(objective, units).Kind, Is.EqualTo(BattleOutcomeKind.Ongoing), "KO'd but the counter is running");
            escort.TickDeathCounter();
            escort.TickDeathCounter();
            Assert.That(escort.Revive(10), Is.True);
            Assert.That(Evaluate(objective, units).Kind, Is.EqualTo(BattleOutcomeKind.Ongoing), "revived in time");
        }

        [Test]
        public void Protected_PermanentlyLost_IsADefeat()
        {
            var escort = Soldier("Escort", Team.Player, "escort");
            var objective = new BattleObjective(VictoryCondition.DefeatAllEnemies, "", "escort");
            Lose(escort);

            var outcome = Evaluate(objective, new[] { Soldier("P", Team.Player), Soldier("E", Team.Enemy) }, escort);

            Assert.That(outcome.Kind, Is.EqualTo(BattleOutcomeKind.Defeat));
            Assert.That(outcome.Reason, Is.EqualTo("Escort was lost"));
        }

        [Test]
        public void LosingAnUnprotectedUnit_IsNotADefeat()
        {
            var private1 = Soldier("Private", Team.Player);
            Lose(private1);
            var objective = new BattleObjective(VictoryCondition.DefeatAllEnemies, "", "escort");

            var outcome = Evaluate(objective, new[] { Soldier("Escort", Team.Player, "escort"), Soldier("E", Team.Enemy) }, private1);

            Assert.That(outcome.Kind, Is.EqualTo(BattleOutcomeKind.Ongoing));
        }

        // ------------------------------------------------------------------ Simultaneous events

        [Test]
        public void LastEnemyAndLastPlayerFallingTogether_IsAVictory()
        {
            var p = Soldier("P", Team.Player);
            var e = Soldier("E", Team.Enemy);
            KnockOut(p);
            KnockOut(e);

            Assert.That(Evaluate(BattleObjective.Standard, new[] { p, e }).Kind, Is.EqualTo(BattleOutcomeKind.Victory));
        }

        [Test]
        public void VictoryInTheSameCheck_BeatsAProtectedUnitLost()
        {
            var escort = Soldier("Escort", Team.Player, "escort");
            Lose(escort);
            var e = Soldier("E", Team.Enemy);
            KnockOut(e);
            var objective = new BattleObjective(VictoryCondition.DefeatAllEnemies, "", "escort");

            Assert.That(Evaluate(objective, new[] { Soldier("P", Team.Player), e }, escort).Kind, Is.EqualTo(BattleOutcomeKind.Victory));
        }

        [Test]
        public void TargetDownInTheSameCheck_BeatsAProtectedUnitLostAndTheLastPlayerDown()
        {
            var escort = Soldier("Escort", Team.Player, "escort");
            Lose(escort);
            var p = Soldier("P", Team.Player);
            KnockOut(p);
            var commander = Soldier("Commander", Team.Enemy, "commander");
            KnockOut(commander);
            var objective = new BattleObjective(VictoryCondition.DefeatTarget, "commander", "escort");

            var outcome = Evaluate(objective, new[] { p, commander, Soldier("E", Team.Enemy) }, escort);

            Assert.That(outcome.Kind, Is.EqualTo(BattleOutcomeKind.Victory));
            Assert.That(outcome.Reason, Is.EqualTo("Commander defeated"));
        }

        [Test]
        public void ProtectedLost_WithoutVictory_IsADefeatEvenWithPlayersStanding()
        {
            var escort = Soldier("Escort", Team.Player, "escort");
            Lose(escort);
            var objective = new BattleObjective(VictoryCondition.DefeatAllEnemies, "", "escort");

            var outcome = Evaluate(objective, new[] { Soldier("P", Team.Player), Soldier("E", Team.Enemy) }, escort);

            Assert.That(outcome.Kind, Is.EqualTo(BattleOutcomeKind.Defeat));
            Assert.That(outcome.Reason, Is.EqualTo("Escort was lost"));
        }

        [Test]
        public void Describe_NamesTheTargetAndProtectedUnits()
        {
            var units = new[]
            {
                Soldier("Escort Lira", Team.Player, "escort"),
                Soldier("Commander Vael", Team.Enemy, "commander"),
            };

            Assert.That(ObjectiveEvaluator.Describe(BattleObjective.Standard, units), Is.EqualTo("Defeat all enemies"));
            Assert.That(ObjectiveEvaluator.Describe(new BattleObjective(VictoryCondition.DefeatTarget, "commander", "escort"), units),
                Is.EqualTo("Defeat Commander Vael  |  Protect: Escort Lira"));
        }

        // ------------------------------------------------------------------ Setups

        private BattleSetup Setup(int playerSize, int enemySize, RosterEntry[] players, RosterEntry[] enemies, BattleObjective objective)
        {
            var setup = ScriptableObject.CreateInstance<BattleSetup>();
            _created.Add(setup);
            setup.Define("Test", playerSize, enemySize, 5, players, enemies, objective);
            return setup;
        }

        private RosterEntry[] Roster(int count, int keyedIndex = -1, string key = "") =>
            Enumerable.Range(0, count).Select(i => new RosterEntry(_job, i == keyedIndex ? key : "")).ToArray();

        [Test]
        public void Fielded_IsCappedByTeamSizeRosterAndDeploymentZone()
        {
            Assert.That(BattleSetup.Fielded(5, 7, 9), Is.EqualTo(5));
            Assert.That(BattleSetup.Fielded(5, 3, 9), Is.EqualTo(3));
            Assert.That(BattleSetup.Fielded(5, 7, 4), Is.EqualTo(4));
        }

        [Test]
        public void Validator_FlagsBrokenReferencesAndSizes()
        {
            var noTarget = Setup(5, 5, Roster(5), Roster(5), new BattleObjective(VictoryCondition.DefeatTarget, "boss"));
            var targetBench = Setup(5, 3, Roster(5), Roster(5, keyedIndex: 4, key: "boss"), new BattleObjective(VictoryCondition.DefeatTarget, "boss"));
            var noEscort = Setup(5, 5, Roster(5), Roster(5), new BattleObjective(VictoryCondition.DefeatAllEnemies, "", "escort"));
            var shortRoster = Setup(5, 5, Roster(3), Roster(5), BattleObjective.Standard);
            var fine = Setup(5, 5, Roster(5, 2, "escort"), Roster(5, 3, "boss"), new BattleObjective(VictoryCondition.DefeatTarget, "boss", "escort"));

            Assert.That(BattleSetupValidator.Validate(noTarget), Has.Some.Contains("isn't among the fielded enemies"));
            Assert.That(BattleSetupValidator.Validate(targetBench), Has.Some.Contains("isn't among the fielded enemies"), "the target sits outside the 3 fielded");
            Assert.That(BattleSetupValidator.Validate(noEscort), Has.Some.Contains("Protected unit"));
            Assert.That(BattleSetupValidator.Validate(shortRoster), Has.Some.Contains("3 of 5"));
            Assert.That(BattleSetupValidator.Validate(fine), Is.Empty);
        }

        [TestCase("Assets/_Project/Data/Setups/Setup_Standard.asset", VictoryCondition.DefeatAllEnemies)]
        [TestCase("Assets/_Project/Data/Setups/Setup_DefeatTheCommander.asset", VictoryCondition.DefeatTarget)]
        [TestCase("Assets/_Project/Data/Setups/Setup_Escort.asset", VictoryCondition.DefeatAllEnemies)]
        public void PrebuiltSetups_AreValidAndFiveASide(string path, VictoryCondition victory)
        {
            var setup = AssetDatabase.LoadAssetAtPath<BattleSetup>(path);
            Assume.That(setup, Is.Not.Null, "run Friendship Warriors > Data > Create Battle Setups");

            Assert.That(BattleSetupValidator.Validate(setup), Is.Empty);
            Assert.That(setup.PlayerTeamSize, Is.EqualTo(BattleSetup.StandardTeamSize));
            Assert.That(setup.EnemyTeamSize, Is.EqualTo(BattleSetup.StandardTeamSize));
            Assert.That(setup.Objective.Victory, Is.EqualTo(victory));
        }

        [Test]
        public void TheTestScenesHillsideMap_FitsFiveASide()
        {
            var map = AssetDatabase.LoadAssetAtPath<BattleMapData>("Assets/_Project/Data/Maps/TestMap_Hillside.asset");
            Assume.That(map, Is.Not.Null);

            Assert.That(map.PlayerDeployment.Count, Is.GreaterThanOrEqualTo(BattleSetup.StandardTeamSize));
            Assert.That(map.EnemyDeployment.Count, Is.GreaterThanOrEqualTo(BattleSetup.StandardTeamSize));
        }

        // ------------------------------------------------------------------ AI

        [Test]
        public void Ai_PrefersAProtectedPlayerUnit_OverAnEqualOne()
        {
            var grid = TestGrids.Flat(9, 9);
            var ai = Soldier("AI", Team.Enemy, x: 4, y: 4);
            // Two identical targets the AI can reach equally well from its own tile.
            var plain = Soldier("Plain", Team.Player, x: 3, y: 4);
            var escort = Soldier("Escort", Team.Player, "escort", x: 5, y: 4);
            var units = new List<Unit> { ai, plain, escort };
            var hints = new AiHints(new[] { escort }, cautious: false);

            var plan = AiPlanner.Plan(grid, ai, units, MovementRules.Default, new System.Random(1), hints);

            Assert.That(plan.Target, Is.SameAs(escort));
        }

        [Test]
        public void Ai_WithoutHints_StillPlansNormally()
        {
            var grid = TestGrids.Flat(8, 8);
            var ai = Soldier("AI", Team.Enemy, x: 2, y: 2);
            var player = Soldier("P", Team.Player, x: 3, y: 2);

            var plan = AiPlanner.Plan(grid, ai, new List<Unit> { ai, player }, MovementRules.Default, new System.Random(1));

            Assert.That(plan.Target, Is.SameAs(player));
        }
    }
}
