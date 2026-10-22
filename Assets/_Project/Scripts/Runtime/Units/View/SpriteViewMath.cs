using UnityEngine;

namespace FriendshipWarriors.Units.View
{
    public static class SpriteViewMath
    {
        /// <summary>
        /// Picks the pose for a unit facing a grid direction under a camera at <paramref name="cameraYaw"/>. The camera
        /// always sits on a diagonal, so a unit is seen from one of four three-quarter angles: two drawn poses (front
        /// and back, both drawn facing screen-right) cover all four by mirroring, as in FFT.
        /// </summary>
        public static SpritePose Resolve(Direction facing, float cameraYaw)
        {
            float facingYaw = facing.ToYaw();
            bool towardCamera = Mathf.Abs(Mathf.DeltaAngle(facingYaw, cameraYaw + 180f)) < 90f;
            bool facesScreenRight = Mathf.Abs(Mathf.DeltaAngle(facingYaw, cameraYaw + 90f)) < 90f;
            return new SpritePose(towardCamera, !facesScreenRight);
        }
    }
}
