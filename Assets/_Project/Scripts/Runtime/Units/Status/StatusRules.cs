namespace FriendshipWarriors.Units.Status
{
    /// <summary>
    /// What each status does and how long it lasts, after FFT (durations in clock ticks, from Final Fantasy Wiki and
    /// Game8's FFT status pages). Where sources disagree or say nothing, the choice is ours and marked here and in
    /// CLAUDE.md.
    /// </summary>
    public static class StatusRules
    {
        /// <summary>Duration value meaning "until cured (or the battle ends)".</summary>
        public const int UntilCured = -1;

        /// <summary>HP lost to Poison at the end of the unit's turn: max HP / 16. FFT is usually cited as / 8; we start
        /// gentler (user decision) and can raise it later.</summary>
        public const int PoisonDivisor = 16;

        /// <summary>HP healed by Regen at the end of the unit's turn: max HP / 8 (FFT).</summary>
        public const int RegenDivisor = 8;

        /// <summary>
        /// Statuses measured in the unit's own turns instead of clock ticks, and how many. Silence lasts 2 turns (user
        /// decision; FFT's lasts until cured).
        /// </summary>
        public static int? TurnDuration(StatusEffect status) => status switch
        {
            StatusEffect.Silence => 2,
            _ => null,
        };

        public static int DefaultDuration(StatusEffect status) => status switch
        {
            StatusEffect.Poison => 36,
            StatusEffect.Regen => 32,
            StatusEffect.Protect => 32,
            StatusEffect.Shell => 32,
            StatusEffect.Haste => 32,
            StatusEffect.Slow => 32,
            StatusEffect.Stop => 20,
            StatusEffect.Sleep => 60,
            StatusEffect.Disable => 24,
            StatusEffect.Immobilize => 24,
            // Blind lasts until cured (PSX); Silence counts the unit's turns instead (see TurnDuration); Panic lasts
            // while Morale stays below 10.
            _ => UntilCured,
        };

        /// <summary>Statuses that cancel each other when applied (FFT: Haste removes Slow and vice versa).</summary>
        public static StatusEffect? Opposite(StatusEffect status) => status switch
        {
            StatusEffect.Haste => StatusEffect.Slow,
            StatusEffect.Slow => StatusEffect.Haste,
            _ => null,
        };

        public static bool PreventsMove(StatusEffect status) =>
            status is StatusEffect.Stop or StatusEffect.Sleep or StatusEffect.Immobilize;

        /// <summary>Panic stops a unit acting: it only runs (FFT Chicken).</summary>
        public static bool PreventsAct(StatusEffect status) =>
            status is StatusEffect.Stop or StatusEffect.Sleep or StatusEffect.Disable or StatusEffect.Panic;

        /// <summary>A unit under these can't evade (FFT: sleeping or stopped units have no evasion).</summary>
        public static bool RemovesEvasion(StatusEffect status) => status is StatusEffect.Stop or StatusEffect.Sleep;

        /// <summary>Reactions don't fire while asleep, stopped, disabled (FFT) or panicking (our choice).</summary>
        public static bool BlocksReactions(StatusEffect status) =>
            status is StatusEffect.Stop or StatusEffect.Sleep or StatusEffect.Disable or StatusEffect.Panic;

        /// <summary>Physical damage taken ×1.5 while asleep or panicking (FFT: Sleep and Chicken targets).</summary>
        public static bool TakesExtraPhysicalDamage(StatusEffect status) => status is StatusEffect.Sleep or StatusEffect.Panic;

        /// <summary>Short label for the over-head indicator.</summary>
        public static string Abbreviation(StatusEffect status) => status switch
        {
            StatusEffect.Poison => "PSN",
            StatusEffect.Regen => "RGN",
            StatusEffect.Protect => "PRT",
            StatusEffect.Shell => "SHL",
            StatusEffect.Haste => "HST",
            StatusEffect.Slow => "SLW",
            StatusEffect.Stop => "STP",
            StatusEffect.Sleep => "SLP",
            StatusEffect.Blind => "BLD",
            StatusEffect.Silence => "SIL",
            StatusEffect.Disable => "DIS",
            StatusEffect.Immobilize => "IMB",
            _ => "PNC",
        };

        public static bool IsHarmful(StatusEffect status) =>
            status is not (StatusEffect.Regen or StatusEffect.Protect or StatusEffect.Shell or StatusEffect.Haste);
    }
}
