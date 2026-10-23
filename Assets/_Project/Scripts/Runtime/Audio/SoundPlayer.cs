using System.Collections.Generic;
using UnityEngine;

namespace FriendshipWarriors.Audio
{
    /// <summary>
    /// Plays <see cref="SoundCue"/>s from the <see cref="SoundCueSet"/> in Resources, on a small pool of 2D audio
    /// sources that lives across scenes. Created on first use; does nothing outside Play mode. The same cue fired
    /// several times in one moment (an area spell hitting five units) plays once.
    /// </summary>
    public sealed class SoundPlayer : MonoBehaviour
    {
        private const int Voices = 8;
        private const float RepeatGuard = 0.05f;

        private static SoundPlayer _instance;
        private readonly List<AudioSource> _sources = new();
        private readonly Dictionary<SoundCue, float> _lastPlayed = new();
        private readonly Dictionary<SoundCue, AudioClip> _blips = new();
        private readonly System.Random _jitter = new();
        private SoundCueSet _set;
        private int _next;

        /// <summary>Plays a cue (or its placeholder blip). Safe to call from anywhere; ignored outside Play mode.</summary>
        public static void Play(SoundCue cue)
        {
            if (cue == SoundCue.None || !Application.isPlaying)
                return;
            if (_instance == null)
            {
                var go = new GameObject("Sound Player");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<SoundPlayer>();
            }
            _instance.PlayCue(cue);
        }

        private void Awake()
        {
            _set = Resources.Load<SoundCueSet>(SoundCueSet.ResourcePath);
            for (int i = 0; i < Voices; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                _sources.Add(source);
            }
        }

        private void OnDestroy()
        {
            foreach (var clip in _blips.Values)
                Destroy(clip);
            if (_instance == this)
                _instance = null;
        }

        private void PlayCue(SoundCue cue)
        {
            float now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(cue, out float last) && now - last < RepeatGuard)
                return;

            AudioClip clip = null;
            float volume = 0.8f, jitter = 0.04f;
            var entry = _set != null ? _set.Find(cue) : null;
            if (entry != null)
            {
                clip = entry.Clips[_jitter.Next(entry.Clips.Length)];
                volume = entry.Volume;
                jitter = entry.PitchJitter;
            }
            else if (_set == null || _set.UsePlaceholderBlips)
            {
                clip = Blip(cue);
            }
            if (clip == null)
                return;

            _lastPlayed[cue] = now;
            var source = _sources[_next];
            _next = (_next + 1) % _sources.Count;
            source.pitch = 1f + (float)(_jitter.NextDouble() * 2 - 1) * jitter;
            source.PlayOneShot(clip, volume * (_set != null ? _set.MasterVolume : 0.7f));
        }

        private AudioClip Blip(SoundCue cue)
        {
            if (_blips.TryGetValue(cue, out var clip))
                return clip;
            var samples = PlaceholderBlips.Samples(cue);
            if (samples.Length == 0)
                return null;
            clip = AudioClip.Create($"Blip {cue}", samples.Length, 1, PlaceholderBlips.SampleRate, false);
            clip.SetData(samples, 0);
            _blips[cue] = clip;
            return clip;
        }
    }
}
