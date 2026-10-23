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
        // Five-shade ramps (ColorRamp order); the first entry of each list is the key ramp itself (no change).
        private static readonly Color32[][] SkinTones =
        {
            KeyPalette.Skin,
            ColorRamp.From(new Color32(250, 206, 172, 255)),
            ColorRamp.From(new Color32(206, 148, 104, 255)),
            ColorRamp.From(new Color32(164, 106, 72, 255)),
            ColorRamp.From(new Color32(118, 76, 52, 255)),
        };

        private static readonly Color32[][] HairColors =
        {
            KeyPalette.Hair,
            ColorRamp.From(new Color32(62, 56, 74, 255)),
            ColorRamp.From(new Color32(226, 184, 96, 255)),
            ColorRamp.From(new Color32(196, 92, 50, 255)),
            ColorRamp.From(new Color32(190, 192, 204, 255)),
            ColorRamp.From(new Color32(150, 46, 62, 255)),
        };

        private static readonly Color32[] PlayerAccent = ColorRamp.From(new Color32(80, 146, 250, 255));
        private static readonly Color32[] EnemyAccent = ColorRamp.From(new Color32(228, 70, 66, 255));

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

        /// <summary>The five-shade ramp around the job colour (the job colour is the mid shade).</summary>
        public static Color32[] JobRamp(Color job) => ColorRamp.From(job);

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
