using FriendshipWarriors.Units.View.PaperDoll;
using UnityEngine;

namespace FriendshipWarriors.Editor.Art
{
    /// <summary>
    /// Where the body is in each frame, shared by every part so they line up: bob, lean, hand and foot positions and
    /// the weapon angle. Coordinates are inside a 40×40 frame, top-left origin, ground on row 39, character facing
    /// screen-right.
    /// </summary>
    internal readonly struct DollRig
    {
        public readonly DollAnimation Animation;
        public readonly int Frame;
        public readonly int Bob;
        public readonly int Lean;
        public readonly int HeadTilt;
        public readonly Vector2Int NearHand;
        public readonly Vector2Int FarHand;
        public readonly int NearFoot;
        public readonly int FarFoot;
        /// <summary>Degrees, 0 = pointing screen-right, 90 = straight up.</summary>
        public readonly float WeaponAngle;
        public readonly int Glow;
        public readonly bool Flash;
        public readonly bool KnockedOut;

        private DollRig(DollAnimation animation, int frame, int bob, int lean, int tilt, Vector2Int near, Vector2Int far,
            int nearFoot, int farFoot, float angle, int glow = 0, bool flash = false, bool knockedOut = false)
        {
            Animation = animation;
            Frame = frame;
            Bob = bob;
            Lean = lean;
            HeadTilt = tilt;
            NearHand = near;
            FarHand = far;
            NearFoot = nearFoot;
            FarFoot = farFoot;
            WeaponAngle = angle;
            Glow = glow;
            Flash = flash;
            KnockedOut = knockedOut;
        }

        public int HeadTop => 7 + Bob;
        public int HeadCx => 21 + Lean + HeadTilt;
        public int TorsoTop => 19 + Bob;
        public int Hip => 30 + Bob;
        public int TorsoLeft => 16 + Lean;
        public int TorsoRight => 25 + Lean;
        public Vector2Int NearShoulder => new(TorsoRight - 1, TorsoTop + 1);
        public Vector2Int FarShoulder => new(TorsoLeft + 1, TorsoTop + 1);

        private static Vector2Int P(int x, int y) => new(x, y);

        public static DollRig For(DollAnimation animation, int frame)
        {
            var a = animation;
            switch (animation)
            {
                case DollAnimation.Idle:
                    return frame == 0
                        ? new DollRig(a, 0, 0, 0, 0, P(26, 29), P(15, 29), 0, 0, -60)
                        : new DollRig(a, 1, 1, 0, 0, P(26, 30), P(15, 30), 0, 0, -60);
                case DollAnimation.Walk:
                    return frame switch
                    {
                        0 => new DollRig(a, 0, 0, 0, 0, P(25, 28), P(16, 29), 2, -1, -50),
                        1 => new DollRig(a, 1, 1, 0, 0, P(26, 30), P(15, 30), 0, 0, -60),
                        2 => new DollRig(a, 2, 0, 0, 0, P(27, 29), P(14, 28), -1, 2, -70),
                        _ => new DollRig(a, 3, 1, 0, 0, P(26, 30), P(15, 30), 0, 0, -60),
                    };
                case DollAnimation.Attack:
                    return frame switch
                    {
                        0 => new DollRig(a, 0, 0, -1, -1, P(23, 21), P(15, 27), 0, 0, 115),
                        1 => new DollRig(a, 1, 0, 2, 1, P(30, 24), P(17, 27), 1, 0, 0, flash: true),
                        _ => new DollRig(a, 2, 1, 1, 0, P(29, 30), P(15, 29), 1, 0, -40),
                    };
                case DollAnimation.Cast:
                    return frame == 0
                        ? new DollRig(a, 0, 0, 0, 0, P(27, 18), P(14, 18), 0, 0, 80, glow: 1)
                        : new DollRig(a, 1, -1, 0, 0, P(28, 16), P(13, 16), 0, 0, 88, glow: 2);
                case DollAnimation.Hurt:
                    return new DollRig(a, 0, 1, -2, -1, P(24, 26), P(13, 25), -1, 0, -85);
                default:
                    // Knocked out: drawn standing in the idle pose, then laid down by PixelCanvas.LyingDown.
                    return new DollRig(DollAnimation.KnockedOut, 0, 0, 0, 0, P(26, 29), P(15, 29), 0, 0, -60, knockedOut: true);
            }
        }
    }
}
