namespace FriendshipWarriors.Units
{
    public enum UnitChangeKind
    {
        HpLost,
        HpRestored,
        MpLost,
        MpRestored,
        Revived,
    }

    /// <summary>An HP or MP change on a unit, raised as it happens so views can show it (damage numbers, sounds).</summary>
    public readonly struct UnitChange
    {
        public readonly UnitChangeKind Kind;
        public readonly int Amount;

        public UnitChange(UnitChangeKind kind, int amount)
        {
            Kind = kind;
            Amount = amount;
        }

        public override string ToString() => $"{Kind} {Amount}";
    }
}
