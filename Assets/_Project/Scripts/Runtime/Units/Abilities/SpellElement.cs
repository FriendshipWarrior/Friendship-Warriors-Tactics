namespace FriendshipWarriors.Units.Abilities
{
    /// <summary>
    /// A spell's element, after FFT. Tagged on spells now; elemental weaknesses, absorption and resistance from gear
    /// come later. Values are serialized into assets, so append only.
    /// </summary>
    public enum SpellElement
    {
        None = 0,
        Fire = 1,
        Ice = 2,
        Lightning = 3,
    }
}
