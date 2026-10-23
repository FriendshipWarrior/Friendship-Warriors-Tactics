using System;
using FriendshipWarriors.Units.Abilities;
using UnityEngine;

namespace FriendshipWarriors.Battle.Drops
{
    /// <summary>One row of the monster chest table: monsters of at least this level drop this item.</summary>
    [Serializable]
    public struct MonsterLootTier
    {
        [SerializeField, Min(1)] private int _minLevel;
        [SerializeField] private AbilityDefinition _item;

        public MonsterLootTier(int minLevel, AbilityDefinition item)
        {
            _minLevel = minLevel;
            _item = item;
        }

        public int MinLevel => _minLevel;
        public AbilityDefinition Item => _item;
    }
}
