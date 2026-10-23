using System.Collections.Generic;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Equipment;

namespace FriendshipWarriors.Battle.Drops
{
    /// <summary>
    /// Gear each team found in chests this battle. Shown when the battle ends; the campaign will add it to the
    /// party's stock (Quick Play just shows it).
    /// </summary>
    public sealed class Spoils
    {
        private readonly Dictionary<Team, List<EquipmentDefinition>> _gear = new();

        public IReadOnlyList<EquipmentDefinition> Of(Team team) =>
            _gear.TryGetValue(team, out var list) ? list : (IReadOnlyList<EquipmentDefinition>)System.Array.Empty<EquipmentDefinition>();

        public void Add(Team team, EquipmentDefinition item)
        {
            if (item == null)
                return;
            if (!_gear.TryGetValue(team, out var list))
                _gear[team] = list = new List<EquipmentDefinition>();
            list.Add(item);
        }

        public void Clear() => _gear.Clear();
    }
}
