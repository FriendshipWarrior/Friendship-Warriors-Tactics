using FriendshipWarriors.Units;

namespace FriendshipWarriors.Battle.Combat
{
    public readonly struct AttackResult
    {
        public readonly AttackAngle Angle;
        public readonly int HitChance;
        public readonly bool Hit;
        public readonly bool Blocked;
        public readonly bool Critical;
        public readonly int Damage;

        public AttackResult(AttackAngle angle, int hitChance, bool hit, bool blocked, bool critical, int damage)
        {
            Angle = angle;
            HitChance = hitChance;
            Hit = hit;
            Blocked = blocked;
            Critical = critical;
            Damage = damage;
        }

        public override string ToString() =>
            !Hit ? "Miss" : Blocked ? "Blocked" : Critical ? $"{Damage} (critical)" : Damage.ToString();
    }
}
