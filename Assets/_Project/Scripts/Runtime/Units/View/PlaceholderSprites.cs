using System.Collections.Generic;
using UnityEngine;

namespace FriendshipWarriors.Units.View
{
    /// <summary>
    /// Generates stand-in unit sprites until real art exists: a small figure in the job's color, drawn facing
    /// screen-right. The front pose shows a face; the back pose shows hair and a cape, so facing reads at a glance.
    /// </summary>
    public static class PlaceholderSprites
    {
        public const int Width = 32;
        public const int Height = 40;
        public const float PixelsPerUnit = 32f;

        private static readonly Color Outline = new(0.08f, 0.08f, 0.10f);
        private static readonly Color Skin = new(0.96f, 0.82f, 0.66f);
        private static readonly Color Hair = new(0.30f, 0.20f, 0.12f);
        private static readonly Color Eye = new(0.10f, 0.10f, 0.15f);

        private static readonly Dictionary<(Color, bool), Sprite> Units = new();
        private static Sprite _groundMarker;
        private static Sprite _shadow;

        public static Sprite Unit(Color jobColor, bool front)
        {
            // Cached sprites can be destroyed between Play sessions when domain reload is off, so check for that too.
            if (Units.TryGetValue((jobColor, front), out var cached) && cached != null)
                return cached;

            var pixels = new Color[Width * Height];
            var cape = Color.Lerp(jobColor, Color.black, 0.35f);
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                Color c = Color.clear;
                bool body = x >= 10 && x <= 21 && y >= 2 && y <= 24 && !(y <= 9 && x >= 15 && x <= 16);
                bool head = (x - 16) * (x - 16) + (y - 30) * (y - 30) <= 36;
                if (body)
                    c = front ? jobColor : cape;
                if (front && body && y == 13)
                    c = Color.Lerp(jobColor, Color.black, 0.5f); // belt
                if (head)
                    c = front ? Skin : Hair;
                if (front && head && y >= 29 && y <= 31 && (x == 18 || x == 20))
                    c = Eye; // eyes sit toward screen-right, the way the figure faces
                if (front && head && y >= 33)
                    c = Hair;
                pixels[y * Width + x] = c;
            }
            AddOutline(pixels);

            var sprite = Create($"Placeholder {(front ? "Front" : "Back")} {ColorUtility.ToHtmlStringRGB(jobColor)}", pixels, Width, Height, new Vector2(0.5f, 0f));
            Units[(jobColor, front)] = sprite;
            return sprite;
        }

        /// <summary>A soft white disc for under a unit's feet; tint it by team.</summary>
        public static Sprite GroundMarker()
        {
            if (_groundMarker != null)
                return _groundMarker;
            const int size = 32;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                float alpha = d > 1f ? 0f : d > 0.75f ? 0.9f : 0.35f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            _groundMarker = Create("Placeholder Ground Marker", pixels, size, size, new Vector2(0.5f, 0.5f));
            return _groundMarker;
        }

        /// <summary>A soft dark disc for the ground shadow under units; lying on the ground, the iso camera shows it as an FFT-style ellipse.</summary>
        public static Sprite Shadow()
        {
            if (_shadow != null)
                return _shadow;
            const int width = 24, height = 24;
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float dx = (x + 0.5f - width / 2f) / (width / 2f);
                float dy = (y + 0.5f - height / 2f) / (height / 2f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                // Solid in the middle, soft towards the rim.
                float alpha = d >= 1f ? 0f : 0.55f * Mathf.Clamp01((1f - d) / 0.45f);
                pixels[y * width + x] = new Color(0f, 0f, 0f, alpha);
            }
            _shadow = Create("Placeholder Shadow", pixels, width, height, new Vector2(0.5f, 0.5f));
            return _shadow;
        }

        private static void AddOutline(Color[] pixels)
        {
            var source = (Color[])pixels.Clone();
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (source[y * Width + x].a > 0f)
                    continue;
                bool touches = (x > 0 && source[y * Width + x - 1].a > 0f) || (x < Width - 1 && source[y * Width + x + 1].a > 0f)
                               || (y > 0 && source[(y - 1) * Width + x].a > 0f) || (y < Height - 1 && source[(y + 1) * Width + x].a > 0f);
                if (touches)
                    pixels[y * Width + x] = Outline;
            }
        }

        private static Sprite Create(string name, Color[] pixels, int width, int height, Vector2 pivot)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            texture.SetPixels(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), pivot, PixelsPerUnit);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
