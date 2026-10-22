using System;
using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Pathfinding;
using NUnit.Framework;

namespace FriendshipWarriors.Tests
{
    public class PathfinderTests
    {
        private static readonly MovementProfile Move2Jump1 = new(2, 1);

        [Test]
        public void FlatGround_ReachesDiamondOfRadiusMove()
        {
            var grid = TestGrids.Flat(7, 7);
            var origin = new GridCoord(3, 3);

            var range = Pathfinder.FindMovementRange(grid, origin, Move2Jump1);

            // Diamond of radius 2 holds 13 tiles; the origin itself is not a destination.
            Assert.That(range.Destinations.Count, Is.EqualTo(12));
            Assert.That(range.Destinations.All(c => c.ManhattanDistance(origin) <= 2), Is.True);
            Assert.That(range.CanMoveTo(origin), Is.False);
            Assert.That(range.CostTo(origin), Is.Zero);
        }

        [Test]
        public void ZeroMove_HasNoDestinations()
        {
            var range = Pathfinder.FindMovementRange(TestGrids.Flat(3, 3), new GridCoord(1, 1), new MovementProfile(0, 5));

            Assert.That(range.Destinations, Is.Empty);
        }

        [Test]
        public void GridEdge_ClipsRange()
        {
            var range = Pathfinder.FindMovementRange(TestGrids.Flat(3, 3), new GridCoord(0, 0), Move2Jump1);

            Assert.That(range.Destinations, Is.EquivalentTo(new[]
            {
                new GridCoord(1, 0), new GridCoord(0, 1),
                new GridCoord(2, 0), new GridCoord(1, 1), new GridCoord(0, 2),
            }));
        }

        [TestCase(2, false)]
        [TestCase(3, true)]
        public void ClimbingUp_IsLimitedByJump(int jump, bool reachesTop)
        {
            var grid = TestGrids.FromRows("03");

            var range = Pathfinder.FindMovementRange(grid, new GridCoord(0, 0), new MovementProfile(5, jump));

            Assert.That(range.CanMoveTo(new GridCoord(1, 0)), Is.EqualTo(reachesTop));
        }

        [TestCase(2, false)]
        [TestCase(3, true)]
        public void DroppingDown_IsLimitedByJump(int jump, bool reachesBottom)
        {
            var grid = TestGrids.FromRows("30");

            var range = Pathfinder.FindMovementRange(grid, new GridCoord(0, 0), new MovementProfile(5, jump));

            Assert.That(range.CanMoveTo(new GridCoord(1, 0)), Is.EqualTo(reachesBottom));
        }

        [Test]
        public void Stairs_CanBeClimbedOneStepAtATime()
        {
            var grid = TestGrids.FromRows("01234");

            var range = Pathfinder.FindMovementRange(grid, new GridCoord(0, 0), new MovementProfile(4, 1));

            Assert.That(range.CanMoveTo(new GridCoord(4, 0)), Is.True);
            Assert.That(range.CostTo(new GridCoord(4, 0)), Is.EqualTo(4));
        }

        [Test]
        public void UnwalkableTiles_AreNeitherDestinationsNorPassable()
        {
            // Deep water and void wall off the right side.
            var grid = TestGrids.FromRows(
                "0~0",
                "0.0",
                "0~0");

            var range = Pathfinder.FindMovementRange(grid, new GridCoord(0, 1), new MovementProfile(6, 9));

            Assert.That(range.CanMoveTo(new GridCoord(1, 0)), Is.False);
            Assert.That(range.CanMoveTo(new GridCoord(1, 1)), Is.False);
            Assert.That(range.CanMoveTo(new GridCoord(2, 1)), Is.False);
            Assert.That(range.Destinations.Count, Is.EqualTo(2));
        }

        [Test]
        public void Enemies_BlockPassage()
        {
            var grid = TestGrids.Flat(4, 1);
            var enemy = new GridCoord(1, 0);

            var range = Pathfinder.FindMovementRange(grid, new GridCoord(0, 0), new MovementProfile(3, 1),
                c => c == enemy ? Occupancy.Enemy : Occupancy.Empty);

            Assert.That(range.Destinations, Is.Empty);
            Assert.That(range.CostTo(enemy), Is.EqualTo(-1));
        }

        [Test]
        public void Allies_CanBePassedButNotStoppedOn()
        {
            var grid = TestGrids.Flat(3, 1);
            var ally = new GridCoord(1, 0);

            var range = Pathfinder.FindMovementRange(grid, new GridCoord(0, 0), new MovementProfile(2, 1),
                c => c == ally ? Occupancy.Ally : Occupancy.Empty);

            Assert.That(range.CanMoveTo(ally), Is.False);
            Assert.That(range.CostTo(ally), Is.EqualTo(1));
            Assert.That(range.CanMoveTo(new GridCoord(2, 0)), Is.True);
        }

        [Test]
        public void Detours_CostMoreThanStraightLines()
        {
            // A cliff in the middle column forces the route around through the top row.
            var grid = TestGrids.FromRows(
                "090",
                "090",
                "000");

            var range = Pathfinder.FindMovementRange(grid, new GridCoord(0, 0), new MovementProfile(6, 1));

            Assert.That(range.CostTo(new GridCoord(2, 0)), Is.EqualTo(6));
        }

        [Test]
        public void TryGetPath_ReturnsShortestAdjacentStepsEndingAtDestination()
        {
            var grid = TestGrids.FromRows(
                "090",
                "090",
                "000");
            var origin = new GridCoord(0, 0);
            var destination = new GridCoord(2, 0);
            var range = Pathfinder.FindMovementRange(grid, origin, new MovementProfile(6, 1));
            var path = new List<GridCoord>();

            Assert.That(range.TryGetPath(destination, path), Is.True);
            Assert.That(path.Count, Is.EqualTo(range.CostTo(destination)));
            Assert.That(path[^1], Is.EqualTo(destination));

            var previous = origin;
            foreach (var step in path)
            {
                Assert.That(step.ManhattanDistance(previous), Is.EqualTo(1), $"{previous} -> {step} is not adjacent");
                Assert.That(Math.Abs(grid[step].Height - grid[previous].Height), Is.LessThanOrEqualTo(1));
                previous = step;
            }
        }

        [Test]
        public void TryGetPath_ToUnreachableTile_ReturnsFalseAndEmptyPath()
        {
            var range = Pathfinder.FindMovementRange(TestGrids.Flat(5, 1), new GridCoord(0, 0), Move2Jump1);
            var path = new List<GridCoord> { new(9, 9) };

            Assert.That(range.TryGetPath(new GridCoord(4, 0), path), Is.False);
            Assert.That(path, Is.Empty);
        }

        [Test]
        public void OriginOutsideGrid_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Pathfinder.FindMovementRange(TestGrids.Flat(2, 2), new GridCoord(5, 5), Move2Jump1));
        }
    }
}
