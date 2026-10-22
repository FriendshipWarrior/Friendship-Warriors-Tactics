namespace FriendshipWarriors.Units
{
    /// <summary>The stats a player sees: raw stats run through the current job, plus the job's Move, Jump and evasion.</summary>
    public readonly struct UnitStats
    {
        public readonly int MaxHp;
        public readonly int MaxMp;
        public readonly int Speed;
        public readonly int PhysicalAttack;
        public readonly int MagicAttack;
        public readonly int Move;
        public readonly int Jump;

        /// <summary>Class evasion, in percent. Only counts against attacks from the front.</summary>
        public readonly int ClassEvasion;

        public UnitStats(int maxHp, int maxMp, int speed, int physicalAttack, int magicAttack, int move, int jump, int classEvasion)
        {
            MaxHp = maxHp;
            MaxMp = maxMp;
            Speed = speed;
            PhysicalAttack = physicalAttack;
            MagicAttack = magicAttack;
            Move = move;
            Jump = jump;
            ClassEvasion = classEvasion;
        }
    }
}
