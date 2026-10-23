using System;

namespace FriendshipWarriors.Battle
{
    /// <summary>
    /// What the active unit may still do this turn. As in FFT, a turn has one Move and one Act in either order, and
    /// Wait ends it. Turn-trade abilities (our addition) let a spent part be stood in for by the unused one: attack a
    /// second time by giving up the Move (Rapid Fire: the first act can be anything, the second only Attack), or move
    /// twice by giving up the Act (Double Time).
    /// </summary>
    public sealed class TurnState
    {
        private readonly bool _canTradeMoveForAct;
        private readonly bool _canTradeActForMove;
        private readonly bool _moveBlocked;
        private readonly bool _actBlocked;
        private bool _concluded;
        private bool _doneActing;

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

        public bool CanMove => !_concluded && !_moveBlocked && (!MoveSpent || (_canTradeActForMove && !ActSpent));
        public bool CanAct => !_concluded && !_doneActing && !_actBlocked && (!ActSpent || (_canTradeMoveForAct && !MoveSpent));

        /// <summary>
        /// Ends what the unit can still do without spending it (CT is charged only for what was used). Starting a
        /// charged spell does this: the caster stands and concentrates until it resolves.
        /// </summary>
        public void Conclude() => _concluded = true;

        /// <summary>
        /// No more acting this turn, but an unused Move stays (starting a charged spell: the caster can still move, then
        /// stands charging; a second act would cancel the spell, so trades can't offer one).
        /// </summary>
        public void StopActing() => _doneActing = true;

        /// <summary>The next Move would use up the Act.</summary>
        public bool NextMoveUsesAct => MoveSpent && CanMove;

        /// <summary>The next Act would use up the Move.</summary>
        public bool NextActUsesMove => ActSpent && CanAct;

        /// <summary>Rapid Fire (user decision, 2026-10-23): the act bought with the Move can only be Attack.</summary>
        public bool NextActIsAttackOnly => NextActUsesMove;

        /// <summary>Whether an act of this kind is allowed now: any act normally, only Attack as Rapid Fire's second.</summary>
        public bool CanActWith(bool isAttack) => CanAct && (isAttack || !NextActIsAttackOnly);

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

        /// <param name="isAttack">The act is Attack (the only act Rapid Fire's second act may be).</param>
        public void RecordAct(bool isAttack = false)
        {
            if (!CanAct)
                throw new InvalidOperationException("No action left this turn.");
            if (!isAttack && NextActIsAttackOnly)
                throw new InvalidOperationException("Rapid Fire: the second act can only be Attack.");
            if (!ActSpent)
                ActSpent = true;
            else
                MoveSpent = true;
            ActsTaken++;
        }
    }
}
