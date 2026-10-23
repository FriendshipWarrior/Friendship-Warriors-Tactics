using System;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;
using FriendshipWarriors.Units.Equipment;
using FriendshipWarriors.Units.Status;

namespace FriendshipWarriors.Battle.Combat
{
    /// <summary>
    /// FFT's physical attack math (weapon formulas and evasion verified against published references; critical
    /// numbers recalled), with Morale standing where FFT used Brave and Resonance where it used Faith. Statuses modify
    /// these as in FFT: Protect cuts physical damage to ⅔, sleeping and panicking targets take ×1.5, Blind halves the
    /// attacker's hit chance, and sleeping or stopped targets can't evade. Elements and zodiac compatibility come later.
    /// </summary>
    public static class CombatFormulas
    {
        /// <summary>FFT's chance for a physical attack to be a critical hit.</summary>
        public const double BaseCriticalChance = 0.05;

        /// <summary>
        /// Melee damage scales with the attacker's Morale: × (Morale + 130) / 200 — 1.0 at 70, 1.15 at 100, 0.65 at 0.
        /// Our addition (the user's Morale design); FFT only scaled bare hands and knight swords by Brave.
        /// </summary>
        public const int MeleeMoraleOffset = 130, MeleeMoraleDivisor = 200;

        /// <summary>
        /// Physical damage taken scales down with the target's Morale: × (270 − Morale) / 200 — 1.0 at 70, 0.85 at 100.
        /// Our addition (the user's Morale design).
        /// </summary>
        public const int DefenseMoraleOffset = 270, DefenseMoraleDivisor = 200;

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
                // FFT bare hands: ⌊PA × Brave / 100⌋ × PA, with Morale as Brave.
                _ => new AttackFactors(s.PhysicalAttack * attacker.Morale / 100, s.PhysicalAttack),
            };
        }

        /// <summary>
        /// Weapons swung at close range get the Morale melee bonus. Bare hands already use Morale in their formula and
        /// rifles are ranged, so neither does.
        /// </summary>
        public static bool IsMeleeWeapon(WeaponType type) =>
            type is WeaponType.Sword or WeaponType.Knife or WeaponType.Rod or WeaponType.Staff;

        public static double MeleeMoraleFactor(Unit attacker) =>
            (attacker.Morale + MeleeMoraleOffset) / (double)MeleeMoraleDivisor;

        public static double DefenseMoraleFactor(Unit target) =>
            (DefenseMoraleOffset - target.Morale) / (double)DefenseMoraleDivisor;

        /// <summary>
        /// Final physical damage from a base amount (weapon formula or ability, critical already applied): the
        /// attacker's melee Morale bonus if <paramref name="melee"/>, the target's Morale defense, Protect (×⅔), and
        /// ×1.5 against sleeping or panicking targets. A hit that had any damage does at least 1.
        /// </summary>
        public static int PhysicalDamage(Unit attacker, Unit target, int baseDamage, bool melee)
        {
            if (baseDamage <= 0)
                return 0;
            double damage = baseDamage;
            if (melee)
                damage *= MeleeMoraleFactor(attacker);
            damage *= DefenseMoraleFactor(target);
            if (target.HasStatus(StatusEffect.Protect))
                damage *= 2.0 / 3.0;
            if (target.Statuses.Any(StatusRules.TakesExtraPhysicalDamage))
                damage *= 1.5;
            return Math.Max(1, (int)Math.Floor(damage + 1e-9));
        }

        /// <summary>Damage the unit's Attack would do to the target without a critical: what previews and the AI use.</summary>
        public static int AttackDamage(Unit attacker, Unit target) =>
            PhysicalDamage(attacker, target, AttackFactors(attacker).Damage, IsMeleeWeapon(attacker.WeaponType));

        /// <summary>
        /// Chance, in percent, that a physical attack lands. Each applicable evasion multiplies the chance down:
        /// class evasion only from the front, shield evasion from the front or side, accessory evasion always, and —
        /// for a target with a Parry-type reaction — weapon evasion from the front or side. Sleeping or stopped targets
        /// can't evade at all. A blinded attacker's chance is halved.
        /// </summary>
        public static int HitChance(Unit attacker, Unit target)
        {
            var angle = FacingRules.AngleOfAttack(target.Coord, target.Facing, attacker.Coord);
            double hit = 1.0;
            if (target.CanEvade)
            {
                if (FacingRules.AppliesClassEvasion(angle))
                    hit *= 1.0 - target.Stats.ClassEvasion / 100.0;
                if (FacingRules.AppliesShieldEvasion(angle) && target.Shield != null)
                    hit *= 1.0 - target.Shield.Evasion / 100.0;
                if (FacingRules.AppliesAccessoryEvasion(angle) && target.Accessory != null)
                    hit *= 1.0 - target.Accessory.Evasion / 100.0;
                if (FacingRules.AppliesShieldEvasion(angle) && Parries(target))
                    hit *= 1.0 - target.Weapon.WeaponEvasion / 100.0;
            }
            if (attacker.HasStatus(StatusEffect.Blind))
                hit *= 0.5;
            return (int)Math.Floor(hit * 100.0 + 1e-9);
        }

        /// <summary>FFT's Parry (Weapon Guard) is passive: no Morale roll, but it needs a weapon and a unit able to react.</summary>
        public static bool Parries(Unit target) =>
            target.Weapon != null && target.Reaction != null && target.Reaction.Reaction == ReactionEffect.Parry && target.CanReact;

        /// <summary>A critical hit raises XA by a random 0 to XA − 1 before multiplying, so up to nearly double damage.</summary>
        public static int CriticalDamage(AttackFactors factors, Random rng) =>
            (factors.Xa + (factors.Xa > 1 ? rng.Next(0, factors.Xa) : 0)) * factors.Multiplier;

        /// <summary>
        /// FFT's Faith factor for magic, with Resonance: caster Resonance/100 × target Resonance/100, then ×⅔ against
        /// Shell. Multiplies magic damage and healing once magic exists. High Resonance hits harder and is hit harder;
        /// near zero is nearly immune but a poor caster.
        /// </summary>
        public static double MagicFactor(Unit caster, Unit target)
        {
            double factor = caster.Resonance / 100.0 * (target.Resonance / 100.0);
            if (target.HasStatus(StatusEffect.Shell))
                factor *= 2.0 / 3.0;
            return factor;
        }
    }
}
