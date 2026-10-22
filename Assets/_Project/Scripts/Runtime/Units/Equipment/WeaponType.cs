namespace FriendshipWarriors.Units.Equipment
{
    /// <summary>
    /// Weapon families, each with FFT's damage formula (see <c>CombatFormulas</c>). Rifles use FFT's bow formula and
    /// range. Values are serialized into assets, so append rather than reorder.
    /// </summary>
    public enum WeaponType
    {
        Unarmed = 0,
        Sword = 1,
        Knife = 2,
        Rifle = 3,
        Rod = 4,
        Staff = 5,
    }
}
