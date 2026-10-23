using System;
using System.Collections.Generic;
using FriendshipWarriors.Units.Abilities;
using FriendshipWarriors.Units.Equipment;
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

        [Header("Abilities")]
        [Tooltip("Name of this job's command in the Act menu (FFT: Squire's \"Fundaments\", Chemist's \"Items\").")]
        [SerializeField] private string _commandName = "Skills";
        [SerializeField] private AbilityDefinition[] _abilities = Array.Empty<AbilityDefinition>();
        [Tooltip("Passives this job always has, equipped or not (FFT's Chemist always throws items).")]
        [SerializeField] private AbilityDefinition[] _innateAbilities = Array.Empty<AbilityDefinition>();

        [Header("Equipment")]
        [SerializeField] private WeaponType[] _weaponTypes = Array.Empty<WeaponType>();
        [SerializeField] private bool _canUseShields;
        [SerializeField] private ArmorType[] _armorTypes = Array.Empty<ArmorType>();

        [Header("Kind")]
        [Tooltip("Monsters leave an item scaled to their level in their treasure chest, and their steel rod only restores (FFT).")]
        [SerializeField] private bool _isMonster;

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
        public string CommandName => _commandName;
        public IReadOnlyList<WeaponType> WeaponTypes => _weaponTypes;
        public bool CanUseShields => _canUseShields;
        public IReadOnlyList<ArmorType> ArmorTypes => _armorTypes;
        public IReadOnlyList<AbilityDefinition> Abilities => _abilities;
        public IReadOnlyList<AbilityDefinition> InnateAbilities => _innateAbilities;

        public void SetInnateAbilities(AbilityDefinition[] abilities) => _innateAbilities = abilities;

        /// <summary>Whether units of this job are monsters rather than people (no monsters exist yet).</summary>
        public bool IsMonster => _isMonster;

        public void SetMonster(bool isMonster) => _isMonster = isMonster;
        public Color PlaceholderColor => _placeholderColor;
        public Sprite FrontSprite => _frontSprite;
        public Sprite BackSprite => _backSprite;

        /// <summary>Sets every gameplay field at once; used by tooling that generates job assets.</summary>
        public void SetAbilities(string commandName, AbilityDefinition[] abilities)
        {
            _commandName = commandName;
            _abilities = abilities;
        }

        public bool Teaches(AbilityDefinition ability) => Array.IndexOf(_abilities, ability) >= 0;

        public void SetEquipment(WeaponType[] weaponTypes, bool canUseShields)
        {
            _weaponTypes = weaponTypes;
            _canUseShields = canUseShields;
        }

        public void SetArmorTypes(ArmorType[] armorTypes) => _armorTypes = armorTypes;

        /// <summary>
        /// Weapons must be a type this job uses, shields need shield training, headgear and body gear must be an armor
        /// type it wears; accessories suit anyone.
        /// </summary>
        public bool CanEquip(EquipmentDefinition item) => item.Category switch
        {
            EquipmentCategory.Weapon => Array.IndexOf(_weaponTypes, item.WeaponType) >= 0,
            EquipmentCategory.Shield => _canUseShields,
            EquipmentCategory.Head or EquipmentCategory.Body => Array.IndexOf(_armorTypes, item.ArmorType) >= 0,
            _ => true,
        };

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
