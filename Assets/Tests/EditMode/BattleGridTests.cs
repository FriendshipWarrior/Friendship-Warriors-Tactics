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
        public void Walkability_ExcludesDeepWaterAndVoid()
        {
            Assert.That(new Tile(0, TerrainType.ShallowWater).IsWalkable, Is.True);
            Assert.That(new Tile(0, TerrainType.DeepWater).IsWalkable, Is.False);
            Assert.That(new Tile(0, TerrainType.Void).IsWalkable, Is.False);
        }
    }
}
