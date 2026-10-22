using System;
using UnityEngine;

namespace FriendshipWarriors.Battle.Combat
{
    /// <summary>
    /// The timed button press (our addition to FFT). A marker sweeps across a bar for <see cref="Duration"/> seconds;
    /// pressing at <see cref="PerfectTime"/> is perfect, and accuracy falls to zero <see cref="Tolerance"/> seconds
    /// either side. The chance it grants grows with accuracy squared, so near-perfect presses matter most, and is
    /// capped below 100%.
    /// </summary>
    [Serializable]
    public struct TimedPressRules
    {
        public static TimedPressRules Default => new()
        {
            _duration = 1.2f,
            _perfectTime = 0.8f,
            _tolerance = 0.3f,
            _maxBlockChance = 0.6f,
            _maxCriticalBonus = 0.45f,
        };

        [SerializeField, Min(0.2f)] private float _duration;
        [SerializeField, Min(0f)] private float _perfectTime;
        [SerializeField, Min(0.01f)] private float _tolerance;
        [Tooltip("Block chance at a perfect press when being attacked. Must stay below 1.")]
        [SerializeField, Range(0f, 0.95f)] private float _maxBlockChance;
        [Tooltip("Critical chance added at a perfect press when attacking, on top of FFT's 5%. The total stays below 1.")]
        [SerializeField, Range(0f, 0.9f)] private float _maxCriticalBonus;

        public float Duration => _duration;
        public float PerfectTime => _perfectTime;
        public float Tolerance => _tolerance;

        /// <summary>The chance never reaches certainty, however perfect the press.</summary>
        public const double ChanceCeiling = 0.95;

        /// <summary>0 (missed the window or no press) to 1 (perfect), from seconds since the prompt appeared.</summary>
        public float Accuracy(float? pressTime)
        {
            if (pressTime is not { } t || t < 0f || t > _duration)
                return 0f;
            return Mathf.Clamp01(1f - Mathf.Abs(t - _perfectTime) / _tolerance);
        }

        public double BlockChance(float accuracy) => Math.Min(ChanceCeiling, _maxBlockChance * accuracy * accuracy);

        public double CriticalChance(float accuracy) =>
            Math.Min(ChanceCeiling, CombatFormulas.BaseCriticalChance + _maxCriticalBonus * accuracy * accuracy);
    }
}
