using FriendshipWarriors.Grid;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;

namespace FriendshipWarriors.Battle.Magic
{
    /// <summary>
    /// A spell being charged (FFT's slow actions). Its own CT rises by the spell's charge speed every clock tick and it
    /// resolves at 100, on whatever stands in the area then. As in FFT, a spell aimed at a unit follows that unit
    /// (wherever it goes; range isn't checked again), while one aimed at a tile stays put, so targets can step out.
    /// </summary>
    public sealed class ChargingAction
    {
        private GridCoord _tile;

        /// <param name="lockedUnit">The unit the spell follows, or null to aim at <paramref name="target"/> itself.</param>
        public ChargingAction(Unit caster, AbilityDefinition spell, GridCoord target, int sequence, Unit lockedUnit = null)
        {
            Caster = caster;
            Spell = spell;
            _tile = lockedUnit != null ? lockedUnit.Coord : target;
            LockedUnit = lockedUnit;
            Sequence = sequence;
        }

        public Unit Caster { get; }
        public AbilityDefinition Spell { get; }

        /// <summary>The unit the spell follows; null when it's aimed at a tile (or its unit has left the battle).</summary>
        public Unit LockedUnit { get; private set; }

        public SpellLock Lock => LockedUnit != null ? SpellLock.Unit : SpellLock.Tile;

        /// <summary>Where the spell will land: its unit's tile right now, or the tile it was aimed at.</summary>
        public GridCoord Target => LockedUnit != null ? LockedUnit.Coord : _tile;

        /// <summary>Order the charge began, used to break ties between spells resolving on the same tick.</summary>
        public int Sequence { get; }

        public int ChargeTime { get; set; }

        /// <summary>Clock ticks left until it resolves (if nothing interrupts it).</summary>
        public int TicksLeft => Spell.ChargeSpeed <= 0 ? 0 : (TurnOrder.TurnThreshold - ChargeTime + Spell.ChargeSpeed - 1) / Spell.ChargeSpeed;

        /// <summary>
        /// The followed unit has left the battle: the spell stays on the last tile it stood on and becomes tile-aimed.
        /// </summary>
        public void ReleaseLock(Unit unit)
        {
            if (LockedUnit != unit || unit == null)
                return;
            _tile = unit.Coord;
            LockedUnit = null;
        }

        public override string ToString() => LockedUnit != null
            ? $"{Caster.Name}: {Spell.DisplayName} on {LockedUnit.Name}"
            : $"{Caster.Name}: {Spell.DisplayName} at {Target}";
    }
}
