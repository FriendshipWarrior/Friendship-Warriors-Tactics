using System.Collections.Generic;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Status;

namespace FriendshipWarriors.Battle.Magic
{
    /// <summary>What a spell did to one unit in its area.</summary>
    public sealed class SpellTargetResult
    {
        public Unit Target { get; set; }
        public bool Hit { get; set; }
        public int HitChance { get; set; }
        public int Damage { get; set; }
        public int Healed { get; set; }
        public bool Revived { get; set; }
        public bool WasKnockedOut { get; set; }
        public List<StatusEffect> Added { get; } = new();
        public List<StatusEffect> Removed { get; } = new();

        public override string ToString()
        {
            if (!Hit) return $"{Target.Name} resists";
            var parts = new List<string>();
            if (Damage > 0) parts.Add($"{Damage} damage{(Target.IsKnockedOut ? " (down!)" : "")}");
            if (Revived) parts.Add($"revived with {Healed} HP");
            else if (Healed > 0) parts.Add($"+{Healed} HP");
            if (Added.Count > 0) parts.Add(string.Join("/", Added));
            if (Removed.Count > 0) parts.Add("cured " + string.Join("/", Removed));
            return $"{Target.Name}: {(parts.Count > 0 ? string.Join(", ", parts) : "no effect")}";
        }
    }
}
