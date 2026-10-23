using FriendshipWarriors.Units.View.Sheets;
using UnityEditor;

namespace FriendshipWarriors.Editor.Art
{
    /// <summary>
    /// Import settings for the Tiny Tactics pack (readable, point-filtered, uncompressed, no mipmaps), so unit sheets
    /// can be recoloured by exact colour and props cut from the tileset stay crisp; flushes composed sheets on reimport.
    /// </summary>
    internal sealed class TinyTacticsImporter : AssetPostprocessor
    {
        private static bool IsPackTexture(string path) => path.StartsWith(TinyTacticsArtBuilder.PackFolder + "/") && path.EndsWith(".png");

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

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
            {
                if (IsPackTexture(path) || path.StartsWith(TinyTacticsArtBuilder.SheetsFolder + "/"))
                {
                    SheetComposer.ClearCache();
                    return;
                }
            }
        }
    }
}
