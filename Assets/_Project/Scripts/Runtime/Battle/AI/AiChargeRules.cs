using FriendshipWarriors.Battle.Combat;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;

namespace FriendshipWarriors.Battle.AI
{
    /// <summary>
    /// Whether an AI unit charging a spell should act anyway, cancelling it (acting cancels a charge; moving doesn't).
    /// Our rule, tune freely: only for an attack that should knock its target out. A new spell never is: it would
    /// throw away the MP already paid for one that's on its way.
    /// </summary>
    public static class AiChargeRules
    {
        public static bool WorthCancelling(Unit unit, AbilityDefinition plannedSpell, Unit plannedTarget)
        {
            if (plannedSpell != null || plannedTarget == null || plannedTarget.IsKnockedOut)
                return false;
            return CombatFormulas.AttackFactors(unit).Damage >= plannedTarget.CurrentHp;
        }
    }
}
