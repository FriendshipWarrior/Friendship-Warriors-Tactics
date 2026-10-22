using System;
using FriendshipWarriors.Units;

namespace FriendshipWarriors.Battle.Combat
{
    /// <summary>
    /// Resolves one physical Attack: evasion first (FFT), then the defender's timed block, then the critical roll
    /// (FFT's 5% plus the attacker's timed bonus). Pure: it reads the units and returns a result without applying it.
    /// </summary>
    public static class AttackResolver
    {
        /// <param name="offenseAccuracy">Attacker's timed-press accuracy, 0–1 (0 if nobody pressed).</param>
        /// <param name="defenseAccuracy">Defender's timed-press accuracy, 0–1 (0 if nobody pressed).</param>
        public static AttackResult Resolve(Unit attacker, Unit target, Random rng, TimedPressRules rules,
            float offenseAccuracy = 0f, float defenseAccuracy = 0f)
        {
            var angle = FacingRules.AngleOfAttack(target.Coord, target.Facing, attacker.Coord);
            int hitChance = CombatFormulas.HitChance(attacker, target);

            if (rng.Next(100) >= hitChance)
                return new AttackResult(angle, hitChance, hit: false, blocked: false, critical: false, damage: 0);

            if (defenseAccuracy > 0f && rng.NextDouble() < rules.BlockChance(defenseAccuracy))
                return new AttackResult(angle, hitChance, hit: true, blocked: true, critical: false, damage: 0);

            var factors = CombatFormulas.AttackFactors(attacker);
            bool critical = rng.NextDouble() < rules.CriticalChance(offenseAccuracy);
            int damage = critical ? CombatFormulas.CriticalDamage(factors, rng) : factors.Damage;
            return new AttackResult(angle, hitChance, hit: true, blocked: false, critical, damage);
        }
    }
}
