using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace FriendshipWarriors.UI
{
    /// <summary>
    /// Draws a <see cref="MenuModel"/>'s items as rows inside a window, with the skin's animated pointer beside the
    /// selected row (FFT's pointing hand). Framed rows (front-end menus) show every item as a button in its state;
    /// unframed rows (battle menus) only frame the selected item. Disabled items are greyed out. The mouse hovers to
    /// select and clicks to confirm, the same path a controller takes. Shared by <see cref="BattleHud"/> and
    /// <see cref="MenuScreen"/>; the owner calls <see cref="Tick"/> to animate the pointer.
    /// </summary>
    public sealed class MenuList
    {
        private readonly RectTransform _parent;
        private readonly int _rowHeight;
        private readonly bool _framed;
        private readonly TextAlignmentOptions _alignment;
        private readonly Action<int> _hover;
        private readonly Action<int> _click;
        private readonly List<Row> _rows = new();
        private MenuModel _menu;
        private int _cursorFrame = -1;

        private sealed class Row
        {
            public GameObject Root;
            public UnityEngine.UI.Image Background;
            public TextMeshProUGUI Label;
            public UnityEngine.UI.Image Cursor;
        }

        public MenuList(RectTransform parent, int rowHeight, bool framed, Action<int> hover, Action<int> click,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            _parent = parent;
            _rowHeight = rowHeight;
            _framed = framed;
            _hover = hover;
            _click = click;
            _alignment = alignment;
        }

        public void Refresh(MenuModel menu)
        {
            _menu = menu;
            while (_rows.Count < menu.Items.Count)
                _rows.Add(NewRow(_rows.Count));

            var skin = UiSkin.Current;
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                bool visible = i < menu.Items.Count;
                row.Root.SetActive(visible);
                if (!visible)
                    continue;

                var item = menu.Items[i];
                bool selected = i == menu.SelectedIndex;
                row.Label.text = item.Label;
                row.Label.color = !item.Enabled ? UiFactory.DisabledTextColor
                    : selected ? UiFactory.SelectedTextColor : UiFactory.TextColor;
                row.Cursor.enabled = selected && row.Cursor.sprite != null;

                if (skin != null && skin.ItemFrames.Texture != null)
                {
                    int state = selected ? UiSkin.ItemSelected : item.Enabled ? UiSkin.ItemNormal : UiSkin.ItemDisabled;
                    row.Background.sprite = UiSkin.Frame(skin.ItemFrames, state);
                    row.Background.type = UnityEngine.UI.Image.Type.Sliced;
                    // Unframed lists keep the window's background behind items that aren't selected.
                    row.Background.color = _framed || selected ? Color.white : Color.clear;
                }
                else
                {
                    row.Background.color = selected ? UiFactory.PlainSelectedColor : new Color(1f, 1f, 1f, 0.04f);
                }
            }
            _cursorFrame = -1;
        }

        /// <summary>Steps the pointer's animation (unscaled time, so it keeps moving in the pause menu).</summary>
        public void Tick(float time)
        {
            var skin = UiSkin.Current;
            if (skin == null || skin.Cursor.Texture == null || _menu == null)
                return;
            int count = skin.Cursor.FrameCount;
            int frame = count > 0 ? (int)(time / skin.CursorFrameSeconds) % count : 0;
            if (frame == _cursorFrame)
                return;
            _cursorFrame = frame;
            var sprite = UiSkin.Frame(skin.Cursor, frame);
            foreach (var row in _rows)
                row.Cursor.sprite = sprite;
            if (_menu.SelectedIndex >= 0 && _menu.SelectedIndex < _rows.Count)
                _rows[_menu.SelectedIndex].Cursor.enabled = true;
        }

        private Row NewRow(int index)
        {
            var go = new GameObject($"Item {index}", typeof(RectTransform), typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.HorizontalLayoutGroup), typeof(UnityEngine.UI.LayoutElement));
            go.transform.SetParent(_parent, false);
            var element = go.GetComponent<UnityEngine.UI.LayoutElement>();
            element.preferredHeight = element.minHeight = _rowHeight;
            var layout = go.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 0, 0);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            var background = go.GetComponent<UnityEngine.UI.Image>();
            background.raycastTarget = true;

            var label = UiFactory.Text(go.transform, "Label", alignment: _alignment);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            // Rows are whole pixels tall; a label one pixel shorter than an odd row would centre on half a pixel.
            label.margin = new Vector4(0f, 1f, 0f, 0f);

            // The pointer sits just left of the row, poking out of the window like FFT's hand. The sheet's feather
            // points left, so it's mirrored to point at the item.
            var cursorGo = new GameObject("Pointer", typeof(RectTransform), typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.LayoutElement));
            cursorGo.GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
            var cursorRect = (RectTransform)cursorGo.transform;
            cursorRect.SetParent(go.transform, false);
            cursorRect.anchorMin = cursorRect.anchorMax = new Vector2(0f, 0.5f);
            cursorRect.pivot = new Vector2(0f, 0.5f);
            var skin = UiSkin.Current;
            int cursorWidth = skin != null && skin.Cursor.Texture != null ? skin.Cursor.FrameWidth : 0;
            int cursorHeight = skin != null && skin.Cursor.Texture != null ? skin.Cursor.FrameHeight : 0;
            cursorRect.sizeDelta = new Vector2(cursorWidth, cursorHeight);
            cursorRect.localScale = new Vector3(-1f, 1f, 1f);
            cursorRect.anchoredPosition = new Vector2(4f, (_rowHeight - cursorHeight) % 2 == 0 ? 0f : 0.5f);
            var cursor = cursorGo.GetComponent<UnityEngine.UI.Image>();
            cursor.raycastTarget = false;
            if (skin != null && skin.Cursor.Texture != null)
                cursor.sprite = UiSkin.Frame(skin.Cursor, 0);
            cursor.enabled = false;

            int captured = index;
            UiFactory.AddPointer(go, () => _hover?.Invoke(captured), () => _click?.Invoke(captured));
            return new Row { Root = go, Background = background, Label = label, Cursor = cursor };
        }
    }
}
