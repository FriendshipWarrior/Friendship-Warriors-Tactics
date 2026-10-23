using System;

namespace FriendshipWarriors.Battle
{
    /// <summary>
    /// What the active unit may still do this turn. As in FFT, a turn has one Move and one Act in either order, and
    /// Wait ends it. Turn-trade abilities (our addition) let a spent part be stood in for by the unused one: act twice
    /// by giving up the Move, or move twice by giving up the Act.
    /// </summary>
    public sealed class TurnState
    {
        private readonly bool _canTradeMoveForAct;
        private readonly bool _canTradeActForMove;
        private readonly bool _moveBlocked;
        private readonly bool _actBlocked;

        /// <param name="moveBlocked">A status forbids moving this turn (Immobilize, Sleep, Stop).</param>
        /// <param name="actBlocked">A status forbids acting this turn (Disable, Sleep, Stop, Panic).</param>
        public TurnState(bool canTradeMoveForAct = false, bool canTradeActForMove = false, bool moveBlocked = false, bool actBlocked = false)
        {
            _canTradeMoveForAct = canTradeMoveForAct;
            _canTradeActForMove = canTradeActForMove;
            _moveBlocked = moveBlocked;
            _actBlocked = actBlocked;
        }

        /// <summary>The turn's Move has been used, by moving or by a second action.</summary>
        public bool MoveSpent { get; private set; }

        /// <summary>The turn's Act has been used, by acting or by a second move.</summary>
        public bool ActSpent { get; private set; }

        public int MovesTaken { get; private set; }
        public int ActsTaken { get; private set; }

        public bool CanMove => !_moveBlocked && (!MoveSpent || (_canTradeActForMove && !ActSpent));
        public bool CanAct => !_actBlocked && (!ActSpent || (_canTradeMoveForAct && !MoveSpent));

        /// <summary>The next Move would use up the Act.</summary>
        public bool NextMoveUsesAct => MoveSpent && CanMove;

        /// <summary>The next Act would use up the Move.</summary>
        public bool NextActUsesMove => ActSpent && CanAct;

        public bool IsFinished => !CanMove && !CanAct;

        public void RecordMove()
        {
            if (!CanMove)
                throw new InvalidOperationException("No move left this turn.");
            if (!MoveSpent)
                MoveSpent = true;
            else
                ActSpent = true;
            MovesTaken++;
        }

        public void RecordAct()
        {
            if (!CanAct)
                throw new InvalidOperationException("No action left this turn.");
            if (!ActSpent)
                ActSpent = true;
            else
                MoveSpent = true;
            ActsTaken++;
        }
    }
}
