using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Units;

namespace FriendshipWarriors.Battle.Objectives
{
    /// <summary>
    /// Decides whether a battle is won, lost or still going. Checked in a fixed order so simultaneous events always
    /// resolve the same way:
    /// <list type="number">
    /// <item>A protected unit permanently lost (death counter ran out) → defeat. This always wins, because losing a
    /// character the level depends on can't be undone.</item>
    /// <item>The victory condition met (the target knocked out, or every enemy knocked out or lost) → victory, even if
    /// the player's last unit fell in the same action: the objective was achieved.</item>
    /// <item>No Player-team unit left standing → defeat (FFT).</item>
    /// </list>
    /// Pure and deterministic: it only reads the units.
    /// </summary>
    public static class ObjectiveEvaluator
    {
        /// <param name="units">Units still on the field (knocked out or standing).</param>
        /// <param name="lost">Units whose death counter ran out and have left the battle.</param>
        public static BattleOutcome Evaluate(BattleObjective objective, IEnumerable<Unit> units, IEnumerable<Unit> lost)
        {
            objective ??= BattleObjective.Standard;
            var field = units.ToList();
            var gone = lost.ToList();

            foreach (var unit in gone)
            {
                if (unit.Team == Team.Player && objective.IsProtected(unit.Key))
                    return BattleOutcome.Defeat($"{unit.Name} was lost");
            }

            var target = objective.Victory == VictoryCondition.DefeatTarget ? FindTarget(objective, field, gone) : null;
            if (target != null)
            {
                if (target.IsKnockedOut || gone.Contains(target))
                    return BattleOutcome.Victory($"{target.Name} defeated");
            }
            else if (!field.Any(u => u.Team == Team.Enemy && !u.IsKnockedOut))
            {
                // Defeat All, or a Defeat Target battle whose target never took the field: every enemy down wins.
                return BattleOutcome.Victory("All enemies defeated");
            }

            if (!field.Any(u => u.Team == Team.Player && !u.IsKnockedOut))
                return BattleOutcome.Defeat("All units are down");

            return BattleOutcome.Ongoing;
        }

        /// <summary>The enemy the objective names, on the field or already lost; null if it isn't in the battle.</summary>
        public static Unit FindTarget(BattleObjective objective, IEnumerable<Unit> units, IEnumerable<Unit> lost = null)
        {
            if (objective == null || objective.Victory != VictoryCondition.DefeatTarget || string.IsNullOrEmpty(objective.TargetKey))
                return null;
            return units.Concat(lost ?? Enumerable.Empty<Unit>())
                .FirstOrDefault(u => u.Team == Team.Enemy && u.Key == objective.TargetKey);
        }

        /// <summary>Player-team units the objective protects.</summary>
        public static IEnumerable<Unit> ProtectedUnits(BattleObjective objective, IEnumerable<Unit> units) =>
            objective == null ? Enumerable.Empty<Unit>() : units.Where(u => u.Team == Team.Player && objective.IsProtected(u.Key));

        /// <summary>One line for the HUD, e.g. "Defeat Commander Vael · Protect: Escort Lira".</summary>
        public static string Describe(BattleObjective objective, IEnumerable<Unit> units)
        {
            objective ??= BattleObjective.Standard;
            var list = units.ToList();
            var target = FindTarget(objective, list);
            string goal = target != null ? $"Defeat {target.Name}" : "Defeat all enemies";
            var protectedNames = ProtectedUnits(objective, list).Select(u => u.Name).ToList();
            return protectedNames.Count > 0 ? $"{goal}  |  Protect: {string.Join(", ", protectedNames)}" : goal;
        }
    }
}
