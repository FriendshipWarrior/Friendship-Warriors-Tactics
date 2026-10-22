using System.Linq;
using FriendshipWarriors.Grid;
using NUnit.Framework;

namespace FriendshipWarriors.Tests
{
    public class GridCoordTests
    {
        [Test]
        public void EqualCoords_AreEqualAndHashAlike()
        {
            var a = new GridCoord(3, 4);
            var b = new GridCoord(3, 4);

            Assert.That(a == b, Is.True);
            Assert.That(a.Equals((object)b), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(a != new GridCoord(4, 3), Is.True);
        }

        [Test]
        public void Arithmetic_AddsAndSubtractsComponentwise()
        {
            Assert.That(new GridCoord(1, 2) + GridCoord.East, Is.EqualTo(new GridCoord(2, 2)));
            Assert.That(new GridCoord(5, 5) - new GridCoord(2, 3), Is.EqualTo(new GridCoord(3, 2)));
        }

        [Test]
        public void ManhattanDistance_SumsAxisDistances()
        {
            Assert.That(new GridCoord(0, 0).ManhattanDistance(new GridCoord(3, -4)), Is.EqualTo(7));
        }

        [Test]
        public void CardinalDirections_AreFourDistinctUnitSteps()
        {
            var directions = GridCoord.CardinalDirections;

            Assert.That(directions.Distinct().Count(), Is.EqualTo(4));
            Assert.That(directions.All(d => d.ManhattanDistance(default) == 1), Is.True);
        }
    }
}
