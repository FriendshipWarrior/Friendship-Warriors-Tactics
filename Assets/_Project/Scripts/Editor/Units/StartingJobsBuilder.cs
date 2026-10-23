using System.Collections.Generic;
using FriendshipWarriors.Editor.Maps;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;
using FriendshipWarriors.Units.Equipment;
using FriendshipWarriors.Units.Status;
using UnityEditor;
using UnityEngine;

namespace FriendshipWarriors.Editor.Units
{
    /// <summary>
    /// Creates the six starting job assets and the job catalog. Numbers are FFT's for the matching job (multipliers
    /// and growth constants verified against published tables; Move, Jump and class evasion recalled). Existing assets
    /// are left alone so Inspector tuning is never overwritten.
    /// </summary>
    public static class StartingJobsBuilder
    {
        public const string JobsFolder = "Assets/_Project/Data/Jobs";
        public const string CatalogPath = JobsFolder + "/JobCatalog.asset";
        public const string AbilitiesFolder = "Assets/_Project/Data/Abilities";
        public const string EquipmentFolder = "Assets/_Project/Data/Equipment";

        [MenuItem("Friendship Warriors/Data/Create Starting Jobs")]
        public static JobCatalog CreateStartingJobs()
        {
            TestSceneBuilder.EnsureFolder(JobsFolder);

            // Base jobs.                FFT job     HP   MP  Spd   PA   MA          growth: HP  MP  Spd  PA  MA   Mv Jp Ev
            var cadet = Job("Cadet", /* Squire */ new(100, 75, 100, 90, 80), new(11, 15, 100, 60, 50), 4, 3, 5,
                new Color(0.80f, 0.65f, 0.38f),
                "Academy recruit drilled in blade, rifle and brass-buckled discipline. Every soldier starts here.");
            var corpsman = Job("Corpsman", /* Chemist */ new(80, 75, 100, 75, 80), new(12, 16, 100, 75, 50), 3, 3, 5,
                new Color(0.30f, 0.70f, 0.50f),
                "Field medic carrying tonics, splints and a satchel of aether vials. The first step on the path of magic.");

            // Advanced jobs, each unlocked at job level 2 in its base job, as in FFT.
            var vanguard = Job("Vanguard", /* Knight */ new(120, 80, 100, 120, 80), new(10, 15, 100, 40, 50), 3, 3, 10,
                new Color(0.55f, 0.62f, 0.78f),
                "Steam-plated shock trooper who holds the line and breaks the enemy's.", new JobRequirement(cadet, 2));
            var marksman = Job("Marksman", /* Archer */ new(100, 65, 100, 110, 80), new(11, 16, 100, 45, 50), 3, 3, 10,
                new Color(0.62f, 0.46f, 0.26f),
                "Long-rifle specialist who strikes from rooftops and ridgelines.", new JobRequirement(cadet, 2));
            var evoker = Job("Evoker", /* Black Mage */ new(75, 120, 100, 60, 150), new(12, 9, 100, 60, 50), 3, 3, 5,
                new Color(0.55f, 0.32f, 0.72f),
                "Battle mage who vents raw aether as fire, frost and lightning.", new JobRequirement(corpsman, 2));
            var warder = Job("Warder", /* White Mage */ new(80, 120, 110, 90, 110), new(10, 10, 100, 50, 50), 3, 3, 5,
                new Color(0.92f, 0.90f, 0.80f),
                "Aether-ward specialist who mends wounds and raises protective barriers.", new JobRequirement(corpsman, 2));

            // Command names and the first abilities. The two turn trades are our addition to FFT.
            var doubleTime = Ability("Double Time", AbilityKind.Movement, 300, TurnTrade.ActForMove,
                "Forced march: give up this turn's Act to Move a second time.");
            var rapidFire = Ability("Rapid Fire", AbilityKind.Support, 400, TurnTrade.MoveForAct,
                "Hold position and keep firing: give up this turn's Move to Act a second time.");
            Teach(cadet, "Drill", CadetAbilities());
            var longToss = Passive("Long Toss", AbilityKind.Support, 350, "Throw items up to 4 tiles away instead of handing them over (FFT: Throw Item).", throwsItems: true);
            var corpsmanAbilities = new List<AbilityDefinition>(CorpsmanItems()) { longToss };
            Teach(corpsman, "Field Kit", corpsmanAbilities.ToArray());
            if (corpsman.InnateAbilities.Count == 0)
            {
                // FFT's Chemist always has Throw Item.
                corpsman.SetInnateAbilities(new[] { longToss });
                EditorUtility.SetDirty(corpsman);
            }
            Teach(vanguard, "Assault", doubleTime);
            Teach(marksman, "Marksmanship", rapidFire);
            Teach(evoker, "Evocation");
            Teach(warder, "Warding");

            // Status cures and reactions (added after the first abilities, so appended to existing jobs).
            AlsoTeach(cadet, FirstAid(), Reaction("Counter Charge", 180, ReactionEffect.CounterStrike, // Counter Tackle, 180 JP
                "When struck in melee range, charge back: P.Atk × 1–4 damage, may knock back. Triggers at Morale %.",
                random: new Vector2Int(1, 4), knockback: true));
            AlsoTeach(corpsman, CureItems());
            AlsoTeach(corpsman, Reaction("Self-Dose", 400, ReactionEffect.AutoHeal, // Auto Potion, 400 JP
                "When hurt, take the team's weakest tonic. Triggers at Morale %."));
            AlsoTeach(vanguard, Reaction("Guard Stance", 200, ReactionEffect.Parry, // Parry / Weapon Guard, 200 JP
                "Turn blows aside: your weapon's evasion counts against front and side attacks. Always on."));
            AlsoTeach(marksman, Reaction("Adrenaline", 800, ReactionEffect.SpeedUp, // Speed Save, 800 JP
                "When hurt, Speed +1 for the rest of the battle. Triggers at Morale %."));
            AlsoTeach(warder, Reaction("Mending Ward", 400, ReactionEffect.GainRegen, // Regenerator, 400 JP
                "When hurt, gain Regen. Triggers at Morale %."));

            // Magic (added after statuses): Evocation after FFT Black Magic, Warding after White Magic.
            AlsoTeach(evoker, EvokerSpells());
            AlsoTeach(evoker, Reaction("Feedback Loop", 800, ReactionEffect.CounterMagic, // Counter Magic, 800 JP
                "When an enemy's spell hurts you, cast it straight back at them (pays its MP). Triggers at Morale %."));
            AlsoTeach(warder, WarderSpells());

            // Which weapons each job wields, after its FFT counterpart (Marksman rifles use FFT's bow rules).
            Arm(cadet, false, WeaponType.Sword, WeaponType.Knife);
            Arm(corpsman, false, WeaponType.Knife);
            Arm(vanguard, true, WeaponType.Sword);
            Arm(marksman, false, WeaponType.Rifle);
            Arm(evoker, false, WeaponType.Rod);
            Arm(warder, false, WeaponType.Staff);

            // Headgear and body gear each job may wear, after its FFT counterpart.
            Dress(cadet, ArmorType.Hat, ArmorType.Clothing);
            Dress(corpsman, ArmorType.Hat, ArmorType.Clothing);
            Dress(vanguard, ArmorType.Helmet, ArmorType.Armor, ArmorType.Robe);
            Dress(marksman, ArmorType.Hat, ArmorType.Clothing);
            Dress(evoker, ArmorType.Hat, ArmorType.Clothing, ArmorType.Robe);
            Dress(warder, ArmorType.Hat, ArmorType.Clothing, ArmorType.Robe);

            var catalog = AssetDatabase.LoadAssetAtPath<JobCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<JobCatalog>();
                catalog.SetJobs(new[] { cadet, corpsman, vanguard, marksman, evoker, warder });
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            AssetDatabase.SaveAssets();
            return catalog;
        }

        /// <summary>
        /// Starter equipment, each matching an FFT starting item: Broad Sword (WP 4), Dagger (3), Long Bow (4, range 5,
        /// two-handed), Rod (3), Oak Staff (3), Escutcheon (10% shield evasion), Leather Helmet (+10 HP), Leather Hat
        /// (+8 HP), Leather Armor (+10 HP), Clothing (+5 HP) and Linen Robe (+10 HP, +10 MP). Gear bonuses are recalled
        /// FFT values. Order matters: units take the first item that fits each slot.
        /// </summary>
        [MenuItem("Friendship Warriors/Data/Create Starter Equipment")]
        public static EquipmentDefinition[] CreateStarterEquipment()
        {
            TestSceneBuilder.EnsureFolder(EquipmentFolder);
            return new[]
            {
                // W-Ev (only used by Parry-type reactions), recalled FFT values: Broad Sword 5, Dagger 5, Long Bow 0, Rod 20, Oak Staff 15.
                WithWeaponEvasion(Weapon("Service Saber", WeaponType.Sword, 4, 1, 1, 3, "Standard-issue officer's saber."), 5),
                WithWeaponEvasion(Weapon("Field Knife", WeaponType.Knife, 3, 1, 1, 3, "Utility blade every corpsman carries."), 5),
                Weapon("Service Rifle", WeaponType.Rifle, 4, 1, 5, 99, "Bolt-action long rifle with brass sights.", twoHanded: true),
                WithWeaponEvasion(Weapon("Aether Rod", WeaponType.Rod, 3, 1, 1, 3, "Copper focus rod humming with aether."), 20),
                WithWeaponEvasion(Weapon("Ward Staff", WeaponType.Staff, 3, 1, 1, 3, "Ironwood staff etched with warding glyphs."), 15),
                Armor("Buckler", EquipmentCategory.Shield, 10, "Riveted steel buckler."),
                Gear("Steel Helm", EquipmentCategory.Head, ArmorType.Helmet, 10, 0, "Riveted helm with a brass crest."),
                Gear("Field Cap", EquipmentCategory.Head, ArmorType.Hat, 8, 0, "Peaked cap with goggles."),
                Gear("Plate Cuirass", EquipmentCategory.Body, ArmorType.Armor, 10, 0, "Steam-pressed steel breastplate."),
                Gear("Aether Robe", EquipmentCategory.Body, ArmorType.Robe, 10, 10, "Robe stitched with aether-conductive thread."),
                Gear("Fatigues", EquipmentCategory.Body, ArmorType.Clothing, 5, 0, "Standard-issue field uniform."),
            };
        }

        private static EquipmentDefinition WithWeaponEvasion(EquipmentDefinition weapon, int weaponEvasion)
        {
            if (weapon.WeaponEvasion == 0 && weaponEvasion != 0)
            {
                weapon.SetWeaponEvasion(weaponEvasion);
                EditorUtility.SetDirty(weapon);
            }
            return weapon;
        }

        private static EquipmentDefinition Weapon(string displayName, WeaponType type, int power, int minRange, int maxRange,
            int verticalReach, string description, bool twoHanded = false)
        {
            var item = LoadOrCreate<EquipmentDefinition>($"{EquipmentFolder}/{displayName.Replace(" ", "")}.asset", out bool created);
            if (created || item.TwoHanded != twoHanded)
            {
                item.DefineWeapon(displayName, description, type, power, minRange, maxRange, verticalReach, twoHanded);
                EditorUtility.SetDirty(item);
            }
            return item;
        }

        private static EquipmentDefinition Armor(string displayName, EquipmentCategory category, int evasion, string description)
        {
            var item = LoadOrCreate<EquipmentDefinition>($"{EquipmentFolder}/{displayName.Replace(" ", "")}.asset", out bool created);
            if (created)
            {
                item.DefineArmor(displayName, description, category, evasion);
                EditorUtility.SetDirty(item);
            }
            return item;
        }

        private static EquipmentDefinition Gear(string displayName, EquipmentCategory category, ArmorType armorType, int hp, int mp, string description)
        {
            var item = LoadOrCreate<EquipmentDefinition>($"{EquipmentFolder}/{displayName.Replace(" ", "")}.asset", out bool created);
            if (created)
            {
                item.DefineGear(displayName, description, category, armorType, hp, mp);
                EditorUtility.SetDirty(item);
            }
            return item;
        }

        /// <summary>Sets a job's headgear and body gear types, unless already set.</summary>
        private static void Dress(JobDefinition job, params ArmorType[] types)
        {
            if (job.ArmorTypes.Count > 0)
                return;
            job.SetArmorTypes(types);
            EditorUtility.SetDirty(job);
        }

        private static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            created = asset == null;
            if (created)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        /// <summary>Sets a job's weapon types and shield use, unless it already has weapon types set.</summary>
        private static void Arm(JobDefinition job, bool shields, params WeaponType[] types)
        {
            if (job.WeaponTypes.Count > 0)
                return;
            job.SetEquipment(types, shields);
            EditorUtility.SetDirty(job);
        }

        /// <summary>Appends abilities a job doesn't teach yet, keeping its existing list and order.</summary>
        private static void AlsoTeach(JobDefinition job, params AbilityDefinition[] abilities)
        {
            var list = new List<AbilityDefinition>(job.Abilities);
            bool changed = false;
            foreach (var ability in abilities)
            {
                if (ability != null && !list.Contains(ability))
                {
                    list.Add(ability);
                    changed = true;
                }
            }
            if (!changed)
                return;
            job.SetAbilities(job.CommandName, list.ToArray());
            EditorUtility.SetDirty(job);
        }

        /// <summary>FFT Squire's Heal: cures Blind and Silence on the user or an adjacent unit (150 JP).</summary>
        private static AbilityDefinition FirstAid()
        {
            var ability = Action("First Aid", 150, "Patch up yourself or an adjacent unit: cures Blind and Silence.",
                AbilityEffect.CureStatus, AbilityTargeting.Standing, 0, 1, 3);
            if (ability.Cures.Count == 0)
            {
                ability.SetCures(StatusEffect.Blind, StatusEffect.Silence);
                EditorUtility.SetDirty(ability);
            }
            return ability;
        }

        /// <summary>
        /// FFT Chemist status cures that matter while only our statuses exist (FFT name and JP in comments; Remedy's
        /// cure list is cut down to the statuses we have).
        /// </summary>
        private static AbilityDefinition[] CureItems() => new[]
        {
            CureItem("Antitoxin", 70, "Cures Poison.", StatusEffect.Poison),                         // Antidote
            CureItem("Eyewash", 80, "Cures Blind.", StatusEffect.Blind),                              // Eye Drops
            CureItem("Throat Lozenge", 120, "Cures Silence.", StatusEffect.Silence),                  // Echo Herbs
            CureItem("Field Remedy", 700, "Cures Poison, Blind, Silence and Sleep.",                  // Remedy
                StatusEffect.Poison, StatusEffect.Blind, StatusEffect.Silence, StatusEffect.Sleep),
        };

        /// <summary>
        /// Evocation, after FFT Black Magic (FFT name and JP in comments). Fire tiers' MP, range, area, speed, power
        /// and JP are verified (Final Fantasy Wiki); Bolt and Ice mirror them as in FFT. Poison's hit bonus and
        /// speed are recalled, flagged in CLAUDE.md. Area radius 1 = FFT effect 2 (a cross of five).
        /// </summary>
        private static AbilityDefinition[] EvokerSpells() => new[]
        {
            Spell("Incendiary Charge", 50, "Fire: M.Atk × 14 to a small cross.", AbilityEffect.Damage, 4, 6, 14, 25, 1, 1, SpellElement.Fire),      // Fire
            Spell("Incendiary Blast", 200, "Fire: M.Atk × 18 to a cross.", AbilityEffect.Damage, 4, 12, 18, 20, 1, 2, SpellElement.Fire),          // Fire 2
            Spell("Incendiary Barrage", 500, "Fire: M.Atk × 24 to a cross.", AbilityEffect.Damage, 4, 24, 24, 15, 1, 3, SpellElement.Fire),       // Fire 3
            Spell("Voltaic Charge", 50, "Lightning: M.Atk × 14 to a small cross.", AbilityEffect.Damage, 4, 6, 14, 25, 1, 1, SpellElement.Lightning),   // Bolt
            Spell("Voltaic Blast", 200, "Lightning: M.Atk × 18 to a cross.", AbilityEffect.Damage, 4, 12, 18, 20, 1, 2, SpellElement.Lightning),       // Bolt 2
            Spell("Voltaic Barrage", 500, "Lightning: M.Atk × 24 to a cross.", AbilityEffect.Damage, 4, 24, 24, 15, 1, 3, SpellElement.Lightning),    // Bolt 3
            Spell("Cryo Charge", 50, "Ice: M.Atk × 14 to a small cross.", AbilityEffect.Damage, 4, 6, 14, 25, 1, 1, SpellElement.Ice),              // Ice
            Spell("Cryo Blast", 200, "Ice: M.Atk × 18 to a cross.", AbilityEffect.Damage, 4, 12, 18, 20, 1, 2, SpellElement.Ice),                  // Ice 2
            Spell("Cryo Barrage", 500, "Ice: M.Atk × 24 to a cross.", AbilityEffect.Damage, 4, 24, 24, 15, 1, 3, SpellElement.Ice),               // Ice 3
            Spell("Toxin Cloud", 150, "Poisons a small cross (hit: M.Atk + 160).", AbilityEffect.AddStatus, 4, 6, 160, 33, 1, 1,                // Poison
                inflicts: new[] { StatusEffect.Poison }),
        };

        /// <summary>
        /// Warding, after FFT White Magic (FFT name and JP in comments). Cure tiers' MP, power and JP are verified or
        /// recalled as noted in CLAUDE.md; buff and Esuna speeds (34) and hit bonuses are recalled. Purge cures every
        /// harmful status we have (FFT Esuna's list is shorter). The revives (appended last, as they came later) only
        /// reach knocked-out units whose death counter is still running.
        /// </summary>
        private static AbilityDefinition[] WarderSpells() => new[]
        {
            Spell("Mend", 50, "Heals M.Atk × 14 in a small cross.", AbilityEffect.RestoreHp, 4, 6, 14, 25, 1, 1),                        // Cure
            Spell("Greater Mend", 180, "Heals M.Atk × 20 in a cross.", AbilityEffect.RestoreHp, 4, 10, 20, 20, 1, 2),                   // Cura / Cure 2
            Spell("Grand Mend", 450, "Heals M.Atk × 30 in a cross.", AbilityEffect.RestoreHp, 4, 16, 30, 15, 1, 3),                     // Curaga / Cure 3
            Spell("Vital Ward", 350, "Grants Regen (hit: M.Atk + 170).", AbilityEffect.AddStatus, 3, 8, 170, 34, 0, 0,                  // Regen
                inflicts: new[] { StatusEffect.Regen }),
            Spell("Plate Ward", 70, "Grants Protect (hit: M.Atk + 200).", AbilityEffect.AddStatus, 3, 6, 200, 34, 0, 0,                 // Protect
                inflicts: new[] { StatusEffect.Protect }),
            Spell("Aether Ward", 70, "Grants Shell (hit: M.Atk + 200).", AbilityEffect.AddStatus, 3, 6, 200, 34, 0, 0,                  // Shell
                inflicts: new[] { StatusEffect.Shell }),
            Spell("Purge", 300, "Cures every harmful status in a cross; Panic by steadying Morale (hit: M.Atk + 190).",  // Esuna
                AbilityEffect.CureStatus, 3, 18, 190, 34, 1, 2,
                cures: new[] { StatusEffect.Poison, StatusEffect.Blind, StatusEffect.Silence, StatusEffect.Sleep, StatusEffect.Slow,
                               StatusEffect.Stop, StatusEffect.Disable, StatusEffect.Immobilize, StatusEffect.Panic }),
            Spell("Resuscitate", 180, "Revives a fallen unit with half its HP (hit: M.Atk + 180).", AbilityEffect.Revive, 4, 10, 180, 25, 0, 0, // Raise
                targeting: AbilityTargeting.KnockedOut, revivePercent: 50),
            Spell("Full Resuscitation", 500, "Revives a fallen unit with full HP (hit: M.Atk + 160).", AbilityEffect.Revive, 4, 20, 160, 10, 0, 0, // Arise / Raise 2
                targeting: AbilityTargeting.KnockedOut, revivePercent: 100),
        };

        /// <summary>A spell: aimed at any tile within <paramref name="range"/> (height doesn't block aiming), then its area.</summary>
        private static AbilityDefinition Spell(string displayName, int cost, string description, AbilityEffect effect, int range, int mp,
            int power, int speed, int radius, int vertical, SpellElement element = SpellElement.None, StatusEffect[] inflicts = null,
            AbilityTargeting targeting = AbilityTargeting.Standing, int revivePercent = 0, StatusEffect[] cures = null)
        {
            var ability = Ability(displayName, AbilityKind.Action, cost, TurnTrade.None, description);
            if (!ability.IsSpell)
            {
                ability.SetAction(effect, targeting, 0, range, 99, mpCost: mp);
                ability.SetSpell(power, speed, radius, vertical, element, inflicts, revivePercent);
                if (cures != null)
                    ability.SetCures(cures);
                EditorUtility.SetDirty(ability);
            }
            return ability;
        }

        private static AbilityDefinition CureItem(string displayName, int cost, string description, params StatusEffect[] cures)
        {
            var item = Item(displayName, cost, description, AbilityEffect.CureStatus, 0);
            if (item.Cures.Count == 0)
            {
                item.SetCures(cures);
                EditorUtility.SetDirty(item);
            }
            return item;
        }

        /// <summary>A reaction ability. Counter strikes carry their damage like an action (range 1, 3h reach).</summary>
        private static AbilityDefinition Reaction(string displayName, int cost, ReactionEffect effect, string description,
            Vector2Int? random = null, bool knockback = false)
        {
            var ability = Ability(displayName, AbilityKind.Reaction, cost, TurnTrade.None, description);
            if (ability.Reaction == ReactionEffect.None)
            {
                ability.SetReaction(effect);
                if (effect == ReactionEffect.CounterStrike)
                    ability.SetAction(AbilityEffect.Damage, AbilityTargeting.Standing, 1, 1, 3, randomMultiplier: random, knockback: knockback);
                EditorUtility.SetDirty(ability);
            }
            return ability;
        }

        /// <summary>Gives a job its command name and abilities, unless it already has abilities set.</summary>
        private static void Teach(JobDefinition job, string commandName, params AbilityDefinition[] abilities)
        {
            if (job.Abilities.Count > 0)
                return;
            job.SetAbilities(commandName, abilities);
            EditorUtility.SetDirty(job);
        }

        private static AbilityDefinition Ability(string displayName, AbilityKind kind, int cost, TurnTrade trade, string description)
        {
            TestSceneBuilder.EnsureFolder(AbilitiesFolder);
            string path = $"{AbilitiesFolder}/{displayName.Replace(" ", "")}.asset";
            var ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
            if (ability != null)
                return ability;
            ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.Define(displayName, description, kind, cost, trade);
            AssetDatabase.CreateAsset(ability, path);
            return ability;
        }

        /// <summary>
        /// Drill, after FFT's Squire Basic Skill (FFT name and JP cost in comments). Damage multipliers are unverified:
        /// sources disagree, so the melee Dash uses ×1–4 and the thrown stone ×1–2.
        /// </summary>
        private static AbilityDefinition[] CadetAbilities() => new[]
        {
            Action("Grit", 300, "Steel yourself: P.Atk +1 until the battle ends. Stacks.", // Accumulate, 300 JP
                AbilityEffect.BoostPhysicalAttack, AbilityTargeting.Self, 0, 0, 0, amount: 1),
            Action("Shoulder Charge", 80, "Barrel into an adjacent unit for P.Atk × 1–4 damage; may knock it back.", // Dash, 80 JP
                AbilityEffect.Damage, AbilityTargeting.Standing, 1, 1, 3, random: new Vector2Int(1, 4), knockback: true),
            Action("Hurl Rivet", 90, "Throw a rivet up to 4 tiles for P.Atk × 1–2 damage; may knock the target back.", // Throw Stone, 90 JP
                AbilityEffect.Damage, AbilityTargeting.Standing, 1, 4, 99, random: new Vector2Int(1, 2), knockback: true),
            Passive("Light Pack", AbilityKind.Movement, 200, "Travel light: Move +1.", moveBonus: 1), // Move +1, 200 JP
            Passive("Diligence", AbilityKind.Support, 200, "Earn 50% more Prof.", proficiencyBonusPercent: 50), // Gained JP Up, 200 JP
        };

        /// <summary>Field Kit items, after FFT's Chemist Item list (FFT name, amount and JP cost in comments).</summary>
        private static AbilityDefinition[] CorpsmanItems() => new[]
        {
            Item("Tonic", 30, "Restores 30 HP.", AbilityEffect.RestoreHp, 30),                     // Potion
            Item("Strong Tonic", 200, "Restores 70 HP.", AbilityEffect.RestoreHp, 70),             // Hi-Potion
            Item("Grand Tonic", 300, "Restores 150 HP.", AbilityEffect.RestoreHp, 150),            // X-Potion
            Item("Aether Vial", 300, "Restores 20 MP.", AbilityEffect.RestoreMp, 20),              // Ether
            Item("Aether Flask", 400, "Restores 50 MP.", AbilityEffect.RestoreMp, 50),             // Hi-Ether
            Item("Panacea", 900, "Restores all HP and MP.", AbilityEffect.RestoreAll, 0),          // Elixir
            Item("Revival Salts", 90, "Revives a knocked-out unit with a little HP.", AbilityEffect.Revive, 0, AbilityTargeting.KnockedOut), // Phoenix Down
        };

        /// <summary>Starting stock for each team in the test scene.</summary>
        public static Battle.Abilities.ItemStock[] TestItemStock() => new[]
        {
            new Battle.Abilities.ItemStock(LoadAbility("Tonic"), 4),
            new Battle.Abilities.ItemStock(LoadAbility("Strong Tonic"), 2),
            new Battle.Abilities.ItemStock(LoadAbility("Grand Tonic"), 1),
            new Battle.Abilities.ItemStock(LoadAbility("Aether Vial"), 2),
            new Battle.Abilities.ItemStock(LoadAbility("Aether Flask"), 1),
            new Battle.Abilities.ItemStock(LoadAbility("Panacea"), 1),
            new Battle.Abilities.ItemStock(LoadAbility("Revival Salts"), 2),
            new Battle.Abilities.ItemStock(LoadAbility("Antitoxin"), 2),
            new Battle.Abilities.ItemStock(LoadAbility("Eyewash"), 2),
            new Battle.Abilities.ItemStock(LoadAbility("Throat Lozenge"), 1),
            new Battle.Abilities.ItemStock(LoadAbility("Field Remedy"), 1),
        };

        public static AbilityDefinition LoadAbility(string displayName) =>
            AssetDatabase.LoadAssetAtPath<AbilityDefinition>($"{AbilitiesFolder}/{displayName.Replace(" ", "")}.asset");

        private static AbilityDefinition Action(string displayName, int cost, string description, AbilityEffect effect,
            AbilityTargeting targeting, int minRange, int maxRange, int reach, int amount = 0, Vector2Int? random = null, bool knockback = false)
        {
            var ability = Ability(displayName, AbilityKind.Action, cost, TurnTrade.None, description);
            if (ability.Effect == AbilityEffect.None)
            {
                ability.SetAction(effect, targeting, minRange, maxRange, reach, amount, random, knockback);
                EditorUtility.SetDirty(ability);
            }
            return ability;
        }

        private static AbilityDefinition Item(string displayName, int cost, string description, AbilityEffect effect, int amount,
            AbilityTargeting targeting = AbilityTargeting.Standing)
        {
            var ability = Ability(displayName, AbilityKind.Action, cost, TurnTrade.None, description);
            if (ability.Effect == AbilityEffect.None)
            {
                ability.SetAction(effect, targeting, 0, 1, 99, amount, isItem: true);
                EditorUtility.SetDirty(ability);
            }
            return ability;
        }

        private static AbilityDefinition Passive(string displayName, AbilityKind kind, int cost, string description,
            int moveBonus = 0, int proficiencyBonusPercent = 0, bool throwsItems = false)
        {
            var ability = Ability(displayName, kind, cost, TurnTrade.None, description);
            if (ability.MoveBonus == 0 && ability.ProficiencyBonusPercent == 0 && !ability.ThrowsItems)
            {
                ability.SetPassive(moveBonus, proficiencyBonusPercent, throwsItems);
                EditorUtility.SetDirty(ability);
            }
            return ability;
        }

        public static JobDefinition Load(string displayName) =>
            AssetDatabase.LoadAssetAtPath<JobDefinition>($"{JobsFolder}/{displayName}.asset");

        private static JobDefinition Job(string displayName, StatFactors multipliers, StatFactors growth,
            int move, int jump, int classEvasion, Color color, string description, params JobRequirement[] requirements)
        {
            string path = $"{JobsFolder}/{displayName}.asset";
            var job = AssetDatabase.LoadAssetAtPath<JobDefinition>(path);
            if (job != null)
                return job;

            job = ScriptableObject.CreateInstance<JobDefinition>();
            job.Define(displayName, description, multipliers, growth, move, jump, classEvasion, requirements, color);
            AssetDatabase.CreateAsset(job, path);
            return job;
        }

        /// <summary>The starting roster for the test scene: three per side, mixing base and advanced jobs.</summary>
        public static (List<JobDefinition> player, List<JobDefinition> enemy) TestRoster() => (
            new List<JobDefinition> { Load("Cadet"), Load("Vanguard"), Load("Warder") },
            new List<JobDefinition> { Load("Marksman"), Load("Corpsman"), Load("Evoker") });
    }
}
