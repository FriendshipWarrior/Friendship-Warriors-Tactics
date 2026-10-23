using FriendshipWarriors.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace FriendshipWarriors.Editor.UI
{
    /// <summary>
    /// Builds the pixel UI skin (<see cref="UiSkin"/>) from the Tiny RPG GUI and font packs: TextMeshPro font assets
    /// (bitmap atlases at each font's native pixel size, point-sampled; an SDF atlas for the outlined damage numbers)
    /// and the frame sheets with their 9-slice borders. Safe to re-run; called by the scene builders.
    /// </summary>
    public static class PixelUiBuilder
    {
        public const string FontsFolder = "Assets/_Project/Data/UI/Fonts";
        public const string SkinFolder = "Assets/_Project/Resources/UI";
        public const string SkinPath = SkinFolder + "/UiSkin.asset";

        // Every character the fonts draw (ASCII plus the Latin-1 letters in Font Kit I); missing ones are skipped.
        private const string Characters =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "¡¿ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖØÙÚÛÜÝÞßàáâãäåæçèéêëìíîïðñòóôõöøùúûüýþÿŒœ";

        [UnityEditor.MenuItem("Friendship Warriors/Art/Build Pixel UI Skin")]
        public static void BuildMenu()
        {
            Build();
            Debug.Log($"[PixelUi] Built {SkinPath}.");
        }

        [UnityEditor.MenuItem("Friendship Warriors/Art/Build Pixel UI Skin (Rebuild Font Atlases)")]
        public static void RebuildMenu()
        {
            Build(rebuildFonts: true);
            Debug.Log($"[PixelUi] Rebuilt the font atlases and {SkinPath}.");
        }

        public static UiSkin Build(bool rebuildFonts = false)
        {
            Maps.TestSceneBuilder.EnsureFolder(FontsFolder);
            Maps.TestSceneBuilder.EnsureFolder(SkinFolder);

            // Font Kit I's outlines sit on a 2-unit pixel grid: units per em / 2 is the native pixel size.
            var body = BitmapFont("TinyRPG-FineFantasyStrategies", 12, rebuildFonts);
            var title = BitmapFont("TinyRPG-BrilliantStrength", 14, rebuildFonts);
            var numbers = SdfFont("TinyRPG-FineFantasyStrategiesItalicNumbers", 48, rebuildFonts);

            var skin = AssetDatabase.LoadAssetAtPath<UiSkin>(SkinPath);
            if (skin == null)
            {
                skin = ScriptableObject.CreateInstance<UiSkin>();
                AssetDatabase.CreateAsset(skin, SkinPath);
            }
            skin.NumbersOutline = OutlineMaterial(numbers);
            skin.Body = body;
            skin.Title = title;
            skin.Numbers = numbers;
            skin.BodySize = 12;
            skin.TitleSize = 14;

            // Panel C (purple, gold rim): its corner filigree reaches 30 px in, so 32 keeps it whole.
            skin.Window = new UiSheet(Gui("20250420manaSoul9SlicesC-Sheet"), 96, 96, new Vector4(32, 32, 32, 32));
            // Button B: normal (plum), selected (blue), pressed, disabled; the swirls at each end are 16 px wide.
            skin.ItemFrames = new UiSheet(Gui("20250421manaSoulButtonB-Sheet"), 96, 22, new Vector4(16, 6, 16, 6));
            // The feather pointer: 5 frames of 37 x 14 (three still, two pulsing).
            skin.Cursor = new UiSheet(Gui("20250421horizontalCursorB-Sheet"), 37, 14);
            // Bar C: an 8 px tall hollow framed by 6 px ends.
            skin.Bar = new UiSheet(Gui("20250421barC-Sheet"), 95, 15, new Vector4(8, 4, 8, 4));
            skin.BarInterior = new Vector4(6, 4, 6, 3);
            EditorUtility.SetDirty(skin);
            AssetDatabase.SaveAssets();
            return skin;
        }

        /// <summary>The numbers font's material with a dark outline drawn outside the glyphs: the face is dilated by
        /// the outline width, so one-pixel strokes keep their colour inside the outline.</summary>
        private static Material OutlineMaterial(TMP_FontAsset font)
        {
            if (font == null)
                return null;
            string path = $"{FontsFolder}/{font.name} Outline.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(font.material);
                AssetDatabase.CreateAsset(material, path);
            }
            material.CopyPropertiesFromMaterial(font.material);
            material.shader = font.material.shader;
            material.name = font.name + " Outline";
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
            material.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(10, 10, 20, 255));
            material.SetFloat(ShaderUtilities.ID_FaceDilate, 0.25f);
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D Gui(string name) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{PixelUiImporter.GuiFolder}/{name}.png");

        private static Font SourceFont(string name) =>
            AssetDatabase.LoadAssetAtPath<Font>($"{PixelUiImporter.FontFolder}/{name}.ttf");

        private static TMP_FontAsset BitmapFont(string name, int pixelSize, bool rebuild) =>
            FontAsset(name, pixelSize, padding: 1, GlyphRenderMode.RASTER, 256, FilterMode.Point, rebuild: rebuild);

        private static TMP_FontAsset SdfFont(string name, int samplingSize, bool rebuild) =>
            FontAsset(name + " SDF", samplingSize, padding: 8, GlyphRenderMode.SDFAA, 1024, FilterMode.Bilinear, name, rebuild);

        /// <summary>The font asset at its path, created with every character pre-rendered into its atlas if it's missing
        /// (or <paramref name="rebuild"/>). Left alone otherwise, so rebuilding the skin doesn't churn the atlases.</summary>
        private static TMP_FontAsset FontAsset(string assetName, int samplingSize, int padding, GlyphRenderMode mode,
            int atlasSize, FilterMode filter, string sourceName = null, bool rebuild = false)
        {
            string path = $"{FontsFolder}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null && !rebuild)
                return existing;
            var font = SourceFont(sourceName ?? assetName);
            if (font == null)
            {
                Debug.LogError($"[PixelUi] Missing font {sourceName ?? assetName}.ttf in {PixelUiImporter.FontFolder}.");
                return existing;
            }
            if (existing != null)
                AssetDatabase.DeleteAsset(path);

            var asset = TMP_FontAsset.CreateFontAsset(font, samplingSize, padding, mode, atlasSize, atlasSize,
                AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: false);
            asset.name = assetName;
            asset.TryAddCharacters(Characters, out _);
            // Static: the atlas ships as built, so nothing is rasterised at runtime (and no glyph can come out blurry).
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(asset, path);

            var atlas = asset.atlasTexture;
            atlas.name = assetName + " Atlas";
            atlas.filterMode = filter;
            AssetDatabase.AddObjectToAsset(atlas, asset);
            var material = asset.material;
            material.name = assetName + " Material";
            // The full SDF shader draws outlines without a keyword (the mobile one needs OUTLINE_ON), which the popups
            // and unit tags rely on.
            if (mode != GlyphRenderMode.RASTER && Shader.Find("TextMeshPro/Distance Field") is { } sdf)
            {
                material.shader = sdf;
                // Hard edges: drawn at whole-pixel sizes, the SDF then looks like the bitmap font (plus its outline).
                material.SetFloat("_Sharpness", 1f);
            }
            AssetDatabase.AddObjectToAsset(material, asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
