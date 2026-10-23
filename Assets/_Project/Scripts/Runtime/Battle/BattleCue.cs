namespace FriendshipWarriors.Battle
{
    /// <summary>
    /// Moments the battle announces for presentation (popups, sounds) that a unit's HP and statuses don't show by
    /// themselves. HP and MP changes come from <see cref="Units.Unit.Changed"/>; statuses are read off the units.
    /// </summary>
    public enum BattleCue
    {
        /// <summary>An attack, ability or spell missed (or was evaded or resisted) this unit.</summary>
        Miss,

        /// <summary>The unit blocked an attack with the timed press.</summary>
        Guarded,

        /// <summary>The next damage this unit takes is a critical hit (raised just before the damage).</summary>
        Critical,

        /// <summary>The unit starts casting (charged or instant).</summary>
        CastStarted,

        /// <summary>A spell resolves; the unit is the caster.</summary>
        SpellResolved,

        /// <summary>A timed press landed inside the zone (the unit is the one whose turn it is).</summary>
        TimedPressSuccess,

        /// <summary>A timed press missed the zone or wasn't made.</summary>
        TimedPressFail,

        /// <summary>Menu cursor moved (no unit).</summary>
        MenuMove,

        /// <summary>Confirm pressed in a menu or on a tile (no unit).</summary>
        MenuConfirm,

        /// <summary>Cancel / back pressed (no unit).</summary>
        MenuCancel,

        /// <summary>
        /// Reactions to what just happened are about to resolve (no unit). Their feedback comes a beat later, as in
        /// FFT, where a counter-attack follows the hit that provoked it.
        /// </summary>
        Reactions,
    }
}
