using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Generation;
using FriendshipWarriors.Grid.Props;
using FriendshipWarriors.Grid.Validation;
using NUnit.Framework;
using UnityEngine;

namespace FriendshipWarriors.Tests
{
    public class MapGeneratorTests
    {
        private readonly List<Object> _created = new();
        private MapGeneratorSettings _settings;

        [SetUp]
        public void SetUp()
        {
            var tree = Create<PropDefinition>();
            var rock = Create<PropDefinition>();
            _settings = Create<MapGeneratorSettings>();
            _settings.SetProps(new[]
            {
                new MapGeneratorSettings.PropChoice(tree, 2f),
                new MapGeneratorSettings.PropChoice(rock, 1f),
            });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                Object.DestroyImmediate(obj);
            _created.Clear();
        }

        private T Create<T>() where T : ScriptableObject
        {
            var obj = ScriptableObject.CreateInstance<T>();
            _created.Add(obj);
            return obj;
        }

        private BattleMapData Generate(int seed, out GenerationResult result)
        {
            var map = Create<BattleMapData>();
            result = MapGenerator.Generate(_settings, seed, map);
            return map;
        }

        [Test]
        public void SameSeed_GivesTheSameMap()
        {
            var a = Generate(1234, out _);
            var b = Generate(1234, out _);

            Assert.That(b.Width, Is.EqualTo(a.Width));
            Assert.That(b.Depth, Is.EqualTo(a.Depth));
            var gridA = a.CreateGrid();
            var gridB = b.CreateGrid();
            foreach (var c in gridA.AllCoords())
            {
                Assert.That(gridB[c].Height, Is.EqualTo(gridA[c].Height), $"height at {c}");
                Assert.That(gridB[c].Terrain, Is.EqualTo(gridA[c].Terrain), $"terrain at {c}");
                Assert.That(gridB[c].Slope, Is.EqualTo(gridA[c].Slope), $"slope at {c}");
                Assert.That(gridB[c].Depth, Is.EqualTo(gridA[c].Depth), $"depth at {c}");
            }
            Assert.That(b.PlayerDeployment, Is.EqualTo(a.PlayerDeployment));
            Assert.That(b.EnemyDeployment, Is.EqualTo(a.EnemyDeployment));
            Assert.That(b.Props.Select(p => p.Coord), Is.EqualTo(a.Props.Select(p => p.Coord)));
        }

        [Test]
        public void DifferentSeeds_GiveDifferentMaps()
        {
            var a = Generate(1, out _).CreateGrid();
            var b = Generate(2, out _).CreateGrid();

            bool differs = a.Width != b.Width || a.Depth != b.Depth ||
                           a.AllCoords().Any(c => a[c].Height != b[c].Height || a[c].Terrain != b[c].Terrain);
            Assert.That(differs, Is.True);
        }

        [Test]
        public void ManySeeds_AllProduceValidMaps()
        {
            for (int seed = 0; seed < 40; seed++)
            {
                var map = Generate(seed, out var result);

                Assert.That(result.Succeeded, Is.True, $"seed {seed}: {string.Join("; ", result.Issues)}");
                Assert.That(MapValidator.Validate(map, _settings.ReferenceJump).Where(i => i.Severity == IssueSeverity.Error), Is.Empty, $"seed {seed}");
                Assert.That(map.PlayerDeployment.Count, Is.EqualTo(_settings.DeploymentSize), $"seed {seed}");
                Assert.That(map.EnemyDeployment.Count, Is.EqualTo(_settings.DeploymentSize), $"seed {seed}");
            }
        }

        [Test]
        public void GeneratedMaps_RespectSizeAndHeightLimits()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var map = Generate(seed, out _);
                var grid = map.CreateGrid();

                Assert.That(map.Width, Is.InRange(_settings.WidthRange.x, _settings.WidthRange.y), $"seed {seed}");
                Assert.That(map.Depth, Is.InRange(_settings.DepthRange.x, _settings.DepthRange.y), $"seed {seed}");
                foreach (var c in grid.AllCoords())
                {
                    var tile = grid[c];
                    Assert.That(tile.Height + tile.SlopeHeight, Is.LessThanOrEqualTo(_settings.MaxHeight * GridMetrics.StepsPerH), $"seed {seed} {c}");
                    Assert.That(tile.SlopeHeight % GridMetrics.StepsPerH, Is.Zero, $"ramps rise in whole h so midpoints land on halves (seed {seed} {c})");
                }
            }
        }

        [Test]
        public void Props_StayOffDeploymentWaterAndRamps()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var map = Generate(seed, out _);
                var deployment = new HashSet<GridCoord>(map.PlayerDeployment.Concat(map.EnemyDeployment));

                foreach (var placement in map.Props)
                {
                    var tile = map.GetTile(placement.Coord);
                    Assert.That(deployment.Contains(placement.Coord), Is.False, $"seed {seed} {placement.Coord}");
                    Assert.That(tile.Terrain, Is.Not.EqualTo(TerrainType.Water).And.Not.EqualTo(TerrainType.Void), $"seed {seed} {placement.Coord}");
                    Assert.That(tile.Slope, Is.EqualTo(SlopeType.Flat), $"seed {seed} {placement.Coord}");
                }
            }
        }
    }
}
