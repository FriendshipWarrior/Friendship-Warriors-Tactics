using System;
using System.Collections.Generic;
using FriendshipWarriors.Units.Equipment;
using UnityEngine;

namespace FriendshipWarriors.Units.View.Sheets
{
    /// <summary>
    /// Weapon swing strips drawn to sit over a unit sheet's attack frames: one frame per attack frame, in bigger frames
    /// centred on the unit's frame (so the swing can reach past it). Chosen by the unit's <see cref="WeaponType"/>;
    /// types without art show nothing.
    /// </summary>
    [CreateAssetMenu(menuName = "Friendship Warriors/Weapon Art", fileName = "WeaponArt")]
    public sealed class WeaponArt : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private WeaponType _type;
            [SerializeField] private Texture2D _front;
            [SerializeField] private Texture2D _back;

            public Entry(WeaponType type, Texture2D front, Texture2D back)
            {
                _type = type;
                _front = front;
                _back = back;
            }

            public WeaponType Type => _type;
            public Texture2D Front => _front;
            public Texture2D Back => _back;
        }

        [SerializeField] private Vector2Int _frameSize = new(48, 48);
        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

        public Vector2Int FrameSize => _frameSize;
        public IReadOnlyList<Entry> Entries => _entries;

        public Entry For(WeaponType type)
        {
            foreach (var entry in _entries)
            {
                if (entry != null && entry.Type == type)
                    return entry;
            }
            return null;
        }

        public void Define(Vector2Int frameSize, Entry[] entries)
        {
            _frameSize = frameSize;
            _entries = entries;
        }
    }
}
