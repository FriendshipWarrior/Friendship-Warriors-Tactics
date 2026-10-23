namespace FriendshipWarriors.Battle.Objectives
{
    /// <summary>Where a battle stands, with the reason shown to the player when it ends.</summary>
    public readonly struct BattleOutcome
    {
        public static readonly BattleOutcome Ongoing = new(BattleOutcomeKind.Ongoing, "");

        public readonly BattleOutcomeKind Kind;
        public readonly string Reason;

        public BattleOutcome(BattleOutcomeKind kind, string reason)
        {
            Kind = kind;
            Reason = reason;
        }

        public bool IsOver => Kind != BattleOutcomeKind.Ongoing;

        public static BattleOutcome Victory(string reason) => new(BattleOutcomeKind.Victory, reason);
        public static BattleOutcome Defeat(string reason) => new(BattleOutcomeKind.Defeat, reason);

        public override string ToString() => IsOver ? $"{Kind}: {Reason}" : "Ongoing";
    }
}
