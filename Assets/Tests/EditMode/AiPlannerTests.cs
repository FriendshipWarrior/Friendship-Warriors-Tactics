using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Battle.AI;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Pathfinding;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Equipment;
using NUnit.Framework;
using UnityEngine;

namespace FriendshipWarriors.Tests
{
    public class AiPlannerTests
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

        /// <summary>P.Atk 6 with a WP 4 sword: 24 damage per hit; HP ~30.</summary>
        private Unit Soldier(string name, Team team, int x, int y, Direction facing = Direction.North)
        {
            var unit = new Unit(name, team, _job, new RawStats(491520, 229376, 6 * 16384, 6 * 16384, 4 * 16384))
            {
                Coord = new GridCoord(x, y),
                Facing = facing,
            };
            unit.TryEquipItem(_sword);
            return unit;
        }

        private static AiPlan Plan(BattleGrid grid, Unit actor, params Unit[] others) =>
            AiPlanner.Plan(grid, actor, others.Prepend(actor).ToList(), MovementRules.Default, new System.Random(1));

        [Test]
        public void AttacksWhenATargetIsInReach()
        {
            var grid = TestGrids.Flat(8, 8);
            var ai = Soldier("AI", Team.Enemy, 2, 2);
            var player = Soldier("P", Team.Player, 4, 2);

            var plan = Plan(grid, ai, player);

            Assert.That(plan.Target, Is.SameAs(player));
            var from = plan.MoveBefore ?? ai.Coord;
            Assert.That(from.ManhattanDistance(player.Coord), Is.EqualTo(1), "attacks from an adjacent tile");
        }

        [Test]
        public void PrefersAKnockOut()
        {
            var grid = TestGrids.Flat(9, 5);
            var ai = Soldier("AI", Team.Enemy, 4, 2);
            var healthy = Soldier("Healthy", Team.Player, 2, 2);
            var wounded = Soldier("Wounded", Team.Player, 6, 2);
            wounded.TakeDamage(wounded.CurrentHp - 5);

            var plan = Plan(grid, ai, healthy, wounded);

            Assert.That(plan.Target, Is.SameAs(wounded));
        }

        [Test]
        public void PrefersAttackingFromBehind()
        {
            var grid = TestGrids.Flat(7, 7);
            // The target faces south, toward the AI; the AI can walk around (4 steps) to its north side.
            var target = Soldier("P", Team.Player, 3, 3, Direction.South);
            var ai = Soldier("AI", Team.Enemy, 2, 1);

            var plan = Plan(grid, ai, target);

            Assert.That(plan.Target, Is.SameAs(target));
            Assert.That(plan.MoveBefore, Is.EqualTo(new GridCoord(3, 4)), "the tile behind the target");
        }

        [Test]
        public void ApproachesTheNearestEnemyWhenNothingIsInReach()
        {
            var grid = TestGrids.Flat(16, 3);
            var ai = Soldier("AI", Team.Enemy, 0, 1);
            var far = Soldier("P", Team.Player, 15, 1);

            var plan = Plan(grid, ai, far);

            Assert.That(plan.Target, Is.Null);
            Assert.That(plan.MoveBefore, Is.Not.Null);
            Assert.That(plan.MoveBefore.Value.ManhattanDistance(far.Coord), Is.EqualTo(15 - 4), "full Move straight at it");
            Assert.That(plan.Facing, Is.EqualTo(Direction.East), "ends facing the enemy");
        }

        [Test]
        public void ApproachesByWalkingDistance_NotStraightLine()
        {
            // A wall (9h) splits the map; the gap is at the top. Straight-line the target is close, but the way round
            // is through the gap, so the AI should head for it rather than press against the wall.
            var grid = TestGrids.FromRows(
                "000000000",
                "000090000",
                "000090000",
                "000090000",
                "000000000");
            var ai = Soldier("AI", Team.Enemy, 3, 1);
            var target = Soldier("P", Team.Player, 6, 1);

            var plan = Plan(grid, ai, target);

            Assert.That(plan.Target, Is.SameAs(target), "Move 4 is enough to go round through the gap");
            Assert.That(plan.MoveBefore, Is.Not.Null);
            var from = plan.MoveBefore.Value;
            Assert.That(grid[from].StandingHeight, Is.Zero, "never stands on the wall");
        }

        [Test]
        public void NeverPlansAMoveToAnUnreachableTile()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var grid = TestGrids.FromRows(
                    "00000000",
                    "0#~00.00",
                    "00030000",
                    "00000#00",
                    "00000000");
                var ai = Soldier("AI", Team.Enemy, seed % 4, 0);
                var ally = Soldier("Ally", Team.Enemy, 1, 0);
                var p1 = Soldier("P1", Team.Player, 7, 4);
                var p2 = Soldier("P2", Team.Player, 6, 2);
                var units = new List<Unit> { ai, ally, p1, p2 };
                var range = Pathfinder.FindMovementRange(grid, ai.Coord, new MovementProfile(ai.Stats.Move, ai.Stats.Jump),
                    UnitOccupancy.For(ai, units), MovementRules.Default);

                var plan = AiPlanner.Plan(grid, ai, units, MovementRules.Default, new System.Random(seed));

                if (plan.MoveBefore is { } before)
                    Assert.That(range.CanMoveTo(before), Is.True, $"seed {seed}: {before}");
                if (plan.MoveAfter is { } after)
                    Assert.That(range.CanMoveTo(after), Is.True, $"seed {seed}: {after}");
                Assert.That(ai.Coord, Is.EqualTo(new GridCoord(seed % 4, 0)), "planning leaves the unit where it was");
            }
        }

        [Test]
        public void IgnoresKnockedOutEnemiesAndAllies()
        {
            var grid = TestGrids.Flat(6, 3);
            var ai = Soldier("AI", Team.Enemy, 2, 1);
            var ally = Soldier("Ally", Team.Enemy, 3, 1);
            var downed = Soldier("Down", Team.Player, 1, 1);
            downed.TakeDamage(9999);

            var plan = Plan(grid, ai, ally, downed);

            Assert.That(plan.Target, Is.Null);
        }

        [Test]
        public void IsDeterministicForASeed()
        {
            var grid = TestGrids.Flat(10, 10);
            var ai = Soldier("AI", Team.Enemy, 1, 1);
            var a = Soldier("A", Team.Player, 6, 6);
            var b = Soldier("B", Team.Player, 6, 2);
            var units = new List<Unit> { ai, a, b };

            var first = AiPlanner.Plan(grid, ai, units, MovementRules.Default, new System.Random(7));
            var second = AiPlanner.Plan(grid, ai, units, MovementRules.Default, new System.Random(7));

            Assert.That(second.ToString(), Is.EqualTo(first.ToString()));
        }
    }
}
