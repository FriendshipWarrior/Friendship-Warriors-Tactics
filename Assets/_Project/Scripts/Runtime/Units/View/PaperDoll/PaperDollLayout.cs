using System.Collections.Generic;
using UnityEngine;

namespace FriendshipWarriors.Units.View.PaperDoll
{
    /// <summary>
    /// The fixed grid every paper-doll part sheet uses (documented for artists in Art/Sprites/README.md).
    /// Frames are 40×40 px, 32 px per world unit, pivot bottom-centre. Rows are animations (see
    /// <see cref="DollAnimation"/>); columns 0–3 hold the front-¾ pose and columns 4–7 the back-¾ pose, both facing
    /// screen-right (the game mirrors them). A sheet is 320×240 px. Pixel coordinates in the docs are top-left
    /// origin, as in Aseprite; Unity textures are bottom-left, which <see cref="FrameRect"/> accounts for.
    /// </summary>
    public static class PaperDollLayout
    {
        public const int FrameWidth = 40;
        public const int FrameHeight = 40;
        public const int ColumnsPerPose = 4;
        public const int Rows = 6;
        public const int SheetWidth = FrameWidth * ColumnsPerPose * 2;
        public const int SheetHeight = FrameHeight * Rows;
        public const float PixelsPerUnit = 32f;

        private static readonly int[] FrameCounts = { 2, 4, 3, 2, 1, 1 };

        /// <summary>Seconds each frame shows; attack and hurt play once, the rest loop.</summary>
        private static readonly float[] FrameSeconds = { 0.55f, 0.15f, 0.12f, 0.25f, 0.35f, 1f };

        public static int FrameCount(DollAnimation animation) => FrameCounts[(int)animation];
        public static float FrameDuration(DollAnimation animation) => FrameSeconds[(int)animation];
        public static bool Loops(DollAnimation animation) => animation is DollAnimation.Idle or DollAnimation.Walk or DollAnimation.Cast;

        /// <summary>Column of a frame: front pose 0–3, back pose 4–7.</summary>
        public static int Column(int frame, bool back) => (back ? ColumnsPerPose : 0) + frame;

        /// <summary>The frame's rectangle in Unity texture space (origin bottom-left).</summary>
        public static RectInt FrameRect(DollAnimation animation, int frame, bool back) =>
            new(Column(frame, back) * FrameWidth, SheetHeight - ((int)animation + 1) * FrameHeight, FrameWidth, FrameHeight);

        /// <summary>The frame's top-left pixel in image space (origin top-left, as Aseprite shows it).</summary>
        public static Vector2Int FrameTopLeft(DollAnimation animation, int frame, bool back) =>
            new(Column(frame, back) * FrameWidth, (int)animation * FrameHeight);

        private static readonly DollSlot[] FrontOrder =
        {
            DollSlot.Shield, DollSlot.Body, DollSlot.BodyGear, DollSlot.Head, DollSlot.Face, DollSlot.Hair,
            DollSlot.Headgear, DollSlot.Weapon,
        };

        private static readonly DollSlot[] BackOrder =
        {
            DollSlot.Weapon, DollSlot.Body, DollSlot.BodyGear, DollSlot.Head, DollSlot.Face, DollSlot.Hair,
            DollSlot.Headgear, DollSlot.Shield,
        };

        /// <summary>
        /// Bottom-to-top stacking. Facing the camera, the shield sits on the far arm behind the body and the weapon is
        /// in front; from behind it's the other way round.
        /// </summary>
        public static IReadOnlyList<DollSlot> LayerOrder(bool back) => back ? BackOrder : FrontOrder;
    }
}
