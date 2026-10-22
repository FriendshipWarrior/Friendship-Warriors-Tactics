using System.Collections.Generic;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Props;
using UnityEditor;
using UnityEngine;

namespace FriendshipWarriors.Editor.Maps
{
    /// <summary>Generates hand-designed maps for exercising battlefield rules until the map editor exists.</summary>
    public static class TestMapFactory
    {
        public const string HillsidePath = "Assets/_Project/Data/Maps/TestMap_Hillside.asset";

        private const int H = GridMetrics.StepsPerH;

        public static BattleMapData CreateHillsideAsset(PropDefinition tree, PropDefinition rock)
        {
            var map = AssetDatabase.LoadAssetAtPath<BattleMapData>(HillsidePath);
            if (map == null)
            {
                map = ScriptableObject.CreateInstance<BattleMapData>();
                AssetDatabase.CreateAsset(map, HillsidePath);
            }

            map.DisplayName = "Hillside (Test)";
            map.CopyTilesFrom(BuildHillside());
            map.SetDeployment(Rect(0, 0, 2, 1), Rect(9, 9, 11, 11));
            map.SetProps(new[]
            {
                new PropPlacement(new GridCoord(0, 11), tree),
                new PropPlacement(new GridCoord(5, 1), tree),
                new PropPlacement(new GridCoord(10, 1), tree),
                new PropPlacement(new GridCoord(7, 10), rock),
                new PropPlacement(new GridCoord(11, 4), rock),
            });
            EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssets();
            return map;
        }

        /// <summary>
        /// 12x12 map covering each battlefield feature: a stepped hill, a pond (depth 1, deep center), a wall with
        /// one gap, a one-tile chasm to leap, and a cliff-edged plateau reachable only by a sloped ramp.
        /// </summary>
        public static BattleGrid BuildHillside()
        {
            var grid = new BattleGrid(12, 12);
            var hillPeak = new GridCoord(3, 3);

            foreach (var c in grid.AllCoords())
            {
                int heightInH = Mathf.Max(1, 4 - c.ManhattanDistance(hillPeak));
                var tile = new Tile(heightInH * H, heightInH > 1 ? TerrainType.Dirt : TerrainType.Grass);

                bool inPond = c.X >= 1 && c.X <= 3 && c.Y >= 7 && c.Y <= 9;
                bool onShore = c.X <= 4 && c.Y >= 6 && c.Y <= 10 && !inPond;
                if (inPond)
                    tile = new Tile(1, TerrainType.Water, depth: c == new GridCoord(2, 8) ? 2 : 1);
                else if (onShore)
                    tile = new Tile(tile.Height, TerrainType.Sand);

                // Wall along x = 6 with a single gap at y = 4.
                if (c.X == 6 && c.Y <= 9 && c.Y != 4)
                    tile = new Tile(5 * H, TerrainType.Stone);

                // One-tile chasm: Jump 2 or more can leap it.
                if (c.X == 8 && c.Y <= 2)
                    tile = new Tile(0, TerrainType.Void);

                // Plateau in the north-east corner, 5h above the grass around it.
                if (c.X >= 8 && c.Y >= 8)
                    tile = new Tile(6 * H, TerrainType.Stone);

                // Ramp up the plateau's south face: a continuous incline rising 1h per tile.
                if (c.X >= 9 && c.X <= 10 && c.Y >= 3 && c.Y <= 7)
                    tile = new Tile((1 + (c.Y - 3)) * H, TerrainType.Dirt, slope: SlopeType.RiseNorth, slopeHeight: H);

                grid[c] = tile;
            }

            return grid;
        }

        private static IEnumerable<GridCoord> Rect(int minX, int minY, int maxX, int maxY)
        {
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
                yield return new GridCoord(x, y);
        }
    }
}
