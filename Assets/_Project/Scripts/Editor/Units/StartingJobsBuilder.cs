using System.Collections.Generic;
using FriendshipWarriors.Editor.Maps;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;
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
