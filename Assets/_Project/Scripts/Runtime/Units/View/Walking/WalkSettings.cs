using UnityEngine;

namespace FriendshipWarriors.Units.View.Walking
{
    /// <summary>Tuning for how movement plays out on screen. Presentation only: rules never read these.</summary>
    [System.Serializable]
    public struct WalkSettings
    {
        [Tooltip("Walking speed. FFT units walk roughly 4–5 tiles a second.")]
        [Min(0.5f)] public float TilesPerSecond;

        [Tooltip("Extra time per h climbed or dropped on a hop, as a fraction of a step.")]
        [Min(0f)] public float HopTimePerH;

        [Tooltip("How far a hop rises above the higher of its two tiles, in world units (1h = 0.5).")]
        [Min(0f)] public float HopHeight;

        [Tooltip("How far a leap rises above the highest tile it passes over, in world units.")]
        [Min(0f)] public float LeapHeight;

        [Tooltip("Seconds a knockback push takes.")]
        [Min(0.01f)] public float PushSeconds;

        [Tooltip("Speed-up while Confirm is held during a walk.")]
        [Min(1f)] public float HurryMultiplier;

        public static WalkSettings Default => new()
        {
            TilesPerSecond = 4.5f,
            HopTimePerH = 0.25f,
            HopHeight = 0.2f,
            LeapHeight = 0.5f,
            PushSeconds = 0.18f,
            HurryMultiplier = 3f,
        };

        /// <summary>Seconds one ordinary step takes.</summary>
        public float StepSeconds => 1f / Mathf.Max(0.5f, TilesPerSecond);
    }
}
