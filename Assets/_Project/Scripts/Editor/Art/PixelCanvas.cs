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

        /// <summary>Filled triangle; pixels whose centres fall inside (edges included).</summary>
        public void Triangle(Vector2 a, Vector2 b, Vector2 c, Color32 color)
        {
            float Cross(Vector2 o, Vector2 p, Vector2 q) => (p.x - o.x) * (q.y - o.y) - (p.y - o.y) * (q.x - o.x);
            int x0 = (int)Math.Floor(Math.Min(a.x, Math.Min(b.x, c.x))), x1 = (int)Math.Ceiling(Math.Max(a.x, Math.Max(b.x, c.x)));
            int y0 = (int)Math.Floor(Math.Min(a.y, Math.Min(b.y, c.y))), y1 = (int)Math.Ceiling(Math.Max(a.y, Math.Max(b.y, c.y)));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d0 = Cross(a, b, p), d1 = Cross(b, c, p), d2 = Cross(c, a, p);
                bool negative = d0 < 0 || d1 < 0 || d2 < 0, positive = d0 > 0 || d1 > 0 || d2 > 0;
                if (!(negative && positive))
                    Set(x, y, color);
            }
        }

        /// <summary>A line <paramref name="width"/> pixels thick, centred on the points (limbs, sleeves).</summary>
        public void Thick(Vector2Int a, Vector2Int b, Color32 color, int width)
        {
            int o = (width - 1) / 2;
            Line(a.x - o, a.y - o, b.x - o, b.y - o, color, width);
        }

        /// <summary>
        /// Volume shading for the region painted <c>ramp[Mid]</c>, lit from the upper left: each pixel's position across
        /// the region (mostly left to right, partly top to bottom) picks light, mid or shadow, and the brightest corner of a
        /// big enough region gets the highlight. Thin shapes come out as lit cylinders, round ones as lit balls.
        /// </summary>
        public void ShadeVolume(Color32[] ramp, bool highlight = true)
        {
            var mid = ramp[2];
            var copy = (Color32[])Pixels.Clone();
            bool Is(int x, int y) => Inside(x, y) && Same(copy[y * Width + x], mid);
            int Run(int x, int y, int dx, int dy)
            {
                int n = 0;
                while (Is(x, y)) { n++; x += dx; y += dy; }
                return n;
            }

            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (!Is(x, y))
                    continue;
                int left = Run(x, y, -1, 0), right = Run(x, y, 1, 0), up = Run(x, y, 0, -1), down = Run(x, y, 0, 1);
                int w = left + right - 1, h = up + down - 1;
                float tx = w > 1 ? (left - 1f) / (w - 1f) : 0.5f;
                float ty = h > 1 ? (up - 1f) / (h - 1f) : 0.5f;
                float t = 0.62f * tx + 0.38f * ty;
                int shade = t >= 0.66f ? 3 : t <= 0.24f ? 1 : 2;
                if (highlight && shade == 1 && t <= 0.1f && w >= 5 && h >= 5)
                    shade = 0;
                Pixels[y * Width + x] = ramp[shade];
            }
        }

        /// <summary>
        /// Adds a 1 px outline on the transparent pixels around everything drawn, coloured by
        /// <paramref name="outlineOf"/> from the neighbouring pixel (a coloured outline: each material's darkest shade).
        /// </summary>
        public void Outline(Func<Color32, Color32> outlineOf)
        {
            var copy = (Color32[])Pixels.Clone();
            bool Solid(int x, int y) => Inside(x, y) && copy[y * Width + x].a > 0;
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (Solid(x, y))
                    continue;
                // Prefer the pixel above or to the left, so the line hugs the shape it was drawn around.
                if (Solid(x, y + 1)) Pixels[y * Width + x] = outlineOf(copy[(y + 1) * Width + x]);
                else if (Solid(x - 1, y)) Pixels[y * Width + x] = outlineOf(copy[y * Width + x - 1]);
                else if (Solid(x + 1, y)) Pixels[y * Width + x] = outlineOf(copy[y * Width + x + 1]);
                else if (Solid(x, y - 1)) Pixels[y * Width + x] = outlineOf(copy[(y - 1) * Width + x]);
            }
        }

        /// <summary>
        /// Copies <paramref name="shape"/> on top; with <paramref name="separator"/> set, also draws that colour on this
        /// canvas's opaque pixels right around the shape, so overlapping pieces of one material stay readable (an arm
        /// in front of the torso).
        /// </summary>
        public void Layer(PixelCanvas shape, Color32? separator = null)
        {
            if (separator is { } line)
            {
                for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    if (shape.Opaque(x, y) || !Opaque(x, y))
                        continue;
                    if (shape.Opaque(x - 1, y) || shape.Opaque(x + 1, y) || shape.Opaque(x, y - 1) || shape.Opaque(x, y + 1))
                        Set(x, y, line);
                }
            }
            Blit(shape, 0, 0);
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
