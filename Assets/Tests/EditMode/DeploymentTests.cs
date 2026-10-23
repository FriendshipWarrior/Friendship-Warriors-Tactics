using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Battle.Deployment;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Units;
using NUnit.Framework;
using UnityEngine;

namespace FriendshipWarriors.Tests
{
    public class DeploymentTests
    {
        private readonly List<Object> _created = new();
        private JobDefinition _job;

        // A 5x3 field; the zone is the bottom row plus (0, 1). '#' at (4, 2) is blocked, '.' at (3, 2) is void.
        private BattleGrid _grid;
        private static readonly GridCoord[] Zone =
        {
            new(0, 0), new(1, 0), new(2, 0), new(3, 0), new(4, 0), new(0, 1),
        };

        [SetUp]
        public void SetUp()
        {
            _job = ScriptableObject.CreateInstance<JobDefinition>();
            _job.Define("Job", "", new StatFactors(100, 100, 100, 100, 100), new StatFactors(11, 15, 100, 60, 50), 4, 3, 5,
                new JobRequirement[0], Color.gray);
            _created.Add(_job);
            _grid = TestGrids.FromRows("11111", "11111", "111.#");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                Object.DestroyImmediate(obj);
            _created.Clear();
        }

        private Unit At(string name, GridCoord coord, Direction facing = Direction.North)
        {
            var unit = new Unit(name, Team.Player, _job, new RawStats(491520, 229376, 6 * 16384, 81920, 65536))
            {
                Coord = coord,
                Facing = facing,
            };
            return unit;
        }

        [Test]
        public void SpawnPositions_AreKept_SoBeginningAtOnceChangesNothing()
        {
            var a = At("A", new GridCoord(0, 0), Direction.East);
            var b = At("B", new GridCoord(2, 0), Direction.North);
            var plan = new DeploymentPlan(_grid, Zone, new[] { a, b });

            Assert.That(plan.Placements.Select(p => (p.Unit, p.Coord, p.Facing)), Is.EqualTo(new[]
            {
                (a, new GridCoord(0, 0), Direction.East),
                (b, new GridCoord(2, 0), Direction.North),
            }));
        }

        [Test]
        public void UnitsOffTheZone_OrSharingATile_TakeTheFirstFreeTiles()
        {
            var a = At("A", new GridCoord(1, 0));
            var b = At("B", new GridCoord(1, 0));  // same tile as A
            var c = At("C", new GridCoord(4, 2));  // not in the zone
            var plan = new DeploymentPlan(_grid, Zone, new[] { a, b, c });

            Assert.That(plan.CoordOf(a), Is.EqualTo(new GridCoord(1, 0)));
            Assert.That(plan.CoordOf(b), Is.EqualTo(new GridCoord(0, 0)));
            Assert.That(plan.CoordOf(c), Is.EqualTo(new GridCoord(2, 0)));
        }

        [Test]
        public void ZoneTiles_ThatCantBeStoodOn_AreLeftOut()
        {
            var zone = Zone.Concat(new[] { new GridCoord(3, 2), new GridCoord(4, 2), new GridCoord(9, 9) });
            var plan = new DeploymentPlan(_grid, zone, new[] { At("A", new GridCoord(0, 0)) });

            Assert.That(plan.Tiles, Is.EqualTo(Zone));
            Assert.That(plan.IsTile(new GridCoord(3, 2)), Is.False, "void");
            Assert.That(plan.IsTile(new GridCoord(4, 2)), Is.False, "blocked");
        }

        [Test]
        public void Placing_OnAFreeZoneTile_MovesTheUnit()
        {
            var a = At("A", new GridCoord(0, 0));
            var plan = new DeploymentPlan(_grid, Zone, new[] { a });

            Assert.That(plan.Place(a, new GridCoord(0, 1)), Is.True);
            Assert.That(plan.CoordOf(a), Is.EqualTo(new GridCoord(0, 1)));
            Assert.That(plan.UnitAt(new GridCoord(0, 0)), Is.Null);
            Assert.That(a.Coord, Is.EqualTo(new GridCoord(0, 0)), "units only move when the plan is applied");

            plan.Apply();
            Assert.That(a.Coord, Is.EqualTo(new GridCoord(0, 1)));
        }

        [Test]
        public void Placing_OnAnotherUnit_SwapsThem()
        {
            var a = At("A", new GridCoord(0, 0), Direction.East);
            var b = At("B", new GridCoord(3, 0), Direction.West);
            var plan = new DeploymentPlan(_grid, Zone, new[] { a, b });

            Assert.That(plan.Place(a, new GridCoord(3, 0)), Is.True);

            Assert.That(plan.CoordOf(a), Is.EqualTo(new GridCoord(3, 0)));
            Assert.That(plan.CoordOf(b), Is.EqualTo(new GridCoord(0, 0)));
            Assert.That(plan.FacingOf(a), Is.EqualTo(Direction.East), "facings stay with their units");
            Assert.That(plan.FacingOf(b), Is.EqualTo(Direction.West));
        }

        [Test]
        public void Placing_OutsideTheZone_IsRefused()
        {
            var a = At("A", new GridCoord(0, 0));
            var stranger = At("Stranger", new GridCoord(1, 0));
            var plan = new DeploymentPlan(_grid, Zone, new[] { a });

            Assert.That(plan.Place(a, new GridCoord(2, 2)), Is.False, "not a zone tile");
            Assert.That(plan.Place(stranger, new GridCoord(1, 0)), Is.False, "not in this side's roster");
            Assert.That(plan.CoordOf(a), Is.EqualTo(new GridCoord(0, 0)));
        }

        [Test]
        public void Facing_IsSetPerUnit_AndApplied()
        {
            var a = At("A", new GridCoord(0, 0), Direction.North);
            var plan = new DeploymentPlan(_grid, Zone, new[] { a });

            Assert.That(plan.SetFacing(a, Direction.South), Is.True);
            plan.Apply();

            Assert.That(a.Facing, Is.EqualTo(Direction.South));
        }

        [Test]
        public void TheSameMoves_GiveTheSamePlacements()
        {
            IReadOnlyList<UnitPlacement> Run()
            {
                var units = new[] { At("A", new GridCoord(0, 0)), At("B", new GridCoord(1, 0)), At("C", new GridCoord(2, 0)) };
                var plan = new DeploymentPlan(_grid, Zone, units);
                plan.Place(units[0], new GridCoord(2, 0));
                plan.Place(units[1], new GridCoord(0, 1));
                plan.SetFacing(units[2], Direction.West);
                return plan.Placements;
            }

            var first = Run().Select(p => (p.Unit.Name, p.Coord, p.Facing)).ToList();
            var second = Run().Select(p => (p.Unit.Name, p.Coord, p.Facing)).ToList();
            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Is.EqualTo(new[]
            {
                ("A", new GridCoord(2, 0), Direction.North),
                ("B", new GridCoord(0, 1), Direction.North),
                ("C", new GridCoord(0, 0), Direction.West),
            }));
        }

        // ------------------------------------------------------------------ Roster selection (for army building)

        [Test]
        public void Capacity_BenchesTheRestOfTheRoster()
        {
            var units = Enumerable.Range(0, 4).Select(i => At($"U{i}", new GridCoord(i, 0))).ToList();
            var plan = new DeploymentPlan(_grid, Zone, units, capacity: 3);

            Assert.That(plan.DeployedCount, Is.EqualTo(3));
            Assert.That(plan.IsDeployed(units[3]), Is.False);
            Assert.That(plan.Placements.Select(p => p.Unit), Is.EqualTo(units.Take(3)));
        }

        [Test]
        public void Deploying_ABenchedUnit_NeedsRoomOrTakesAnotherUnitsPlace()
        {
            var units = Enumerable.Range(0, 4).Select(i => At($"U{i}", new GridCoord(i, 0))).ToList();
            var plan = new DeploymentPlan(_grid, Zone, units, capacity: 3);

            Assert.That(plan.Deploy(units[3], new GridCoord(0, 1)), Is.False, "full");
            Assert.That(plan.Deploy(units[3], new GridCoord(1, 0)), Is.True, "swaps with U1, who goes to the bench");
            Assert.That(plan.IsDeployed(units[1]), Is.False);
            Assert.That(plan.Withdraw(units[0]), Is.True);
            Assert.That(plan.Deploy(units[1], new GridCoord(0, 1)), Is.True, "room again");
            Assert.That(plan.DeployedCount, Is.EqualTo(3));
        }

        [Test]
        public void TheLastDeployedUnit_CantBeWithdrawn()
        {
            var a = At("A", new GridCoord(0, 0));
            var plan = new DeploymentPlan(_grid, Zone, new[] { a });

            Assert.That(plan.Withdraw(a), Is.False);
        }

        [Test]
        public void Capacity_NeverExceedsTheZone()
        {
            var units = Enumerable.Range(0, 8).Select(i => At($"U{i}", new GridCoord(i % 5, 0))).ToList();
            var plan = new DeploymentPlan(_grid, Zone, units);

            Assert.That(plan.Capacity, Is.EqualTo(Zone.Length));
            Assert.That(plan.Placements.Select(p => p.Coord).Distinct().Count(), Is.EqualTo(Zone.Length));
        }
    }
}
