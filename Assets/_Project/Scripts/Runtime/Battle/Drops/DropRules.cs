using System;
using System.Collections.Generic;
using FriendshipWarriors.Battle.Abilities;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;
using FriendshipWarriors.Units.Equipment;

namespace FriendshipWarriors.Battle.Drops
{
    /// <summary>
    /// FFT's crystals and treasure chests, with a glowing steel rod as our crystal. When a knocked-out unit's death
    /// counter runs out it leaves one or the other on its tile, and a unit of either side that ends its move there
    /// takes it. A rod offers a choice: learn the fallen unit's abilities, or restore HP and MP in full (a monster's
    /// rod only restores, as in FFT). A chest holds one piece of the fallen unit's equipped gear, or for a monster an
    /// item scaled to its level. Pure and seeded, so drops replay exactly.
    /// </summary>
    public static class DropRules
    {
        /// <summary>The AI takes a rod's HP and MP when below this share of max HP (or MP).</summary>
        public const double AiRestoreBelowHp = 0.6, AiRestoreBelowMp = 0.4;

        /// <summary>What a drop is worth to the AI for ending its move on it.</summary>
        public const double AiRodValue = 6.0, AiRodPerMissingHp = 30.0, AiRodPerAbility = 3.0, AiChestValue = 5.0;

        /// <summary>
        /// The drop a lost unit leaves on its tile: a rod at <see cref="DropSettings.RodChance"/>%, otherwise a chest.
        /// A chest with nothing to give (no gear, or no loot row for the monster's level) becomes a rod instead.
        /// </summary>
        public static BattleDrop Create(int id, Unit fallen, Random rng, DropSettings settings,
            IReadOnlyList<MonsterLootTier> monsterLoot = null)
        {
            bool rod = rng.Next(100) < settings.RodChance;
            EquipmentDefinition gear = null;
            AbilityDefinition item = null;
            if (!rod)
            {
                if (fallen.IsMonster)
                    item = MonsterItem(fallen.Level, monsterLoot);
                else
                    gear = RandomGear(fallen, rng);
                rod = gear == null && item == null;
            }
            var abilities = rod && !fallen.IsMonster ? Teachings(fallen) : new List<BattleDrop.Teaching>();
            return new BattleDrop(id, rod ? DropKind.SteelRod : DropKind.Chest, fallen.Coord, fallen.Name, fallen.Team,
                fallen.IsMonster, abilities, gear, item);
        }

        /// <summary>One of the fallen unit's equipped pieces, picked evenly from its filled slots; null if it wore nothing.</summary>
        public static EquipmentDefinition RandomGear(Unit unit, Random rng)
        {
            var worn = new List<EquipmentDefinition>();
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                var piece = unit.Equipped(slot);
                if (piece != null)
                    worn.Add(piece);
            }
            return worn.Count > 0 ? worn[rng.Next(worn.Count)] : null;
        }

        /// <summary>The item in a monster's chest: the highest row of the table its level reaches.</summary>
        public static AbilityDefinition MonsterItem(int level, IReadOnlyList<MonsterLootTier> tiers)
        {
            if (tiers == null)
                return null;
            AbilityDefinition best = null;
            int bestLevel = int.MinValue;
            foreach (var tier in tiers)
            {
                if (tier.Item != null && tier.MinLevel <= level && tier.MinLevel >= bestLevel)
                {
                    best = tier.Item;
                    bestLevel = tier.MinLevel;
                }
            }
            return best;
        }

        /// <summary>Everything the unit had learned, each with a job of the unit's that teaches it (for the unlock check).</summary>
        public static List<BattleDrop.Teaching> Teachings(Unit unit)
        {
            var list = new List<BattleDrop.Teaching>();
            var jobs = new List<JobDefinition>(unit.KnownJobs);
            foreach (var ability in unit.LearnedAbilities)
            {
                if (ability == null)
                    continue;
                JobDefinition teacher = null;
                foreach (var job in jobs)
                {
                    if (job.Teaches(ability))
                    {
                        teacher = job;
                        break;
                    }
                }
                list.Add(new BattleDrop.Teaching(ability, teacher));
            }
            // Learned abilities live in a set; sort so the list (and any per-ability rolls) is the same every run.
            list.Sort((a, b) => string.CompareOrdinal(a.Ability.DisplayName, b.Ability.DisplayName));
            return list;
        }

        /// <summary>
        /// The rod's abilities this unit could learn: ones it doesn't know yet, from jobs it has unlocked when the
        /// settings ask for that (FFT).
        /// </summary>
        public static List<AbilityDefinition> Inheritable(BattleDrop drop, Unit picker, DropSettings settings)
        {
            var list = new List<AbilityDefinition>();
            if (drop.Kind != DropKind.SteelRod)
                return list;
            foreach (var teaching in drop.Abilities)
            {
                if (picker.HasLearned(teaching.Ability))
                    continue;
                if (settings.RequireUnlockedJob && teaching.Job != null && !picker.MeetsRequirements(teaching.Job))
                    continue;
                list.Add(teaching.Ability);
            }
            return list;
        }

        /// <summary>Whether taking this drop needs a choice from the picker (a rod with something to teach).</summary>
        public static bool NeedsChoice(BattleDrop drop, Unit picker, DropSettings settings) =>
            drop.Kind == DropKind.SteelRod && Inheritable(drop, picker, settings).Count > 0;

        /// <summary>
        /// Takes the drop. Rods: <see cref="PickupChoice.Inherit"/> learns the abilities (each at
        /// <see cref="DropSettings.InheritChance"/>%), anything else restores HP and MP. Chests: gear goes to the
        /// team's spoils and items to its stock.
        /// </summary>
        public static PickupResult Take(BattleDrop drop, Unit picker, PickupChoice choice, Inventory inventory, Spoils spoils,
            Random rng, DropSettings settings)
        {
            if (drop.Kind == DropKind.Chest)
            {
                var opened = new PickupResult(drop, picker, PickupChoice.None) { Gear = drop.Gear, Item = drop.Item };
                if (drop.Gear != null)
                    spoils?.Add(picker.Team, drop.Gear);
                if (drop.Item != null)
                    inventory?.Add(drop.Item, 1);
                return opened;
            }

            var inheritable = Inheritable(drop, picker, settings);
            if (choice == PickupChoice.Inherit && inheritable.Count > 0)
            {
                var result = new PickupResult(drop, picker, PickupChoice.Inherit);
                foreach (var ability in inheritable)
                {
                    bool learns = settings.InheritChance >= 100 || rng.Next(100) < settings.InheritChance;
                    if (learns && picker.Inherit(ability))
                        result.Learned.Add(ability);
                }
                return result;
            }

            var restored = new PickupResult(drop, picker, PickupChoice.Restore);
            restored.HpRestored = picker.RestoreHp(picker.Stats.MaxHp);
            restored.MpRestored = picker.RestoreMp(picker.Stats.MaxMp);
            return restored;
        }

        /// <summary>The AI's choice at a rod: restore when hurt or drained, otherwise learn if there's anything to learn.</summary>
        public static PickupChoice AiChoice(BattleDrop drop, Unit picker, DropSettings settings)
        {
            if (drop.Kind != DropKind.SteelRod)
                return PickupChoice.None;
            var stats = picker.Stats;
            bool hurt = picker.CurrentHp < stats.MaxHp * AiRestoreBelowHp;
            bool drained = stats.MaxMp > 0 && picker.CurrentMp < stats.MaxMp * AiRestoreBelowMp;
            if (hurt || drained || Inheritable(drop, picker, settings).Count == 0)
                return PickupChoice.Restore;
            return PickupChoice.Inherit;
        }

        /// <summary>
        /// What ending a move on this drop is worth to an AI unit: rods more the more HP it's missing (or the more
        /// it could learn), chests a little.
        /// </summary>
        public static double AiValue(BattleDrop drop, Unit unit, DropSettings settings)
        {
            if (drop.Kind == DropKind.Chest)
                return AiChestValue;
            var stats = unit.Stats;
            double missing = stats.MaxHp > 0 ? 1.0 - (double)unit.CurrentHp / stats.MaxHp : 0.0;
            double learn = drop.FromMonster ? 0.0 : Inheritable(drop, unit, settings).Count * AiRodPerAbility;
            return AiRodValue + Math.Max(missing * AiRodPerMissingHp, learn);
        }
    }
}
