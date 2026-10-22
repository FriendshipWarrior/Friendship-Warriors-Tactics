using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Props;
using FriendshipWarriors.Grid.Validation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FriendshipWarriors.Tests
{
    public class MapValidatorTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                Object.DestroyImmediate(obj);
            _created.Clear();
        }

        /// <summary>A map from rows (see <see cref="TestGrids.FromRows"/>) with the player in the bottom-left and the enemy top-right.</summary>
        private BattleMapData Map(params string[] rows)
        {
            var map = ScriptableObject.CreateInstance<BattleMapData>();
            _created.Add(map);
            map.CopyTilesFrom(TestGrids.FromRows(rows));
            map.SetDeployment(new[] { new GridCoord(0, 0) }, new[] { new GridCoord(map.Width - 1, map.Depth - 1) });
            return map;
        }

        private static string[] Messages(BattleMapData map, IssueSeverity severity) =>
            MapValidator.Validate(map).Where(i => i.Severity == severity).Select(i => i.Message).ToArray();

        [Test]
        public void CleanMap_HasNoIssues()
        {
            Assert.That(MapValidator.Validate(Map("111", "111")), Is.Empty);
        }

        [Test]
        public void EmptyDeploymentZones_AreErrors()
        {
            var map = Map("11");
            map.SetDeployment(new GridCoord[0], new GridCoord[0]);

            var errors = Messages(map, IssueSeverity.Error);

            Assert.That(errors, Has.Some.Contains("Player deployment zone is empty"));
            Assert.That(errors, Has.Some.Contains("Enemy deployment zone is empty"));
        }

        [Test]
        public void DeploymentOnVoid_IsAnError()
        {
            var map = Map("1.");
            map.SetDeployment(new[] { new GridCoord(0, 0) }, new[] { new GridCoord(1, 0) });

            Assert.That(Messages(map, IssueSeverity.Error), Has.Some.Contains("can't be stood on"));
        }

        [Test]
        public void DeploymentInDeepWater_IsAWarning()
        {
            var map = Map("~1");

            Assert.That(Messages(map, IssueSeverity.Warning), Has.Some.Contains("can't act"));
        }

        [Test]
        public void BlockingPropOnDeployment_IsAnError()
        {
            var map = Map("11");
            var tree = ScriptableObject.CreateInstance<PropDefinition>();
            _created.Add(tree);
            map.SetPropAt(new GridCoord(0, 0), tree);

            Assert.That(Messages(map, IssueSeverity.Error), Has.Some.Contains("blocking prop covers a deployment tile"));
        }

        [Test]
        public void SlopeMidpointBetweenHalves_IsAWarning()
        {
            var map = Map("11");
            map.SetTile(new GridCoord(1, 0), new Tile(2, TerrainType.Dirt, slope: SlopeType.RiseEast, slopeHeight: 3));

            Assert.That(Messages(map, IssueSeverity.Warning), Has.Some.Contains("between half heights"));
        }

        [Test]
        public void UnreachableEnemyDeployment_IsAnError()
        {
            // A 5h cliff separates the zones; Jump 3 can't climb it.
            var map = Map("15");

            Assert.That(Messages(map, IssueSeverity.Error), Has.Some.Contains("can't be reached"));
        }

        [Test]
        public void Reachability_UsesTheReferenceJump()
        {
            var map = Map("15");

            Assert.That(MapValidator.Validate(map, referenceJump: 4), Is.Empty);
        }

        [Test]
        public void StrandedTiles_AreAWarning()
        {
            // The middle tile is a 9h pillar nobody can climb, but the zones can still reach each other around it.
            var map = Map("111", "191", "111");

            var warnings = Messages(map, IssueSeverity.Warning);

            Assert.That(warnings, Has.Some.Contains("1 walkable tile(s) can't be reached"));
            Assert.That(Messages(map, IssueSeverity.Error), Is.Empty);
        }

        [Test]
        public void WaterDepthOnDryTerrain_IsAWarning()
        {
            var map = Map("11");
            map.SetTile(new GridCoord(1, 0), new Tile(2, TerrainType.Grass, depth: 1));

            Assert.That(Messages(map, IssueSeverity.Warning), Has.Some.Contains("Water depth 1 on Grass"));
        }

        [Test]
        public void TheTestScenesHillsideMap_HasNoErrors()
        {
            var map = AssetDatabase.LoadAssetAtPath<BattleMapData>("Assets/_Project/Data/Maps/TestMap_Hillside.asset");
            Assume.That(map, Is.Not.Null);

            Assert.That(Messages(map, IssueSeverity.Error), Is.Empty);
        }
    }
}
