using System;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Units;

namespace FriendshipWarriors.Battle.Abilities
{
    /// <summary>
    /// FFT's knockback: a hit can push the target one tile directly away from the attacker. The chance is FFT's
    /// attacker Brave / (attacker Brave + target Brave), with Morale as Brave (50% when both are equal).
    /// The push fails into a wall (higher ground), a gap, a blocked or occupied tile, or off the map. Falling onto lower
    /// ground is allowed without fall damage (FFT's fall damage is not modelled yet).
    /// </summary>
    public static class Knockback
    {
        public static double Chance(Unit attacker, Unit target)
        {
            int total = attacker.Morale + target.Morale;
            return total == 0 ? 0.5 : attacker.Morale / (double)total;
        }

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

        public static bool Roll(Random rng, Unit attacker, Unit target) => rng.NextDouble() < Chance(attacker, target);
    }
}
