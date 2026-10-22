using System.Collections.Generic;
using FriendshipWarriors.Editor.Maps;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;
using FriendshipWarriors.Units.Equipment;
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
            Teach(cadet, "Drill");
            Teach(corpsman, "Field Kit");
            Teach(vanguard, "Assault", doubleTime);
            Teach(marksman, "Marksmanship", rapidFire);
            Teach(evoker, "Evocation");
            Teach(warder, "Warding");

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
                Weapon("Service Saber", WeaponType.Sword, 4, 1, 1, 3, "Standard-issue officer's saber."),
                Weapon("Field Knife", WeaponType.Knife, 3, 1, 1, 3, "Utility blade every corpsman carries."),
                Weapon("Service Rifle", WeaponType.Rifle, 4, 1, 5, 99, "Bolt-action long rifle with brass sights.", twoHanded: true),
                Weapon("Aether Rod", WeaponType.Rod, 3, 1, 1, 3, "Copper focus rod humming with aether."),
                Weapon("Ward Staff", WeaponType.Staff, 3, 1, 1, 3, "Ironwood staff etched with warding glyphs."),
                Armor("Buckler", EquipmentCategory.Shield, 10, "Riveted steel buckler."),
                Gear("Steel Helm", EquipmentCategory.Head, ArmorType.Helmet, 10, 0, "Riveted helm with a brass crest."),
                Gear("Field Cap", EquipmentCategory.Head, ArmorType.Hat, 8, 0, "Peaked cap with goggles."),
                Gear("Plate Cuirass", EquipmentCategory.Body, ArmorType.Armor, 10, 0, "Steam-pressed steel breastplate."),
                Gear("Aether Robe", EquipmentCategory.Body, ArmorType.Robe, 10, 10, "Robe stitched with aether-conductive thread."),
                Gear("Fatigues", EquipmentCategory.Body, ArmorType.Clothing, 5, 0, "Standard-issue field uniform."),
            };
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
