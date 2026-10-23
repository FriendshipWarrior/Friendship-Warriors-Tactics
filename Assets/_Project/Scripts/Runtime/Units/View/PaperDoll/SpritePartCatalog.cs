using System;
using FriendshipWarriors.Units.Equipment;
using UnityEngine;

namespace FriendshipWarriors.Units.View.PaperDoll
{
    /// <summary>
    /// Which part sheet each slot uses. Headgear and body gear are chosen by armor type, weapons by weapon type, unless
    /// a specific item has its own part in <see cref="ItemPart"/> overrides. Lives in Resources so units find it at
    /// runtime; created by Friendship Warriors → Art → Generate Paper-Doll Parts.
    /// </summary>
    [CreateAssetMenu(menuName = "Friendship Warriors/Sprite Part Catalog", fileName = "SpritePartCatalog")]
    public sealed class SpritePartCatalog : ScriptableObject
    {
        public const string ResourcePath = "PaperDoll/SpritePartCatalog";

        [Serializable]
        public struct ArmorPart
        {
            [SerializeField] private ArmorType _type;
            [SerializeField] private Texture2D _sheet;
            public ArmorPart(ArmorType type, Texture2D sheet) { _type = type; _sheet = sheet; }
            public ArmorType Type => _type;
            public Texture2D Sheet => _sheet;
        }

        [Serializable]
        public struct WeaponPart
        {
            [SerializeField] private WeaponType _type;
            [SerializeField] private Texture2D _sheet;
            public WeaponPart(WeaponType type, Texture2D sheet) { _type = type; _sheet = sheet; }
            public WeaponType Type => _type;
            public Texture2D Sheet => _sheet;
        }

        [Serializable]
        public struct ItemPart
        {
            [SerializeField] private EquipmentDefinition _item;
            [SerializeField] private Texture2D _sheet;
            public ItemPart(EquipmentDefinition item, Texture2D sheet) { _item = item; _sheet = sheet; }
            public EquipmentDefinition Item => _item;
            public Texture2D Sheet => _sheet;
        }

        [SerializeField] private Texture2D _body;
        [SerializeField] private Texture2D _head;
        [SerializeField] private Texture2D _face;
        [SerializeField] private Texture2D[] _hairStyles = Array.Empty<Texture2D>();
        [SerializeField] private ArmorPart[] _headgear = Array.Empty<ArmorPart>();
        [SerializeField] private ArmorPart[] _bodyGear = Array.Empty<ArmorPart>();
        [SerializeField] private WeaponPart[] _weapons = Array.Empty<WeaponPart>();
        [SerializeField] private Texture2D _shield;
        [Tooltip("Specific items with their own part, overriding the type default.")]
        [SerializeField] private ItemPart[] _itemOverrides = Array.Empty<ItemPart>();

        private static SpritePartCatalog _loaded;

        /// <summary>The catalog in Resources, or null if parts haven't been generated yet.</summary>
        public static SpritePartCatalog Load()
        {
            if (_loaded == null)
                _loaded = Resources.Load<SpritePartCatalog>(ResourcePath);
            return _loaded;
        }

        public int HairStyleCount => _hairStyles.Length;

        public void Define(Texture2D body, Texture2D head, Texture2D face, Texture2D[] hairStyles, ArmorPart[] headgear,
            ArmorPart[] bodyGear, WeaponPart[] weapons, Texture2D shield)
        {
            _body = body;
            _head = head;
            _face = face;
            _hairStyles = hairStyles;
            _headgear = headgear;
            _bodyGear = bodyGear;
            _weapons = weapons;
            _shield = shield;
        }

        /// <summary>The sheet drawn in a slot for this unit, or null for nothing.</summary>
        public Texture2D PartFor(DollSlot slot, Unit unit, int hairStyle)
        {
            switch (slot)
            {
                case DollSlot.Body: return _body;
                case DollSlot.Head: return _head;
                case DollSlot.Face: return _face;
                case DollSlot.Hair: return _hairStyles.Length > 0 ? _hairStyles[Mathf.Clamp(hairStyle, 0, _hairStyles.Length - 1)] : null;
                case DollSlot.Headgear: return Override(unit.Head) ?? Armor(_headgear, unit.Head);
                case DollSlot.BodyGear: return Override(unit.Body) ?? Armor(_bodyGear, unit.Body);
                case DollSlot.Weapon: return Override(unit.Weapon) ?? Weapon(unit.Weapon);
                case DollSlot.Shield: return unit.Shield != null ? Override(unit.Shield) ?? _shield : null;
                default: return null;
            }
        }

        private Texture2D Override(EquipmentDefinition item)
        {
            if (item == null)
                return null;
            foreach (var entry in _itemOverrides)
            {
                if (entry.Item == item && entry.Sheet != null)
                    return entry.Sheet;
            }
            return null;
        }

        private static Texture2D Armor(ArmorPart[] parts, EquipmentDefinition item)
        {
            if (item == null)
                return null;
            foreach (var part in parts)
            {
                if (part.Type == item.ArmorType)
                    return part.Sheet;
            }
            return null;
        }

        private Texture2D Weapon(EquipmentDefinition item)
        {
            if (item == null)
                return null;
            foreach (var part in _weapons)
            {
                if (part.Type == item.WeaponType)
                    return part.Sheet;
            }
            return null;
        }
    }
}
