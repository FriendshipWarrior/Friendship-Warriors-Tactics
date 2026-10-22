using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Props;
using NUnit.Framework;
using UnityEngine;

namespace FriendshipWarriors.Tests
{
    public class BattleMapEditingTests
    {
        private BattleMapData _map;
        private PropDefinition _tree;
        private PropDefinition _rock;

        [SetUp]
        public void SetUp()
        {
            _map = ScriptableObject.CreateInstance<BattleMapData>();
            _map.CopyTilesFrom(TestGrids.Flat(4, 3, heightInH: 1));
            _tree = ScriptableObject.CreateInstance<PropDefinition>();
            _rock = ScriptableObject.CreateInstance<PropDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_map);
            Object.DestroyImmediate(_tree);
            Object.DestroyImmediate(_rock);
        }

        [Test]
        public void SetTile_ThenGetTile_RoundTrips()
        {
            var coord = new GridCoord(3, 2);
            _map.SetTile(coord, new Tile(5, TerrainType.Water, depth: 2, slope: SlopeType.RiseEast, slopeHeight: 2));

            var tile = _map.GetTile(coord);

            Assert.That(tile.Height, Is.EqualTo(5));
            Assert.That(tile.Terrain, Is.EqualTo(TerrainType.Water));
            Assert.That(tile.Depth, Is.EqualTo(2));
            Assert.That(tile.Slope, Is.EqualTo(SlopeType.RiseEast));
            Assert.That(tile.SlopeHeight, Is.EqualTo(2));
            Assert.That(_map.CreateGrid()[coord].Height, Is.EqualTo(5));
        }

        [Test]
        public void SetTile_OnAFreshAsset_SizesItsArrays()
        {
            var fresh = ScriptableObject.CreateInstance<BattleMapData>();
            fresh.SetTile(new GridCoord(9, 9), new Tile(3, TerrainType.Stone));

            Assert.That(fresh.GetTile(new GridCoord(9, 9)).Height, Is.EqualTo(3));
            Object.DestroyImmediate(fresh);
        }

        [Test]
        public void Resize_KeepsWhatFitsAndFillsTheRest()
        {
            _map.SetTile(new GridCoord(1, 1), new Tile(8, TerrainType.Stone));
            _map.SetPropAt(new GridCoord(3, 2), _tree);
            _map.SetDeploymentAt(new GridCoord(0, 0), DeploymentSide.Player);
            _map.SetDeploymentAt(new GridCoord(3, 0), DeploymentSide.Enemy);

            _map.Resize(2, 5, fillHeight: 4);

            Assert.That(_map.Width, Is.EqualTo(2));
            Assert.That(_map.Depth, Is.EqualTo(5));
            Assert.That(_map.GetTile(new GridCoord(1, 1)).Height, Is.EqualTo(8));
            Assert.That(_map.GetTile(new GridCoord(0, 4)).Height, Is.EqualTo(4));
            Assert.That(_map.Props, Is.Empty, "the prop at x = 3 no longer fits");
            Assert.That(_map.PlayerDeployment, Is.EqualTo(new[] { new GridCoord(0, 0) }));
            Assert.That(_map.EnemyDeployment, Is.Empty);
        }

        [Test]
        public void SetPropAt_ReplacesAndRemoves()
        {
            var coord = new GridCoord(2, 1);

            _map.SetPropAt(coord, _tree);
            _map.SetPropAt(coord, _rock);
            Assert.That(_map.GetPropAt(coord), Is.SameAs(_rock));
            Assert.That(_map.Props.Count, Is.EqualTo(1));

            _map.SetPropAt(coord, null);
            Assert.That(_map.GetPropAt(coord), Is.Null);
            Assert.That(_map.Props, Is.Empty);
        }

        [Test]
        public void SetDeploymentAt_MovesATileBetweenSides()
        {
            var coord = new GridCoord(1, 2);

            _map.SetDeploymentAt(coord, DeploymentSide.Player);
            _map.SetDeploymentAt(coord, DeploymentSide.Enemy);

            Assert.That(_map.GetDeploymentAt(coord), Is.EqualTo(DeploymentSide.Enemy));
            Assert.That(_map.PlayerDeployment, Is.Empty);

            _map.SetDeploymentAt(coord, DeploymentSide.None);
            Assert.That(_map.GetDeploymentAt(coord), Is.EqualTo(DeploymentSide.None));
            Assert.That(_map.EnemyDeployment, Is.Empty);
        }

        [Test]
        public void Editing_OutsideTheMap_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _map.SetTile(new GridCoord(4, 0), default));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _map.SetPropAt(new GridCoord(-1, 0), _tree));
        }

        [TestCase(2, "1h")]
        [TestCase(3, "1.5h")]
        [TestCase(0, "0h")]
        public void FormatHeight_ShowsHalves(int steps, string expected)
        {
            Assert.That(GridMetrics.FormatHeight(steps), Is.EqualTo(expected));
        }
    }
}
