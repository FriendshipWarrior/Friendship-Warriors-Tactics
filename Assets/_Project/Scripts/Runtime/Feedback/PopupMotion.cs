namespace FriendshipWarriors.Feedback
{
    /// <summary>
    /// The FFT-style path of a popup: it springs up from the unit, drops back with a couple of shrinking bounces,
    /// holds, then fades. Offsets are in canvas pixels (1920x1080 reference) above the popup's anchor.
    /// </summary>
    public static class PopupMotion
    {
        public const float Duration = 1.15f;
        public const float BounceTime = 0.55f;
        public const float FadeTime = 0.3f;
        public const float RestHeight = 18f;
        public const float BounceHeight = 46f;

        /// <summary>Height above the anchor at <paramref name="t"/> seconds after the popup appears.</summary>
        public static float Height(float t)
        {
            if (t <= 0f)
                return RestHeight;
            if (t >= BounceTime)
                return RestHeight;
            float u = t / BounceTime;
            // |sin| gives the arcs, the squared fall-off makes each bounce lower than the one before.
            float arc = System.MathF.Abs(System.MathF.Sin(u * 2.5f * System.MathF.PI));
            return RestHeight + BounceHeight * arc * (1f - u) * (1f - u);
        }

        /// <summary>Opacity: fully shown, then fading over the last <see cref="FadeTime"/>.</summary>
        public static float Alpha(float t)
        {
            if (t < 0f || t >= Duration)
                return 0f;
            float fadeStart = Duration - FadeTime;
            return t <= fadeStart ? 1f : 1f - (t - fadeStart) / FadeTime;
        }

        /// <summary>A quick swell when the popup appears (crits swell more).</summary>
        public static float Scale(float t, float punch = 0.25f)
        {
            const float settle = 0.12f;
            if (t <= 0f)
                return 1f + punch;
            return t >= settle ? 1f : 1f + punch * (1f - t / settle);
        }

        public static bool IsFinished(float t) => t >= Duration;
    }
}
