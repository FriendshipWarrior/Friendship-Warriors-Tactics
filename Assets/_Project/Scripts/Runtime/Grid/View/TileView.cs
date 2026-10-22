using UnityEngine;

namespace FriendshipWarriors.Grid.View
{
    /// <summary>Marks a tile's collider so pointer raycasts can be mapped back to grid coordinates.</summary>
    public sealed class TileView : MonoBehaviour
    {
        public GridCoord Coord { get; private set; }
        public Renderer Cap { get; private set; }
        public Color BaseColor { get; private set; }

        internal void Initialize(GridCoord coord, Renderer cap, Color baseColor)
        {
            Coord = coord;
            Cap = cap;
            BaseColor = baseColor;
        }
    }
}
