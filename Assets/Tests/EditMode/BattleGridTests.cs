using System;
using System.Linq;
using FriendshipWarriors.Grid;
using NUnit.Framework;

namespace FriendshipWarriors.Tests
{
    public class BattleGridTests
    {
        [Test]
        public void Indexer_StoresTilesPerCoordinate()
        {
            var grid = new BattleGrid(3, 2);
            grid[new GridCoord(2, 1)] = new Tile(4, TerrainType.Stone);

            Assert.That(grid[new GridCoord(2, 1)].Height, Is.EqualTo(4));
            Assert.That(grid[new GridCoord(2, 1)].Terrain, Is.EqualTo(TerrainType.Stone));
            Assert.That(grid[new GridCoord(1, 1)].Height, Is.EqualTo(0));
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(3, 0)]
        [TestCase(0, 2)]
        public void OutOfBounds_IsRejected(int x, int y)
        {
            var grid = new BattleGrid(3, 2);
            var coord = new GridCoord(x, y);

            Assert.That(grid.Contains(coord), Is.False);
            Assert.That(grid.TryGetTile(coord, out _), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = grid[coord]);
        }

        [Test]
        public void Constructor_RejectsNonPositiveSize()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleGrid(0, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleGrid(5, -1));
        }

        [Test]
        public void AllCoords_CoversEveryTileOnce()
        {
            var grid = new BattleGrid(4, 3);
            var coords = grid.AllCoords().ToList();

            Assert.That(coords.Count, Is.EqualTo(12));
            Assert.That(coords.Distinct().Count(), Is.EqualTo(12));
            Assert.That(coords.All(grid.Contains), Is.True);
        }

        [Test]
        public void Walkability_ExcludesVoidAndBlockedTiles()
        {
            Assert.That(new Tile(0, TerrainType.Water, depth: 2).IsWalkable, Is.True);
            Assert.That(new Tile(0, TerrainType.Void).IsWalkable, Is.False);
            Assert.That(new Tile(0, TerrainType.Grass, blocked: true).IsWalkable, Is.False);
        }

        [Test]
        public void FlatTiles_IgnoreSlopeHeight()
        {
            var tile = new Tile(4, TerrainType.Grass, slope: SlopeType.Flat, slopeHeight: 3);

            Assert.That(tile.SlopeHeight, Is.Zero);
            Assert.That(tile.StandingHeight, Is.EqualTo(4));
        }

        [Test]
        public void SlopedTiles_StandAtTheMiddleOfTheSlope()
        {
            // A ramp from 1h (2 steps) to 2h (4 steps) counts as 1.5h (3 steps).
            var tile = new Tile(2, TerrainType.Dirt, slope: SlopeType.RiseNorth, slopeHeight: 2);

            Assert.That(tile.StandingHeight, Is.EqualTo(3));
        }

        [Test]
        public void SlopeMidpoints_BetweenHalfSteps_RoundDown()
        {
            var tile = new Tile(2, TerrainType.Dirt, slope: SlopeType.RiseNorth, slopeHeight: 3);

            Assert.That(tile.StandingHeight, Is.EqualTo(3));
        }

        [TestCase(0, true)]
        [TestCase(1, true)]
        [TestCase(2, false)]
        [TestCase(5, false)]
        public void DeepWater_StopsUnitsActing(int depth, bool allowsActions)
        {
            Assert.That(new Tile(0, TerrainType.Water, depth: depth).AllowsActions, Is.EqualTo(allowsActions));
        }
    }
}
