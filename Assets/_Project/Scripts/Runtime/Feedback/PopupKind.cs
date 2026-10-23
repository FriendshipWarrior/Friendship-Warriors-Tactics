namespace FriendshipWarriors.Feedback
{
    /// <summary>What a floating popup over a unit says.</summary>
    public enum PopupKind
    {
        Damage,
        CriticalDamage,
        Heal,
        MpLoss,
        MpRestore,
        Miss,
        Guarded,
        StatusGained,
        StatusLifted,
        Revived,

        /// <summary>What a unit got from a steel rod or chest ("Learned: Grit", "Obtained: Iron Saber").</summary>
        Pickup,
    }
}
