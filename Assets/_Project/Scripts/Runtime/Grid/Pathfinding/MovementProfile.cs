using System;

namespace FriendshipWarriors.Grid.Pathfinding
{
    /// <summary>A unit's movement stats, using FFT's Move and Jump.</summary>
    public readonly struct MovementProfile
    {
        /// <summary>Maximum number of tiles moved in one turn.</summary>
        public readonly int Move;

        /// <summary>Maximum height difference, in h, a unit can climb between tiles. FFT caps Jump at 7.</summary>
        public readonly int Jump;

        public MovementProfile(int move, int jump)
        {
            if (move < 0) throw new ArgumentOutOfRangeException(nameof(move), move, "Move cannot be negative.");
            if (jump < 0) throw new ArgumentOutOfRangeException(nameof(jump), jump, "Jump cannot be negative.");
            Move = move;
            Jump = jump;
        }

        /// <summary>Widest gap, in tiles, a unit can leap across: one tile per 2 Jump, as in FFT.</summary>
        public int HorizontalJump => Jump / 2;

        /// <summary>Jump expressed in the half-h steps tile heights are stored in.</summary>
        public int JumpSteps => Jump * GridMetrics.StepsPerH;
    }
}
