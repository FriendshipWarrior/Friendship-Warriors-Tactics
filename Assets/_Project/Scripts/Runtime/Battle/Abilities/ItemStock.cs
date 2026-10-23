using System;
using FriendshipWarriors.Units.Abilities;
using UnityEngine;

namespace FriendshipWarriors.Battle.Abilities
{
    /// <summary>An item and how many of it, for seeding inventories from the Inspector.</summary>
    [Serializable]
    public struct ItemStock
    {
        [SerializeField] private AbilityDefinition _item;
        [SerializeField, Min(0)] private int _count;

        public ItemStock(AbilityDefinition item, int count)
        {
            _item = item;
            _count = count;
        }

        public AbilityDefinition Item => _item;
        public int Count => _count;
    }
}
