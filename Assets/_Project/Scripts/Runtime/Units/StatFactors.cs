using System;
using UnityEngine;

namespace FriendshipWarriors.Units
{
    /// <summary>One number per growable stat; used for both a job's multipliers and its growth constants.</summary>
    [Serializable]
    public struct StatFactors
    {
        [SerializeField] private int _hp;
        [SerializeField] private int _mp;
        [SerializeField] private int _speed;
        [SerializeField] private int _physicalAttack;
        [SerializeField] private int _magicAttack;

        public StatFactors(int hp, int mp, int speed, int physicalAttack, int magicAttack)
        {
            _hp = hp;
            _mp = mp;
            _speed = speed;
            _physicalAttack = physicalAttack;
            _magicAttack = magicAttack;
        }

        public int Hp => _hp;
        public int Mp => _mp;
        public int Speed => _speed;
        public int PhysicalAttack => _physicalAttack;
        public int MagicAttack => _magicAttack;
    }
}
