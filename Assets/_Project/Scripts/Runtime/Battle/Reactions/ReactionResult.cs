using FriendshipWarriors.Grid;

namespace FriendshipWarriors.Battle.Reactions
{
    /// <summary>What a reaction did, for messages and moving views.</summary>
    public readonly struct ReactionResult
    {
        public static readonly ReactionResult None = default;

        public readonly bool Triggered;
        public readonly string Message;

        /// <summary>Where a counter-strike knocked the attacker, if it did.</summary>
        public readonly GridCoord? AttackerKnockedTo;

        public ReactionResult(string message, GridCoord? attackerKnockedTo = null)
        {
            Triggered = true;
            Message = message;
            AttackerKnockedTo = attackerKnockedTo;
        }
    }
}
