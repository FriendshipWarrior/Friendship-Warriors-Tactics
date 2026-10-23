using System.Collections.Generic;
using UnityEngine;

namespace FriendshipWarriors.Units.View.Sheets
{
    /// <summary>
    /// Recolours a group of a sheet's colours (a shading ramp, e.g. a shirt's highlight, mid and shadow) toward a target
    /// colour while keeping the ramp: the group's middle shade becomes the target, and every other shade keeps its
    /// brightness, saturation and hue offset relative to that middle one. Results are exact colours, applied with
    /// <see cref="PaperDoll.PaletteSwap"/>.
    /// </summary>
    public static class SheetRecolor
    {
        public static Color32[] Remap(IReadOnlyList<Color32> sources, Color target)
        {
            var result = new Color32[sources.Count];
            if (sources.Count == 0)
                return result;

            var reference = Reference(sources);
            Color.RGBToHSV(reference, out float refH, out float refS, out float refV);
            Color.RGBToHSV(target, out float toH, out float toS, out float toV);
            for (int i = 0; i < sources.Count; i++)
            {
                Color.RGBToHSV(sources[i], out float h, out float s, out float v);
                float hue = Mathf.Repeat(toH + Mathf.DeltaAngle(refH * 360f, h * 360f) / 360f, 1f);
                float sat = Mathf.Clamp01(refS > 0.01f ? toS * s / refS : toS);
                float val = Mathf.Clamp01(refV > 0.01f ? v * toV / refV : toV);
                var c = (Color32)Color.HSVToRGB(hue, sat, val);
                c.a = 255;
                result[i] = c;
            }
            return result;
        }

        /// <summary>The middle shade by brightness (the one that becomes the target colour exactly).</summary>
        public static Color32 Reference(IReadOnlyList<Color32> sources)
        {
            var sorted = new List<Color32>(sources);
            sorted.Sort((a, b) => Brightness(b).CompareTo(Brightness(a)));
            return sorted[sorted.Count / 2];
        }

        private static float Brightness(Color32 c)
        {
            Color.RGBToHSV(c, out _, out _, out float v);
            return v;
        }

        /// <summary>Adds source → recoloured pairs to an exact-RGB swap map.</summary>
        public static void AddTo(Dictionary<int, Color32> map, IReadOnlyList<Color32> sources, Color target)
        {
            var remapped = Remap(sources, target);
            for (int i = 0; i < sources.Count; i++)
                map[PaperDoll.PaletteSwap.Pack(sources[i])] = remapped[i];
        }
    }
}
