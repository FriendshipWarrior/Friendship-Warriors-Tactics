using System;
using UnityEngine;

namespace FriendshipWarriors.Grid.View
{
    /// <summary>
    /// Pixel-art textures for the battlefield, packed in one point-filtered atlas: per terrain type, a few top
    /// variants, side (cliff) variants that repeat down tall columns, and an optional lip drawn over the top of each
    /// wall (grass hanging over a cliff edge). Built by code from the art packs (<c>PixelTerrainBuilder</c>); a
    /// terrain type with no entry keeps its flat <see cref="TerrainPalette"/> colour.
    /// </summary>
    [CreateAssetMenu(menuName = "Friendship Warriors/Terrain Atlas", fileName = "TerrainAtlas")]
    public sealed class TerrainAtlas : ScriptableObject
    {
        [Serializable]
        public sealed class Tiles
        {
            [SerializeField] private TerrainType _terrain;
            [Tooltip("Atlas cells for the tile tops; each tile picks one from its coordinates.")]
            [SerializeField] private int[] _tops = Array.Empty<int>();
            [Tooltip("Also mirror top cells left-right, doubling the variants (for art without a direction).")]
            [SerializeField] private bool _mirrorTops = true;
            [Tooltip("Atlas cells for side walls, one cell per 2h of wall, aligned to world height.")]
            [SerializeField] private int[] _sides = Array.Empty<int>();
            [Tooltip("Cell drawn over the top of every wall (with transparency), or -1 for none.")]
            [SerializeField] private int _lip = -1;
            [Tooltip("How much of the lip cell, from its top, hangs down the wall, in pixels.")]
            [SerializeField, Range(0, 32)] private int _lipPixels;

            public Tiles(TerrainType terrain, int[] tops, bool mirrorTops, int[] sides, int lip, int lipPixels)
            {
                _terrain = terrain;
                _tops = tops;
                _mirrorTops = mirrorTops;
                _sides = sides;
                _lip = lip;
                _lipPixels = lipPixels;
            }

            public TerrainType Terrain => _terrain;
            public int[] Tops => _tops;
            public bool MirrorTops => _mirrorTops;
            public int[] Sides => _sides;
            public int Lip => _lip;
            public int LipPixels => _lipPixels;
            public bool HasLip => _lip >= 0 && _lipPixels > 0;

            /// <summary>How many top looks a tile can get (mirroring counts double).</summary>
            public int TopVariantCount => _tops.Length * (_mirrorTops ? 2 : 1);
        }

        [SerializeField] private Texture2D _texture;
        [SerializeField, Min(1)] private int _cellPixels = 32;
        [SerializeField, Min(0)] private int _gutter = 2;
        [SerializeField, Min(1)] private int _columns = 8;
        [SerializeField] private Tiles[] _tiles = Array.Empty<Tiles>();

        public Texture2D Texture => _texture;
        public int CellPixels => _cellPixels;
        public int Gutter => _gutter;
        public int Columns => _columns;

        /// <summary>The tiles for a terrain type, if the atlas has any tops for it.</summary>
        public bool TryGet(TerrainType terrain, out Tiles tiles)
        {
            foreach (var entry in _tiles)
            {
                if (entry != null && entry.Terrain == terrain && entry.Tops.Length > 0)
                {
                    tiles = entry;
                    return true;
                }
            }
            tiles = null;
            return false;
        }

        public Rect CellUv(int cell) =>
            _texture == null
                ? new Rect(0f, 0f, 1f, 1f)
                : TerrainTiling.CellUv(cell, _columns, _cellPixels, _gutter, _texture.width, _texture.height);

        /// <summary>Sets the whole atlas; used by art tooling.</summary>
        public void Set(Texture2D texture, int cellPixels, int gutter, int columns, Tiles[] tiles)
        {
            _texture = texture;
            _cellPixels = cellPixels;
            _gutter = gutter;
            _columns = columns;
            _tiles = tiles;
        }
    }
}
