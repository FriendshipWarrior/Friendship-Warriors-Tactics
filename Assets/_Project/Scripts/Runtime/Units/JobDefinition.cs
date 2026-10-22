using System;
using System.Collections.Generic;
using UnityEngine;

namespace FriendshipWarriors.Units
{
    /// <summary>
    /// A job (FFT's term for a class). Holds the stat multipliers and growth constants that shape a unit's numbers,
    /// its Move, Jump and class evasion, and what it takes to unlock. Art is optional; without it a generated
    /// placeholder in <see cref="PlaceholderColor"/> is used.
    /// </summary>
    [CreateAssetMenu(menuName = "Friendship Warriors/Job", fileName = "Job")]
    public sealed class JobDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName = "Job";
        [SerializeField, TextArea] private string _description;

        [Header("Stats (FFT model)")]
        [Tooltip("Percent of raw stats shown for this job. Higher is better.")]
        [SerializeField] private StatFactors _multipliers = new(100, 100, 100, 100, 100);
        [Tooltip("Level-up growth: raw += raw / (constant + level). Lower is better.")]
        [SerializeField] private StatFactors _growth = new(11, 15, 100, 60, 50);
        [SerializeField, Min(0)] private int _move = 3;
        [SerializeField, Range(0, 7)] private int _jump = 3;
        [SerializeField, Range(0, 100)] private int _classEvasion = 5;

        [Header("Unlock")]
        [SerializeField] private JobRequirement[] _requirements = Array.Empty<JobRequirement>();

        [Header("Look")]
        [SerializeField] private Color _placeholderColor = Color.gray;
        [Tooltip("Front three-quarter pose, facing screen-right. Mirrored for the other side.")]
        [SerializeField] private Sprite _frontSprite;
        [Tooltip("Back three-quarter pose, facing screen-right. Mirrored for the other side.")]
        [SerializeField] private Sprite _backSprite;

        public string DisplayName => _displayName;
        public string Description => _description;
        public StatFactors Multipliers => _multipliers;
        public StatFactors Growth => _growth;
        public int Move => _move;
        public int Jump => _jump;
        public int ClassEvasion => _classEvasion;
        public IReadOnlyList<JobRequirement> Requirements => _requirements;
        public Color PlaceholderColor => _placeholderColor;
        public Sprite FrontSprite => _frontSprite;
        public Sprite BackSprite => _backSprite;

        /// <summary>Sets every gameplay field at once; used by tooling that generates job assets.</summary>
        public void Define(string displayName, string description, StatFactors multipliers, StatFactors growth,
            int move, int jump, int classEvasion, JobRequirement[] requirements, Color placeholderColor)
        {
            _displayName = displayName;
            _description = description;
            _multipliers = multipliers;
            _growth = growth;
            _move = move;
            _jump = jump;
            _classEvasion = classEvasion;
            _requirements = requirements;
            _placeholderColor = placeholderColor;
        }
    }
}
