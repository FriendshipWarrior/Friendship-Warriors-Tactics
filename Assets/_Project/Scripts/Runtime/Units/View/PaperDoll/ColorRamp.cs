using UnityEngine;

namespace FriendshipWarriors.Units.View.PaperDoll
{
    /// <summary>
    /// Builds a five-shade material ramp (highlight, light, mid, shadow, outline) from one mid colour, the way pixel
    /// artists usually shade: lighter shades drift toward warm yellow and lose saturation, darker ones drift toward
    /// cool purple and gain it, so shadows read as coloured rather than grey. Used for per-unit skin, hair, job and team
    /// colours; the key ramps in <see cref="KeyPalette"/> are fixed constants and don't depend on this.
    /// </summary>
    public static class ColorRamp
    {
        public const int Highlight = 0, Light = 1, Mid = 2, Shadow = 3, Line = 4, Length = 5;

        private const float Warm = 1f / 6f, Cool = 0.72f, GreyHue = 0.62f;

        public static Color32[] From(Color32 mid)
        {
            Color.RGBToHSV(mid, out float h, out float s, out float v);
            bool grey = s < 0.08f;
            Color32 Shade(float target, float shift, float saturation, float value)
            {
                float hue = Toward(grey ? GreyHue : h, target, shift);
                float sat = grey ? Mathf.Max(s * saturation, value < 0.7f * v ? 0.1f : 0.04f) : Mathf.Min(1f, s * saturation);
                return (Color32)Color.HSVToRGB(hue, sat, Mathf.Clamp01(value));
            }

            return new[]
            {
                Shade(Warm, 0.04f, 0.62f, v + (1f - v) * 0.5f),
                Shade(Warm, 0.02f, 0.85f, v + (1f - v) * 0.25f),
                mid,
                Shade(Cool, 0.045f, 1.12f, v * 0.70f),
                Shade(Cool, 0.07f, 1.05f, v * 0.38f),
            };
        }

        /// <summary>Moves hue <paramref name="h"/> up to <paramref name="amount"/> toward <paramref name="target"/> the short way round.</summary>
        private static float Toward(float h, float target, float amount)
        {
            float d = Mathf.Repeat(target - h + 0.5f, 1f) - 0.5f;
            if (Mathf.Abs(d) <= amount)
                return Mathf.Repeat(target, 1f);
            return Mathf.Repeat(h + amount * Mathf.Sign(d), 1f);
        }
    }
}
