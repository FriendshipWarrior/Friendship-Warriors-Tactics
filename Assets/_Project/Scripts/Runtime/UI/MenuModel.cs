using System;
using System.Collections.Generic;

namespace FriendshipWarriors.UI
{
    /// <summary>
    /// A vertical list of options with a selection that skips disabled items and wraps around. Views draw it and
    /// forward input; the battle flow reads <see cref="Selected"/>.
    /// </summary>
    public sealed class MenuModel
    {
        private readonly List<MenuItem> _items = new();

        public MenuModel(string title = "")
        {
            Title = title;
        }

        public string Title { get; set; }
        public IReadOnlyList<MenuItem> Items => _items;
        public int SelectedIndex { get; private set; } = -1;
        public MenuItem? Selected => SelectedIndex >= 0 ? _items[SelectedIndex] : null;

        public event Action Changed;

        /// <summary>Replaces the items, keeping <paramref name="preferredId"/> selected if it's still enabled.</summary>
        public void SetItems(IEnumerable<MenuItem> items, string preferredId = null)
        {
            _items.Clear();
            _items.AddRange(items);
            SelectedIndex = _items.FindIndex(i => i.Enabled && i.Id == preferredId);
            if (SelectedIndex < 0)
                SelectedIndex = _items.FindIndex(i => i.Enabled);
            Changed?.Invoke();
        }

        /// <summary>Moves the selection by <paramref name="delta"/> enabled items, wrapping at the ends.</summary>
        public void MoveSelection(int delta)
        {
            if (SelectedIndex < 0 || delta == 0)
                return;
            int step = Math.Sign(delta);
            for (int moved = 0; moved < Math.Abs(delta); moved++)
            {
                int index = SelectedIndex;
                for (int tries = 0; tries < _items.Count; tries++)
                {
                    index = (index + step + _items.Count) % _items.Count;
                    if (_items[index].Enabled)
                        break;
                }
                SelectedIndex = index;
            }
            Changed?.Invoke();
        }

        public bool Select(int index)
        {
            if (index < 0 || index >= _items.Count || !_items[index].Enabled || index == SelectedIndex)
                return index == SelectedIndex;
            SelectedIndex = index;
            Changed?.Invoke();
            return true;
        }
    }
}
