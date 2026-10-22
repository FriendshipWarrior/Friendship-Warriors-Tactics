using System;

namespace FriendshipWarriors.Grid.Pathfinding
{
    /// <summary>Battle-wide movement tuning, as opposed to per-unit stats in <see cref="MovementProfile"/>.</summary>
    public readonly struct MovementRules
    {
        public static readonly MovementRules Default = new(enemyLeapClearance: 2);

        /// <summary>
        /// How much higher, in h, the take-off tile must be than an enemy's tile for a unit to leap over that enemy.
        /// Empty gaps only need to be lower; an enemy is treated as an obstacle that needs this much clearance.
        /// </summary>
        public readonly int EnemyLeapClearance;

        public MovementRules(int enemyLeapClearance)
        {
            if (enemyLeapClearance < 0)
                throw new ArgumentOutOfRangeException(nameof(enemyLeapClearance), enemyLeapClearance, "Clearance cannot be negative.");
            EnemyLeapClearance = enemyLeapClearance;
        }

        public int EnemyLeapClearanceSteps => EnemyLeapClearance * GridMetrics.StepsPerH;
    }
}
