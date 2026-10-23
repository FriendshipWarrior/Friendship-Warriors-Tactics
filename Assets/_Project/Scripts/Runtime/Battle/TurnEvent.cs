using FriendshipWarriors.Battle.Magic;
using FriendshipWarriors.Units;

namespace FriendshipWarriors.Battle
{
    public enum TurnEventKind
    {
        /// <summary>Nothing can ever happen again (no speed, no spells).</summary>
        None,
        UnitTurn,

        /// <summary>A charged spell's CT reached 100: resolve it now, before any unit's turn this tick (FFT).</summary>
        SpellResolves,

        /// <summary>A charged spell was cut off (its caster was knocked out, stopped, put to sleep or silenced).</summary>
        SpellInterrupted,
    }

    /// <summary>What happens next on the CT clock.</summary>
    public readonly struct TurnEvent
    {
        public readonly TurnEventKind Kind;
        public readonly Unit Unit;
        public readonly ChargingAction Charge;

        private TurnEvent(TurnEventKind kind, Unit unit, ChargingAction charge)
        {
            Kind = kind;
            Unit = unit;
            Charge = charge;
        }

        public static TurnEvent None => new(TurnEventKind.None, null, null);
        public static TurnEvent Turn(Unit unit) => new(TurnEventKind.UnitTurn, unit, null);
        public static TurnEvent Resolves(ChargingAction charge) => new(TurnEventKind.SpellResolves, charge.Caster, charge);
        public static TurnEvent Interrupted(ChargingAction charge) => new(TurnEventKind.SpellInterrupted, charge.Caster, charge);
    }
}
