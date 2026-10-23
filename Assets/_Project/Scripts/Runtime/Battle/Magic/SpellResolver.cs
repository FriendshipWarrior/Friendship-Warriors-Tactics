using System;
using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Battle.Abilities;
using FriendshipWarriors.Battle.Combat;
using FriendshipWarriors.Battle.Reactions;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;
using FriendshipWarriors.Units.Status;

namespace FriendshipWarriors.Battle.Magic
{
    /// <summary>
    /// Resolves a spell on everyone standing in its area when it lands (FFT magick: friend and foe alike). Damage and
    /// heals always land and ignore physical evasion; status, revive and cure spells roll their hit rate per target.
    /// Cures lift Panic by raising Morale to the panic line, since Panic follows Morale. The caster earns EXP once per spell if anything landed — level-based
    /// against the first enemy it hurt for offensive spells, support EXP otherwise — plus Prof (allies get ¼).
    /// </summary>
    public static class SpellResolver
    {
        public static SpellResult Resolve(Unit caster, AbilityDefinition spell, GridCoord tile, BattleGrid grid,
            IReadOnlyList<Unit> units, Random rng, bool grantRewards = true)
        {
            var area = SpellArea.Tiles(grid, tile, spell.AreaRadius, spell.AreaVertical);
            var areaSet = new HashSet<GridCoord>(area);
            var results = new List<SpellTargetResult>();
            foreach (var target in units.Where(u => areaSet.Contains(u.Coord) && AbilityRules.IsValidTarget(spell, caster, u)).ToList())
                results.Add(ApplyTo(caster, spell, target, rng));

            if (grantRewards && results.Any(r => r.Hit))
                Reward(caster, spell, results, units);
            return new SpellResult(caster, spell, tile, area, results);
        }

        private static SpellTargetResult ApplyTo(Unit caster, AbilityDefinition spell, Unit target, Random rng)
        {
            var result = new SpellTargetResult { Target = target, HitChance = 100 };
            switch (spell.Effect)
            {
                case AbilityEffect.Damage:
                {
                    bool wasDown = target.IsKnockedOut;
                    result.Hit = true;
                    result.Damage = target.TakeDamage(MagicFormulas.Damage(caster, target, spell.Power));
                    result.WasKnockedOut = !wasDown && target.IsKnockedOut;
                    break;
                }
                case AbilityEffect.RestoreHp:
                    result.Hit = true;
                    result.Healed = target.RestoreHp(MagicFormulas.Heal(caster, target, spell.Power));
                    break;
                case AbilityEffect.AddStatus:
                    result.HitChance = MagicFormulas.HitChance(caster, target, spell.Power);
                    if (rng.Next(100) >= result.HitChance)
                        break;
                    result.Hit = true;
                    foreach (var status in spell.Inflicts)
                    {
                        if (target.AddStatus(status))
                            result.Added.Add(status);
                    }
                    break;
                case AbilityEffect.Revive:
                    result.HitChance = MagicFormulas.HitChance(caster, target, spell.Power);
                    if (rng.Next(100) >= result.HitChance)
                        break;
                    int hp = Math.Max(1, target.Stats.MaxHp * spell.RevivePercent / 100);
                    result.Revived = target.Revive(hp);
                    result.Hit = result.Revived;
                    result.Healed = result.Revived ? target.CurrentHp : 0;
                    break;
                case AbilityEffect.CureStatus:
                    result.HitChance = MagicFormulas.HitChance(caster, target, spell.Power);
                    if (rng.Next(100) >= result.HitChance)
                        break;
                    result.Hit = true;
                    foreach (var status in spell.Cures)
                    {
                        bool removed;
                        if (status == StatusEffect.Panic)
                        {
                            if (!target.HasStatus(StatusEffect.Panic))
                                continue;
                            target.AdjustMorale(Unit.PanicThreshold - target.Morale);
                            removed = !target.HasStatus(StatusEffect.Panic);
                        }
                        else
                        {
                            removed = target.RemoveStatus(status);
                        }
                        if (removed)
                            result.Removed.Add(status);
                    }
                    break;
            }
            return result;
        }

        private static void Reward(Unit caster, AbilityDefinition spell, List<SpellTargetResult> results, IReadOnlyList<Unit> units)
        {
            var enemyHit = results.FirstOrDefault(r => r.Hit && r.Target.Team != caster.Team);
            caster.GainExperience(spell.IsOffensive && enemyHit != null
                ? BattleRewards.Experience(caster, enemyHit.Target)
                : BattleRewards.SupportExperience(caster));
            int prof = BattleRewards.Proficiency(caster);
            caster.GainProficiency(prof);
            foreach (var ally in units.Where(u => u != caster && u.Team == caster.Team && !u.IsKnockedOut))
                ally.GainProficiency(BattleRewards.SpilloverProficiency(prof));
        }

        /// <summary>
        /// FFT's Counter Magic: a unit hit by an enemy's offensive spell may cast the same spell straight back at the
        /// caster's tile, instantly, rolling against its Morale (FFT: Brave). It needs a working voice (not silenced) and
        /// pays the spell's MP. Counters don't trigger further counters or earn rewards. Paying MP and the
        /// counter being instant are our reading of FFT (flagged in CLAUDE.md).
        /// </summary>
        public static SpellResult TryCounterMagic(Unit reactor, SpellResult incoming, BattleGrid grid, IReadOnlyList<Unit> units, Random rng)
        {
            var reaction = reactor.Reaction;
            var spell = incoming.Spell;
            var caster = incoming.Caster;
            if (reaction == null || reaction.Reaction != ReactionEffect.CounterMagic || !reactor.CanReact)
                return null;
            if (!spell.IsOffensive || caster.Team == reactor.Team || caster.IsKnockedOut)
                return null;
            if (reactor.HasStatus(StatusEffect.Silence) || reactor.CurrentMp < spell.MpCost)
                return null;
            if (rng.NextDouble() >= ReactionResolver.TriggerChance(reactor))
                return null;
            reactor.TrySpendMp(spell.MpCost);
            return Resolve(reactor, spell, caster.Coord, grid, units, rng, grantRewards: false);
        }
    }
}
