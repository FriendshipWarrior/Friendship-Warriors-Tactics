using FriendshipWarriors.Grid;

namespace FriendshipWarriors.Tests
{
    internal static class TestGrids
    {
        public static BattleGrid Flat(int width, int depth, int height = 0)
        {
            var grid = new BattleGrid(width, depth);
            foreach (var c in grid.AllCoords())
                grid[c] = new Tile(height, TerrainType.Grass);
            return grid;
        }

        /// <summary>
        /// Builds a grid from rows of characters, where rows[0] is y = 0 and each character is one tile:
        /// a digit is a grass tile of that height, '~' is deep water, '.' is void.
        /// </summary>
        public static BattleGrid FromRows(params string[] rows)
        {
            var grid = new BattleGrid(rows[0].Length, rows.Length);
            foreach (var c in grid.AllCoords())
            {
                char symbol = rows[c.Y][c.X];
                grid[c] = symbol switch
                {
                    '~' => new Tile(0, TerrainType.DeepWater),
                    '.' => new Tile(0, TerrainType.Void),
                    _ => new Tile(symbol - '0', TerrainType.Grass),
                };
            }
            return grid;
        }
    }
}
