using FriendshipWarriors.Battle.Objectives;
using FriendshipWarriors.Editor.Maps;
using FriendshipWarriors.Editor.Units;
using UnityEditor;
using UnityEngine;

namespace FriendshipWarriors.Editor.Setups
{
    /// <summary>
    /// Creates the prebuilt battle setups: a standard 5 v 5 match and two campaign-style objectives to test with.
    /// Existing assets are left alone so Inspector edits stick.
    /// </summary>
    public static class BattleSetupBuilder
    {
        public const string SetupsFolder = "Assets/_Project/Data/Setups";
        public const string StandardPath = SetupsFolder + "/Setup_Standard.asset";
        public const string CommanderPath = SetupsFolder + "/Setup_DefeatTheCommander.asset";
        public const string EscortPath = SetupsFolder + "/Setup_Escort.asset";

        [MenuItem("Friendship Warriors/Data/Create Battle Setups")]
        public static void CreateSetups()
        {
            TestSceneBuilder.EnsureFolder(SetupsFolder);

            Create(StandardPath, "Standard (5 v 5)", PlayerRoster(), EnemyRoster(), BattleObjective.Standard);

            var commanderEnemies = EnemyRoster();
            commanderEnemies[3] = new RosterEntry(StartingJobsBuilder.Load("Vanguard"), "commander", "Commander Vael", 7);
            Create(CommanderPath, "Defeat the Commander", PlayerRoster(), commanderEnemies,
                new BattleObjective(VictoryCondition.DefeatTarget, "commander"));

            var escortPlayers = PlayerRoster();
            escortPlayers[2] = new RosterEntry(StartingJobsBuilder.Load("Warder"), "escort", "Escort Lira");
            Create(EscortPath, "Escort", escortPlayers, EnemyRoster(),
                new BattleObjective(VictoryCondition.DefeatAllEnemies, "", "escort"));

            AssetDatabase.SaveAssets();
        }

        /// <summary>Five a side, all six jobs between them.</summary>
        private static RosterEntry[] PlayerRoster() => new[]
        {
            new RosterEntry(StartingJobsBuilder.Load("Cadet")),
            new RosterEntry(StartingJobsBuilder.Load("Vanguard")),
            new RosterEntry(StartingJobsBuilder.Load("Warder")),
            new RosterEntry(StartingJobsBuilder.Load("Evoker")),
            new RosterEntry(StartingJobsBuilder.Load("Corpsman")),
        };

        private static RosterEntry[] EnemyRoster() => new[]
        {
            new RosterEntry(StartingJobsBuilder.Load("Marksman")),
            new RosterEntry(StartingJobsBuilder.Load("Corpsman")),
            new RosterEntry(StartingJobsBuilder.Load("Evoker")),
            new RosterEntry(StartingJobsBuilder.Load("Vanguard")),
            new RosterEntry(StartingJobsBuilder.Load("Cadet")),
        };

        private static void Create(string path, string displayName, RosterEntry[] players, RosterEntry[] enemies, BattleObjective objective)
        {
            if (AssetDatabase.LoadAssetAtPath<BattleSetup>(path) != null)
                return;
            var setup = ScriptableObject.CreateInstance<BattleSetup>();
            setup.Define(displayName, BattleSetup.StandardTeamSize, BattleSetup.StandardTeamSize, 5, players, enemies, objective);
            AssetDatabase.CreateAsset(setup, path);
        }
    }
}
