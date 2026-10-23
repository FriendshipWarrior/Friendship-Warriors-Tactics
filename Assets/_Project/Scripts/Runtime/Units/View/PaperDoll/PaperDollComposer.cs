using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace FriendshipWarriors.Units.View.PaperDoll
{
    /// <summary>
    /// Builds a unit's sheet from its part sheets: stacks the layers in <see cref="PaperDollLayout.LayerOrder"/> for each
    /// pose, swaps the key colours, and caches the result by loadout and palette so identical units share one texture.
    /// Composition happens once per distinct loadout; frames are then just sprite lookups.
    /// </summary>
    public static class PaperDollComposer
    {
        private static readonly Dictionary<string, DollSheet> Cache = new();
        private static readonly Dictionary<Texture2D, Color32[]> PixelCache = new();
        private static readonly DollSlot[] AllSlots = (DollSlot[])System.Enum.GetValues(typeof(DollSlot));

        /// <summary>Number of distinct sheets built so far (for tests and diagnostics).</summary>
        public static int CachedSheetCount => Cache.Count;

        /// <summary>The unit's sheet, or null if no catalog exists yet (callers fall back to placeholders).</summary>
        public static DollSheet SheetFor(Unit unit) => SheetFor(unit, SpritePartCatalog.Load());

        public static DollSheet SheetFor(Unit unit, SpritePartCatalog catalog)
        {
            if (catalog == null || unit == null)
                return null;

            var palette = DollPalette.For(unit, catalog.HairStyleCount);
            var parts = Parts(unit, catalog, palette, out string cacheKey);
            if (Cache.TryGetValue(cacheKey, out var cached) && cached.IsAlive)
                return cached;

            var pixels = Build(parts, palette);
            var texture = new Texture2D(PaperDollLayout.SheetWidth, PaperDollLayout.SheetHeight, TextureFormat.RGBA32, false)
            {
                name = $"Doll {unit.Job.DisplayName}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var result = new DollSheet(texture);
            Cache[cacheKey] = result;
            return result;
        }

        /// <summary>The unit's recoloured sheet pixels (texture order), uncached; for previews and tools.</summary>
        public static Color32[] PixelsFor(Unit unit, SpritePartCatalog catalog)
        {
            var palette = DollPalette.For(unit, catalog.HairStyleCount);
            return Build(Parts(unit, catalog, palette, out _), palette);
        }

        private static Dictionary<DollSlot, Texture2D> Parts(Unit unit, SpritePartCatalog catalog, DollPalette palette, out string cacheKey)
        {
            var parts = new Dictionary<DollSlot, Texture2D>();
            var key = new StringBuilder(palette.Key);
            foreach (var slot in AllSlots)
            {
                var sheet = catalog.PartFor(slot, unit, palette.HairStyle);
                if (sheet == null || sheet.width != PaperDollLayout.SheetWidth || sheet.height != PaperDollLayout.SheetHeight)
                    continue;
                parts[slot] = sheet;
                key.Append('|').Append(slot).Append(':').Append(sheet.GetEntityId().ToString());
            }
            cacheKey = key.ToString();
            return parts;
        }

        private static Color32[] Build(Dictionary<DollSlot, Texture2D> parts, DollPalette palette)
        {
            var layers = new Dictionary<DollSlot, Color32[]>();
            foreach (var (slot, sheet) in parts)
                layers[slot] = Pixels(sheet);
            var pixels = Compose(layers, PaperDollLayout.SheetWidth, PaperDollLayout.SheetHeight);
            PaletteSwap.Apply(pixels, palette.ToSwapMap());
            return pixels;
        }

        /// <summary>
        /// Stacks layers per pose. Each pose block (front columns, back columns) uses its own order; a pixel shows the
        /// topmost opaque layer. Pixels are in Unity texture order (bottom-left origin), all the same size.
        /// </summary>
        public static Color32[] Compose(IReadOnlyDictionary<DollSlot, Color32[]> layers, int width, int height)
        {
            var result = new Color32[width * height];
            int poseWidth = width / 2;
            for (int pose = 0; pose < 2; pose++)
            {
                int x0 = pose * poseWidth;
                foreach (var slot in PaperDollLayout.LayerOrder(pose == 1))
                {
                    if (!layers.TryGetValue(slot, out var source) || source == null || source.Length != result.Length)
                        continue;
                    for (int y = 0; y < height; y++)
                    {
                        int row = y * width;
                        for (int x = x0; x < x0 + poseWidth; x++)
                        {
                            var p = source[row + x];
                            if (p.a > 0)
                                result[row + x] = p;
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>Drops cached sheets, e.g. after part PNGs are edited and reimported.</summary>
        public static void ClearCache()
        {
            foreach (var sheet in Cache.Values)
                sheet.Release();
            Cache.Clear();
            PixelCache.Clear();
        }

        private static Color32[] Pixels(Texture2D sheet)
        {
            if (!PixelCache.TryGetValue(sheet, out var pixels) || pixels == null)
            {
                pixels = sheet.GetPixels32();
                PixelCache[sheet] = pixels;
            }
            return pixels;
        }
    }
}
