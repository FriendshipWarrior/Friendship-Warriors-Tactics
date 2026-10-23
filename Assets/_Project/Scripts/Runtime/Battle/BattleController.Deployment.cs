namespace FriendshipWarriors.Battle
{
    public sealed partial class BattleController
    {
        /// <summary>
        /// Stops the current battle without a result and lets go of the battlefield, so another phase (the formation
        /// before a battle) can use the cursor, highlights and HUD. <see cref="Begin"/> starts the next battle.
        /// </summary>
        public void Halt()
        {
            _phase = Phase.Idle;
            _active = null;
            _views.Clear();
            _order = null;
            _pendingResult = null;
            _spellArea.Clear();
            ResetAiTurn();
            _hud.HideMenu();
            _hud.HideTimingPrompt();
            _hud.SetTurnOrder(System.Array.Empty<TurnOrderEntry>(), null);
            _gridView.ClearHighlights();
        }
    }
}
