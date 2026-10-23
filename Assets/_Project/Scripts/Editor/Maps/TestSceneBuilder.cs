using FriendshipWarriors.Battle;
using FriendshipWarriors.Cameras;
using FriendshipWarriors.Controls;
using FriendshipWarriors.Dev;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Generation;
using FriendshipWarriors.Grid.Props;
using FriendshipWarriors.Grid.View;
using FriendshipWarriors.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using System.Linq;

namespace FriendshipWarriors.Editor.Maps
{
    /// <summary>
    /// Regenerates the placeholder assets and the Battle_Test scene from code, so the test setup is reproducible
    /// and never depends on hand-wired references.
    /// </summary>
    public static class TestSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Battle_Test.unity";
        private const string TerrainMaterialPath = ProjectAssets.TerrainMaterialPath;
        private const string HighlightMaterialPath = ProjectAssets.HighlightMaterialPath;
        private const string PalettePath = ProjectAssets.PlaceholderPalettePath;
        private const string TreePath = ProjectAssets.PropsFolder + "/Tree.asset";
        private const string RockPath = ProjectAssets.PropsFolder + "/Rock.asset";
        private const string ControlsPath = ProjectAssets.BattleControlsPath;

        [MenuItem("Friendship Warriors/Dev/Rebuild Battle Test Scene")]
        public static void Rebuild()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolder(ProjectAssets.PropsFolder);
            CreateTerrainMaterial();
            CreateHighlightMaterial();
            CreatePalette();
            var tree = CreateProp(TreePath, "Tree", PropShape.Pillar, new Color(0.18f, 0.42f, 0.20f), 6, 0.5f);
            var rock = CreateProp(RockPath, "Rock", PropShape.Box, new Color(0.45f, 0.45f, 0.48f), 2, 0.7f);
            TestMapFactory.CreateHillsideAsset(tree, rock);
            CreateGeneratorSettings(tree, rock);
            Units.StartingJobsBuilder.CreateStartingJobs();
            Units.StartingJobsBuilder.CreateStarterEquipment();
            Setups.BattleSetupBuilder.CreateSetups();
            AssetDatabase.SaveAssets();

            // NewScene unloads unreferenced assets, so everything the scene references is loaded after it.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            BuildScene();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void BuildScene()
        {
            var light = GameObject.Find("Directional Light");
            if (light != null)
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var input = new GameObject("Battle Input").AddComponent<BattleInput>();
            Set(input, "_actions", AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControlsPath));

            var grid = new GameObject("Battlefield").AddComponent<GridView>();
            Set(grid, "_map", AssetDatabase.LoadAssetAtPath<BattleMapData>(TestMapFactory.HillsidePath));
            Set(grid, "_palette", AssetDatabase.LoadAssetAtPath<TerrainPalette>(PalettePath));
            Set(grid, "_terrainMaterial", AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialPath));
            Set(grid, "_highlightMaterial", AssetDatabase.LoadAssetAtPath<Material>(HighlightMaterialPath));

            var camera = Camera.main;
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200f;
            var rig = new GameObject("Camera Rig").AddComponent<IsoCameraRig>();
            camera.transform.SetParent(rig.transform, false);
            Set(rig, "_input", input);
            Set(rig, "_camera", camera);

            var cursor = new GameObject("Tile Cursor").AddComponent<TileCursor>();
            Set(cursor, "_input", input);
            Set(cursor, "_gridView", grid);
            Set(cursor, "_cameraRig", rig);
            Set(cursor, "_camera", camera);

            var hud = CreateHud();
            var battle = new GameObject("Battle Controller").AddComponent<BattleController>();
            Set(battle, "_input", input);
            Set(battle, "_gridView", grid);
            Set(battle, "_cursor", cursor);
            Set(battle, "_cameraRig", rig);
            Set(battle, "_hud", hud);

            var sandbox = new GameObject("Battle Sandbox").AddComponent<BattleSandbox>();
            Set(sandbox, "_input", input);
            Set(sandbox, "_gridView", grid);
            Set(sandbox, "_cameraRig", rig);
            Set(sandbox, "_battle", battle);
            Set(sandbox, "_generatorSettings", AssetDatabase.LoadAssetAtPath<MapGeneratorSettings>(ProjectAssets.DefaultGeneratorSettingsPath));
            Set(sandbox, "_setup", AssetDatabase.LoadAssetAtPath<FriendshipWarriors.Battle.Objectives.BattleSetup>(Setups.BattleSetupBuilder.StandardPath));
            SetArray(sandbox, "_starterGear", Units.StartingJobsBuilder.CreateStarterEquipment());
            Set(sandbox, "_itemJob", Units.StartingJobsBuilder.Load("Corpsman"));
            var stock = Units.StartingJobsBuilder.TestItemStock();
            var stockSo = new SerializedObject(sandbox);
            var stockProperty = stockSo.FindProperty("_startingItems");
            stockProperty.arraySize = stock.Length;
            for (int i = 0; i < stock.Length; i++)
            {
                var element = stockProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_item").objectReferenceValue = stock[i].Item;
                element.FindPropertyRelative("_count").intValue = stock[i].Count;
            }
            // A few starting statuses to try statuses, cures and reactions (rosters: Cadet, Vanguard, Warder vs
            // Marksman, Corpsman, Evoker), and an enemy Corpsman at Morale 8 so Panic shows up.
            var seeds = new (Team team, int index, FriendshipWarriors.Units.Status.StatusEffect status)[]
            {
                (Team.Player, 0, FriendshipWarriors.Units.Status.StatusEffect.Haste),
                (Team.Player, 1, FriendshipWarriors.Units.Status.StatusEffect.Protect),
                (Team.Player, 2, FriendshipWarriors.Units.Status.StatusEffect.Poison),
                (Team.Enemy, 0, FriendshipWarriors.Units.Status.StatusEffect.Blind),
                (Team.Enemy, 2, FriendshipWarriors.Units.Status.StatusEffect.Slow),
            };
            var seedProperty = stockSo.FindProperty("_startingStatuses");
            seedProperty.arraySize = seeds.Length;
            for (int i = 0; i < seeds.Length; i++)
            {
                var element = seedProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_team").enumValueIndex = (int)seeds[i].team;
                element.FindPropertyRelative("_rosterIndex").intValue = seeds[i].index;
                element.FindPropertyRelative("_status").enumValueIndex = (int)seeds[i].status;
            }
            stockSo.FindProperty("_lowMoraleEnemy").intValue = 1;
            stockSo.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A Screen Space Overlay canvas scaled from 1920x1080, plus the EventSystem for mouse input on UI.</summary>
        private static FriendshipWarriors.UI.BattleHud CreateHud()
        {
            var canvasGo = new GameObject("Battle HUD", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var hud = canvasGo.AddComponent<FriendshipWarriors.UI.BattleHud>();

            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            return hud;
        }

        private static void CreateGeneratorSettings(PropDefinition tree, PropDefinition rock)
        {
            if (AssetDatabase.LoadAssetAtPath<MapGeneratorSettings>(ProjectAssets.DefaultGeneratorSettingsPath) != null)
                return;
            EnsureFolder(ProjectAssets.GenerationFolder);
            var settings = ScriptableObject.CreateInstance<MapGeneratorSettings>();
            settings.SetProps(new[]
            {
                new MapGeneratorSettings.PropChoice(tree, 2f),
                new MapGeneratorSettings.PropChoice(rock, 1f),
            });
            AssetDatabase.CreateAsset(settings, ProjectAssets.DefaultGeneratorSettingsPath);
        }

        private static void CreateTerrainMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialPath) != null)
                return;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(material, TerrainMaterialPath);
        }

        private static void CreateHighlightMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(HighlightMaterialPath) != null)
                return;
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.5f));
            AssetDatabase.CreateAsset(material, HighlightMaterialPath);
        }

        private static void CreatePalette()
        {
            var palette = AssetDatabase.LoadAssetAtPath<TerrainPalette>(PalettePath);
            if (palette != null)
                return;
            palette = ScriptableObject.CreateInstance<TerrainPalette>();
            palette.SetLooks(new[]
            {
                Look(TerrainType.Grass, new Color(0.42f, 0.62f, 0.30f)),
                Look(TerrainType.Dirt, new Color(0.55f, 0.42f, 0.28f)),
                Look(TerrainType.Stone, new Color(0.58f, 0.58f, 0.62f)),
                Look(TerrainType.Sand, new Color(0.85f, 0.78f, 0.55f)),
                Look(TerrainType.Water, new Color(0.30f, 0.55f, 0.85f)),
            });
            AssetDatabase.CreateAsset(palette, PalettePath);
        }

        private static TerrainPalette.Look Look(TerrainType terrain, Color top) =>
            new(terrain, top, Color.Lerp(top, Color.black, 0.4f));

        private static PropDefinition CreateProp(string path, string displayName, PropShape shape, Color color, int height, float footprint)
        {
            var prop = AssetDatabase.LoadAssetAtPath<PropDefinition>(path);
            if (prop != null)
                return prop;
            prop = ScriptableObject.CreateInstance<PropDefinition>();
            AssetDatabase.CreateAsset(prop, path);
            var so = new SerializedObject(prop);
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_blocksMovement").boolValue = true;
            so.FindProperty("_height").intValue = height;
            so.FindProperty("_shape").enumValueIndex = (int)shape;
            so.FindProperty("_color").colorValue = color;
            so.FindProperty("_footprint").floatValue = footprint;
            so.ApplyModifiedPropertiesWithoutUndo();
            return prop;
        }

        private static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null)
                throw new System.ArgumentException($"{target.GetType().Name} has no serialized field {field}");
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
