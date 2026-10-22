using FriendshipWarriors.Grid;
using NUnit.Framework;
using UnityEngine;

namespace FriendshipWarriors.Tests
{
    public class BattleMapDataTests
    {
        private BattleMapData _map;

        [SetUp]
        public void SetUp() => _map = ScriptableObject.CreateInstance<BattleMapData>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_map);

        [Test]
        public void CopyFrom_ThenCreateGrid_RoundTripsEveryTile()
        {
            var source = TestGrids.FromRows(
                "012",
                "~.3");
            source[new GridCoord(0, 0)] = new Tile(7, TerrainType.Sand);

            _map.CopyFrom(source);
            var result = _map.CreateGrid();

            Assert.That(result.Width, Is.EqualTo(3));
            Assert.That(result.Depth, Is.EqualTo(2));
            foreach (var c in source.AllCoords())
            {
                Assert.That(result[c].Height, Is.EqualTo(source[c].Height), $"height at {c}");
                Assert.That(result[c].Terrain, Is.EqualTo(source[c].Terrain), $"terrain at {c}");
            }
        }

        [Test]
        public void CreateGrid_OnFreshAsset_IsFlatGrass()
        {
            var grid = _map.CreateGrid();

            foreach (var c in grid.AllCoords())
            {
                Assert.That(grid[c].Height, Is.Zero);
                Assert.That(grid[c].Terrain, Is.EqualTo(TerrainType.Grass));
            }
        }
    }
}
