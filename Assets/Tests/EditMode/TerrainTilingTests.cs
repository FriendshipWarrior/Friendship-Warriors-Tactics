using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Cameras;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.View;
using NUnit.Framework;
using UnityEngine;

namespace FriendshipWarriors.Tests
{
    public class TerrainTilingTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                Object.DestroyImmediate(obj);
            _created.Clear();
        }

        // ------------------------------------------------------------------ Atlas cells

        [Test]
        public void CellUv_SkipsTheGutterAndCountsRowsFromTheTop()
        {
            // 8 columns of 36 px (32 + 2 × 2 gutter), 3 rows: 288 × 108.
            var first = TerrainTiling.CellUv(0, 8, 32, 2, 288, 108);
            Assert.That(first.x, Is.EqualTo(2f / 288f).Within(1e-6));
            Assert.That(first.y, Is.EqualTo((108f - 2f - 32f) / 108f).Within(1e-6), "the top row sits at the top of the image");
            Assert.That(first.width, Is.EqualTo(32f / 288f).Within(1e-6));
            Assert.That(first.height, Is.EqualTo(32f / 108f).Within(1e-6));

            var ninth = TerrainTiling.CellUv(9, 8, 32, 2, 288, 108);
            Assert.That(ninth.x, Is.EqualTo((36f + 2f) / 288f).Within(1e-6), "second column");
            Assert.That(ninth.y, Is.EqualTo((108f - 36f - 2f - 32f) / 108f).Within(1e-6), "second row, one stride lower");
        }

        // ------------------------------------------------------------------ Variants

        [Test]
        public void PickVariant_IsStablePerCoordinateAndInRange()
        {
            for (int x = 0; x < 12; x++)
            for (int y = 0; y < 12; y++)
            {
                int pick = TerrainTiling.PickVariant(x, y, 0, 5);
                Assert.That(pick, Is.InRange(0, 4));
                Assert.That(TerrainTiling.PickVariant(x, y, 0, 5), Is.EqualTo(pick), "same coordinate, same variant");
            }
            Assert.That(TerrainTiling.PickVariant(3, 4, 0, 1), Is.Zero);
            Assert.That(TerrainTiling.PickVariant(3, 4, 0, 0), Is.Zero);
        }

        [Test]
        public void PickVariant_UsesEveryVariantAndDependsOnTheSalt()
        {
            var seen = new HashSet<int>();
            int differ = 0;
            for (int x = 0; x < 10; x++)
            for (int y = 0; y < 10; y++)
            {
                seen.Add(TerrainTiling.PickVariant(x, y, 0, 4));
                if (TerrainTiling.PickVariant(x, y, 0, 4) != TerrainTiling.PickVariant(x, y, 1, 4))
                    differ++;
            }
            Assert.That(seen, Is.EquivalentTo(new[] { 0, 1, 2, 3 }));
            Assert.That(differ, Is.GreaterThan(30), "tops and walls pick independently");
        }

        [Test]
        public void PickVariant_IsFixedAcrossRuns()
        {
            // Pinned values: a map must look the same on every machine and every launch.
            Assert.That(new[]
            {
                TerrainTiling.PickVariant(0, 0, 0, 8), TerrainTiling.PickVariant(1, 0, 0, 8),
                TerrainTiling.PickVariant(0, 1, 0, 8), TerrainTiling.PickVariant(5, 7, 3, 8),
            }, Is.EqualTo(PinnedVariants));
        }

        // Recorded from the implementation; change only on purpose (it reshuffles every map's tiles).
        private static readonly int[] PinnedVariants = { 5, 6, 4, 1 };

        // ------------------------------------------------------------------ Wall bands

        [Test]
        public void SliceWall_CutsAtWholeCellHeights()
        {
            // A 2.5-unit wall from the ground: two full cells and half of a third.
            var bands = TerrainTiling.SliceWall(1f, 2.5f, 2.5f, 0f, 0f);

            Assert.That(bands.Select(b => b.Band), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(bands[2].Points.Max(p => p.Y), Is.EqualTo(2.5f).Within(1e-5));
            Assert.That(bands[2].Points.Min(p => p.Y), Is.EqualTo(2f).Within(1e-5));
            Assert.That(TerrainTiling.BandV(2.5f, 2), Is.EqualTo(0.5f).Within(1e-5), "the top band shows the lower half of its cell");
        }

        [Test]
        public void SliceWall_LinesUpAcrossColumnsWithDifferentBottoms()
        {
            // Two neighbouring walls with the same top but different bottoms cut at the same world heights, so the
            // repeating cliff texture continues from one column to the next.
            var tall = TerrainTiling.SliceWall(1f, 2.25f, 2.25f, 0f, 0f);
            var short_ = TerrainTiling.SliceWall(1f, 2.25f, 2.25f, 1.5f, 1.5f);

            var tallTop = tall.Single(b => b.Band == 2);
            var shortTop = short_.Single(b => b.Band == 2);
            Assert.That(shortTop.Points.Select(p => p.Y).Distinct().OrderBy(y => y),
                Is.EqualTo(tallTop.Points.Select(p => p.Y).Distinct().OrderBy(y => y)).Within(1e-5));
            Assert.That(short_.Select(b => b.Band), Is.EqualTo(new[] { 1, 2 }), "starts in the band containing its bottom");
            Assert.That(short_[0].Points.Min(p => p.Y), Is.EqualTo(1.5f).Within(1e-5));
        }

        [Test]
        public void SliceWall_FollowsASlopedTop()
        {
            // Under a ramp the top edge slants from 1.5 (left) to 1.0 (right).
            var bands = TerrainTiling.SliceWall(1f, 1.5f, 1f, 0f, 0f);

            Assert.That(bands.Select(b => b.Band), Is.EqualTo(new[] { 0, 1 }));
            var upper = bands[1];
            Assert.That(upper.Points.Count, Is.EqualTo(3), "a triangle above y = 1");
            Assert.That(upper.Points.Any(p => Mathf.Approximately(p.S, 0f) && Mathf.Approximately(p.Y, 1.5f)), Is.True);
            Assert.That(bands[0].Points.Count, Is.EqualTo(4));
        }

        [Test]
        public void SliceWall_KeepsTheWallsWinding()
        {
            // Left-top, right-top, right-bottom, left-bottom: clockwise in (s, y) as seen from outside.
            foreach (var band in TerrainTiling.SliceWall(1f, 2f, 1.25f, -1f, 0.5f))
            {
                float area = 0f;
                for (int i = 0; i < band.Points.Count; i++)
                {
                    var a = band.Points[i];
                    var b = band.Points[(i + 1) % band.Points.Count];
                    area += a.S * b.Y - b.S * a.Y;
                }
                Assert.That(area, Is.LessThan(0f), $"band {band.Band} keeps clockwise order");
            }
        }

        [Test]
        public void SliceWall_HandlesBelowGroundAndEmptyWalls()
        {
            Assert.That(TerrainTiling.SliceWall(1f, 0.5f, 0.5f, -1f, -1f).Select(b => b.Band), Is.EqualTo(new[] { -1, 0 }));
            Assert.That(TerrainTiling.SliceWall(1f, 1f, 1f, 1f, 1f), Is.Empty);
        }

        // ------------------------------------------------------------------ Atlas and mesh

        private TerrainAtlas MakeAtlas(bool grassLip = true)
        {
            var texture = new Texture2D(288, 108);
            var atlas = ScriptableObject.CreateInstance<TerrainAtlas>();
            atlas.Set(texture, 32, 2, 8, new[]
            {
                new TerrainAtlas.Tiles(TerrainType.Grass, new[] { 0, 1 }, true, new[] { 4, 5 }, grassLip ? 6 : -1, 10),
                new TerrainAtlas.Tiles(TerrainType.Water, new[] { 2 }, false, new[] { 4 }, -1, 0),
                new TerrainAtlas.Tiles(TerrainType.Sand, new int[0], true, new[] { 4 }, -1, 0),
            });
            _created.Add(texture);
            _created.Add(atlas);
            return atlas;
        }

        [Test]
        public void Atlas_CoversOnlyTerrainWithTops()
        {
            var atlas = MakeAtlas();
            Assert.That(atlas.TryGet(TerrainType.Grass, out var grass), Is.True);
            Assert.That(grass.TopVariantCount, Is.EqualTo(4), "two cells, each also mirrored");
            Assert.That(atlas.TryGet(TerrainType.Water, out var water), Is.True);
            Assert.That(water.TopVariantCount, Is.EqualTo(1));
            Assert.That(atlas.TryGet(TerrainType.Sand, out _), Is.False, "no tops: keeps its flat colour");
            Assert.That(atlas.TryGet(TerrainType.Stone, out _), Is.False);
        }

        [Test]
        public void TopCell_MapsVariantsToCellsAndMirroring()
        {
            var atlas = MakeAtlas();
            atlas.TryGet(TerrainType.Grass, out var grass);
            var seen = new HashSet<(int, bool)>();
            for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
            {
                var pick = BattlefieldMeshBuilder.TopCell(grass, new GridCoord(x, y));
                Assert.That(pick.cell, Is.EqualTo(0).Or.EqualTo(1));
                seen.Add(pick);
            }
            Assert.That(seen.Count, Is.EqualTo(4), "both cells, plain and mirrored");
        }

        [Test]
        public void Mesh_UsesAtlasCellsForCoveredTerrain()
        {
            var atlas = MakeAtlas();
            var grid = TestGrids.FromRows("12");
            var mesh = BattlefieldMeshBuilder.Build(grid, 1f, atlas);
            _created.Add(mesh);

            var uvs = mesh.uv;
            var tops = mesh.GetTriangles(BattlefieldMeshBuilder.TopSubmesh(TerrainType.Grass));
            var grassCells = new[] { atlas.CellUv(0), atlas.CellUv(1) };
            foreach (int index in tops)
                Assert.That(grassCells.Any(r => Inside(r, uvs[index])), Is.True, $"top UV {uvs[index]} lies in a grass cell");

            var sides = mesh.GetTriangles(BattlefieldMeshBuilder.SideSubmesh(TerrainType.Grass));
            var cliffCells = new[] { atlas.CellUv(4), atlas.CellUv(5) };
            Assert.That(sides, Is.Not.Empty);
            foreach (int index in sides)
                Assert.That(cliffCells.Any(r => Inside(r, uvs[index])), Is.True, $"side UV {uvs[index]} lies in a cliff cell");
        }

        [Test]
        public void Mesh_AddsLipsOnlyWhereTheTerrainHasOne()
        {
            var grid = TestGrids.FromRows("12");
            var withLip = BattlefieldMeshBuilder.Build(grid, 1f, MakeAtlas(grassLip: true));
            var withoutLip = BattlefieldMeshBuilder.Build(grid, 1f, MakeAtlas(grassLip: false));
            var plain = BattlefieldMeshBuilder.Build(grid, 1f);
            _created.Add(withLip);
            _created.Add(withoutLip);
            _created.Add(plain);

            Assert.That(withLip.GetTriangles(BattlefieldMeshBuilder.LipSubmesh), Is.Not.Empty);
            Assert.That(withoutLip.GetTriangles(BattlefieldMeshBuilder.LipSubmesh), Is.Empty);
            Assert.That(plain.GetTriangles(BattlefieldMeshBuilder.LipSubmesh), Is.Empty, "no atlas, no lips");
        }

        [Test]
        public void Mesh_KeepsPlainUvsForTerrainTheAtlasLacks()
        {
            var atlas = MakeAtlas();
            var grid = new BattleGrid(2, 1);
            grid[new GridCoord(0, 0)] = new Tile(0, TerrainType.Stone);
            grid[new GridCoord(1, 0)] = new Tile(0, TerrainType.Grass);
            var mesh = BattlefieldMeshBuilder.Build(grid, 1f, atlas);
            _created.Add(mesh);

            var uvs = mesh.uv;
            var stoneTops = mesh.GetTriangles(BattlefieldMeshBuilder.TopSubmesh(TerrainType.Stone)).Select(i => uvs[i]).ToArray();
            // The old mapping: one UV unit per tile, offset so tile (0, 0) spans 0..1.
            Assert.That(stoneTops.Min(uv => uv.x), Is.EqualTo(0f).Within(1e-5));
            Assert.That(stoneTops.Max(uv => uv.x), Is.EqualTo(1f).Within(1e-5));
        }

        [Test]
        public void Mesh_SideBandsLineUpBetweenNeighbouringColumns()
        {
            // Two 3h columns side by side over a 0h row: their south walls are cut at the same heights.
            var atlas = MakeAtlas();
            var grid = TestGrids.FromRows("33", "00");
            var mesh = BattlefieldMeshBuilder.Build(grid, 1f, atlas);
            _created.Add(mesh);

            var vertices = mesh.vertices;
            var sides = mesh.GetTriangles(BattlefieldMeshBuilder.SideSubmesh(TerrainType.Grass));
            float Round(float v) => Mathf.Round(v * 1000f) / 1000f;
            var leftHeights = new HashSet<float>();
            var rightHeights = new HashSet<float>();
            foreach (int index in sides)
            {
                var v = vertices[index];
                // South walls of the 3h row stand at z = 0.5 (between rows 0 and 1).
                if (!Mathf.Approximately(v.z, 0.5f))
                    continue;
                (v.x < 0.5f ? leftHeights : rightHeights).Add(Round(v.y));
            }
            Assert.That(leftHeights, Is.EquivalentTo(rightHeights));
            Assert.That(leftHeights, Is.SupersetOf(new[] { 0f, 1f, 1.5f }), "cut at 1 world unit (2h), up to 3h = 1.5 units");
        }

        private static bool Inside(Rect rect, Vector2 uv) =>
            uv.x >= rect.xMin - 1e-5f && uv.x <= rect.xMax + 1e-5f && uv.y >= rect.yMin - 1e-5f && uv.y <= rect.yMax + 1e-5f;

        // ------------------------------------------------------------------ Pixel look

        [TestCase(800, 360, 2)]
        [TestCase(720, 360, 2)]
        [TestCase(1080, 360, 3)]
        [TestCase(1440, 360, 4)]
        [TestCase(300, 360, 1)]
        public void PixelScale_IsAWholeNumberNearTheReferenceHeight(int screenHeight, int reference, int scale)
        {
            Assert.That(PixelRendering.ScaleFor(screenHeight, reference), Is.EqualTo(scale));
        }

        [Test]
        public void PixelRenderScale_IsTheInverseOfTheUpscale()
        {
            Assert.That(PixelRendering.RenderScaleFor(2), Is.EqualTo(0.5f));
            Assert.That(PixelRendering.RenderScaleFor(3), Is.EqualTo(1f / 3f).Within(1e-6));
            Assert.That(PixelRendering.RenderScaleFor(1), Is.EqualTo(1f));
            Assert.That(PixelRendering.RenderScaleFor(20), Is.EqualTo(0.1f), "URP's minimum");
        }
    }
}
