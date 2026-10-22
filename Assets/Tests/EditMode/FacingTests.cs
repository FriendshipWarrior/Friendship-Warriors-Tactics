using FriendshipWarriors.Grid;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.View;
using NUnit.Framework;

namespace FriendshipWarriors.Tests
{
    public class FacingTests
    {
        private static readonly GridCoord Target = new(5, 5);

        [TestCase(5, 6, AttackAngle.Front)]  // directly ahead
        [TestCase(5, 8, AttackAngle.Front)]  // ahead at range
        [TestCase(6, 7, AttackAngle.Front)]  // ahead and slightly to the side
        [TestCase(6, 5, AttackAngle.Side)]   // directly beside
        [TestCase(4, 5, AttackAngle.Side)]
        [TestCase(6, 6, AttackAngle.Side)]   // exact diagonals count as side
        [TestCase(4, 4, AttackAngle.Side)]
        [TestCase(5, 4, AttackAngle.Back)]   // directly behind
        [TestCase(4, 2, AttackAngle.Back)]
        public void AttackAngle_IsMeasuredAgainstTheTargetsFacing(int x, int y, AttackAngle expected)
        {
            Assert.That(FacingRules.AngleOfAttack(Target, Direction.North, new GridCoord(x, y)), Is.EqualTo(expected));
        }

        [Test]
        public void AttackAngle_RotatesWithFacing()
        {
            var attacker = new GridCoord(6, 5); // east of the target
            Assert.That(FacingRules.AngleOfAttack(Target, Direction.East, attacker), Is.EqualTo(AttackAngle.Front));
            Assert.That(FacingRules.AngleOfAttack(Target, Direction.West, attacker), Is.EqualTo(AttackAngle.Back));
            Assert.That(FacingRules.AngleOfAttack(Target, Direction.South, attacker), Is.EqualTo(AttackAngle.Side));
        }

        [Test]
        public void Evasion_ShrinksFromFrontToSideToBack()
        {
            Assert.That(FacingRules.AppliesClassEvasion(AttackAngle.Front), Is.True);
            Assert.That(FacingRules.AppliesClassEvasion(AttackAngle.Side), Is.False);
            Assert.That(FacingRules.AppliesShieldEvasion(AttackAngle.Side), Is.True);
            Assert.That(FacingRules.AppliesShieldEvasion(AttackAngle.Back), Is.False);
            Assert.That(FacingRules.AppliesAccessoryEvasion(AttackAngle.Back), Is.True);
        }

        [Test]
        public void Direction_HelpersRoundTrip()
        {
            foreach (Direction d in System.Enum.GetValues(typeof(Direction)))
            {
                Assert.That(DirectionExtensions.FromStep(d.ToStep()), Is.EqualTo(d));
                Assert.That(d.Opposite().Opposite(), Is.EqualTo(d));
            }
            Assert.That(DirectionExtensions.Toward(new GridCoord(0, 0), new GridCoord(3, 1)), Is.EqualTo(Direction.East));
            Assert.That(DirectionExtensions.Toward(new GridCoord(0, 0), new GridCoord(-1, -4)), Is.EqualTo(Direction.South));
        }

        // Default camera (yaw 45) looks north-east: south and west face the camera, south toward screen-right.
        [TestCase(Direction.South, 45f, true, false)]
        [TestCase(Direction.West, 45f, true, true)]
        [TestCase(Direction.East, 45f, false, false)]
        [TestCase(Direction.North, 45f, false, true)]
        // Rotated half way round, everything swaps.
        [TestCase(Direction.North, 225f, true, false)]
        [TestCase(Direction.South, 225f, false, true)]
        public void SpritePose_PicksFrontOrBackAndMirrorsByCameraAngle(Direction facing, float cameraYaw, bool front, bool flip)
        {
            var pose = SpriteViewMath.Resolve(facing, cameraYaw);

            Assert.That(pose.ShowsFront, Is.EqualTo(front), "front");
            Assert.That(pose.FlipX, Is.EqualTo(flip), "flip");
        }

        [Test]
        public void SpritePose_CoversAllFourViewsForEachCameraAngle()
        {
            foreach (float yaw in new[] { 45f, 135f, 225f, 315f })
            {
                var seen = new System.Collections.Generic.HashSet<(bool, bool)>();
                foreach (Direction d in System.Enum.GetValues(typeof(Direction)))
                {
                    var pose = SpriteViewMath.Resolve(d, yaw);
                    seen.Add((pose.ShowsFront, pose.FlipX));
                }
                Assert.That(seen.Count, Is.EqualTo(4), $"yaw {yaw}");
            }
        }
    }
}
