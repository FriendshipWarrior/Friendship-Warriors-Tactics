using UnityEngine;

namespace FriendshipWarriors.Feedback
{
    /// <summary>
    /// Placeholder art for drops, drawn in code at the units' 32 px per unit: a steel rod with brass caps (our
    /// crystal), the soft glow behind it, and a small iron-banded chest. Swap for drawn sprites later.
    /// </summary>
    public static class DropSprites
    {
        private const float PixelsPerUnit = 32f;
        private static Sprite _rod, _glow, _chest;

        public static Sprite Rod => _rod != null ? _rod : _rod = Make(RodPixels(), 6, 24, new Vector2(0.5f, 0f));
        public static Sprite Glow => _glow != null ? _glow : _glow = Make(GlowPixels(), 28, 28, new Vector2(0.5f, 0.3f));
        public static Sprite Chest => _chest != null ? _chest : _chest = Make(ChestPixels(), 16, 13, new Vector2(0.5f, 0f));

        /// <summary>Rod pixels, bottom row first: steel shaded left to right, brass caps and a brass band in the middle.</summary>
        public static Color32[] RodPixels()
        {
            const int w = 6, h = 24;
            var steel = new[] { new Color32(70, 78, 92, 255), new Color32(150, 162, 178, 255), new Color32(225, 235, 245, 255),
                new Color32(190, 200, 214, 255), new Color32(120, 130, 146, 255), new Color32(60, 66, 80, 255) };
            var brass = new[] { new Color32(110, 76, 30, 255), new Color32(200, 150, 60, 255), new Color32(250, 214, 120, 255),
                new Color32(226, 180, 84, 255), new Color32(170, 120, 46, 255), new Color32(96, 64, 26, 255) };
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool cap = y < 3 || y >= h - 3 || y == 11 || y == 12;
                // The shaft is narrower than the caps.
                bool inside = cap || (x >= 1 && x <= 4);
                pixels[y * w + x] = inside ? (cap ? brass[x] : steel[x]) : new Color32(0, 0, 0, 0);
            }
            return pixels;
        }

        /// <summary>A soft round glow, white with alpha falling off from the centre (tinted by the renderer).</summary>
        public static Color32[] GlowPixels()
        {
            const int size = 28;
            var pixels = new Color32[size * size];
            float c = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - d);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
            }
            return pixels;
        }

        /// <summary>Chest pixels: dark wood with lighter planks, iron bands, a brass lock and a darker lid line.</summary>
        public static Color32[] ChestPixels()
        {
            const int w = 16, h = 13;
            var outline = new Color32(40, 24, 14, 255);
            var wood = new Color32(130, 78, 40, 255);
            var woodLight = new Color32(166, 104, 56, 255);
            var iron = new Color32(96, 104, 116, 255);
            var brass = new Color32(236, 188, 86, 255);
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color32 color;
                if (x == 0 || x == w - 1 || y == 0 || y == h - 1 || y == 7)
                    color = outline;
                else if (x == 3 || x == w - 4)
                    color = iron;
                else if ((x == 7 || x == 8) && y >= 5 && y <= 8)
                    color = brass;
                else
                    color = y > 7 ? woodLight : (y % 3 == 0 ? woodLight : wood);
                pixels[y * w + x] = color;
            }
            return pixels;
        }

        private static Sprite Make(Color32[] pixels, int width, int height, Vector2 pivot)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Drop Placeholder",
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), pivot, PixelsPerUnit);
        }
    }
}
