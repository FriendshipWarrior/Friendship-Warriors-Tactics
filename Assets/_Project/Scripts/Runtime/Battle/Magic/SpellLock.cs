namespace FriendshipWarriors.Battle.Magic
{
    /// <summary>
    /// What a charged spell is aimed at (FFT): a unit, which the spell follows until it resolves, or a tile, which stays
    /// put however the units move.
    /// </summary>
    public enum SpellLock
    {
        Tile,
        Unit,
    }
}
