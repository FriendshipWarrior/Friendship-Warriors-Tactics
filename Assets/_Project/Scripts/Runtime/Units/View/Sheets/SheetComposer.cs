using System.Collections.Generic;

namespace FriendshipWarriors.Units.View.Sheets
{
    /// <summary>
    /// Builds and caches a unit's recoloured sheet by (sheet, palette), so units of the same job and team that share
    /// a hair colour share one texture, as <see cref="PaperDoll.PaperDollComposer"/> does for paper dolls.
    /// </summary>
    public static class SheetComposer
    {
        private static readonly Dictionary<string, ComposedSheet> Cache = new();

        /// <summary>Number of distinct sheets built so far (for tests and diagnostics).</summary>
        public static int CachedSheetCount => Cache.Count;

        /// <summary>The unit's composed sheet, or null when its job has no sheet (callers use the paper doll).</summary>
        public static ComposedSheet SheetFor(Unit unit)
        {
            var sheet = unit?.Job != null ? unit.Job.Sheet : null;
            if (sheet == null)
                return null;
            var palette = SheetPalette.For(unit, sheet);
            string key = $"{sheet.GetEntityId()}|{palette.Key}";
            if (Cache.TryGetValue(key, out var cached) && cached.IsAlive)
                return cached;
            var composed = new ComposedSheet(sheet, palette.Map);
            Cache[key] = composed;
            return composed;
        }

        /// <summary>Drops cached sheets, e.g. after the sheet textures or assets change.</summary>
        public static void ClearCache()
        {
            foreach (var sheet in Cache.Values)
                sheet.Release();
            Cache.Clear();
        }
    }
}
