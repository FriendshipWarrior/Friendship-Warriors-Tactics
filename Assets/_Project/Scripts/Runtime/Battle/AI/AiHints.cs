using System.Collections.Generic;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Units;

namespace FriendshipWarriors.Battle.AI
{
    /// <summary>
    /// What the battle's objective means for the AI: units worth extra effort to attack (the player's protected
    /// characters) and whether the acting unit should play safe (it is the player's target). Also what ending a move
    /// on a tile is worth beyond fighting there (a steel rod or chest to pick up).
    /// </summary>
    public sealed class AiHints
    {
        /// <summary>Attack scores against priority targets are multiplied by this.</summary>
        public const double PriorityTargetWeight = 1.5;

        /// <summary>A cautious unit weighs the threat of standing somewhere this many times more.</summary>
        public const double CautiousThreatWeight = 2.0;

        /// <summary>
        /// Scale for <see cref="AiSpellLure"/>: how much catching enemies in an enemy spell that follows this unit is worth,
        /// next to attacking and danger (1 = its expected damage counts like attack damage).
        /// </summary>
        public const double LockedSpellLureWeight = 1.0;

        // Declared before None: static fields initialize in order, and None's constructor reads this.
        private static readonly Dictionary<GridCoord, double> NoTileValues = new();

        public static readonly AiHints None = new(new HashSet<Unit>(), cautious: false);

        public AiHints(IReadOnlyCollection<Unit> priorityTargets, bool cautious,
            IReadOnlyDictionary<GridCoord, double> tileValues = null, IReadOnlyDictionary<GridCoord, double> standValues = null)
        {
            PriorityTargets = priorityTargets;
            Cautious = cautious;
            TileValues = tileValues ?? NoTileValues;
            StandValues = standValues ?? NoTileValues;
        }

        /// <summary>
        /// Score for ending the turn standing on these tiles, staying put included (where a spell following the unit
        /// will land; see <see cref="AiSpellLure"/>).
        /// </summary>
        public IReadOnlyDictionary<GridCoord, double> StandValues { get; }

        public double StandValue(GridCoord coord) => StandValues.TryGetValue(coord, out double value) ? value : 0.0;

        /// <summary>Extra score for ending the move on these tiles (drops to pick up).</summary>
        public IReadOnlyDictionary<GridCoord, double> TileValues { get; }

        public double TileValue(GridCoord coord) => TileValues.TryGetValue(coord, out double value) ? value : 0.0;

        public IReadOnlyCollection<Unit> PriorityTargets { get; }
        public bool Cautious { get; }

        public bool IsPriority(Unit unit)
        {
            foreach (var target in PriorityTargets)
            {
                if (target == unit)
                    return true;
            }
            return false;
        }
    }
}
