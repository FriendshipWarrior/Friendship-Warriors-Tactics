using FriendshipWarriors.Grid;
using NUnit.Framework;

namespace FriendshipWarriors.Tests
{
    public class TileShapeTests
    {
        private static TileCorners CornersOf(SlopeType slope) =>
            TileShape.Corners(new Tile(2, TerrainType.Grass, slope: slope, slopeHeight: 4));

        [Test]
        public void Flat_HasAllCornersAtBaseHeight()
        {
            var c = CornersOf(SlopeType.Flat);

            Assert.That(new[] { c.SouthWest, c.SouthEast, c.NorthEast, c.NorthWest }, Is.All.EqualTo(2));
        }

        [TestCase(SlopeType.RiseNorth, 2, 2, 6, 6)]
        [TestCase(SlopeType.RiseEast, 2, 6, 6, 2)]
        [TestCase(SlopeType.RiseSouth, 6, 6, 2, 2)]
        [TestCase(SlopeType.RiseWest, 6, 2, 2, 6)]
        [TestCase(SlopeType.ConvexNorthEast, 2, 2, 6, 2)]
        [TestCase(SlopeType.ConvexSouthWest, 6, 2, 2, 2)]
        [TestCase(SlopeType.ConcaveNorthEast, 6, 6, 2, 6)]
        [TestCase(SlopeType.ConcaveSouthWest, 2, 6, 6, 6)]
        public void Slopes_RaiseTheExpectedCorners(SlopeType slope, int sw, int se, int ne, int nw)
        {
            var c = CornersOf(slope);

            Assert.That(new[] { c.SouthWest, c.SouthEast, c.NorthEast, c.NorthWest }, Is.EqualTo(new[] { sw, se, ne, nw }));
        }

        [TestCase(SlopeType.ConvexNorthEast, true)]
        [TestCase(SlopeType.ConcaveSouthWest, true)]
        [TestCase(SlopeType.ConvexNorthWest, false)]
        [TestCase(SlopeType.RiseNorth, false)]
        public void CornerSlopes_SplitAcrossTheOddCorner(SlopeType slope, bool splitsNwSe)
        {
            Assert.That(TileShape.SplitsNorthWestToSouthEast(slope), Is.EqualTo(splitsNwSe));
        }
    }
}
