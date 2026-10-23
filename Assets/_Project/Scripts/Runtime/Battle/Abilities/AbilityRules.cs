using System;
using System.Collections.Generic;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;
using FriendshipWarriors.Units.Status;

namespace FriendshipWarriors.Battle.Abilities
{
    /// <summary>Who and where an action ability can reach, and whether it can be used right now.</summary>
    public static class AbilityRules
    {
        /// <summary>Range of a thrown item (FFT's Throw Item); without it, items reach only the user and adjacent units.</summary>
        public const int ThrownItemRange = 4;

        /// <summary>Items are lobbed, so height doesn't stop them.</summary>
        public const int ItemVerticalReach = 99;

        public static (int min, int max, int verticalReach) Range(AbilityDefinition ability, Unit user)
        {
            if (ability.Targeting == AbilityTargeting.Self)
                return (0, 0, 0);
            if (ability.IsItem)
                return (0, user.ThrowsItems ? ThrownItemRange : 1, ItemVerticalReach);
            return (ability.MinRange, ability.MaxRange, ability.VerticalReach);
        }

        /// <summary>Tiles the ability can be aimed at, whether or not anyone stands there.</summary>
        public static List<GridCoord> TargetTiles(BattleGrid grid, AbilityDefinition ability, Unit user)
        {
            var (min, max, reach) = Range(ability, user);
            int reachSteps = reach * GridMetrics.StepsPerH;
            int fromHeight = grid[user.Coord].StandingHeight;
            var tiles = new List<GridCoord>();
            for (int dy = -max; dy <= max; dy++)
            for (int dx = -max; dx <= max; dx++)
            {
                int distance = Math.Abs(dx) + Math.Abs(dy);
                var coord = user.Coord + new GridCoord(dx, dy);
                if (distance < min || distance > max || !grid.TryGetTile(coord, out var tile) || tile.Terrain == TerrainType.Void)
                    continue;
                if (Math.Abs(tile.StandingHeight - fromHeight) <= reachSteps)
                    tiles.Add(coord);
            }
            return tiles;
        }

        public static bool IsValidTarget(AbilityDefinition ability, Unit user, Unit target) => ability.Targeting switch
        {
            AbilityTargeting.Self => target == user,
            AbilityTargeting.KnockedOut => target.IsKnockedOut && !target.IsGone,
            _ => !target.IsKnockedOut,
        };

        /// <summary>Units the ability could be used on right now.</summary>
        public static List<Unit> Targets(BattleGrid grid, AbilityDefinition ability, Unit user, IEnumerable<Unit> units)
        {
            var tiles = new HashSet<GridCoord>(TargetTiles(grid, ability, user));
            var targets = new List<Unit>();
            foreach (var unit in units)
            {
                if (tiles.Contains(unit.Coord) && IsValidTarget(ability, user, unit))
                    targets.Add(unit);
            }
            return targets;
        }

        /// <summary>Why the ability can't be used now, or null if it can.</summary>
        public static string Unavailable(BattleGrid grid, AbilityDefinition ability, Unit user, IEnumerable<Unit> units, Inventory inventory)
        {
            if (!user.HasLearned(ability))
                return "not learned";
            if (ability.IsSpell && user.HasStatus(StatusEffect.Silence))
                return "silenced";
            if (ability.IsItem && (inventory == null || inventory.Count(ability) <= 0))
                return "none left";
            if (ability.MpCost > user.CurrentMp)
                return "not enough MP";
            // Spells aim at a tile and may be cast on empty ground (FFT); only revives need someone to revive.
            if (ability.IsSpell && ability.Targeting != AbilityTargeting.KnockedOut)
                return null;
            if (Targets(grid, ability, user, units).Count == 0)
                return "no target";
            return null;
        }
    }
}
