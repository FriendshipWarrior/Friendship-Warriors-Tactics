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
            // Blocked tiles and void wall off the right side.
            var grid = TestGrids.FromRows(
                "0#0",
                "0.0",
                "0#0");

            var range = Pathfinder.FindMovementRange(grid, new GridCoord(0, 1), new MovementProfile(6, 1));

            Assert.That(range.CanMoveTo(new GridCoord(1, 0)), Is.False);
            Assert.That(range.CanMoveTo(new GridCoord(1, 1)), Is.False);
            Assert.That(range.CanMoveTo(new GridCoord(2, 1)), Is.False);
            Assert.That(range.Destinations.Count, Is.EqualTo(2));
        }

        [Test]
        public void Water_IsWalkable()
        {
            var range = Pathfinder.FindMovementRange(TestGrids.FromRows("0~0"), new GridCoord(0, 0), new MovementProfile(2, 1));

            Assert.That(range.CanMoveTo(new GridCoord(1, 0)), Is.True);
            Assert.That(range.CanMoveTo(new GridCoord(2, 0)), Is.True);
        }

        [TestCase(1, false)]
        [TestCase(2, true)]
        public void Leaping_AOneTileGap_NeedsJumpTwo(int jump, bool reachesFarSide)
        {
            var grid = TestGrids.FromRows("1.1");

            var range = Pathfinder.FindMovementRange(grid, new GridCoord(0, 0), new MovementProfile(4, jump));

            Assert.That(range.CanMoveTo(new GridCoord(2, 0)), Is.EqualTo(reachesFarSide));
        }

        [Test]
        public void Leaping_CostsMoveAsIfTheGapWereFlat()
        {
            var grid = TestGrids.FromRows("1..1");

            var range = Pathfinder.FindMovementRange(grid, new GridCoord(0, 0), new MovementProfile(3, 4));

            Assert.That(range.CostTo(new GridCoord(3, 0)), Is.EqualTo(3));
        }

        [Test]
        public void Leaping_TwoTileGap_NeedsJumpFour()
        {
            var grid = TestGrids.FromRows("1..1");

            var jump3 = Pathfinder.FindMovementRange(grid, new GridCoord(0, 0), new MovementProfile(5, 3));
            var jump4 = Pathfinder.FindMovementRange(grid, new GridCoord(0, 0), new MovementProfile(5, 4));

            Assert.That(jump3.CanMoveTo(new GridCoord(3, 0)), Is.False);
            Assert.That(jump4.CanMoveTo(new GridCoord(3, 0)), Is.True);
        }

        [Test]
        public void Leaping_CrossesLowerGroundThatIsTooDeepToWalkInto()
        {
            // Dropping 3h into the ditch exceeds Jump 2, but leaping straight over it is fine.
            var grid = TestGrids.FromRows("303");

            var range = Pathfinder.FindMovementRange(grid, new GridCoord(0, 0), new MovementProfile(4, 2));

            Assert.That(range.CanMoveTo(new GridCoord(1, 0)), Is.False);
            Assert.That(range.CanMoveTo(new GridCoord(2, 0)), Is.True);
        }

        [TestCase("1#1")]
        [TestCase("192")]
        public void Leaping_CannotClearBlockedOrHigherTiles(string row)
        {
            var range = Pathfinder.FindMovementRange(TestGrids.FromRows(row), new GridCoord(0, 0), new MovementProfile(4, 4));

            Assert.That(range.CanMoveTo(new GridCoord(2, 0)), Is.False);
        }

        [TestCase(3, true)]
        [TestCase(1, false)]
        public void Leaping_ClearsAnEnemyStandingLower(int jump, bool reachesFarSide)
        {
            // Standing at 3h next to an enemy at 1h: Jump 3 hops over it; Jump 1 can't leap at all.
            var grid = TestGrids.FromRows("313");
            var enemy = new GridCoord(1, 0);

            var range = Pathfinder.FindMovementRange(grid, new GridCoord(0, 0), new MovementProfile(4, jump),
                c => c == enemy ? Occupancy.Enemy : Occupancy.Empty);

            Assert.That(range.CanMoveTo(new GridCoord(2, 0)), Is.EqualTo(reachesFarSide));
        }

        [TestCase("333")]
        [TestCase("353")]
        public void Leaping_CannotClearAnEnemyAtTheSameHeightOrHigher(string row)
        {
            var enemy = new GridCoord(1, 0);

            var range = Pathfinder.FindMovementRange(TestGrids.FromRows(row), new GridCoord(0, 0), new MovementProfile(4, 7),
                c => c == enemy ? Occupancy.Enemy : Occupancy.Empty);

            Assert.That(range.CanMoveTo(new GridCoord(2, 0)), Is.False);
        }

        [Test]
        public void Leaping_PastTheMapEdgeIsImpossible()
        {
            var range = Pathfinder.FindMovementRange(TestGrids.FromRows("1."), new GridCoord(0, 0), new MovementProfile(4, 7));

            Assert.That(range.Destinations, Is.Empty);
        }

        [Test]
        public void Slopes_LetUnitsClimbHeightsTheyCouldNotStepUp()
        {
            // A 2h step is too tall for Jump 1, but a ramp splitting it into two 1h rises is not.
            var steps = TestGrids.FromRows("02");
            var ramp = TestGrids.FromRows("012");
            ramp[new GridCoord(1, 0)] = new Tile(0, TerrainType.Grass, slope: SlopeType.RiseEast, slopeHeight: 4);

            var stepRange = Pathfinder.FindMovementRange(steps, new GridCoord(0, 0), new MovementProfile(3, 1));
            var rampRange = Pathfinder.FindMovementRange(ramp, new GridCoord(0, 0), new MovementProfile(3, 1));

            Assert.That(stepRange.CanMoveTo(new GridCoord(1, 0)), Is.False);
            Assert.That(rampRange.CanMoveTo(new GridCoord(2, 0)), Is.True);
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
            // Jump 1 means no leaps, so every step must be adjacent.
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
