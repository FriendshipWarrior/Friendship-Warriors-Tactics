using System;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Units;

namespace FriendshipWarriors.Battle
{
    /// <summary>
    /// A move the player hasn't confirmed yet (our addition; FFT has no undo). The unit stands on the destination so the
    /// view and highlights can show it, but the turn hasn't spent its Move and nothing the move would trigger (taking a
    /// drop) has happened. <see cref="Confirm"/> makes it a real move; <see cref="Cancel"/> puts the unit back exactly
    /// as it was. Only confirmed moves are battle commands, so a match replays from those alone.
    /// </summary>
    public sealed class TentativeMove
    {
        public TentativeMove(Unit unit, TurnState turn, GridCoord destination, Direction facing)
        {
            if (!turn.CanMove)
                throw new InvalidOperationException("No move left this turn.");
            Unit = unit;
            Turn = turn;
            Origin = unit.Coord;
            OriginFacing = unit.Facing;
            Destination = destination;
            unit.Coord = destination;
            unit.Facing = facing;
        }

        public Unit Unit { get; }
        public TurnState Turn { get; }
        public GridCoord Origin { get; }
        public Direction OriginFacing { get; }
        public GridCoord Destination { get; }

        /// <summary>Confirmed or cancelled; either way it can't be used again.</summary>
        public bool IsSettled { get; private set; }

        /// <summary>The move counts: the turn spends its Move (or, with Double Time, the Act).</summary>
        public void Confirm()
        {
            if (IsSettled)
                throw new InvalidOperationException("This move was already settled.");
            Turn.RecordMove();
            IsSettled = true;
        }

        /// <summary>Back to the starting tile and facing, with the Move unspent.</summary>
        public void Cancel()
        {
            if (IsSettled)
                throw new InvalidOperationException("This move was already settled.");
            Unit.Coord = Origin;
            Unit.Facing = OriginFacing;
            IsSettled = true;
        }
    }
}
