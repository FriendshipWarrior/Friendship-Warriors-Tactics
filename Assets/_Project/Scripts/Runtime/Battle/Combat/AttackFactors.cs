namespace FriendshipWarriors.Battle.Combat
{
    /// <summary>FFT splits physical damage into XA (from the attacker's stats) times a multiplier (usually WP).</summary>
    public readonly struct AttackFactors
    {
        public readonly int Xa;
        public readonly int Multiplier;

        public AttackFactors(int xa, int multiplier)
        {
            Xa = xa;
            Multiplier = multiplier;
        }

        public int Damage => Xa * Multiplier;
    }
}
