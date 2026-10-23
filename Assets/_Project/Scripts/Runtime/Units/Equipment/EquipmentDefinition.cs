using UnityEngine;
using UnityEngine.Serialization;

namespace FriendshipWarriors.Units.Equipment
{
    /// <summary>
    /// A weapon, shield, headgear, body gear or accessory. Weapons bring Weapon Power and reach; shields and accessories
    /// bring evasion (S-Ev and A-Ev), which only counts against attacks from the directions FFT allows; headgear and
    /// body gear add to max HP and MP, as in FFT.
    /// </summary>
    [CreateAssetMenu(menuName = "Friendship Warriors/Equipment", fileName = "Equipment")]
    public sealed class EquipmentDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName = "Equipment";
        [SerializeField, TextArea] private string _description;
        [FormerlySerializedAs("_slot")]
        [SerializeField] private EquipmentCategory _category;

        [Header("Weapon")]
        [SerializeField] private WeaponType _weaponType = WeaponType.Sword;
        [Tooltip("Weapon Power (WP).")]
        [SerializeField, Min(0)] private int _power = 4;
        [SerializeField, Min(1)] private int _minRange = 1;
        [SerializeField, Min(1)] private int _maxRange = 1;
        [Tooltip("Largest height difference, in h, the weapon can reach.")]
        [SerializeField, Min(0)] private int _verticalReach = 3;
        [Tooltip("Needs both hands, leaving the off hand empty (FFT bows and similar).")]
        [SerializeField] private bool _twoHanded;
        [Tooltip("W-Ev, in percent: only counts for a unit with a Parry-type reaction, against front and side physical attacks (FFT).")]
        [SerializeField, Range(0, 100)] private int _weaponEvasion;

        [Header("Shield / Accessory")]
        [Tooltip("S-Ev for shields, A-Ev for accessories, in percent.")]
        [SerializeField, Range(0, 100)] private int _evasion;

        [Header("Headgear / Body Gear")]
        [SerializeField] private ArmorType _armorType;
        [SerializeField, Min(0)] private int _hpBonus;
        [SerializeField, Min(0)] private int _mpBonus;

        public string DisplayName => _displayName;
        public string Description => _description;
        public EquipmentCategory Category => _category;
        public WeaponType WeaponType => _weaponType;
        public int Power => _power;
        public int MinRange => _minRange;
        public int MaxRange => _maxRange;
        public int VerticalReach => _verticalReach;
        public bool TwoHanded => _twoHanded;
        public int WeaponEvasion => _weaponEvasion;

        public void SetWeaponEvasion(int weaponEvasion) => _weaponEvasion = weaponEvasion;
        public int Evasion => _evasion;
        public ArmorType ArmorType => _armorType;
        public int HpBonus => _hpBonus;
        public int MpBonus => _mpBonus;

        public void DefineWeapon(string displayName, string description, WeaponType type, int power, int minRange, int maxRange,
            int verticalReach, bool twoHanded = false)
        {
            _displayName = displayName;
            _description = description;
            _category = EquipmentCategory.Weapon;
            _twoHanded = twoHanded;
            _weaponType = type;
            _power = power;
            _minRange = minRange;
            _maxRange = maxRange;
            _verticalReach = verticalReach;
        }

        /// <summary>Defines a shield or accessory (evasion only).</summary>
        public void DefineArmor(string displayName, string description, EquipmentCategory category, int evasion)
        {
            _displayName = displayName;
            _description = description;
            _category = category;
            _evasion = evasion;
        }

        /// <summary>Defines headgear or body gear.</summary>
        public void DefineGear(string displayName, string description, EquipmentCategory category, ArmorType armorType, int hpBonus, int mpBonus)
        {
            _displayName = displayName;
            _description = description;
            _category = category;
            _armorType = armorType;
            _hpBonus = hpBonus;
            _mpBonus = mpBonus;
        }
    }
}
