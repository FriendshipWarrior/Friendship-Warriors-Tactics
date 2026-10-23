using System;
using System.Collections.Generic;
using UnityEngine;

namespace FriendshipWarriors.Audio
{
    /// <summary>
    /// Which clip each <see cref="SoundCue"/> plays. Cues without clips play a generated placeholder blip while
    /// <see cref="UsePlaceholderBlips"/> is on, and nothing when it's off. Loaded from <c>Resources/Audio/SoundCues</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Friendship Warriors/Sound Cue Set", fileName = "SoundCues")]
    public sealed class SoundCueSet : ScriptableObject
    {
        public const string ResourcePath = "Audio/SoundCues";

        [Serializable]
        public sealed class Entry
        {
            public SoundCue Cue;
            [Tooltip("One is picked at random each time.")]
            public AudioClip[] Clips = Array.Empty<AudioClip>();
            [Range(0f, 1f)] public float Volume = 0.8f;
            [Tooltip("Random pitch spread, e.g. 0.05 plays at 95–105%.")]
            [Range(0f, 0.5f)] public float PitchJitter = 0.04f;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();
        [Tooltip("Play a generated blip for cues that have no clip yet (placeholder audio).")]
        [SerializeField] private bool _usePlaceholderBlips = true;
        [SerializeField, Range(0f, 1f)] private float _masterVolume = 0.7f;

        public bool UsePlaceholderBlips => _usePlaceholderBlips;
        public float MasterVolume => _masterVolume;
        public IReadOnlyList<Entry> Entries => _entries;

        /// <summary>The entry for a cue with at least one clip, or null.</summary>
        public Entry Find(SoundCue cue)
        {
            foreach (var entry in _entries)
            {
                if (entry != null && entry.Cue == cue && entry.Clips != null && entry.Clips.Length > 0)
                    return entry;
            }
            return null;
        }

        public void Define(Entry[] entries, bool usePlaceholderBlips, float masterVolume)
        {
            _entries = entries;
            _usePlaceholderBlips = usePlaceholderBlips;
            _masterVolume = masterVolume;
        }
    }
}
