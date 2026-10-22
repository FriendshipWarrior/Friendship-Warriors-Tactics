using FriendshipWarriors.Grid;
using UnityEditor;
using UnityEngine;

namespace FriendshipWarriors.Editor.Maps
{
    /// <summary>Generates hand-designed maps for exercising movement rules until a real map editor exists.</summary>
    public static class TestMapFactory
    {
        public const string HillsidePath = "Assets/_Project/Data/Maps/TestMap_Hillside.asset";

        [MenuItem("Friendship Warriors/Maps/Create Hillside Test Map")]
        public static void CreateHillsideAsset()
        {
            var map = AssetDatabase.LoadAssetAtPath<BattleMapData>(HillsidePath);
            if (map == null)
            {
                map = ScriptableObject.CreateInstance<BattleMapData>();
                AssetDatabase.CreateAsset(map, HillsidePath);
            }

            map.CopyFrom(BuildHillside());
            EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssets();
            Selection.activeObject = map;
        }

        /// <summary>
        /// 12x12 map with a stepped hill, a pond with a deep center, a wall with one gap, and a cliff-edged plateau
        /// reachable only by a ramp — enough to see Jump limits and blocked tiles at work.
        /// </summary>
        public static BattleGrid BuildHillside()
        {
            var grid = new BattleGrid(12, 12);
            var hillPeak = new GridCoord(3, 3);

            foreach (var c in grid.AllCoords())
            {
                int height = Mathf.Max(1, 5 - c.ManhattanDistance(hillPeak));
                var terrain = height > 1 ? TerrainType.Dirt : TerrainType.Grass;

                bool inPond = c.X >= 1 && c.X <= 3 && c.Y >= 7 && c.Y <= 9;
                bool pondShore = c.X >= 0 && c.X <= 4 && c.Y >= 6 && c.Y <= 10 && !inPond;
                if (inPond)
                {
                    height = 0;
                    terrain = c == new GridCoord(2, 8) ? TerrainType.DeepWater : TerrainType.ShallowWater;
                }
                else if (pondShore)
                {
                    terrain = TerrainType.Sand;
                }

                // Wall along x = 6 with a single gap at y = 4.
                if (c.X == 6 && c.Y <= 9 && c.Y != 4)
                {
                    height = 5;
                    terrain = TerrainType.Stone;
                }

                // Plateau in the north-east corner, 5 steps above the grass around it.
                if (c.X >= 8 && c.Y >= 8)
                {
                    height = 6;
                    terrain = TerrainType.Stone;
                }

                // Ramp up the plateau's south face: one step per tile.
                if (c.X >= 9 && c.X <= 10 && c.Y >= 3 && c.Y <= 7)
                {
                    height = c.Y - 2;
                    terrain = TerrainType.Dirt;
                }

                grid[c] = new Tile(height, terrain);
            }

            return grid;
        }
    }
}
