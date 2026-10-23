using System;
using System.Collections.Generic;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;

namespace FriendshipWarriors.Battle.Magic
{
    /// <summary>
    /// Starting a cast: the MP is paid up front (and lost if the spell is interrupted — user decision), then an instant
    /// spell resolves at once and a charged one goes on the clock.
    /// </summary>
    public static class SpellCasting
    {
        /// <summary>
        /// Pays the spell's MP and either resolves it now (returns the result, <paramref name="charge"/> null) or starts
        /// its charge (returns null), following <paramref name="lockOn"/> if given. Throws if the caster can't afford it.
        /// </summary>
        public static SpellResult Begin(Unit caster, AbilityDefinition spell, GridCoord tile, TurnOrder order, BattleGrid grid,
            IReadOnlyList<Unit> units, Random rng, out ChargingAction charge, Unit lockOn = null)
        {
            if (!caster.TrySpendMp(spell.MpCost))
                throw new InvalidOperationException($"{caster.Name} can't afford {spell.DisplayName}.");
            if (spell.ChargeSpeed <= 0)
            {
                charge = null;
                return SpellResolver.Resolve(caster, spell, tile, grid, units, rng);
            }
            charge = order.BeginCharge(caster, spell, tile, lockOn);
            return null;
        }
    }
}
