using System.Collections.Generic;
using FriendshipWarriors.Units.Abilities;

namespace FriendshipWarriors.Battle.Abilities
{
    /// <summary>A team's shared item stock, as in FFT where every unit draws from the party's items.</summary>
    public sealed class Inventory
    {
        private readonly Dictionary<AbilityDefinition, int> _counts = new();

        public int Count(AbilityDefinition item) => item != null && _counts.TryGetValue(item, out int n) ? n : 0;

        public void Add(AbilityDefinition item, int amount)
        {
            if (item == null || amount <= 0)
                return;
            _counts[item] = Count(item) + amount;
        }

        /// <summary>Uses one of the item. Returns false (changing nothing) when none are left.</summary>
        public bool TryConsume(AbilityDefinition item)
        {
            int n = Count(item);
            if (n <= 0)
                return false;
            _counts[item] = n - 1;
            return true;
        }
    }
}
