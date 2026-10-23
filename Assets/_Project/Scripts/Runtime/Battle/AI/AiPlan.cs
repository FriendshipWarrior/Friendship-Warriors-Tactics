using FriendshipWarriors.Grid;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;

namespace FriendshipWarriors.Battle.AI
{
    /// <summary>
    /// What an AI unit will do this turn, in order: optionally move, optionally attack, optionally move again
    /// (attack-then-reposition, as FFT's AI often does), then face <see cref="Facing"/>.
    /// </summary>
    public sealed class AiPlan
    {
        /// <summary>Tile to move to before acting, or null to act from where it stands.</summary>
        public GridCoord? MoveBefore { get; set; }

        /// <summary>Unit to attack, or null if no attack is worth making.</summary>
        public Unit Target { get; set; }

        /// <summary>Spell to cast instead of attacking (after <see cref="MoveBefore"/>), or null.</summary>
        public AbilityDefinition Spell { get; set; }

        /// <summary>Tile the spell is aimed at.</summary>
        public GridCoord SpellTile { get; set; }

        /// <summary>Tile to fall back to after attacking from the starting tile, or null to stay.</summary>
        public GridCoord? MoveAfter { get; set; }

        public Direction Facing { get; set; }

        /// <summary>The score of the chosen attack (for debugging and tests).</summary>
        public double Score { get; set; }

        public override string ToString() =>
            $"move {MoveBefore?.ToString() ?? "-"}, " +
            (Spell != null ? $"cast {Spell.DisplayName} at {SpellTile}" : $"attack {Target?.Name ?? "-"}") +
            $", then move {MoveAfter?.ToString() ?? "-"}, face {Facing}";
    }
}
