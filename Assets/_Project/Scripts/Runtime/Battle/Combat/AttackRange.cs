using System;
using System.Collections.Generic;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Units;

namespace FriendshipWarriors.Battle.Combat
{
    public static class AttackRange
    {
        /// <summary>Bare hands reach the four adjacent tiles within 3h, like a melee weapon.</summary>
        public const int UnarmedVerticalReach = 3;

        /// <summary>
        /// Tiles the unit's Attack can target: within the weapon's min–max range (Manhattan distance) and within its
        /// vertical reach. Not yet modelled (approximations to revisit): FFT bows gaining range from height, and line
        /// of fire for straight-shot weapons.
        /// </summary>
        public static List<GridCoord> Targets(BattleGrid grid, Unit attacker)
        {
            var weapon = attacker.Weapon;
            int min = weapon != null ? weapon.MinRange : 1;
            int max = weapon != null ? weapon.MaxRange : 1;
            int reachSteps = (weapon != null ? weapon.VerticalReach : UnarmedVerticalReach) * GridMetrics.StepsPerH;
            int fromHeight = grid[attacker.Coord].StandingHeight;

            var tiles = new List<GridCoord>();
            for (int dy = -max; dy <= max; dy++)
            for (int dx = -max; dx <= max; dx++)
            {
                int distance = Math.Abs(dx) + Math.Abs(dy);
                var coord = attacker.Coord + new GridCoord(dx, dy);
                if (distance < min || distance > max || !grid.TryGetTile(coord, out var tile) || tile.Terrain == TerrainType.Void)
                    continue;
                if (Math.Abs(tile.StandingHeight - fromHeight) <= reachSteps)
                    tiles.Add(coord);
            }
            return tiles;
        }
    }
}
