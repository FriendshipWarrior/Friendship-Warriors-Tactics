using System.Collections.Generic;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;
using FriendshipWarriors.Units.Equipment;

namespace FriendshipWarriors.Battle.Drops
{
    /// <summary>
    /// A steel rod or treasure chest on the tile where a unit was lost. Its contents are fixed when it appears (from
    /// the battle's seeded random), so every replay of a battle leaves the same drops.
    /// </summary>
    public sealed class BattleDrop
    {
        /// <summary>An ability the rod can teach, with the job that teaches it (null when no known job does).</summary>
        public readonly struct Teaching
        {
            public readonly AbilityDefinition Ability;
            public readonly JobDefinition Job;

            public Teaching(AbilityDefinition ability, JobDefinition job)
            {
                Ability = ability;
                Job = job;
            }
        }

        public BattleDrop(int id, DropKind kind, GridCoord coord, string fallenName, Team fallenTeam, bool fromMonster,
            IReadOnlyList<Teaching> abilities, EquipmentDefinition gear, AbilityDefinition item)
        {
            Id = id;
            Kind = kind;
            Coord = coord;
            FallenName = fallenName;
            FallenTeam = fallenTeam;
            FromMonster = fromMonster;
            Abilities = abilities ?? new List<Teaching>();
            Gear = gear;
            Item = item;
        }

        public int Id { get; }
        public DropKind Kind { get; }
        public GridCoord Coord { get; }
        public string FallenName { get; }
        public Team FallenTeam { get; }
        public bool FromMonster { get; }

        /// <summary>Rods: what the fallen unit had learned (empty for monsters, whose rods only restore).</summary>
        public IReadOnlyList<Teaching> Abilities { get; }

        /// <summary>Chests from people: the piece of gear inside.</summary>
        public EquipmentDefinition Gear { get; }

        /// <summary>Chests from monsters: the item inside.</summary>
        public AbilityDefinition Item { get; }

        public string DisplayName => Kind == DropKind.SteelRod ? "Steel Rod" : "Treasure Chest";

        public override string ToString() =>
            Kind == DropKind.SteelRod
                ? $"{FallenName}'s steel rod at {Coord} ({Abilities.Count} abilities)"
                : $"{FallenName}'s chest at {Coord} ({(Gear != null ? Gear.DisplayName : Item != null ? Item.DisplayName : "empty")})";
    }
}
