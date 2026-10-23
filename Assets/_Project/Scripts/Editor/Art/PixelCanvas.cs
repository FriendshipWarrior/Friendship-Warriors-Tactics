using System;
using UnityEngine;

namespace FriendshipWarriors.Editor.Art
{
    /// <summary>A small pixel buffer with top-left origin (as image editors show it) and pixel-art drawing helpers.</summary>
    internal sealed class PixelCanvas
    {
        public readonly int Width;
        public readonly int Height;
        public readonly Color32[] Pixels;

        public PixelCanvas(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new Color32[width * height];
        }

        public bool Inside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public bool Opaque(int x, int y) => Inside(x, y) && Pixels[y * Width + x].a > 0;
        public Color32 Get(int x, int y) => Inside(x, y) ? Pixels[y * Width + x] : default;

        public void Set(int x, int y, Color32 c)
        {
            if (Inside(x, y))
                Pixels[y * Width + x] = c;
        }

        public void Clear(int x, int y) => Set(x, y, default);

        /// <summary>Filled rectangle, corners inclusive, in any order.</summary>
        public void Fill(int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = Math.Min(y0, y1); y <= Math.Max(y0, y1); y++)
            for (int x = Math.Min(x0, x1); x <= Math.Max(x0, x1); x++)
                Set(x, y, c);
        }

        /// <summary>Filled ellipse; pixels whose centres fall inside.</summary>
        public void Ellipse(float cx, float cy, float rx, float ry, Color32 c, Func<int, int, bool> where = null)
        {
            for (int y = (int)Math.Floor(cy - ry - 1); y <= (int)Math.Ceiling(cy + ry + 1); y++)
            for (int x = (int)Math.Floor(cx - rx - 1); x <= (int)Math.Ceiling(cx + rx + 1); x++)
            {
                float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                if (dx * dx + dy * dy <= 1f && (where == null || where(x, y)))
                    Set(x, y, c);
            }
        }

        /// <summary>Line with a square brush of <paramref name="thickness"/> pixels.</summary>
        public void Line(int x0, int y0, int x1, int y1, Color32 c, int thickness = 1)
        {
            int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                for (int ty = 0; ty < thickness; ty++)
                for (int tx = 0; tx < thickness; tx++)
                    Set(x0 + tx, y0 + ty, c);
                if (x0 == x1 && y0 == y1)
                    break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        public void Line(Vector2Int a, Vector2Int b, Color32 c, int thickness = 1) => Line(a.x, a.y, b.x, b.y, c, thickness);

        /// <summary>
        /// Three-tone shading for every pixel currently painted <paramref name="mid"/>: pixels on the bottom/right
        /// edge of that region turn <paramref name="dark"/>, pixels on the top/left edge turn <paramref name="light"/>.
        /// Light comes from the upper left, as is usual for pixel art.
        /// </summary>
        public void Shade(Color32 mid, Color32 light, Color32 dark, bool lightEdges = true)
        {
            var copy = (Color32[])Pixels.Clone();
            bool Is(int x, int y) => Inside(x, y) && Same(copy[y * Width + x], mid);
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (!Is(x, y))
                    continue;
                if (!Is(x + 1, y) || !Is(x, y + 1))
                    Pixels[y * Width + x] = dark;
                else if (lightEdges && (!Is(x - 1, y) || !Is(x, y - 1)))
                    Pixels[y * Width + x] = light;
            }
        }

        /// <summary>Adds a 1 px outline on the transparent pixels around everything drawn.</summary>
        public void Outline(Color32 outline)
        {
            var copy = (Color32[])Pixels.Clone();
            bool Solid(int x, int y) => Inside(x, y) && copy[y * Width + x].a > 0;
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (Solid(x, y))
                    continue;
                if (Solid(x - 1, y) || Solid(x + 1, y) || Solid(x, y - 1) || Solid(x, y + 1))
                    Pixels[y * Width + x] = outline;
            }
        }

        /// <summary>Copies the non-transparent pixels of <paramref name="source"/> to (ox, oy).</summary>
        public void Blit(PixelCanvas source, int ox, int oy)
        {
            for (int y = 0; y < source.Height; y++)
            for (int x = 0; x < source.Width; x++)
            {
                var p = source.Pixels[y * source.Width + x];
                if (p.a > 0)
                    Set(ox + x, oy + y, p);
            }
        }

        /// <summary>
        /// The knocked-out version of a standing frame: turned 90° counter-clockwise (head to the left, lying on its
        /// back) and dropped to the ground row. The same fixed transform for every part keeps layers aligned.
        /// </summary>
        public PixelCanvas LyingDown()
        {
            var result = new PixelCanvas(Width, Height);
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                var p = Pixels[y * Width + x];
                if (p.a == 0)
                    continue;
                result.Set(y - 3, (Height - 1) - x + 9, p);
            }
            return result;
        }

        public static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        /// <summary>Unity texture order (bottom-left origin) for SetPixels32.</summary>
        public Color32[] ToTextureOrder()
        {
            var result = new Color32[Pixels.Length];
            for (int y = 0; y < Height; y++)
                Array.Copy(Pixels, y * Width, result, (Height - 1 - y) * Width, Width);
            return result;
        }
    }
}
