using FriendshipWarriors.Grid;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;

namespace FriendshipWarriors.Battle.Magic
{
    /// <summary>
    /// A spell being charged (FFT's slow actions). Its own CT rises by the spell's charge speed every clock tick and it
    /// resolves at 100, on whatever stands in the area then: spells aim at a tile, not a unit, so targets can step out.
    /// </summary>
    public sealed class ChargingAction
    {
        public ChargingAction(Unit caster, AbilityDefinition spell, GridCoord target, int sequence)
        {
            Caster = caster;
            Spell = spell;
            Target = target;
            Sequence = sequence;
        }

        public Unit Caster { get; }
        public AbilityDefinition Spell { get; }
        public GridCoord Target { get; }

        /// <summary>Order the charge began, used to break ties between spells resolving on the same tick.</summary>
        public int Sequence { get; }

        public int ChargeTime { get; set; }

        /// <summary>Clock ticks left until it resolves (if nothing interrupts it).</summary>
        public int TicksLeft => Spell.ChargeSpeed <= 0 ? 0 : (TurnOrder.TurnThreshold - ChargeTime + Spell.ChargeSpeed - 1) / Spell.ChargeSpeed;

        public override string ToString() => $"{Caster.Name}: {Spell.DisplayName} at {Target}";
    }
}
