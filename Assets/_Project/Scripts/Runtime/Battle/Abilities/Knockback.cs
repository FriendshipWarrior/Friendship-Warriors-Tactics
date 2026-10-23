using System;
using FriendshipWarriors.Battle.Combat;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Units;

namespace FriendshipWarriors.Battle.Abilities
{
    /// <summary>
    /// FFT's knockback: a hit can push the target one tile directly away from the attacker. The chance is
    /// attacker Brave / (attacker Brave + target Brave); with Brave standing in at 70 for everyone, that's 50%.
    /// The push fails into a wall (higher ground), a gap, a blocked or occupied tile, or off the map. Falling onto lower
    /// ground is allowed without fall damage (FFT's fall damage is not modelled yet).
    /// </summary>
    public static class Knockback
    {
        public const double Chance = CombatFormulas.BraveStandIn / (double)(CombatFormulas.BraveStandIn + CombatFormulas.BraveStandIn);

        /// <summary>Where the target would land, or null if the push is blocked.</summary>
        public static GridCoord? Destination(BattleGrid grid, GridCoord attacker, GridCoord target, Func<GridCoord, bool> occupied)
        {
            if (attacker == target)
                return null;
            var step = DirectionExtensions.Toward(attacker, target).ToStep();
            var landing = target + step;
            if (!grid.TryGetTile(landing, out var tile) || !tile.IsWalkable || occupied(landing))
                return null;
            if (tile.StandingHeight > grid[target].StandingHeight)
                return null;
            return landing;
        }

        public static bool Roll(Random rng) => rng.NextDouble() < Chance;
    }
}
