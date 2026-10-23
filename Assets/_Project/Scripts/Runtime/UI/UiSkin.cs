using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace FriendshipWarriors.UI
{
    /// <summary>
    /// The pixel-art UI look: fonts (Tiny RPG Font Kit I) and frames (Tiny RPG Mana Soul GUI), loaded from
    /// <c>Resources/UI/UiSkin</c>. Built by <i>Friendship Warriors → Art → Build Pixel UI Skin</i>. Sizes are in logical
    /// UI pixels (see <see cref="PixelUiScale"/>); font sizes are whole multiples of each font's native pixel size so
    /// every glyph pixel lands on whole screen pixels. Code falls back to plain panels when the skin is missing.
    /// </summary>
    [CreateAssetMenu(menuName = "Friendship Warriors/UI Skin", fileName = "UiSkin")]
    public sealed class UiSkin : ScriptableObject
    {
        public const string ResourcePath = "UI/UiSkin";

        /// <summary>Item frame states in <see cref="ItemFrames"/>, in the pack's order.</summary>
        public const int ItemNormal = 0, ItemSelected = 1, ItemPressed = 2, ItemDisabled = 3;

        [Header("Fonts")]
        [Tooltip("Body text and menus: Fine Fantasy Strategies (12 px native).")]
        public TMP_FontAsset Body;
        [Tooltip("Titles, banners, Victory / Defeat: Brilliant Strength (14 px native).")]
        public TMP_FontAsset Title;
        [Tooltip("Floating damage numbers: Fine Fantasy Strategies Italic Numbers (SDF, so popups keep their outline).")]
        public TMP_FontAsset Numbers;
        [Tooltip("The numbers font's material with a dark outline pushed outside the glyphs (pixel strokes are one pixel " +
                 "thin, so a centred outline would swallow them). Shared by popups and unit tags.")]
        public Material NumbersOutline;
        [Min(1)] public int BodySize = 12;
        [Min(1)] public int TitleSize = 14;

        [Header("Frames")]
        [Tooltip("Window background (9-sliced).")]
        public UiSheet Window;
        [Tooltip("Menu item backgrounds: normal, selected, pressed, disabled (9-sliced).")]
        public UiSheet ItemFrames;
        [Tooltip("The pointer beside the selected menu item; its frames loop.")]
        public UiSheet Cursor;
        [Min(0.02f)] public float CursorFrameSeconds = 0.12f;
        [Tooltip("HP / MP / CT bar frame (9-sliced horizontally); the fill is drawn inside BarInterior.")]
        public UiSheet Bar;
        [Tooltip("The bar's hollow inside, in pixels from each edge: left, bottom, right, top.")]
        public Vector4 BarInterior;

        [Header("Colours")]
        public Color TextColor = new(0.99f, 0.95f, 0.85f);
        public Color SelectedTextColor = Color.white;
        public Color DisabledTextColor = new(0.55f, 0.50f, 0.66f);
        public Color MutedTextColor = new(0.72f, 0.66f, 0.80f);
        public Color AccentColor = new(0.98f, 0.78f, 0.29f);
        public Color HpColor = new(0.55f, 0.86f, 0.26f);
        public Color MpColor = new(0.27f, 0.62f, 0.93f);
        public Color CtColor = new(0.98f, 0.78f, 0.29f);
        public Color BarBackColor = new(0.07f, 0.05f, 0.16f);

        private static UiSkin _current;
        private static bool _loaded;
        private static readonly Dictionary<(Texture2D texture, int frameWidth, int index), Sprite> Sprites = new();

        /// <summary>The project's skin, or null when it hasn't been built (plain fallback panels are used then).</summary>
        public static UiSkin Current
        {
            get
            {
                if (!_loaded || (_current == null && Application.isPlaying))
                {
                    _current = Resources.Load<UiSkin>(ResourcePath);
                    _loaded = true;
                }
                return _current;
            }
        }

        /// <summary>A sprite for one frame of <paramref name="sheet"/>, cut once and cached, with the sheet's 9-slice border.
        /// 100 pixels per unit matches the canvas default, so one art pixel is one logical UI pixel.</summary>
        public static Sprite Frame(UiSheet sheet, int index)
        {
            if (sheet?.Texture == null)
                return null;
            var key = (sheet.Texture, sheet.FrameWidth, index);
            if (Sprites.TryGetValue(key, out var sprite) && sprite != null)
                return sprite;
            var rect = sheet.FrameRect(index);
            sprite = Sprite.Create(sheet.Texture, new Rect(rect.x, rect.y, rect.width, rect.height), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, sheet.Border);
            sprite.name = $"{sheet.Texture.name} {index}";
            Sprites[key] = sprite;
            return sprite;
        }
    }
}
