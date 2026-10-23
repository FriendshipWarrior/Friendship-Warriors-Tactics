using System.Collections.Generic;
using FriendshipWarriors.Units;

namespace FriendshipWarriors.Battle.AI
{
    /// <summary>
    /// What the battle's objective means for the AI: units worth extra effort to attack (the player's protected
    /// characters) and whether the acting unit should play safe (it is the player's target).
    /// </summary>
    public sealed class AiHints
    {
        /// <summary>Attack scores against priority targets are multiplied by this.</summary>
        public const double PriorityTargetWeight = 1.5;

        /// <summary>A cautious unit weighs the threat of standing somewhere this many times more.</summary>
        public const double CautiousThreatWeight = 2.0;

        public static readonly AiHints None = new(new HashSet<Unit>(), cautious: false);

        public AiHints(IReadOnlyCollection<Unit> priorityTargets, bool cautious)
        {
            PriorityTargets = priorityTargets;
            Cautious = cautious;
        }

        public IReadOnlyCollection<Unit> PriorityTargets { get; }
        public bool Cautious { get; }

        public bool IsPriority(Unit unit)
        {
            foreach (var target in PriorityTargets)
            {
                if (target == unit)
                    return true;
            }
            return false;
        }
    }
}
