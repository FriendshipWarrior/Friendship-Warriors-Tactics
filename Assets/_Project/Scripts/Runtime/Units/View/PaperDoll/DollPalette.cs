using System.Collections.Generic;
using UnityEngine;

namespace FriendshipWarriors.Units.View.PaperDoll
{
    /// <summary>
    /// What a unit's key colours become: job primary from the job colour, team accent from the side, and a skin tone,
    /// hair colour and hairstyle picked deterministically from the unit's name, so a unit always looks the same.
    /// </summary>
    public readonly struct DollPalette
    {
        private static readonly Color32[][] SkinTones =
        {
            KeyPalette.Skin,
            new Color32[] { new(255, 224, 196, 255), new(236, 184, 146, 255), new(196, 136, 104, 255) },
            new Color32[] { new(214, 160, 116, 255), new(180, 124, 84, 255), new(132, 86, 58, 255) },
            new Color32[] { new(168, 112, 76, 255), new(132, 82, 54, 255), new(92, 56, 38, 255) },
            new Color32[] { new(120, 78, 54, 255), new(92, 58, 40, 255), new(62, 38, 28, 255) },
        };

        private static readonly Color32[][] HairColors =
        {
            KeyPalette.Hair,
            new Color32[] { new(64, 60, 72, 255), new(40, 36, 48, 255), new(26, 22, 32, 255) },
            new Color32[] { new(250, 220, 140, 255), new(220, 180, 96, 255), new(170, 130, 64, 255) },
            new Color32[] { new(190, 96, 56, 255), new(146, 66, 38, 255), new(100, 44, 28, 255) },
            new Color32[] { new(210, 210, 216, 255), new(168, 168, 178, 255), new(120, 120, 132, 255) },
            new Color32[] { new(150, 50, 60, 255), new(110, 34, 44, 255), new(72, 22, 30, 255) },
        };

        private static readonly Color32[] PlayerAccent = { new(96, 160, 255, 255), new(44, 92, 196, 255) };
        private static readonly Color32[] EnemyAccent = { new(236, 78, 72, 255), new(150, 36, 44, 255) };

        public readonly Color32[] Skin;
        public readonly Color32[] Hair;
        public readonly Color32[] Primary;
        public readonly Color32[] Accent;

        /// <summary>Index into the catalog's hairstyles, also derived from the unit.</summary>
        public readonly int HairStyle;

        /// <summary>Identifies this colour set for caching.</summary>
        public readonly string Key;

        public DollPalette(Color32[] skin, Color32[] hair, Color32[] primary, Color32[] accent, int hairStyle, string key)
        {
            Skin = skin;
            Hair = hair;
            Primary = primary;
            Accent = accent;
            HairStyle = hairStyle;
            Key = key;
        }

        public static DollPalette For(Unit unit, int hairStyleCount)
        {
            uint hash = StableHash(unit.Name);
            int skin = (int)(hash % (uint)SkinTones.Length);
            int hair = (int)((hash / 7u) % (uint)HairColors.Length);
            int style = hairStyleCount > 0 ? (int)((hash / 53u) % (uint)hairStyleCount) : 0;
            var primary = JobRamp(unit.Job.PlaceholderColor);
            var accent = unit.Team == Team.Player ? PlayerAccent : EnemyAccent;
            string key = $"{ColorUtility.ToHtmlStringRGB(unit.Job.PlaceholderColor)}/{unit.Team}/{skin}/{hair}/{style}";
            return new DollPalette(SkinTones[skin], HairColors[hair], primary, accent, style, key);
        }

        /// <summary>A three-tone ramp around the job colour: lighter, as given, darker.</summary>
        public static Color32[] JobRamp(Color job) => new Color32[]
        {
            Color.Lerp(job, Color.white, 0.3f), job, Color.Lerp(job, Color.black, 0.42f),
        };

        /// <summary>Key colour → replacement, for <see cref="PaletteSwap"/>.</summary>
        public Dictionary<int, Color32> ToSwapMap()
        {
            var map = new Dictionary<int, Color32>();
            Add(map, KeyPalette.Skin, Skin);
            Add(map, KeyPalette.Hair, Hair);
            Add(map, KeyPalette.Primary, Primary);
            Add(map, KeyPalette.Accent, Accent);
            return map;
        }

        private static void Add(Dictionary<int, Color32> map, Color32[] keys, Color32[] values)
        {
            for (int i = 0; i < keys.Length && i < values.Length; i++)
                map[PaletteSwap.Pack(keys[i])] = values[i];
        }

        /// <summary>FNV-1a: stable across runs and platforms, unlike string.GetHashCode.</summary>
        public static uint StableHash(string text)
        {
            uint hash = 2166136261;
            foreach (char c in text ?? "")
            {
                hash ^= c;
                hash *= 16777619;
            }
            return hash;
        }
    }
}
