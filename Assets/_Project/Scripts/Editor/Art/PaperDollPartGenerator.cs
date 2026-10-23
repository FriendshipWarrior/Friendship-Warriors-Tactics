using System.Collections.Generic;
using System.IO;
using System.Text;
using FriendshipWarriors.Units.Equipment;
using FriendshipWarriors.Units.View.PaperDoll;
using UnityEditor;
using UnityEngine;

namespace FriendshipWarriors.Editor.Art
{
    /// <summary>
    /// Friendship Warriors → Art → Generate Paper-Doll Parts: draws the first-draft part sheets, exports the key
    /// palette and points the <see cref="SpritePartCatalog"/> at the parts. Existing PNGs are kept (they may be hand
    /// edited) unless Regenerate (Overwrite) is chosen and confirmed.
    /// </summary>
    public static class PaperDollPartGenerator
    {
        public const string PartsFolder = "Assets/_Project/Art/Sprites/Parts";
        public const string PalettesFolder = "Assets/_Project/Art/Palettes";
        public const string CatalogPath = "Assets/_Project/Resources/" + SpritePartCatalog.ResourcePath + ".asset";

        private static readonly PartKind[] Hair = { PartKind.HairCrop, PartKind.HairSwept, PartKind.HairPonytail, PartKind.HairLong };

        [MenuItem("Friendship Warriors/Art/Generate Paper-Doll Parts", priority = 100)]
        public static void GenerateMissing() => Generate(overwrite: false);

        [MenuItem("Friendship Warriors/Art/Regenerate Paper-Doll Parts (Overwrite)...", priority = 101)]
        public static void RegenerateWithConfirmation()
        {
            if (EditorUtility.DisplayDialog("Regenerate paper-doll parts",
                    $"Overwrite every generated part PNG in {PartsFolder}? Hand edits to those files will be lost.",
                    "Overwrite", "Cancel"))
                Generate(overwrite: true);
        }

        /// <summary>Writes every part and palette, then refreshes the catalog. Returns how many files were written.</summary>
        public static int Generate(bool overwrite)
        {
            int written = 0;
            foreach (PartKind kind in System.Enum.GetValues(typeof(PartKind)))
            {
                var sheet = DrawSheet(kind);
                if (WritePart($"{PartsFolder}/{PaperDollArt.FileName(kind)}.png", sheet.EncodeToPNG(), overwrite))
                    written++;
                Object.DestroyImmediate(sheet);
            }
            if (WritePart($"{PalettesFolder}/PaperDollKeys.gpl", Encoding.UTF8.GetBytes(Gpl()), overwrite))
                written++;
            if (WritePart($"{PalettesFolder}/PaperDollKeys.png", PalettePng(), overwrite))
                written++;

            AssetDatabase.Refresh();
            UpdateCatalog();
            Debug.Log($"[PaperDoll] Wrote {written} file(s){(overwrite ? " (overwrite)" : ", kept existing")}.");
            return written;
        }

        /// <summary>
        /// Writes <paramref name="bytes"/> to <paramref name="path"/> unless the file exists and
        /// <paramref name="overwrite"/> is false. Returns whether it wrote.
        /// </summary>
        public static bool WritePart(string path, byte[] bytes, bool overwrite)
        {
            if (File.Exists(path) && !overwrite)
                return false;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
            return true;
        }

        /// <summary>A full 320×240 part sheet (both poses, every animation) as a texture.</summary>
        internal static Texture2D DrawSheet(PartKind kind)
        {
            var sheet = new PixelCanvas(PaperDollLayout.SheetWidth, PaperDollLayout.SheetHeight);
            for (int row = 0; row < PaperDollLayout.Rows; row++)
            {
                var animation = (DollAnimation)row;
                for (int frame = 0; frame < PaperDollLayout.FrameCount(animation); frame++)
                for (int pose = 0; pose < 2; pose++)
                {
                    bool back = pose == 1;
                    var drawn = PaperDollArt.Draw(kind, DollRig.For(animation, frame), back);
                    if (animation == DollAnimation.KnockedOut)
                        drawn = drawn.LyingDown();
                    var at = PaperDollLayout.FrameTopLeft(animation, frame, back);
                    sheet.Blit(drawn, at.x, at.y);
                }
            }

            var texture = new Texture2D(sheet.Width, sheet.Height, TextureFormat.RGBA32, false);
            texture.SetPixels32(sheet.ToTextureOrder());
            texture.Apply();
            return texture;
        }

        private static string Gpl()
        {
            var text = new StringBuilder();
            text.AppendLine("GIMP Palette");
            text.AppendLine("Name: Friendship Warriors Paper-Doll Keys");
            text.AppendLine("Columns: 8");
            text.AppendLine("# (key) colours are recoloured per unit; paint parts only with these colours.");
            foreach (var (name, c) in KeyPalette.All())
                text.AppendLine($"{c.r,3} {c.g,3} {c.b,3}\t{name}");
            return text.ToString();
        }

        /// <summary>One 8×8 swatch per colour in a row, the format Aseprite and most editors load as a palette image.</summary>
        private static byte[] PalettePng()
        {
            var colors = new List<Color32>();
            foreach (var (_, c) in KeyPalette.All())
                colors.Add(c);
            const int swatch = 8;
            var texture = new Texture2D(colors.Count * swatch, swatch, TextureFormat.RGBA32, false);
            var pixels = new Color32[texture.width * texture.height];
            for (int y = 0; y < swatch; y++)
            for (int x = 0; x < texture.width; x++)
                pixels[y * texture.width + x] = colors[x / swatch];
            texture.SetPixels32(pixels);
            texture.Apply();
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            return png;
        }

        private static Texture2D Part(PartKind kind) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{PartsFolder}/{PaperDollArt.FileName(kind)}.png");

        /// <summary>Creates the catalog if needed and points it at the generated parts (item overrides are kept).</summary>
        public static SpritePartCatalog UpdateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SpritePartCatalog>(CatalogPath);
            if (catalog == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath)!);
                catalog = ScriptableObject.CreateInstance<SpritePartCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var hair = new Texture2D[Hair.Length];
            for (int i = 0; i < Hair.Length; i++)
                hair[i] = Part(Hair[i]);

            catalog.Define(Part(PartKind.Body), Part(PartKind.Head), Part(PartKind.Face), hair,
                new[]
                {
                    new SpritePartCatalog.ArmorPart(ArmorType.Hat, Part(PartKind.Cap)),
                    new SpritePartCatalog.ArmorPart(ArmorType.Helmet, Part(PartKind.Helm)),
                },
                new[]
                {
                    new SpritePartCatalog.ArmorPart(ArmorType.Clothing, Part(PartKind.Fatigues)),
                    new SpritePartCatalog.ArmorPart(ArmorType.Armor, Part(PartKind.Cuirass)),
                    new SpritePartCatalog.ArmorPart(ArmorType.Robe, Part(PartKind.Robe)),
                },
                new[]
                {
                    new SpritePartCatalog.WeaponPart(WeaponType.Sword, Part(PartKind.Saber)),
                    new SpritePartCatalog.WeaponPart(WeaponType.Knife, Part(PartKind.Knife)),
                    new SpritePartCatalog.WeaponPart(WeaponType.Rifle, Part(PartKind.Rifle)),
                    new SpritePartCatalog.WeaponPart(WeaponType.Rod, Part(PartKind.Rod)),
                    new SpritePartCatalog.WeaponPart(WeaponType.Staff, Part(PartKind.Staff)),
                },
                Part(PartKind.Buckler));
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            PaperDollComposer.ClearCache();
            return catalog;
        }
    }
}
