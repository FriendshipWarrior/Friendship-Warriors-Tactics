using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;

namespace FriendshipWarriors.Battle.Magic
{
    public sealed class SpellResult
    {
        public SpellResult(Unit caster, AbilityDefinition spell, GridCoord tile, List<GridCoord> area, List<SpellTargetResult> targets)
        {
            Caster = caster;
            Spell = spell;
            Tile = tile;
            Area = area;
            Targets = targets;
        }

        public Unit Caster { get; }
        public AbilityDefinition Spell { get; }
        public GridCoord Tile { get; }
        public List<GridCoord> Area { get; }
        public List<SpellTargetResult> Targets { get; }

        public bool AnyHit => Targets.Any(t => t.Hit);

        public string Message =>
            $"{Caster.Name}: {Spell.DisplayName} - " +
            (Targets.Count == 0 ? "no one there" : string.Join(", ", Targets.Select(t => t.ToString())));
    }
}
