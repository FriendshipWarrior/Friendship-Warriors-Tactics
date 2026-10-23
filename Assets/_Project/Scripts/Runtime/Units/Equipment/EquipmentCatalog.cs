using System;
using System.Collections.Generic;
using UnityEngine;

namespace FriendshipWarriors.Units.Equipment
{
    /// <summary>Every piece of equipment in the game, for systems that pick gear (quick play rosters, shops later).</summary>
    [CreateAssetMenu(menuName = "Friendship Warriors/Equipment Catalog", fileName = "EquipmentCatalog")]
    public sealed class EquipmentCatalog : ScriptableObject
    {
        [SerializeField] private EquipmentDefinition[] _items = Array.Empty<EquipmentDefinition>();

        public IReadOnlyList<EquipmentDefinition> Items => _items;

        public void SetItems(EquipmentDefinition[] items) => _items = items;
    }
}
