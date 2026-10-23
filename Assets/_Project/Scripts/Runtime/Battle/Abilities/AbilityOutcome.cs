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

        public AbilityOutcome(bool hit, int hitChance, int damage = 0, int hpRestored = 0, int mpRestored = 0,
            int physicalAttackGained = 0, bool revived = false)
        {
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
            if (Damage > 0) return $"{Damage} damage";
            if (PhysicalAttackGained > 0) return $"P.Atk +{PhysicalAttackGained}";
            if (HpRestored > 0 && MpRestored > 0) return $"+{HpRestored} HP, +{MpRestored} MP";
            if (MpRestored > 0) return $"+{MpRestored} MP";
            return $"+{HpRestored} HP";
        }
    }
}
