using System.Linq;
using FriendshipWarriors.Battle;
using FriendshipWarriors.Cameras;
using FriendshipWarriors.Controls;
using FriendshipWarriors.Editor.Maps;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Generation;
using FriendshipWarriors.Grid.View;
using FriendshipWarriors.Menus;
using FriendshipWarriors.QuickPlay;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Equipment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FriendshipWarriors.Editor.Scenes
{
    /// <summary>
    /// Builds the game's front-end scenes from code — MainMenu and QuickPlay — and the Build Settings order
    /// (MainMenu, QuickPlay, then the Battle_Test dev sandbox). Relies on the shared assets the test scene builder
    /// creates (materials, palette, jobs, equipment, generator settings).
    /// </summary>
    public static class GameScenesBuilder
    {
        public const string MainMenuPath = "Assets/_Project/Scenes/MainMenu.unity";
        public const string QuickPlayPath = "Assets/_Project/Scenes/QuickPlay.unity";
        public const string BattleTestPath = "Assets/_Project/Scenes/Battle_Test.unity";
        public const string EquipmentCatalogPath = Units.StartingJobsBuilder.EquipmentFolder + "/EquipmentCatalog.asset";

        [MenuItem("Friendship Warriors/Dev/Rebuild Game Scenes")]
        public static void Rebuild()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Units.StartingJobsBuilder.CreateStartingJobs();
            CreateEquipmentCatalog();
            AssetDatabase.SaveAssets();

            // NewScene unloads unreferenced assets, so each scene loads what it needs after it is created.
            var menu = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            BuildMainMenu();
            EditorSceneManager.SaveScene(menu, MainMenuPath);

            var quickPlay = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            BuildQuickPlay();
            EditorSceneManager.SaveScene(quickPlay, QuickPlayPath);

            ApplyBuildSettings();
            EditorSceneManager.OpenScene(MainMenuPath);
        }

        /// <summary>MainMenu first (the game boots into it), then QuickPlay, then the dev sandbox — whichever exist.</summary>
        public static void ApplyBuildSettings()
        {
            EditorBuildSettings.scenes = new[] { MainMenuPath, QuickPlayPath, BattleTestPath }
                .Where(path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                .Select(path => new EditorBuildSettingsScene(path, true))
                .ToArray();
        }

        public static EquipmentCatalog CreateEquipmentCatalog()
        {
            var items = Units.StartingJobsBuilder.CreateStarterEquipment();
            var catalog = AssetDatabase.LoadAssetAtPath<EquipmentCatalog>(EquipmentCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<EquipmentCatalog>();
                AssetDatabase.CreateAsset(catalog, EquipmentCatalogPath);
            }
            catalog.SetItems(items);
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        // ------------------------------------------------------------------ Main menu

        private static void BuildMainMenu()
        {
            var camera = Camera.main;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.07f, 0.09f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200f;
            // The title backdrop gets the same pixel look as battles.
            camera.gameObject.AddComponent<PixelRendering>();
            TiltLight();

            var grid = CreateGridView("Backdrop Battlefield");
            var backdrop = new GameObject("Menu Backdrop").AddComponent<MenuBackdrop>();
            Set(backdrop, "_gridView", grid);
            Set(backdrop, "_camera", camera);
            Set(backdrop, "_generatorSettings", AssetDatabase.LoadAssetAtPath<MapGeneratorSettings>(ProjectAssets.DefaultGeneratorSettingsPath));

            var menuInput = CreateMenuInput();
            var screen = CreateMenuScreen("Main Menu Canvas", menuInput, sortingOrder: 0);
            SetFloat(screen, "_width", 760f);
            var controller = new GameObject("Main Menu").AddComponent<MainMenuController>();
            Set(controller, "_screen", screen);
            CreateEventSystem();
        }

        // ------------------------------------------------------------------ Quick play

        private static void BuildQuickPlay()
        {
            TiltLight();
            var input = new GameObject("Battle Input").AddComponent<BattleInput>();
            Set(input, "_actions", ControlsAsset);
            var grid = CreateGridView("Battlefield");

            var camera = Camera.main;
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200f;
            // Low-resolution pixel look, on by default (untick Pixelated on the camera for full resolution).
            camera.gameObject.AddComponent<PixelRendering>();
            var rig = new GameObject("Camera Rig").AddComponent<IsoCameraRig>();
            camera.transform.SetParent(rig.transform, false);
            Set(rig, "_input", input);
            Set(rig, "_camera", camera);

            var cursor = new GameObject("Tile Cursor").AddComponent<TileCursor>();
            Set(cursor, "_input", input);
            Set(cursor, "_gridView", grid);
            Set(cursor, "_cameraRig", rig);
            Set(cursor, "_camera", camera);

            var hud = CreateCanvas("Battle HUD", 0).AddComponent<FriendshipWarriors.UI.BattleHud>();
            var battle = new GameObject("Battle Controller").AddComponent<BattleController>();
            Set(battle, "_input", input);
            Set(battle, "_gridView", grid);
            Set(battle, "_cursor", cursor);
            Set(battle, "_cameraRig", rig);
            Set(battle, "_hud", hud);

            var menuInput = CreateMenuInput();
            var pause = CreateMenuScreen("Pause Canvas", menuInput, sortingOrder: 10);
            SetBool(pause, "_dimBackground", true);
            var deployment = new GameObject("Deployment").AddComponent<FriendshipWarriors.Battle.Deployment.DeploymentController>();
            Set(deployment, "_input", input);
            Set(deployment, "_gridView", grid);
            Set(deployment, "_cursor", cursor);
            Set(deployment, "_cameraRig", rig);
            Set(deployment, "_hud", hud);

            var director = new GameObject("Quick Play").AddComponent<QuickPlayDirector>();
            Set(director, "_battleInput", input);
            Set(director, "_menuInput", menuInput);
            Set(director, "_gridView", grid);
            Set(director, "_cameraRig", rig);
            Set(director, "_battle", battle);
            Set(director, "_deployment", deployment);
            Set(director, "_hud", hud);
            Set(director, "_pauseMenu", pause);
            Set(director, "_generatorSettings", AssetDatabase.LoadAssetAtPath<MapGeneratorSettings>(ProjectAssets.DefaultGeneratorSettingsPath));
            Set(director, "_jobs", AssetDatabase.LoadAssetAtPath<JobCatalog>(Units.StartingJobsBuilder.CatalogPath));
            Set(director, "_equipment", AssetDatabase.LoadAssetAtPath<EquipmentCatalog>(EquipmentCatalogPath));

            var stock = Units.StartingJobsBuilder.TestItemStock();
            var so = new SerializedObject(director);
            var items = so.FindProperty("_startingItems");
            items.arraySize = stock.Length;
            for (int i = 0; i < stock.Length; i++)
            {
                items.GetArrayElementAtIndex(i).FindPropertyRelative("_item").objectReferenceValue = stock[i].Item;
                items.GetArrayElementAtIndex(i).FindPropertyRelative("_count").intValue = stock[i].Count;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            CreateEventSystem();
        }

        // ------------------------------------------------------------------ Shared pieces

        private static InputActionAsset ControlsAsset => AssetDatabase.LoadAssetAtPath<InputActionAsset>(ProjectAssets.BattleControlsPath);

        private static void TiltLight()
        {
            var light = GameObject.Find("Directional Light");
            if (light != null)
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static GridView CreateGridView(string name)
        {
            var grid = new GameObject(name).AddComponent<GridView>();
            // Hillside is only the map built on Awake; the scene's driver swaps in a generated map on Start.
            Set(grid, "_map", AssetDatabase.LoadAssetAtPath<BattleMapData>(TestMapFactory.HillsidePath));
            Set(grid, "_palette", AssetDatabase.LoadAssetAtPath<TerrainPalette>(ProjectAssets.PlaceholderPalettePath));
            Set(grid, "_terrainMaterial", AssetDatabase.LoadAssetAtPath<Material>(ProjectAssets.TerrainMaterialPath));
            Set(grid, "_highlightMaterial", AssetDatabase.LoadAssetAtPath<Material>(ProjectAssets.HighlightMaterialPath));
            return grid;
        }

        private static MenuInput CreateMenuInput()
        {
            var menuInput = new GameObject("Menu Input").AddComponent<MenuInput>();
            Set(menuInput, "_actions", ControlsAsset);
            return menuInput;
        }

        private static FriendshipWarriors.UI.MenuScreen CreateMenuScreen(string name, MenuInput input, int sortingOrder)
        {
            var screen = CreateCanvas(name, sortingOrder).AddComponent<FriendshipWarriors.UI.MenuScreen>();
            Set(screen, "_input", input);
            return screen;
        }

        /// <summary>A Screen Space Overlay canvas scaled from 1920x1080 (readable on Steam Deck's 1280x800).</summary>
        private static GameObject CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return go;
        }

        private static void CreateEventSystem() =>
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

        private static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field) ?? throw new System.ArgumentException($"{target.GetType().Name} has no serialized field {field}");
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
