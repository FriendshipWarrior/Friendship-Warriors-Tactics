using System.Collections.Generic;
using FriendshipWarriors.Battle.Magic;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;

namespace FriendshipWarriors.Battle.AI
{
    /// <summary>
    /// A unit an enemy's harmful area spell is following (locked on it, radius 1 or more) can't escape it, but it can
    /// choose where it lands: next to the caster's side, so they're caught too (user decision, 2026-10-23). Each tile's
    /// value is the spell's expected damage to the actor's enemies in the area centred there, minus that to its allies
    /// (the actor itself is hit wherever it stands), using the spell's own radius and vertical reach. Single-target
    /// spells and tile-locked ones give nothing to gain. Deterministic: units are read as they stand now.
    /// </summary>
    public static class AiSpellLure
    {
        /// <summary>A harmful spell with no damage (a status) counts this much per unit at 100% to hit.</summary>
        public const double StatusValue = 10.0;

        public static Dictionary<GridCoord, double> Values(BattleGrid grid, Unit actor, IReadOnlyList<Unit> units,
            IEnumerable<ChargingAction> charges, double weight = AiHints.LockedSpellLureWeight)
        {
            var values = new Dictionary<GridCoord, double>();
            foreach (var charge in charges)
            {
                var spell = charge.Spell;
                if (charge.LockedUnit != actor || charge.Caster.Team == actor.Team || spell.IsSupportAction || spell.AreaRadius < 1)
                    continue;
                foreach (var center in grid.AllCoords())
                {
                    double value = AreaValue(grid, actor, charge.Caster, spell, center, units) * weight;
                    if (value == 0.0)
                        continue;
                    values.TryGetValue(center, out double sum);
                    values[center] = sum + value;
                }
            }
            return values;
        }

        /// <summary>Expected harm to the actor's enemies minus its allies, for the spell landing on <paramref name="center"/>.</summary>
        public static double AreaValue(BattleGrid grid, Unit actor, Unit caster, AbilityDefinition spell, GridCoord center,
            IReadOnlyList<Unit> units)
        {
            if (!grid.Contains(center))
                return 0.0;
            var area = new HashSet<GridCoord>(SpellArea.Tiles(grid, center, spell.AreaRadius, spell.AreaVertical));
            double value = 0.0;
            foreach (var unit in units)
            {
                if (unit == actor || unit.IsKnockedOut || !area.Contains(unit.Coord))
                    continue;
                double harm = spell.Effect == AbilityEffect.Damage
                    ? MagicFormulas.Damage(caster, unit, spell.Power)
                    : MagicFormulas.HitChance(caster, unit, spell.Power) / 100.0 * StatusValue;
                value += unit.Team == actor.Team ? -harm : harm;
            }
            return value;
        }
    }
}
