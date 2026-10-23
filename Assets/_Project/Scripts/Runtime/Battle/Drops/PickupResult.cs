using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;
using FriendshipWarriors.Units.Equipment;

namespace FriendshipWarriors.Battle.Drops
{
    /// <summary>What taking a drop did.</summary>
    public sealed class PickupResult
    {
        public PickupResult(BattleDrop drop, Unit picker, PickupChoice choice)
        {
            Drop = drop;
            Picker = picker;
            Choice = choice;
        }

        public BattleDrop Drop { get; }
        public Unit Picker { get; }
        public PickupChoice Choice { get; }
        public List<AbilityDefinition> Learned { get; } = new();
        public int HpRestored { get; set; }
        public int MpRestored { get; set; }
        public EquipmentDefinition Gear { get; set; }
        public AbilityDefinition Item { get; set; }

        /// <summary>Short text for a popup over the picker ("Learned: Grit, Shoulder Charge", "Obtained: Iron Saber").</summary>
        public string Summary =>
            Gear != null ? $"Obtained: {Gear.DisplayName}"
            : Item != null ? $"Obtained: {Item.DisplayName}"
            : Choice == PickupChoice.Inherit ? (Learned.Count > 0 ? $"Learned: {string.Join(", ", Learned.Select(a => a.DisplayName))}" : "Nothing learned")
            : Choice == PickupChoice.Restore ? "HP/MP restored"
            : "Empty";

        /// <summary>
        /// The popup over the picker: like <see cref="Summary"/>, but a long list of learned abilities is cut to the
        /// first two and a count, so it fits over the unit.
        /// </summary>
        public string PopupText =>
            Choice == PickupChoice.Inherit && Learned.Count > 3
                ? $"Learned: {Learned[0].DisplayName}, {Learned[1].DisplayName} +{Learned.Count - 2} more"
                : Summary;

        /// <summary>The battle banner line.</summary>
        public string Message => $"{Picker.Name} takes {Drop.FallenName}'s {Drop.DisplayName.ToLowerInvariant()}: {Summary}";
    }
}
