namespace FriendshipWarriors.Units
{
    /// <summary>
    /// FFT's "critical" HP: a unit at or below a fifth of its max HP (and still standing) kneels, and some reactions
    /// key off it. Recalled FFT threshold, unverified; tune <see cref="Divisor"/>.
    /// </summary>
    public static class CriticalHp
    {
        public const int Divisor = 5;

        public static bool IsCritical(int hp, int maxHp) => hp > 0 && maxHp > 0 && hp * Divisor <= maxHp;

        public static bool IsCritical(Unit unit) => unit != null && IsCritical(unit.CurrentHp, unit.Stats.MaxHp);
    }
}
