using System;
using System.Collections.Generic;
using FriendshipWarriors.Controls;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FriendshipWarriors.UI
{
    /// <summary>
    /// A centered, controller-friendly menu built in code: a title, optional subtitle, and a <see cref="MenuModel"/>'s
    /// items. Up/down (with key repeat) moves the selection, Submit activates, Back reports <see cref="BackPressed"/>;
    /// the mouse can hover and click. Disabled items are greyed out, skipped by navigation and ignore clicks.
    /// Used by the main menu and the quick-play pause menu.
    /// </summary>
    public sealed class MenuScreen : MonoBehaviour
    {
        [SerializeField] private MenuInput _input;
        [SerializeField, Min(200f)] private float _width = 520f;
        [SerializeField] private Vector2 _offset = Vector2.zero;
        [SerializeField, Min(0.05f)] private float _repeatDelay = 0.35f;
        [SerializeField, Min(0.02f)] private float _repeatInterval = 0.12f;
        [Tooltip("Darken everything behind the menu while it's open (pause menus).")]
        [SerializeField] private bool _dimBackground;

        private readonly List<(UnityEngine.UI.Image background, TextMeshProUGUI label)> _rows = new();
        private RectTransform _panel;
        private UnityEngine.UI.Image _dim;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _subtitle;
        private MenuModel _menu;
        private int _heldDirection;
        private float _nextRepeat;
        private float _ignoreInputUntil;

        /// <summary>An enabled item was chosen (Submit or click).</summary>
        public event Action<MenuItem> ItemActivated;

        /// <summary>Back / Cancel was pressed while the menu was open.</summary>
        public event Action BackPressed;

        public bool IsOpen => _menu != null;

        private void Awake()
        {
            if (_dimBackground)
            {
                _dim = UiFactory.Fill(transform, "Dim", new Color(0f, 0f, 0f, 0.45f), blocksPointer: true);
                _dim.gameObject.SetActive(false);
            }
            _panel = UiFactory.Panel(transform, "Menu", new Vector2(0.5f, 0.5f), _offset, _width);
            _title = UiFactory.Text(_panel, "Title", 44, FontStyles.Bold, TextAlignmentOptions.Center);
            _subtitle = UiFactory.Text(_panel, "Subtitle", 24, FontStyles.Normal, TextAlignmentOptions.Center);
            _panel.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            _input.Submit.performed += OnSubmit;
            _input.Back.performed += OnBack;
        }

        private void OnDisable()
        {
            _input.Submit.performed -= OnSubmit;
            _input.Back.performed -= OnBack;
        }

        public void Show(MenuModel menu, string title, string subtitle = null)
        {
            if (_menu != null)
                _menu.Changed -= Refresh;
            _menu = menu;
            _menu.Changed += Refresh;
            _title.text = title;
            _subtitle.text = subtitle ?? "";
            _subtitle.gameObject.SetActive(!string.IsNullOrEmpty(subtitle));
            _panel.gameObject.SetActive(true);
            if (_dim != null)
                _dim.gameObject.SetActive(true);
            _heldDirection = 0;
            // The press that opened this menu shouldn't also activate an item in it.
            _ignoreInputUntil = Time.unscaledTime + 0.15f;
            Refresh();
        }

        public void Hide()
        {
            if (_menu != null)
                _menu.Changed -= Refresh;
            _menu = null;
            _panel.gameObject.SetActive(false);
            if (_dim != null)
                _dim.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_menu == null)
                return;

            float y = _input.Navigate.ReadValue<Vector2>().y;
            int direction = y > 0.5f ? -1 : y < -0.5f ? 1 : 0;
            float now = Time.unscaledTime;
            if (direction == 0)
            {
                _heldDirection = 0;
                return;
            }
            if (direction != _heldDirection)
            {
                _heldDirection = direction;
                _nextRepeat = now + _repeatDelay;
                _menu.MoveSelection(direction);
            }
            else if (now >= _nextRepeat)
            {
                _nextRepeat = now + _repeatInterval;
                _menu.MoveSelection(direction);
            }
        }

        private void OnSubmit(InputAction.CallbackContext _)
        {
            if (_menu == null || Time.unscaledTime < _ignoreInputUntil)
                return;
            if (_menu.Selected is { Enabled: true } item)
                ItemActivated?.Invoke(item);
        }

        private void OnBack(InputAction.CallbackContext _)
        {
            if (_menu != null && Time.unscaledTime >= _ignoreInputUntil)
                BackPressed?.Invoke();
        }

        private void Refresh()
        {
            while (_rows.Count < _menu.Items.Count)
            {
                int index = _rows.Count;
                _rows.Add(UiFactory.MenuRow(_panel, $"Item {index}", 64f, 34f,
                    onHover: () => _menu?.Select(index),
                    onClick: () =>
                    {
                        if (_menu != null && index < _menu.Items.Count && _menu.Items[index].Enabled)
                        {
                            _menu.Select(index);
                            ItemActivated?.Invoke(_menu.Items[index]);
                        }
                    }));
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                var (background, label) = _rows[i];
                bool visible = i < _menu.Items.Count;
                background.gameObject.SetActive(visible);
                if (!visible)
                    continue;
                var item = _menu.Items[i];
                bool selected = i == _menu.SelectedIndex;
                label.text = item.Label;
                background.color = selected ? UiFactory.SelectedColor : UiFactory.ItemColor;
                label.color = !item.Enabled ? UiFactory.DisabledTextColor : selected ? UiFactory.SelectedTextColor : UiFactory.TextColor;
            }
        }
    }
}
