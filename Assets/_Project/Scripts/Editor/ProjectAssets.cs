using FriendshipWarriors.Grid.View;
using UnityEditor;
using UnityEngine;

namespace FriendshipWarriors.Editor
{
    /// <summary>Locations of shared assets that editor tooling creates or relies on.</summary>
    public static class ProjectAssets
    {
        public const string TerrainMaterialPath = "Assets/_Project/Materials/Terrain.mat";
        public const string HighlightMaterialPath = "Assets/_Project/Materials/Highlight.mat";
        public const string PlaceholderPalettePath = "Assets/_Project/Data/TerrainPalette_Placeholder.asset";
        public const string PropsFolder = "Assets/_Project/Data/Props";
        public const string MapsFolder = "Assets/_Project/Data/Maps";
        public const string BattleControlsPath = "Assets/_Project/Input/BattleControls.inputactions";
        public const string GenerationFolder = "Assets/_Project/Data/Generation";
        public const string DefaultGeneratorSettingsPath = GenerationFolder + "/MapGenerator_Default.asset";

        public static Material TerrainMaterial => AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialPath);
        public static Material HighlightMaterial => AssetDatabase.LoadAssetAtPath<Material>(HighlightMaterialPath);

        /// <summary>The placeholder palette if present, otherwise the first palette in the project.</summary>
        public static TerrainPalette DefaultPalette
        {
            get
            {
                var palette = AssetDatabase.LoadAssetAtPath<TerrainPalette>(PlaceholderPalettePath);
                if (palette != null)
                    return palette;
                var guids = AssetDatabase.FindAssets("t:" + nameof(TerrainPalette));
                return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<TerrainPalette>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
            }
        }

        public static T[] FindAll<T>() where T : Object
        {
            var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            var assets = new T[guids.Length];
            for (int i = 0; i < guids.Length; i++)
                assets[i] = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));
            return assets;
        }
    }
}
