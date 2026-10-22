using System;
using FriendshipWarriors.Grid;

namespace FriendshipWarriors.Units
{
    /// <summary>
    /// FFT's facing rules. Facing never changes damage; it decides which evasion the target gets: from the front,
    /// class, shield and accessory evasion all apply; from the side, class evasion is lost; from behind, only
    /// accessory evasion remains.
    /// </summary>
    public static class FacingRules
    {
        /// <summary>
        /// Classifies an attack by comparing how far the attacker is in front of or behind the target with how far it
        /// is to the side. An attacker exactly on a diagonal counts as a side attack (our approximation of FFT).
        /// </summary>
        public static AttackAngle AngleOfAttack(GridCoord target, Direction targetFacing, GridCoord attacker)
        {
            var offset = attacker - target;
            var forward = targetFacing.ToStep();
            int along = offset.X * forward.X + offset.Y * forward.Y;
            int across = Math.Abs(offset.X * forward.Y - offset.Y * forward.X);
            if (along > across)
                return AttackAngle.Front;
            if (-along > across)
                return AttackAngle.Back;
            return AttackAngle.Side;
        }

        public static bool AppliesClassEvasion(AttackAngle angle) => angle == AttackAngle.Front;
        public static bool AppliesShieldEvasion(AttackAngle angle) => angle != AttackAngle.Back;
        public static bool AppliesAccessoryEvasion(AttackAngle angle) => true;
    }
}
