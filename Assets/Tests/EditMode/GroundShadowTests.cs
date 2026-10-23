using FriendshipWarriors.Grid;
using FriendshipWarriors.Units.View.Walking;
using NUnit.Framework;
using UnityEngine;

namespace FriendshipWarriors.Tests
{
    public class GroundShadowTests
    {
        private const float H = GridMetrics.StepHeight * GridMetrics.StepsPerH; // world units per h

        [Test]
        public void OnFlatGround_TheShadowSitsOnTheTile()
        {
            var grid = TestGrids.FromRows("23");

            Assert.That(GroundShadow.TrySurfaceHeight(grid, new Vector3(0.2f, 5f, 0f), out float a), Is.True);
            Assert.That(a, Is.EqualTo(2 * H).Within(1e-4f));
            Assert.That(GroundShadow.TrySurfaceHeight(grid, new Vector3(0.8f, 5f, 0f), out float b), Is.True, "past the edge: the next tile");
            Assert.That(b, Is.EqualTo(3 * H).Within(1e-4f));
        }

        [Test]
        public void OnASlope_TheShadowFollowsTheSurface()
        {
            var grid = new BattleGrid(1, 1);
            grid[new GridCoord(0, 0)] = new Tile(2, TerrainType.Grass, slope: SlopeType.RiseEast, slopeHeight: 2);

            GroundShadow.TrySurfaceHeight(grid, new Vector3(-0.5f, 0f, 0f), out float west);
            GroundShadow.TrySurfaceHeight(grid, new Vector3(0f, 0f, 0f), out float middle);
            GroundShadow.TrySurfaceHeight(grid, new Vector3(0.5f, 0f, 0f), out float east);
            Assert.That(west, Is.EqualTo(1 * H).Within(1e-4f));
            Assert.That(middle, Is.EqualTo(1.5f * H).Within(1e-4f));
            Assert.That(east, Is.EqualTo(2 * H).Within(1e-4f));
        }

        [Test]
        public void MidLeap_OverLowerGround_TheShadowDropsToThatGround()
        {
            // A leap from 3h over a 1h tile to 3h: halfway, the shadow is on the 1h tile, far below the unit.
            var grid = TestGrids.FromRows("313");
            var leap = WalkPath.ForRoute(grid, new GridCoord(0, 0), new[] { new GridCoord(2, 0) }, WalkSettings.Default);
            var unit = leap.PositionAt(leap.Duration * 0.5f);

            Assert.That(GroundShadow.TrySurfaceHeight(grid, unit, out float ground), Is.True);
            Assert.That(ground, Is.EqualTo(1 * H).Within(1e-4f));
            Assert.That(unit.y - ground, Is.GreaterThan(2 * H));
        }

        [Test]
        public void OverAVoidChasm_OrOffTheMap_ThereIsNoGround()
        {
            var grid = TestGrids.FromRows("2.2");

            Assert.That(GroundShadow.TrySurfaceHeight(grid, new Vector3(1f, 3f, 0f), out _), Is.False);
            Assert.That(GroundShadow.TrySurfaceHeight(grid, new Vector3(-2f, 3f, 0f), out _), Is.False);
        }

        [Test]
        public void TheShadowShrinksAndFadesWithHeight_WithinLimits()
        {
            Assert.That(GroundShadow.Scale(0f), Is.EqualTo(1f));
            Assert.That(GroundShadow.Opacity(0f), Is.EqualTo(1f));
            Assert.That(GroundShadow.Scale(0.5f), Is.LessThan(1f).And.GreaterThan(GroundShadow.MinScale));
            Assert.That(GroundShadow.Scale(1f), Is.LessThan(GroundShadow.Scale(0.5f)));
            Assert.That(GroundShadow.Opacity(1f), Is.LessThan(GroundShadow.Opacity(0.5f)));
            Assert.That(GroundShadow.Scale(10f), Is.EqualTo(GroundShadow.MinScale).Within(1e-5f));
            Assert.That(GroundShadow.Opacity(10f), Is.EqualTo(GroundShadow.MinOpacity).Within(1e-5f));
            Assert.That(GroundShadow.Scale(-1f), Is.EqualTo(1f), "never grows below the ground");
        }
    }
}
