using System;
using System.Collections.Generic;
using FriendshipWarriors.Grid;

namespace FriendshipWarriors.Battle.Magic
{
    public static class SpellArea
    {
        /// <summary>
        /// Tiles a spell centred on <paramref name="center"/> reaches: a diamond of the spell's radius (FFT effect 2 =
        /// radius 1, a cross of five), keeping only tiles within <paramref name="verticalH"/> h of the centre's height.
        /// </summary>
        public static List<GridCoord> Tiles(BattleGrid grid, GridCoord center, int radius, int verticalH)
        {
            var tiles = new List<GridCoord>();
            if (!grid.TryGetTile(center, out var middle))
                return tiles;
            int reach = verticalH * GridMetrics.StepsPerH;
            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (Math.Abs(dx) + Math.Abs(dy) > radius)
                    continue;
                var coord = center + new GridCoord(dx, dy);
                if (!grid.TryGetTile(coord, out var tile) || tile.Terrain == TerrainType.Void)
                    continue;
                if (Math.Abs(tile.StandingHeight - middle.StandingHeight) <= reach)
                    tiles.Add(coord);
            }
            return tiles;
        }
    }
}
