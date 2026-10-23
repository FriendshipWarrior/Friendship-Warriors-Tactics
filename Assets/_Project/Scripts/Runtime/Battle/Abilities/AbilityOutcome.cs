using System.Collections.Generic;
using FriendshipWarriors.Units.Status;

namespace FriendshipWarriors.Battle.Abilities
{
    /// <summary>What an ability did, for messages and rewards.</summary>
    public readonly struct AbilityOutcome
    {
        public readonly bool Hit;
        public readonly int HitChance;
        public readonly int Damage;
        public readonly int HpRestored;
        public readonly int MpRestored;
        public readonly int PhysicalAttackGained;
        public readonly bool Revived;

        /// <summary>Statuses the ability removes (Cure Status), and how many the target actually had.</summary>
        public readonly IReadOnlyList<StatusEffect> Cures;
        public readonly int CuredCount;

        public AbilityOutcome(bool hit, int hitChance, int damage = 0, int hpRestored = 0, int mpRestored = 0,
            int physicalAttackGained = 0, bool revived = false, IReadOnlyList<StatusEffect> cures = null, int curedCount = 0)
        {
            Cures = cures;
            CuredCount = curedCount;
            Hit = hit;
            HitChance = hitChance;
            Damage = damage;
            HpRestored = hpRestored;
            MpRestored = mpRestored;
            PhysicalAttackGained = physicalAttackGained;
            Revived = revived;
        }

        public override string ToString()
        {
            if (!Hit) return "Miss";
            if (Revived) return $"revived with {HpRestored} HP";
            if (Cures != null) return CuredCount > 0 ? $"cured {CuredCount} status{(CuredCount == 1 ? "" : "es")}" : "nothing to cure";
            if (Damage > 0) return $"{Damage} damage";
            if (PhysicalAttackGained > 0) return $"P.Atk +{PhysicalAttackGained}";
            if (HpRestored > 0 && MpRestored > 0) return $"+{HpRestored} HP, +{MpRestored} MP";
            if (MpRestored > 0) return $"+{MpRestored} MP";
            return $"+{HpRestored} HP";
        }
    }
}
