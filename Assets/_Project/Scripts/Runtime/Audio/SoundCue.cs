namespace FriendshipWarriors.Audio
{
    /// <summary>Game sounds, by what they mean rather than which clip plays. Serialized into assets, so append only.</summary>
    public enum SoundCue
    {
        None = 0,
        Hit = 1,
        CriticalHit = 2,
        Miss = 3,
        Block = 4,
        Heal = 5,
        CastStart = 6,
        SpellResolve = 7,
        KnockOut = 8,
        MenuMove = 9,
        MenuConfirm = 10,
        MenuCancel = 11,
        TimedPressSuccess = 12,
        TimedPressFail = 13,
        StatusGained = 14,
        Revive = 15,
        DropAppears = 16,
        RodTaken = 17,
        ChestOpened = 18,
    }
}
