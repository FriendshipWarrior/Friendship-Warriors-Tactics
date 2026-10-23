using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;

namespace FriendshipWarriors.Battle
{
    /// <summary>One line of the turn-order preview: a unit's turn, or a charged spell resolving.</summary>
    public readonly struct TurnOrderEntry
    {
        public readonly Unit Unit;

        /// <summary>Set when this entry is a spell resolving; <see cref="Unit"/> is then its caster.</summary>
        public readonly AbilityDefinition Spell;

        public TurnOrderEntry(Unit unit, AbilityDefinition spell = null)
        {
            Unit = unit;
            Spell = spell;
        }
    }
}
