using System.Collections.Generic;
using UnityEngine;

namespace FriendshipWarriors.Units.View.PaperDoll
{
    /// <summary>
    /// The colours part sheets are painted with. Every material is a five-shade ramp in <see cref="ColorRamp"/> order
    /// (highlight, light, mid, shadow, outline): parts are outlined in the darkest shade of the material they border
    /// (a coloured outline, as in FFT), not in black. KEY ramps are recoloured per unit (skin, hair, job primary, team
    /// accent) by exact RGB, outline shade included; metal, leather, undersuit, brass, copper and the single colours stay
    /// as painted. Exported as Art/Palettes/PaperDollKeys.gpl for painting.
    /// </summary>
    public static class KeyPalette
    {
        public const int Highlight = ColorRamp.Highlight, Light = ColorRamp.Light, Mid = ColorRamp.Mid,
            Shadow = ColorRamp.Shadow, Line = ColorRamp.Line;

        // Key ramps (recoloured per unit).
        public static readonly Color32[] Skin = Ramp((244, 224, 180), (239, 201, 152), (234, 178, 134), (164, 99, 85), (89, 50, 49));
        public static readonly Color32[] Hair = Ramp((203, 177, 125), (176, 135, 84), (150, 98, 58), (105, 45, 33), (57, 21, 20));
        public static readonly Color32[] Primary = Ramp((189, 195, 155), (153, 166, 118), (118, 136, 90), (71, 95, 59), (37, 52, 33));
        public static readonly Color32[] Accent = Ramp((143, 206, 240), (103, 172, 233), (78, 138, 226), (42, 58, 158), (28, 27, 86));

        // Fixed material ramps (never recoloured).
        public static readonly Color32[] Metal = Ramp((201, 211, 219), (178, 188, 200), (158, 166, 182), (109, 110, 127), (60, 60, 69));
        public static readonly Color32[] Leather = Ramp((192, 169, 123), (161, 124, 81), (130, 86, 54), (91, 40, 31), (49, 19, 19));
        public static readonly Color32[] Undersuit = Ramp((165, 158, 178), (122, 118, 139), (84, 82, 100), (60, 56, 70), (33, 31, 38));
        public static readonly Color32[] Brass = Ramp((230, 218, 130), (217, 187, 89), (204, 154, 62), (143, 74, 31), (78, 34, 21));
        public static readonly Color32[] Copper = Ramp((226, 183, 129), (212, 142, 87), (198, 104, 60), (139, 36, 30), (75, 20, 26));

        // Single fixed colours.
        /// <summary>The outline for anything that isn't a ramp material (eyes, lenses, glows).</summary>
        public static readonly Color32 Outline = new(30, 22, 34, 255);
        public static readonly Color32 Lens = new(150, 230, 255, 255);
        public static readonly Color32 LensDark = new(60, 90, 110, 255);
        public static readonly Color32 Eye = new(40, 28, 44, 255);
        public static readonly Color32 EyeWhite = new(250, 248, 240, 255);
        public static readonly Color32 Mouth = new(176, 92, 80, 255);
        public static readonly Color32 Glow = new(130, 220, 255, 255);
        public static readonly Color32 GlowCore = new(232, 250, 255, 255);
        public static readonly Color32 Flash = new(255, 232, 120, 255);

        /// <summary>The swappable ramps, for checks and tools.</summary>
        public static IEnumerable<Color32[]> KeyRamps()
        {
            yield return Skin;
            yield return Hair;
            yield return Primary;
            yield return Accent;
        }

        /// <summary>Every ramp material, key and fixed.</summary>
        public static IEnumerable<Color32[]> Ramps()
        {
            foreach (var ramp in KeyRamps())
                yield return ramp;
            yield return Metal;
            yield return Leather;
            yield return Undersuit;
            yield return Brass;
            yield return Copper;
        }

        private static readonly string[] ShadeNames = { "highlight", "light", "mid", "shadow", "outline" };

        /// <summary>Every colour in painting order, with names, for the exported palettes.</summary>
        public static IEnumerable<(string name, Color32 color)> All()
        {
            foreach (var (name, ramp, key) in new[]
                     {
                         ("Skin", Skin, true), ("Hair", Hair, true), ("Job primary", Primary, true), ("Team accent", Accent, true),
                         ("Metal", Metal, false), ("Leather", Leather, false), ("Undersuit", Undersuit, false),
                         ("Brass", Brass, false), ("Copper", Copper, false),
                     })
                for (int i = 0; i < ramp.Length; i++)
                    yield return ($"{name} {i + 1} {ShadeNames[i]}{(key ? " (key)" : "")}", ramp[i]);
            yield return ("Outline (other)", Outline);
            yield return ("Lens", Lens);
            yield return ("Lens dark", LensDark);
            yield return ("Eye", Eye);
            yield return ("Eye white", EyeWhite);
            yield return ("Mouth", Mouth);
            yield return ("Aether glow", Glow);
            yield return ("Aether glow core", GlowCore);
            yield return ("Muzzle flash", Flash);
        }

        private static Color32[] Ramp(params (byte r, byte g, byte b)[] shades)
        {
            var ramp = new Color32[shades.Length];
            for (int i = 0; i < shades.Length; i++)
                ramp[i] = new Color32(shades[i].r, shades[i].g, shades[i].b, 255);
            return ramp;
        }
    }
}
