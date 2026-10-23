using FriendshipWarriors.Grid;
using UnityEngine;

namespace FriendshipWarriors.Units.View.Walking
{
    /// <summary>
    /// Where a unit's ground shadow sits and how it looks: on the battlefield surface straight below the unit (grid-local
    /// space), so it stays on the ground while the unit hops or leaps, shrinking and fading a little with height. Over
    /// a void chasm or off the map there's no ground to fall on, so the shadow hides. Pure.
    /// </summary>
    public static class GroundShadow
    {
        /// <summary>Smallest size the shadow shrinks to, as a fraction of its size on the ground.</summary>
        public const float MinScale = 0.6f;

        /// <summary>Faintest the shadow gets, as a fraction of its opacity on the ground.</summary>
        public const float MinOpacity = 0.45f;

        /// <summary>Height above the ground (world units; 1h = 0.5) at which the shadow is smallest and faintest.</summary>
        public const float FullFadeHeight = 1.5f;

        /// <summary>
        /// The height of the surface under the horizontal point <paramref name="local"/>, following slopes (corner
        /// heights blended across the tile). False over void tiles and outside the map.
        /// </summary>
        public static bool TrySurfaceHeight(BattleGrid grid, Vector3 local, out float height)
        {
            height = 0f;
            var coord = GridMetrics.WorldToCoord(local);
            if (!grid.TryGetTile(coord, out var tile) || tile.Terrain == TerrainType.Void)
                return false;

            // Position within the tile, 0–1 from its south-west corner.
            float u = Mathf.Clamp01(local.x / GridMetrics.TileSize - coord.X + 0.5f);
            float v = Mathf.Clamp01(local.z / GridMetrics.TileSize - coord.Y + 0.5f);
            var c = TileShape.Corners(tile);
            float south = Mathf.Lerp(c.SouthWest, c.SouthEast, u);
            float north = Mathf.Lerp(c.NorthWest, c.NorthEast, u);
            height = Mathf.Lerp(south, north, v) * GridMetrics.StepHeight;
            return true;
        }

        /// <summary>Shadow size at <paramref name="heightAbove"/> world units over the ground (1 on the ground).</summary>
        public static float Scale(float heightAbove) => Mathf.Lerp(1f, MinScale, Fade(heightAbove));

        /// <summary>Shadow opacity at <paramref name="heightAbove"/> world units over the ground (1 on the ground).</summary>
        public static float Opacity(float heightAbove) => Mathf.Lerp(1f, MinOpacity, Fade(heightAbove));

        private static float Fade(float heightAbove) => Mathf.Clamp01(heightAbove / FullFadeHeight);
    }
}
