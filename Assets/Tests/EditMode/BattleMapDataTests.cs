using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Props;
using NUnit.Framework;
using UnityEditor;
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
        public void CopyTilesFrom_ThenCreateGrid_RoundTripsEveryField()
        {
            var source = TestGrids.FromRows(
                "012",
                "~.3");
            source[new GridCoord(0, 0)] = new Tile(7, TerrainType.Sand, depth: 1, slope: SlopeType.ConcaveNorthWest, slopeHeight: 2);

            _map.CopyTilesFrom(source);
            var result = _map.CreateGrid();

            Assert.That(result.Width, Is.EqualTo(3));
            Assert.That(result.Depth, Is.EqualTo(2));
            foreach (var c in source.AllCoords())
            {
                var expected = source[c];
                var actual = result[c];
                Assert.That(actual.Height, Is.EqualTo(expected.Height), $"height at {c}");
                Assert.That(actual.Terrain, Is.EqualTo(expected.Terrain), $"terrain at {c}");
                Assert.That(actual.Depth, Is.EqualTo(expected.Depth), $"depth at {c}");
                Assert.That(actual.Slope, Is.EqualTo(expected.Slope), $"slope at {c}");
                Assert.That(actual.SlopeHeight, Is.EqualTo(expected.SlopeHeight), $"slope height at {c}");
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
                Assert.That(grid[c].IsWalkable, Is.True);
            }
        }

        [Test]
        public void Props_ThatBlockMovement_BlockTheirTile()
        {
            var blocker = CreateProp(blocksMovement: true);
            var decoration = CreateProp(blocksMovement: false);
            _map.CopyTilesFrom(TestGrids.Flat(3, 1));
            _map.SetProps(new[]
            {
                new PropPlacement(new GridCoord(0, 0), blocker),
                new PropPlacement(new GridCoord(1, 0), decoration),
            });

            var grid = _map.CreateGrid();

            Assert.That(grid[new GridCoord(0, 0)].IsWalkable, Is.False);
            Assert.That(grid[new GridCoord(1, 0)].IsWalkable, Is.True);
            Assert.That(grid[new GridCoord(2, 0)].IsWalkable, Is.True);

            Object.DestroyImmediate(blocker);
            Object.DestroyImmediate(decoration);
        }

        [Test]
        public void Deployment_RoundTrips()
        {
            _map.SetDeployment(new[] { new GridCoord(0, 0), new GridCoord(1, 0) }, new[] { new GridCoord(5, 5) });

            Assert.That(_map.PlayerDeployment, Is.EqualTo(new[] { new GridCoord(0, 0), new GridCoord(1, 0) }));
            Assert.That(_map.EnemyDeployment, Is.EqualTo(new[] { new GridCoord(5, 5) }));
        }

        private static PropDefinition CreateProp(bool blocksMovement)
        {
            var prop = ScriptableObject.CreateInstance<PropDefinition>();
            var so = new SerializedObject(prop);
            so.FindProperty("_blocksMovement").boolValue = blocksMovement;
            so.ApplyModifiedPropertiesWithoutUndo();
            return prop;
        }
    }
}
