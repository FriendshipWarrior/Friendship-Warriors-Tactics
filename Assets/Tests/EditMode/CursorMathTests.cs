using FriendshipWarriors.Controls;
using FriendshipWarriors.Grid;
using NUnit.Framework;
using UnityEngine;

namespace FriendshipWarriors.Tests
{
    public class CursorMathTests
    {
        [Test]
        public void DefaultView_MapsEachDirectionToOneGridAxis()
        {
            Assert.That(CursorMath.ToGridStep(Vector2.up, 45f), Is.EqualTo(GridCoord.North));
            Assert.That(CursorMath.ToGridStep(Vector2.right, 45f), Is.EqualTo(GridCoord.East));
            Assert.That(CursorMath.ToGridStep(Vector2.down, 45f), Is.EqualTo(GridCoord.South));
            Assert.That(CursorMath.ToGridStep(Vector2.left, 45f), Is.EqualTo(GridCoord.West));
        }

        [TestCase(135f, 0, -1)]
        [TestCase(225f, -1, 0)]
        [TestCase(315f, 0, 1)]
        public void RotatedViews_TurnTheMappingWithTheCamera(float yaw, int expectedX, int expectedY)
        {
            // Pressing Right always moves the cursor along the same screen direction (up-right).
            var step = CursorMath.ToGridStep(Vector2.right, yaw);

            Assert.That(step, Is.EqualTo(new GridCoord(expectedX, expectedY)));
        }

        [Test]
        public void UnwrappedYaw_GivesTheSameAnswerAsWrapped()
        {
            Assert.That(CursorMath.ToGridStep(Vector2.up, 45f + 360f * 3), Is.EqualTo(GridCoord.North));
            Assert.That(CursorMath.ToGridStep(Vector2.up, 45f - 360f * 2), Is.EqualTo(GridCoord.North));
        }

        [Test]
        public void NoInput_GivesNoStep()
        {
            Assert.That(CursorMath.ToGridStep(Vector2.zero, 45f), Is.EqualTo(default(GridCoord)));
        }
    }
}
