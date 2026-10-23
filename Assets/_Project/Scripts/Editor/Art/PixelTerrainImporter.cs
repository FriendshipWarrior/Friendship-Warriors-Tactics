using UnityEditor;

namespace FriendshipWarriors.Editor.Art
{
    /// <summary>
    /// Import settings for the Green Wilderness pack and the terrain atlas built from it: readable (the builder reads
    /// pixels), point-filtered, uncompressed, no mipmaps, clamped. Pixel art must never be smoothed or compressed.
    /// </summary>
    internal sealed class PixelTerrainImporter : AssetPostprocessor
    {
        private static bool IsPixelTerrainTexture(string path) =>
            path.EndsWith(".png") &&
            (path.StartsWith(PixelTerrainBuilder.PackFolder + "/") || path.StartsWith(PixelTerrainBuilder.AtlasFolder + "/"));

        private void OnPreprocessTexture()
        {
            if (!IsPixelTerrainTexture(assetPath) || assetImporter is not TextureImporter importer)
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
