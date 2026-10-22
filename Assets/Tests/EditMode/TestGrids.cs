using FriendshipWarriors.Grid;

namespace FriendshipWarriors.Tests
{
    internal static class TestGrids
    {
        /// <param name="heightInH">Height in whole h (FFT units), converted to the half-h steps tiles store.</param>
        public static BattleGrid Flat(int width, int depth, int heightInH = 0)
        {
            var grid = new BattleGrid(width, depth);
            foreach (var c in grid.AllCoords())
                grid[c] = new Tile(heightInH * GridMetrics.StepsPerH, TerrainType.Grass);
            return grid;
        }

        /// <summary>
        /// Builds a grid from rows of characters, where rows[0] is y = 0 and each character is one tile:
        /// a digit is a grass tile that many h high, '~' is deep water at height 0, '.' is void and '#' is a
        /// blocked tile (as if a tree stood there) at height 0.
        /// </summary>
        public static BattleGrid FromRows(params string[] rows)
        {
            var grid = new BattleGrid(rows[0].Length, rows.Length);
            foreach (var c in grid.AllCoords())
            {
                char symbol = rows[c.Y][c.X];
                grid[c] = symbol switch
                {
                    '~' => new Tile(0, TerrainType.Water, depth: 2),
                    '.' => new Tile(0, TerrainType.Void),
                    '#' => new Tile(0, TerrainType.Grass, blocked: true),
                    _ => new Tile((symbol - '0') * GridMetrics.StepsPerH, TerrainType.Grass),
                };
            }
            return grid;
        }
    }
}
