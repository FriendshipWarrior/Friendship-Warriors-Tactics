using UnityEditor;

namespace FriendshipWarriors.Editor.UI
{
    /// <summary>
    /// Import settings for the Tiny RPG GUI pack (readable, point-filtered, uncompressed, no mipmaps), so panels,
    /// cursors and bars stay crisp at integer scales. Sprites are cut from these textures at runtime by
    /// <see cref="FriendshipWarriors.UI.UiSkin"/>, so the importer leaves them as plain textures.
    /// </summary>
    internal sealed class PixelUiImporter : AssetPostprocessor
    {
        public const string GuiFolder = "Assets/_Project/Art/ThirdParty/TinyRPG_ManaSoulGUI";
        public const string FontFolder = "Assets/_Project/Art/ThirdParty/TinyRPG_FontKit01";

        private static bool IsPackTexture(string path) =>
            (path.StartsWith(GuiFolder + "/") || path.StartsWith(FontFolder + "/")) && path.EndsWith(".png");

        private void OnPreprocessTexture()
        {
            if (!IsPackTexture(assetPath) || assetImporter is not TextureImporter importer)
                return;
            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.filterMode = UnityEngine.FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
        }
    }
}
