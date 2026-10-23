using System;
using FriendshipWarriors.Battle.Combat;
using FriendshipWarriors.Units;

namespace FriendshipWarriors.Battle.Magic
{
    /// <summary>
    /// FFT's magick math with Resonance in place of Faith. Damage and healing: M.Atk × Q × (caster Resonance / 100) ×
    /// (target Resonance / 100), damage also ×⅔ against Shell. Status, revive and cure spells: hit rate (M.Atk + power)
    /// × the same two Resonance factors, in percent. Magic ignores physical evasion. Verified for damage/heal formulas
    /// (Final Fantasy Wiki); the per-spell powers are listed in CLAUDE.md with what's verified.
    /// </summary>
    public static class MagicFormulas
    {
        public static int Damage(Unit caster, Unit target, int power) =>
            (int)Math.Floor(caster.Stats.MagicAttack * power * CombatFormulas.MagicFactor(caster, target) + 1e-9);

        /// <summary>Healing uses both Resonances like damage but isn't reduced by Shell.</summary>
        public static int Heal(Unit caster, Unit target, int power) =>
            (int)Math.Floor(caster.Stats.MagicAttack * power * Resonances(caster, target) + 1e-9);

        public static int HitChance(Unit caster, Unit target, int power) =>
            Math.Clamp((int)Math.Floor((caster.Stats.MagicAttack + power) * Resonances(caster, target) + 1e-9), 0, 100);

        private static double Resonances(Unit caster, Unit target) => caster.Resonance / 100.0 * (target.Resonance / 100.0);
    }
}
