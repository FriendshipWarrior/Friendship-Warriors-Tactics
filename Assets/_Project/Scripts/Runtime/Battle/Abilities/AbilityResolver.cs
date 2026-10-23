using System;
using FriendshipWarriors.Battle.Combat;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;

namespace FriendshipWarriors.Battle.Abilities
{
    /// <summary>
    /// Resolves and applies an action ability on one target, following FFT's Squire Basic Skill and Chemist items.
    /// Damage abilities are physical, so evasion applies exactly as for Attack; items, buffs and revives always land.
    /// </summary>
    public static class AbilityResolver
    {
        /// <summary>HP a revive item restores: a random 1–20 (recalled FFT Phoenix Down value; unverified).</summary>
        public const int ReviveHpMin = 1, ReviveHpMax = 20;

        public static AbilityOutcome Resolve(AbilityDefinition ability, Unit user, Unit target, Random rng)
        {
            switch (ability.Effect)
            {
                case AbilityEffect.Damage:
                {
                    int hitChance = CombatFormulas.HitChance(user, target);
                    if (rng.Next(100) >= hitChance)
                        return new AbilityOutcome(false, hitChance);
                    var range = ability.RandomMultiplier;
                    int multiplier = rng.Next(Math.Min(range.x, range.y), Math.Max(range.x, range.y) + 1);
                    int damage = CombatFormulas.PhysicalDamage(user, target, user.Stats.PhysicalAttack * multiplier, ability.IsMelee);
                    return new AbilityOutcome(true, hitChance, damage: damage);
                }
                case AbilityEffect.BoostPhysicalAttack:
                    return new AbilityOutcome(true, 100, physicalAttackGained: ability.Amount);
                case AbilityEffect.RestoreHp:
                    return new AbilityOutcome(true, 100, hpRestored: ability.Amount);
                case AbilityEffect.RestoreMp:
                    return new AbilityOutcome(true, 100, mpRestored: ability.Amount);
                case AbilityEffect.RestoreAll:
                {
                    var stats = target.Stats;
                    return new AbilityOutcome(true, 100, hpRestored: stats.MaxHp, mpRestored: stats.MaxMp);
                }
                case AbilityEffect.Revive:
                    return new AbilityOutcome(true, 100, hpRestored: rng.Next(ReviveHpMin, ReviveHpMax + 1), revived: true);
                case AbilityEffect.CureStatus:
                    return new AbilityOutcome(true, 100, cures: ability.Cures);
                default:
                    return new AbilityOutcome(false, 0);
            }
        }

        /// <summary>Applies an outcome to the target and returns it with the amounts actually changed.</summary>
        public static AbilityOutcome Apply(AbilityOutcome outcome, Unit target)
        {
            if (!outcome.Hit)
                return outcome;
            if (outcome.Revived)
            {
                bool revived = target.Revive(outcome.HpRestored);
                return new AbilityOutcome(revived, outcome.HitChance, hpRestored: revived ? target.CurrentHp : 0, revived: revived);
            }

            int damage = outcome.Damage > 0 ? target.TakeDamage(outcome.Damage) : 0;
            int hp = target.RestoreHp(outcome.HpRestored);
            int mp = target.RestoreMp(outcome.MpRestored);
            if (outcome.PhysicalAttackGained > 0)
                target.AddPhysicalAttackBonus(outcome.PhysicalAttackGained);
            int cured = 0;
            if (outcome.Cures != null)
            {
                foreach (var status in outcome.Cures)
                {
                    if (target.RemoveStatus(status))
                        cured++;
                }
            }
            return new AbilityOutcome(true, outcome.HitChance, damage, hp, mp, outcome.PhysicalAttackGained, cures: outcome.Cures, curedCount: cured);
        }
    }
}
