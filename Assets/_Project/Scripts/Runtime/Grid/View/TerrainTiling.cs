using System.Collections.Generic;
using UnityEngine;

namespace FriendshipWarriors.Grid.View
{
    /// <summary>
    /// Pure maths for texturing the battlefield from a <see cref="TerrainAtlas"/>: where a cell sits in the atlas,
    /// which variant a tile gets (stable per coordinate, so a map always looks the same), and how a side wall is cut
    /// into bands one cell tall that line up with world height, so neighbouring columns' textures continue.
    /// </summary>
    public static class TerrainTiling
    {
        /// <summary>World height one side cell covers: a 32 px cell at 32 px per unit, i.e. 2h.</summary>
        public const float SideCellHeight = GridMetrics.TileSize;

        /// <summary>
        /// The UV rectangle of atlas cell <paramref name="cell"/> (counted left to right, top to bottom), excluding its
        /// gutter. Cells are <paramref name="cellPixels"/> square with <paramref name="gutter"/> extruded pixels around
        /// them, so point sampling at a cell's edge never reads its neighbour.
        /// </summary>
        public static Rect CellUv(int cell, int columns, int cellPixels, int gutter, int textureWidth, int textureHeight)
        {
            int stride = cellPixels + gutter * 2;
            int column = cell % columns;
            int row = cell / columns;
            float x = column * stride + gutter;
            // Atlas rows count down from the image top; UVs count up from the bottom.
            float yTop = row * stride + gutter;
            float y = textureHeight - yTop - cellPixels;
            return new Rect(x / textureWidth, y / textureHeight, (float)cellPixels / textureWidth, (float)cellPixels / textureHeight);
        }

        /// <summary>
        /// A variant index in [0, <paramref name="count"/>) that depends only on the coordinate and a salt (so tops and
        /// each wall pick independently). Deterministic across runs and platforms (no <c>GetHashCode</c>).
        /// </summary>
        public static int PickVariant(int x, int y, int salt, int count)
        {
            if (count <= 1)
                return 0;
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)x) * 16777619u;
                h = (h ^ (uint)y) * 16777619u;
                h = (h ^ (uint)salt) * 16777619u;
                // Final avalanche so neighbouring coordinates don't pick neighbouring variants.
                h ^= h >> 15;
                h *= 0x2c1b3c6du;
                h ^= h >> 12;
                return (int)(h % (uint)count);
            }
        }

        /// <summary>A point on a side wall: <see cref="S"/> runs along the wall's bottom edge (0 at its left), <see cref="Y"/> is world height.</summary>
        public readonly struct WallPoint
        {
            public readonly float S;
            public readonly float Y;

            public WallPoint(float s, float y)
            {
                S = s;
                Y = y;
            }

            public override string ToString() => $"({S:0.###}, {Y:0.###})";
        }

        /// <summary>One band of a wall: the part of it between world heights <c>Band</c> and <c>Band + 1</c> cells.</summary>
        public sealed class WallBand
        {
            public int Band;
            public readonly List<WallPoint> Points = new();
        }

        /// <summary>
        /// Cuts a wall (left edge from <paramref name="leftBottom"/> to <paramref name="leftTop"/>, right edge at
        /// <paramref name="width"/> from <paramref name="rightBottom"/> to <paramref name="rightTop"/>) into horizontal
        /// bands one <see cref="SideCellHeight"/> tall, aligned to world height 0. Each band is a convex polygon in the
        /// wall's order (left-top, right-top, right-bottom, left-bottom), so its winding matches the uncut wall.
        /// </summary>
        public static List<WallBand> SliceWall(float width, float leftTop, float rightTop, float leftBottom, float rightBottom)
        {
            var bands = new List<WallBand>();
            float top = Mathf.Max(leftTop, rightTop);
            float bottom = Mathf.Min(leftBottom, rightBottom);
            if (top - bottom < 0.0001f)
                return bands;

            var wall = new List<WallPoint>
            {
                new(0f, leftTop), new(width, rightTop), new(width, rightBottom), new(0f, leftBottom),
            };

            int first = Mathf.FloorToInt(bottom / SideCellHeight + 0.0001f);
            int last = Mathf.CeilToInt(top / SideCellHeight - 0.0001f) - 1;
            for (int k = first; k <= last; k++)
            {
                float low = k * SideCellHeight;
                float high = low + SideCellHeight;
                var clipped = Clip(Clip(wall, low, keepAbove: true), high, keepAbove: false);
                if (clipped.Count < 3 || Area(clipped) < 0.000001f)
                    continue;
                var band = new WallBand { Band = k };
                band.Points.AddRange(clipped);
                bands.Add(band);
            }
            return bands;
        }

        /// <summary>The band polygon's V inside its cell: 0 at the band's bottom, 1 at its top.</summary>
        public static float BandV(float y, int band) => y / SideCellHeight - band;

        /// <summary>Sutherland–Hodgman against one horizontal line.</summary>
        private static List<WallPoint> Clip(List<WallPoint> polygon, float line, bool keepAbove)
        {
            var result = new List<WallPoint>();
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                bool aIn = keepAbove ? a.Y >= line : a.Y <= line;
                bool bIn = keepAbove ? b.Y >= line : b.Y <= line;
                if (aIn)
                    result.Add(a);
                if (aIn != bIn)
                {
                    float t = (line - a.Y) / (b.Y - a.Y);
                    result.Add(new WallPoint(a.S + (b.S - a.S) * t, line));
                }
            }
            // Drop repeated points (where a corner lies exactly on the line).
            for (int i = result.Count - 1; i >= 0 && result.Count > 1; i--)
            {
                var p = result[i];
                var q = result[(i + 1) % result.Count];
                if (Mathf.Abs(p.S - q.S) < 0.00001f && Mathf.Abs(p.Y - q.Y) < 0.00001f)
                    result.RemoveAt(i);
            }
            return result;
        }

        private static float Area(List<WallPoint> polygon)
        {
            float sum = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                sum += a.S * b.Y - b.S * a.Y;
            }
            return Mathf.Abs(sum) * 0.5f;
        }
    }
}
