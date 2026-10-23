using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FriendshipWarriors.UI
{
    /// <summary>
    /// Building blocks for the pixel-art UI shared by the battle HUD and the front-end menus: 9-sliced windows and
    /// one-line strips from the <see cref="UiSkin"/>, pixel-font text, framed bars and hover/click wiring. Sizes are in
    /// logical UI pixels on a canvas kept at a whole-number scale by <see cref="PixelCanvasScaler"/> (reference 640x360).
    /// Without a skin (it hasn't been built) everything falls back to plain dark panels with the default font.
    /// </summary>
    public static class UiFactory
    {
        /// <summary>Gap between the screen edge and the panels, in logical pixels.</summary>
        public const int Margin = 8;

        // Fallback colours when no skin is loaded.
        private static readonly Color PlainPanel = new(0.07f, 0.08f, 0.11f, 0.92f);
        private static readonly Color PlainSelected = new(0.95f, 0.70f, 0.25f, 0.90f);

        public static readonly Color PlayerColor = new(0.36f, 0.62f, 1f);
        public static readonly Color EnemyColor = new(1f, 0.38f, 0.40f);

        public static UiSkin Skin => UiSkin.Current;
        public static Color TextColor => Skin != null ? Skin.TextColor : new Color(0.93f, 0.92f, 0.88f);
        public static Color SelectedTextColor => Skin != null ? Skin.SelectedTextColor : new Color(0.10f, 0.08f, 0.05f);
        public static Color DisabledTextColor => Skin != null ? Skin.DisabledTextColor : new Color(0.45f, 0.45f, 0.50f);
        public static Color MutedTextColor => Skin != null ? Skin.MutedTextColor : new Color(0.62f, 0.62f, 0.66f);
        public static Color AccentColor => Skin != null ? Skin.AccentColor : new Color(0.95f, 0.70f, 0.25f);

        /// <summary>Rich-text hex for a colour, e.g. for &lt;color=#…&gt; tags.</summary>
        public static string Hex(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color);

        /// <summary>A 9-sliced window anchored at <paramref name="anchor"/> that grows to fit its vertically stacked
        /// content (and its width too when <paramref name="fitWidth"/>, never below <paramref name="width"/>).</summary>
        public static RectTransform Window(Transform parent, string windowName, Vector2 anchor, Vector2 offset, float width,
            bool fitWidth = false)
        {
            var rect = Stack(parent, windowName, anchor, offset, width, fitWidth, new RectOffset(14, 14, 11, 12), 2f);
            var skin = Skin;
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            if (skin != null && skin.Window.Texture != null)
            {
                image.sprite = UiSkin.Frame(skin.Window, 0);
                image.type = UnityEngine.UI.Image.Type.Sliced;
                image.color = Color.white;
                // The corners are 32 px; a smaller window would squash them.
                var minimum = rect.GetComponent<UnityEngine.UI.LayoutElement>();
                if (minimum == null)
                    minimum = rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                minimum.minWidth = Mathf.Max(minimum.minWidth, skin.Window.Border.x + skin.Window.Border.z);
                minimum.minHeight = skin.Window.Border.y + skin.Window.Border.w;
            }
            return rect;
        }

        /// <summary>A slim framed strip for one or two lines of text (the objective, hints, the seed).</summary>
        public static RectTransform Strip(Transform parent, string stripName, Vector2 anchor, Vector2 offset, float width)
        {
            var rect = Stack(parent, stripName, anchor, offset, width, false, new RectOffset(10, 10, 5, 5), 0f);
            var skin = Skin;
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            if (skin != null && skin.ItemFrames.Texture != null)
            {
                image.sprite = UiSkin.Frame(skin.ItemFrames, UiSkin.ItemPressed);
                image.type = UnityEngine.UI.Image.Type.Sliced;
                image.color = Color.white;
            }
            return rect;
        }

        /// <summary>The old name for a window, kept for callers that build plain stacks.</summary>
        public static RectTransform Panel(Transform parent, string panelName, Vector2 anchor, Vector2 offset, float width) =>
            Window(parent, panelName, anchor, offset, width);

        private static RectTransform Stack(Transform parent, string stackName, Vector2 anchor, Vector2 offset, float width,
            bool fitWidth, RectOffset padding, float spacing)
        {
            var go = new GameObject(stackName, typeof(RectTransform), typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.VerticalLayoutGroup), typeof(UnityEngine.UI.ContentSizeFitter));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(width, 0f);

            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.color = PlainPanel;
            image.raycastTarget = false;

            var layout = go.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = padding;
            layout.spacing = spacing;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = go.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            if (fitWidth)
            {
                fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
                var minimum = go.AddComponent<UnityEngine.UI.LayoutElement>();
                minimum.minWidth = width;
            }
            return rect;
        }

        /// <summary>A full-screen solid fill, e.g. to dim the battle behind a pause menu.</summary>
        public static UnityEngine.UI.Image Fill(Transform parent, string fillName, Color color, bool blocksPointer)
        {
            var go = new GameObject(fillName, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = blocksPointer;
            return image;
        }

        /// <summary>
        /// Pixel-font text: the body font (Fine Fantasy Strategies) or, with <paramref name="title"/>, the title font
        /// (Brilliant Strength), at <paramref name="scale"/> times its native pixel size. No faux bold or italics: they
        /// smear pixel fonts.
        /// </summary>
        public static TextMeshProUGUI Text(Transform parent, string textName, int scale = 1, bool title = false,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            var go = new GameObject(textName, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            var skin = Skin;
            if (skin != null && (title ? skin.Title : skin.Body) is { } font)
            {
                text.font = font;
                text.fontSize = (title ? skin.TitleSize : skin.BodySize) * Mathf.Max(1, scale);
            }
            else
            {
                text.fontSize = (title ? 15 : 12) * Mathf.Max(1, scale);
            }
            text.fontStyle = FontStyles.Normal;
            text.color = title ? AccentColor : TextColor;
            text.raycastTarget = false;
            text.richText = true;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.extraPadding = false;
            return text;
        }

        /// <summary>A framed HP/MP/CT bar <paramref name="width"/> logical pixels wide, filled with <paramref name="fill"/>.</summary>
        public static UiBar Bar(Transform parent, string barName, Color fill, int width) => new(parent, barName, fill, width);

        /// <summary>Pointer enter selects, click confirms: the same path a controller takes through a menu.</summary>
        public static void AddPointer(GameObject target, Action onHover, Action onClick)
        {
            var trigger = target.AddComponent<EventTrigger>();
            Add(trigger, EventTriggerType.PointerEnter, onHover);
            Add(trigger, EventTriggerType.PointerClick, onClick);
        }

        private static void Add(EventTrigger trigger, EventTriggerType type, Action action)
        {
            if (action == null)
                return;
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        /// <summary>Plain highlight colour for menus when no skin is loaded.</summary>
        internal static Color PlainSelectedColor => PlainSelected;
    }
}
