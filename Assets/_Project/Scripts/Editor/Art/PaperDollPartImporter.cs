using FriendshipWarriors.Units.View.PaperDoll;
using UnityEditor;

namespace FriendshipWarriors.Editor.Art
{
    /// <summary>
    /// Import settings for part and palette PNGs (readable, point-filtered, uncompressed, no mipmaps) so the composer can read exact
    /// key colours, and a cache flush when parts change so edits show up without restarting.
    /// </summary>
    internal sealed class PaperDollPartImporter : AssetPostprocessor
    {
        private static bool IsPart(string path) => path.StartsWith(PaperDollPartGenerator.PartsFolder + "/") && path.EndsWith(".png");

        private static bool IsPalette(string path) => path.StartsWith(PaperDollPartGenerator.PalettesFolder + "/") && path.EndsWith(".png");

        private void OnPreprocessTexture()
        {
            if (!(IsPart(assetPath) || IsPalette(assetPath)) || assetImporter is not TextureImporter importer)
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
                if (IsPart(path))
                {
                    PaperDollComposer.ClearCache();
                    return;
                }
            }
        }
    }
}
