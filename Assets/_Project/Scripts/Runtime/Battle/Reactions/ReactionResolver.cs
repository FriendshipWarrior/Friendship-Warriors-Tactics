using System;
using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Battle.Abilities;
using FriendshipWarriors.Battle.Combat;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;
using FriendshipWarriors.Units.Status;

namespace FriendshipWarriors.Battle.Reactions
{
    /// <summary>
    /// Fires the equipped reaction of a unit that just took damage from an action. As in FFT, triggered reactions
    /// roll against the reactor's Morale (FFT: Brave) in percent, and nothing reacts while knocked out, asleep, stopped
    /// or disabled (or, our choice, panicking). Parry is passive and lives in the hit-chance formula instead.
    /// Order in an attack: evasion → timed block → critical → damage → reaction. Reactions never chain.
    /// </summary>
    public static class ReactionResolver
    {
        public static double TriggerChance(Unit reactor) => reactor.Morale / 100.0;

        /// <param name="physical">The damage came from a physical attack or ability (counter strikes need this).</param>
        public static ReactionResult React(Unit reactor, Unit attacker, bool physical, int damageTaken, BattleGrid grid,
            IReadOnlyList<Unit> units, Inventory inventory, Random rng)
        {
            var reaction = reactor.Reaction;
            if (reaction == null || damageTaken <= 0 || !reactor.CanReact)
                return ReactionResult.None;

            switch (reaction.Reaction)
            {
                case ReactionEffect.CounterStrike:
                    return physical ? CounterStrike(reaction, reactor, attacker, grid, units, rng) : ReactionResult.None;

                case ReactionEffect.AutoHeal:
                {
                    var item = WeakestHpItem(inventory);
                    if (item == null || !Roll(reactor, rng))
                        return ReactionResult.None;
                    inventory.TryConsume(item);
                    int healed = reactor.RestoreHp(item.Amount);
                    return new ReactionResult($"{reactor.Name}: {reaction.DisplayName} - {item.DisplayName}, +{healed} HP");
                }

                case ReactionEffect.SpeedUp:
                    if (!Roll(reactor, rng))
                        return ReactionResult.None;
                    reactor.AddSpeedBonus(1);
                    return new ReactionResult($"{reactor.Name}: {reaction.DisplayName} - Speed +1");

                case ReactionEffect.GainRegen:
                    if (!Roll(reactor, rng))
                        return ReactionResult.None;
                    reactor.AddStatus(StatusEffect.Regen);
                    return new ReactionResult($"{reactor.Name}: {reaction.DisplayName} - Regen");

                default:
                    return ReactionResult.None;
            }
        }

        private static bool Roll(Unit reactor, Random rng) => rng.NextDouble() < TriggerChance(reactor);

        /// <summary>
        /// FFT's Counter Tackle: strike the attacker back with the reaction's own damage (Dash's P.Atk × 1–4 and its
        /// knockback), if the attacker is within reach. Physical, so the attacker's evasion still applies.
        /// </summary>
        private static ReactionResult CounterStrike(AbilityDefinition reaction, Unit reactor, Unit attacker, BattleGrid grid,
            IReadOnlyList<Unit> units, Random rng)
        {
            if (attacker.IsKnockedOut || !InReach(reaction, reactor, attacker, grid) || !Roll(reactor, rng))
                return ReactionResult.None;

            int hitChance = CombatFormulas.HitChance(reactor, attacker);
            if (rng.Next(100) >= hitChance)
                return new ReactionResult($"{reactor.Name}: {reaction.DisplayName} - miss");

            var range = reaction.RandomMultiplier;
            int multiplier = rng.Next(Math.Min(range.x, range.y), Math.Max(range.x, range.y) + 1);
            int damage = CombatFormulas.PhysicalDamage(reactor, attacker, reactor.Stats.PhysicalAttack * multiplier, melee: true);
            attacker.TakeDamage(damage);

            GridCoord? knockedTo = null;
            if (reaction.Knockback && !attacker.IsKnockedOut && Knockback.Roll(rng, reactor, attacker))
            {
                knockedTo = Knockback.Destination(grid, reactor.Coord, attacker.Coord,
                    c => units.Any(u => u != attacker && u.Coord == c));
                if (knockedTo is { } landing)
                    attacker.Coord = landing;
            }
            return new ReactionResult($"{reactor.Name}: {reaction.DisplayName} - {damage} damage" +
                                      (knockedTo != null ? ", knocked back" : "") +
                                      (attacker.IsKnockedOut ? $"  {attacker.Name} is down!" : ""), knockedTo);
        }

        private static bool InReach(AbilityDefinition reaction, Unit reactor, Unit attacker, BattleGrid grid)
        {
            int distance = reactor.Coord.ManhattanDistance(attacker.Coord);
            int max = Math.Max(1, reaction.MaxRange);
            if (distance < 1 || distance > max)
                return false;
            int reach = reaction.VerticalReach * GridMetrics.StepsPerH;
            return Math.Abs(grid[reactor.Coord].StandingHeight - grid[attacker.Coord].StandingHeight) <= reach;
        }

        /// <summary>FFT's Auto Potion uses the weakest potion available.</summary>
        public static AbilityDefinition WeakestHpItem(Inventory inventory) =>
            inventory?.Items
                .Where(i => i.Effect == AbilityEffect.RestoreHp && inventory.Count(i) > 0)
                .OrderBy(i => i.Amount)
                .ThenBy(i => i.DisplayName, StringComparer.Ordinal)
                .FirstOrDefault();
    }
}
