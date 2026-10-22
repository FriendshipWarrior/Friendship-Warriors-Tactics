namespace FriendshipWarriors.Units
{
    /// <summary>
    /// A unit's hidden base stats, FFT-style. They are large integers that grow on level-up; the numbers players see
    /// come from multiplying them by the current job's multipliers (see <see cref="StatFormulas"/>).
    /// </summary>
    public readonly struct RawStats
    {
        public readonly int Hp;
        public readonly int Mp;
        public readonly int Speed;
        public readonly int PhysicalAttack;
        public readonly int MagicAttack;

        public RawStats(int hp, int mp, int speed, int physicalAttack, int magicAttack)
        {
            Hp = hp;
            Mp = mp;
            Speed = speed;
            PhysicalAttack = physicalAttack;
            MagicAttack = magicAttack;
        }
    }
}
