using System.Collections.Generic;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Generation;
using FriendshipWarriors.Grid.Props;
using FriendshipWarriors.Grid.View;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Equipment;
using FriendshipWarriors.Units.View.Sheets;
using UnityEditor;
using UnityEngine;

namespace FriendshipWarriors.Editor.Art
{
    /// <summary>
    /// Wires the CC0 "Tiny Tactics – Battle Kit I" pack (Gabriel "tiopalada" Lima) in as placeholder art: a unit sheet
    /// per character (fighter, mage, cleric) with weapon swings, every job pointed at one, sprite props cut from the
    /// tileset, and terrain colours sampled from its palette. Re-running it rewrites those assets (idempotent); the pack
    /// PNGs themselves are never changed. Clear a job's Sheet to go back to its paper doll.
    /// </summary>
    public static class TinyTacticsArtBuilder
    {
        public const string PackFolder = "Assets/_Project/Art/ThirdParty/TinyTactics";
        public const string SheetsFolder = "Assets/_Project/Data/UnitSheets";
        public const string WeaponsPath = SheetsFolder + "/TinyTactics_Weapons.asset";

        /// <summary>
        /// Art pixels per world unit for the characters (before <c>UnitView.SpriteScale</c>). The pack draws a tile 32 px
        /// wide; our tile spans about 1.41 units across the screen, so ~22.6 px per unit matches the pack's proportions
        /// (28 here, × 1.25 sprite scale).
        /// </summary>
        public const float UnitPixelsPerUnit = 28f;

        /// <summary>Pixels per unit for props, at the pack's tile scale (see <see cref="UnitPixelsPerUnit"/>).</summary>
        public const float PropPixelsPerUnit = 23f;

        private const string Tileset = "20240420tinyTacticsTileset00";

        [MenuItem("Friendship Warriors/Art/Build Tiny Tactics Placeholders")]
        public static void Build()
        {
            var weapons = BuildWeapons();
            var fighter = BuildSheet("fighter", weapons,
                new UnitSheet.RecolorGroup(RecolorRole.JobOutfit, Colors("A8E3D1", "84BDC3", "7491AE")),
                new UnitSheet.RecolorGroup(RecolorRole.TeamAccent, Colors("4394D6", "455EB1")),
                new UnitSheet.RecolorGroup(RecolorRole.Hair, Colors("F2186C", "C9126E", "A80D70")));
            var mage = BuildSheet("mage", weapons,
                new UnitSheet.RecolorGroup(RecolorRole.JobOutfit, Colors("D92CBE", "B13DFF", "8A0C86", "A80D70", "47255D")),
                new UnitSheet.RecolorGroup(RecolorRole.TeamAccent, Colors("4394D6", "455EB1")));
            var cleric = BuildSheet("cleric", weapons,
                new UnitSheet.RecolorGroup(RecolorRole.JobOutfit, Colors("F27746", "D95572", "AF417D")),
                new UnitSheet.RecolorGroup(RecolorRole.TeamAccent, Colors("4394D6", "455EB1")));

            AssignJobs(("Cadet", fighter), ("Vanguard", fighter), ("Marksman", fighter), ("Corpsman", cleric),
                ("Warder", cleric), ("Evoker", mage));
            BuildProps();
            ApplyTerrainColors();
            AssetDatabase.SaveAssets();
            SheetComposer.ClearCache();
        }

        private static UnitSheet BuildSheet(string character, WeaponArt weapons, params UnitSheet.RecolorGroup[] recolors)
        {
            Texture2D T(string animation, string direction) => Texture($"20240427{character}-{animation}{direction}");
            UnitSheet.Strip Strip(SheetAnimation animation, string file, int originY, int frames, float seconds, bool swapped = false) =>
                new(animation, T(file, swapped ? "NE" : "SE"), T(file, swapped ? "SE" : "NE"), new Vector2Int(0, originY), frames, seconds);

            var strips = new[]
            {
                // The walking strip has two rows: the top one strides (walk), the bottom one barely steps (used as idle).
                Strip(SheetAnimation.Idle, "walking", 32, 4, 0.3f),
                // Walk frames follow the walk's own clock (WalkPlayback.WalkFrame), four frames like paper dolls.
                Strip(SheetAnimation.Walk, "walking", 0, 4, 0.15f),
                Strip(SheetAnimation.Attack, "attack", 0, 4, 0.1f),
                // The pack's charging pose is filed the other way round (its "NE" file shows the front).
                Strip(SheetAnimation.Charging, "charging", 0, 1, 0.5f, swapped: true),
                Strip(SheetAnimation.Release, "release", 0, 1, 0.4f),
                Strip(SheetAnimation.Hurt, "damage", 0, 1, 0.35f),
                Strip(SheetAnimation.Weak, "weak", 0, 1, 1f),
                Strip(SheetAnimation.KnockedOut, "dead", 0, 1, 1f),
            };
            var hair = character == "fighter"
                ? new Color[]
                {
                    Hex("C9126E"), new Color32(62, 56, 74, 255), new Color32(226, 184, 96, 255),
                    new Color32(196, 92, 50, 255), new Color32(190, 192, 204, 255), new Color32(120, 72, 44, 255),
                }
                : new Color[0];

            string path = $"{SheetsFolder}/TinyTactics_{char.ToUpperInvariant(character[0])}{character.Substring(1)}.asset";
            var sheet = LoadOrCreate<UnitSheet>(path);
            sheet.Define(new Vector2Int(32, 32), UnitPixelsPerUnit, strips, recolors, hair, weapons);
            EditorUtility.SetDirty(sheet);
            return sheet;
        }

        private static WeaponArt BuildWeapons()
        {
            WeaponArt.Entry Entry(WeaponType type, string name) =>
                new(type, Texture($"20240429weapons-{name}attackSE"), Texture($"20240429weapons-{name}attackNE"));

            var weapons = LoadOrCreate<WeaponArt>(WeaponsPath);
            // Rifles have no art in the pack, so Marksmen swing nothing. Iron Axe and Wooden Sword are unused for now.
            weapons.Define(new Vector2Int(48, 48), new[]
            {
                Entry(WeaponType.Sword, "IronSword"),
                Entry(WeaponType.Knife, "Hatchet"),
                Entry(WeaponType.Rod, "Scepter"),
                Entry(WeaponType.Staff, "WoodenStaff"),
            });
            EditorUtility.SetDirty(weapons);
            return weapons;
        }

        private static void AssignJobs(params (string job, UnitSheet sheet)[] assignments)
        {
            foreach (var (name, sheet) in assignments)
            {
                var job = Units.StartingJobsBuilder.Load(name);
                if (job == null)
                {
                    Debug.LogWarning($"[TinyTactics] No job named {name}; run Rebuild Battle Test Scene first.");
                    continue;
                }
                job.SetSheet(sheet);
                EditorUtility.SetDirty(job);
            }
        }

        /// <summary>Gives Tree and Rock the pack's sprites and adds Bush, Boulder, Stump and Log props.</summary>
        private static void BuildProps()
        {
            var tileset = Texture(Tileset);
            var tree = Prop("Tree", tileset, new RectInt(32, 102, 32, 47), 6);
            var rock = Prop("Rock", tileset, new RectInt(5, 131, 26, 27), 2);
            var bush = Prop("Bush", tileset, new RectInt(1, 100, 31, 26), 2);
            var boulder = Prop("Boulder", tileset, new RectInt(3, 164, 27, 26), 3);
            var stump = Prop("Stump", tileset, new RectInt(36, 165, 24, 16), 1);
            // Logs are drawn lying along one iso diagonal, so as a camera-facing sprite they look right from half the
            // camera angles; available in the map editor, left out of random maps.
            Prop("Log", tileset, new RectInt(0, 224, 32, 30), 2);

            var settings = AssetDatabase.LoadAssetAtPath<MapGeneratorSettings>(ProjectAssets.DefaultGeneratorSettingsPath);
            if (settings != null)
            {
                settings.SetProps(new[]
                {
                    new MapGeneratorSettings.PropChoice(tree, 2f),
                    new MapGeneratorSettings.PropChoice(rock, 1f),
                    new MapGeneratorSettings.PropChoice(bush, 1.5f),
                    new MapGeneratorSettings.PropChoice(boulder, 0.6f),
                    new MapGeneratorSettings.PropChoice(stump, 0.5f),
                });
                EditorUtility.SetDirty(settings);
            }
        }

        private static PropDefinition Prop(string name, Texture2D texture, RectInt rect, int height)
        {
            string path = $"{ProjectAssets.PropsFolder}/{name}.asset";
            var prop = AssetDatabase.LoadAssetAtPath<PropDefinition>(path);
            if (prop == null)
            {
                prop = ScriptableObject.CreateInstance<PropDefinition>();
                AssetDatabase.CreateAsset(prop, path);
                var so = new SerializedObject(prop);
                so.FindProperty("_displayName").stringValue = name;
                so.FindProperty("_blocksMovement").boolValue = true;
                so.FindProperty("_height").intValue = height;
                so.FindProperty("_color").colorValue = Color.gray;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            prop.SetSprite(texture, rect, PropPixelsPerUnit);
            EditorUtility.SetDirty(prop);
            return prop;
        }

        /// <summary>Terrain colours from the pack's palette: green grass over brick-red earth, slate stone, blue water.</summary>
        private static void ApplyTerrainColors()
        {
            var palette = AssetDatabase.LoadAssetAtPath<TerrainPalette>(ProjectAssets.PlaceholderPalettePath);
            if (palette == null)
                return;
            palette.SetLooks(new[]
            {
                new TerrainPalette.Look(TerrainType.Grass, Hex("55DC34"), Hex("D95572")),
                new TerrainPalette.Look(TerrainType.Dirt, Hex("F27746"), Hex("AF417D")),
                new TerrainPalette.Look(TerrainType.Stone, Hex("84BDC3"), Hex("6B6A9C")),
                new TerrainPalette.Look(TerrainType.Sand, Hex("F5A13A"), Hex("D95572")),
                new TerrainPalette.Look(TerrainType.Water, Hex("4394D6"), Hex("455EB1")),
            });
            EditorUtility.SetDirty(palette);
        }

        private static Texture2D Texture(string file)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PackFolder}/{file}.png");
            if (texture == null)
                Debug.LogWarning($"[TinyTactics] Missing {PackFolder}/{file}.png");
            return texture;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            Maps.TestSceneBuilder.EnsureFolder(SheetsFolder);
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static Color32[] Colors(params string[] hex)
        {
            var colors = new List<Color32>();
            foreach (var h in hex)
                colors.Add(Hex(h));
            return colors.ToArray();
        }

        private static Color32 Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }
    }
}
