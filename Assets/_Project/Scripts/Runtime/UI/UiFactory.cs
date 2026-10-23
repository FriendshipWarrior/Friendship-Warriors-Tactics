using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FriendshipWarriors.UI
{
    /// <summary>
    /// Builds the placeholder UI look shared by the front-end screens (dark panels, TextMeshPro text, highlighted
    /// menu rows), matching the battle HUD's colors. Everything is built in code, scaled from 1920x1080.
    /// </summary>
    public static class UiFactory
    {
        public static readonly Color PanelColor = new(0.07f, 0.08f, 0.11f, 0.88f);
        public static readonly Color ItemColor = new(1f, 1f, 1f, 0.04f);
        public static readonly Color SelectedColor = new(0.95f, 0.70f, 0.25f, 0.90f);
        public static readonly Color TextColor = new(0.93f, 0.92f, 0.88f);
        public static readonly Color SelectedTextColor = new(0.10f, 0.08f, 0.05f);
        public static readonly Color DisabledTextColor = new(0.42f, 0.42f, 0.46f);

        /// <summary>A dark panel anchored at <paramref name="anchor"/> that grows to fit its vertically stacked content.</summary>
        public static RectTransform Panel(Transform parent, string panelName, Vector2 anchor, Vector2 offset, float width, Color? color = null)
        {
            var go = new GameObject(panelName, typeof(RectTransform), typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.VerticalLayoutGroup), typeof(UnityEngine.UI.ContentSizeFitter));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(width, 0f);

            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.color = color ?? PanelColor;
            image.raycastTarget = false;

            var layout = go.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 18, 18);
            layout.spacing = 8f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            go.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
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

        public static TextMeshProUGUI Text(Transform parent, string textName, float size, FontStyles style,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var go = new GameObject(textName, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.color = TextColor;
            text.raycastTarget = false;
            text.richText = true;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        /// <summary>One selectable menu row; hovering calls <paramref name="onHover"/>, clicking <paramref name="onClick"/>.</summary>
        public static (UnityEngine.UI.Image background, TextMeshProUGUI label) MenuRow(Transform parent, string rowName,
            float height, float fontSize, Action onHover, Action onClick)
        {
            var row = new GameObject(rowName, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
            var background = row.GetComponent<UnityEngine.UI.Image>();
            background.raycastTarget = true;

            var label = Text(row.transform, "Label", fontSize, FontStyles.Normal);
            var rect = (RectTransform)label.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(24f, 0f);
            rect.offsetMax = new Vector2(-16f, 0f);

            var trigger = row.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, onHover);
            AddTrigger(trigger, EventTriggerType.PointerClick, onClick);
            return (background, label);
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }
    }
}
