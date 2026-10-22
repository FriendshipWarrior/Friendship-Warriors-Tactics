using UnityEngine;

namespace FriendshipWarriors.Grid
{
    /// <summary>Units and conversions between grid space and world space.</summary>
    public static class GridMetrics
    {
        public const float TileSize = 1f;

        /// <summary>Tile heights are stored in half-h steps, so FFT's half heights (2.5h) are exact integers.</summary>
        public const int StepsPerH = 2;

        /// <summary>World height of one half-h step; 1h is half a tile tall.</summary>
        public const float StepHeight = 0.25f;

        /// <summary>World position of the point a unit stands on: the middle of the tile's top surface.</summary>
        public static Vector3 SurfaceCenter(GridCoord coord, in Tile tile) =>
            new(coord.X * TileSize, tile.StandingHeight * StepHeight, coord.Y * TileSize);

        /// <summary>World-space center of the grid at ground level, useful for framing the camera.</summary>
        public static Vector3 GridCenter(BattleGrid grid) =>
            new((grid.Width - 1) * TileSize * 0.5f, 0f, (grid.Depth - 1) * TileSize * 0.5f);

        /// <summary>Formats half-h steps the way FFT shows heights: 3 steps → "1.5h", 4 steps → "2h".</summary>
        public static string FormatHeight(int steps) =>
            steps % StepsPerH == 0
                ? $"{steps / StepsPerH}h"
                : $"{(steps / (float)StepsPerH).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}h";

        /// <summary>The tile whose column contains a world point (heights ignored).</summary>
        public static GridCoord WorldToCoord(Vector3 world) =>
            new(Mathf.RoundToInt(world.x / TileSize), Mathf.RoundToInt(world.z / TileSize));
    }
}
