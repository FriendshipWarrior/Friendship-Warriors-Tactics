using FriendshipWarriors.Battle.Drops;
using FriendshipWarriors.Editor.Maps;
using FriendshipWarriors.Editor.Units;
using UnityEditor;
using UnityEngine;

namespace FriendshipWarriors.Editor.Drops
{
    /// <summary>
    /// Creates the drop table the battle loads at runtime (<c>Resources/Battle/DropTable</c>): even rod/chest odds,
    /// rods teaching everything from unlocked jobs, and monster chests stepping up through the Field Kit items by
    /// level. An existing asset is left alone so Inspector edits stick.
    /// </summary>
    public static class DropTableBuilder
    {
        public const string Folder = "Assets/_Project/Resources/Battle";
        public const string Path = Folder + "/DropTable.asset";

        [MenuItem("Friendship Warriors/Data/Create Drop Table")]
        public static DropTable Create()
        {
            var existing = AssetDatabase.LoadAssetAtPath<DropTable>(Path);
            if (existing != null)
                return existing;
            TestSceneBuilder.EnsureFolder(Folder);
            var table = ScriptableObject.CreateInstance<DropTable>();
            table.Define(DropSettings.Default, new[]
            {
                // Placeholder steps, in order of the items' worth (their Prof costs); tune with real monsters.
                new MonsterLootTier(1, StartingJobsBuilder.LoadAbility("Tonic")),
                new MonsterLootTier(8, StartingJobsBuilder.LoadAbility("Revival Salts")),
                new MonsterLootTier(15, StartingJobsBuilder.LoadAbility("Strong Tonic")),
                new MonsterLootTier(25, StartingJobsBuilder.LoadAbility("Aether Vial")),
                new MonsterLootTier(35, StartingJobsBuilder.LoadAbility("Grand Tonic")),
                new MonsterLootTier(45, StartingJobsBuilder.LoadAbility("Aether Flask")),
                new MonsterLootTier(60, StartingJobsBuilder.LoadAbility("Panacea")),
            });
            AssetDatabase.CreateAsset(table, Path);
            AssetDatabase.SaveAssets();
            return table;
        }
    }
}
