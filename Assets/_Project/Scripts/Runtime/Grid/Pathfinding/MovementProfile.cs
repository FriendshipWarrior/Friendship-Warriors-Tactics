using System;

namespace FriendshipWarriors.Grid.Pathfinding
{
    /// <summary>A unit's movement stats.</summary>
    public readonly struct MovementProfile
    {
        /// <summary>Maximum number of tiles moved in one turn.</summary>
        public readonly int Move;

        /// <summary>Maximum height difference, in steps, a unit can climb or drop between adjacent tiles.</summary>
        public readonly int Jump;

        public MovementProfile(int move, int jump)
        {
            if (move < 0) throw new ArgumentOutOfRangeException(nameof(move), move, "Move cannot be negative.");
            if (jump < 0) throw new ArgumentOutOfRangeException(nameof(jump), jump, "Jump cannot be negative.");
            Move = move;
            Jump = jump;
        }
    }
}
