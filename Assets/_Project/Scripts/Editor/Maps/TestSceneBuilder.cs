using FriendshipWarriors.Cameras;
using FriendshipWarriors.Controls;
using FriendshipWarriors.Dev;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Props;
using FriendshipWarriors.Grid.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace FriendshipWarriors.Editor.Maps
{
    /// <summary>
    /// Regenerates the placeholder assets and the Battle_Test scene from code, so the test setup is reproducible
    /// and never depends on hand-wired references.
    /// </summary>
    public static class TestSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Battle_Test.unity";
        private const string TerrainMaterialPath = "Assets/_Project/Materials/Terrain.mat";
        private const string HighlightMaterialPath = "Assets/_Project/Materials/Highlight.mat";
        private const string PalettePath = "Assets/_Project/Data/TerrainPalette_Placeholder.asset";
        private const string TreePath = "Assets/_Project/Data/Props/Tree.asset";
        private const string RockPath = "Assets/_Project/Data/Props/Rock.asset";
        private const string ControlsPath = "Assets/_Project/Input/BattleControls.inputactions";

        [MenuItem("Friendship Warriors/Dev/Rebuild Battle Test Scene")]
        public static void Rebuild()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolder("Assets/_Project/Data/Props");
            CreateTerrainMaterial();
            CreateHighlightMaterial();
            CreatePalette();
            var tree = CreateProp(TreePath, "Tree", PropShape.Pillar, new Color(0.18f, 0.42f, 0.20f), 6, 0.5f);
            var rock = CreateProp(RockPath, "Rock", PropShape.Box, new Color(0.45f, 0.45f, 0.48f), 2, 0.7f);
            TestMapFactory.CreateHillsideAsset(tree, rock);
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

            var unit = new GameObject("Unit");
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(unit.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            body.transform.localScale = new Vector3(0.5f, 0.6f, 0.5f);

            var sandbox = new GameObject("Grid Sandbox").AddComponent<GridSandbox>();
            Set(sandbox, "_gridView", grid);
            Set(sandbox, "_cursor", cursor);
            Set(sandbox, "_cameraRig", rig);
            Set(sandbox, "_unit", unit.transform);
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

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null)
                throw new System.ArgumentException($"{target.GetType().Name} has no serialized field {field}");
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
