namespace FriendshipWarriors.Units.Status
{
    /// <summary>
    /// FFT status effects (War of the Lions names where they differ from the PSX ones: Blind = Darkness, Disable =
    /// Don't Act, Immobilize = Don't Move; Panic = Chicken). Values are serialized into assets, so append only.
    /// </summary>
    public enum StatusEffect
    {
        Poison = 0,
        Regen = 1,
        Protect = 2,
        Shell = 3,
        Haste = 4,
        Slow = 5,
        Stop = 6,
        Sleep = 7,
        Blind = 8,
        Silence = 9,
        Disable = 10,
        Immobilize = 11,
        Panic = 12,
    }
}
