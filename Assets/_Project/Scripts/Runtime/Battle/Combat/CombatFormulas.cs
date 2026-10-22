using System;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Equipment;

namespace FriendshipWarriors.Battle.Combat
{
    /// <summary>
    /// FFT's physical attack math (weapon formulas and evasion verified against published references; critical
    /// numbers recalled). Statuses, elements, zodiac compatibility and support abilities that modify these come later.
    /// </summary>
    public static class CombatFormulas
    {
        /// <summary>
        /// Stand-in for Brave, which we've removed: FFT's bare-handed formula needs it. 70 is a typical FFT value.
        /// Replace when our own stat exists.
        /// </summary>
        public const int BraveStandIn = 70;

        /// <summary>FFT's chance for a physical attack to be a critical hit.</summary>
        public const double BaseCriticalChance = 0.05;

        public static AttackFactors AttackFactors(Unit attacker)
        {
            var s = attacker.Stats;
            int wp = attacker.Weapon != null ? attacker.Weapon.Power : 0;
            return attacker.WeaponType switch
            {
                WeaponType.Sword => new AttackFactors(s.PhysicalAttack, wp),
                WeaponType.Knife => new AttackFactors((s.PhysicalAttack + s.Speed) / 2, wp),
                WeaponType.Rifle => new AttackFactors((s.PhysicalAttack + s.Speed) / 2, wp), // FFT's bow formula
                WeaponType.Rod => new AttackFactors(s.PhysicalAttack, wp),
                WeaponType.Staff => new AttackFactors(s.MagicAttack, wp),
                _ => new AttackFactors(s.PhysicalAttack * BraveStandIn / 100, s.PhysicalAttack),
            };
        }

        /// <summary>
        /// Chance, in percent, that a physical attack lands. Each applicable evasion multiplies the chance down:
        /// class evasion only from the front, shield evasion from the front or side, accessory evasion always.
        /// </summary>
        public static int HitChance(Unit attacker, Unit target)
        {
            var angle = FacingRules.AngleOfAttack(target.Coord, target.Facing, attacker.Coord);
            double hit = 1.0;
            if (FacingRules.AppliesClassEvasion(angle))
                hit *= 1.0 - target.Stats.ClassEvasion / 100.0;
            if (FacingRules.AppliesShieldEvasion(angle) && target.Shield != null)
                hit *= 1.0 - target.Shield.Evasion / 100.0;
            if (FacingRules.AppliesAccessoryEvasion(angle) && target.Accessory != null)
                hit *= 1.0 - target.Accessory.Evasion / 100.0;
            return (int)Math.Floor(hit * 100.0 + 1e-9);
        }

        /// <summary>A critical hit raises XA by a random 0 to XA − 1 before multiplying, so up to nearly double damage.</summary>
        public static int CriticalDamage(AttackFactors factors, Random rng) =>
            (factors.Xa + (factors.Xa > 1 ? rng.Next(0, factors.Xa) : 0)) * factors.Multiplier;
    }
}
