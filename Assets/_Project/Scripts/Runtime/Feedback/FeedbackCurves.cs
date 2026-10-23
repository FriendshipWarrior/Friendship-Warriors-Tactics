namespace FriendshipWarriors.Feedback
{
    /// <summary>
    /// Time curves for hit reactions, in battle seconds since the hit: the white flash (two quick blinks, as FFT units
    /// blink when struck), the sideways knock-shake, a soft glow for healing and the camera shake on criticals.
    /// </summary>
    public static class FeedbackCurves
    {
        public const float FlashTime = 0.2f;
        public const float ShakeTime = 0.25f;
        public const float GlowTime = 0.6f;
        public const float KnockOutFlashTime = 0.35f;

        /// <summary>White flash strength 0–1: on, off, on again, then gone.</summary>
        public static float Flash(float t)
        {
            if (t < 0f || t >= FlashTime)
                return 0f;
            if (t < 0.06f)
                return 0.9f;
            if (t < 0.11f)
                return 0f;
            return 0.75f * (1f - (t - 0.11f) / (FlashTime - 0.11f));
        }

        /// <summary>Sideways offset (world units) of a struck sprite: a fast wobble that dies out.</summary>
        public static float Shake(float t, float amplitude = 0.07f)
        {
            if (t < 0f || t >= ShakeTime)
                return 0f;
            float decay = 1f - t / ShakeTime;
            return amplitude * decay * System.MathF.Sin(t * 70f);
        }

        /// <summary>Strength 0–1 of the soft heal glow: rises and falls once.</summary>
        public static float Glow(float t)
        {
            if (t < 0f || t >= GlowTime)
                return 0f;
            return 0.7f * System.MathF.Sin(t / GlowTime * System.MathF.PI);
        }

        /// <summary>The longer flash as a unit drops: bright, then fading into the knocked-out grey.</summary>
        public static float KnockOutFlash(float t)
        {
            if (t < 0f || t >= KnockOutFlashTime)
                return 0f;
            return 1f - t / KnockOutFlashTime;
        }

        /// <summary>A drop fades in this long after its unit's ghost starts rising.</summary>
        public const float DropDelay = 0.6f, DropFadeIn = 0.35f;

        /// <summary>How visible a new drop is, 0–1, <paramref name="t"/> seconds after its unit was lost.</summary>
        public static float DropAppear(float t) =>
            t <= DropDelay ? 0f : System.Math.Min(1f, (t - DropDelay) / DropFadeIn);

        /// <summary>The steel rod hovers: a slow bob in world units.</summary>
        public static float RodBob(float t) => 0.06f * System.MathF.Sin(t * 2.2f);

        /// <summary>The rod's glow breathes between 0.45 and 1 about once every two seconds.</summary>
        public static float RodGlow(float t) => 0.725f + 0.275f * System.MathF.Sin(t * 3.1f);

        /// <summary>
        /// Camera shake offset (x, y in camera space, world units) <paramref name="t"/> seconds into a shake lasting
        /// <paramref name="duration"/>. Two detuned sines rather than random noise, so it's repeatable.
        /// </summary>
        public static (float x, float y) CameraShake(float t, float duration, float amplitude)
        {
            if (t < 0f || t >= duration || duration <= 0f)
                return (0f, 0f);
            float decay = 1f - t / duration;
            decay *= decay;
            return (amplitude * decay * System.MathF.Sin(t * 83f), amplitude * decay * System.MathF.Sin(t * 61f + 1.3f));
        }
    }
}
