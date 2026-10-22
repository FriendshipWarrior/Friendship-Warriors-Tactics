using UnityEngine;

namespace FriendshipWarriors.Grid
{
    /// <summary>Conversions between grid space and world space.</summary>
    public static class GridMetrics
    {
        public const float TileSize = 1f;

        /// <summary>World height of one height step. Half a tile, like FFT's half-height increments.</summary>
        public const float StepHeight = 0.5f;

        /// <summary>World position of the center of a tile's top surface.</summary>
        public static Vector3 TileTopCenter(GridCoord coord, int height) =>
            new(coord.X * TileSize, height * StepHeight, coord.Y * TileSize);

        /// <summary>World-space center of the grid at ground level, useful for framing the camera.</summary>
        public static Vector3 GridCenter(BattleGrid grid) =>
            new((grid.Width - 1) * TileSize * 0.5f, 0f, (grid.Depth - 1) * TileSize * 0.5f);
    }
}
