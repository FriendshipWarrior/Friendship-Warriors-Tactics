using System;
using System.Collections.Generic;
using System.IO;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Generation;
using FriendshipWarriors.Grid.Props;
using FriendshipWarriors.Grid.View;
using UnityEditor;
using UnityEngine;

namespace FriendshipWarriors.Editor.Art
{
    /// <summary>
    /// Builds the pixel-art battlefield from tiopalada's "Tiny RPG – Green Wilderness" (CC BY 4.0): a point-filtered
    /// terrain atlas (grass, dirt and water tops, cliff faces, grass/dirt lips; stone and sand drawn here in the pack's
    /// palette, which has none), the <see cref="TerrainAtlas"/> asset pointing at its cells, the placeholder palette
    /// using it, and sprite props (pine, oak, shrub, rock, mushroom, wildflowers) for the map editor, the random
    /// generator and the test map. Idempotent; the pack's PNG is only read. Cell coordinates are (column, row) from
    /// the sheet's top left, see the pack folder's README.
    /// </summary>
    public static class PixelTerrainBuilder
    {
        public const string PackFolder = "Assets/_Project/Art/ThirdParty/TinyRPG_GreenWilderness";
        public const string AtlasFolder = "Assets/_Project/Art/Terrain";
        public const string AtlasTexturePath = AtlasFolder + "/TerrainAtlas.png";
        public const string AtlasAssetPath = "Assets/_Project/Data/TerrainAtlas.asset";
        private const string SheetPath = PackFolder + "/20240303tilesetWilderness32x32.png";

        private const int Cell = 32;
        private const int Gutter = 2;
        private const int Columns = 8;

        /// <summary>Props at the units' pixel density, so a pixel is the same size on a unit and on a tree.</summary>
        public const float PropPixelsPerUnit = TinyTacticsArtBuilder.PropPixelsPerUnit;

        // The pack's palette, for the terrain it doesn't draw.
        private static readonly Color32 RockOutline = new(76, 70, 130, 255);
        private static readonly Color32 RockDark = new(111, 141, 170, 255);
        private static readonly Color32 RockMid = new(123, 182, 188, 255);
        private static readonly Color32 RockLight = new(156, 217, 198, 255);
        private static readonly Color32 SandMid = new(239, 161, 66, 255);
        private static readonly Color32 SandLight = new(246, 212, 59, 255);
        private static readonly Color32 SandDark = new(236, 120, 75, 255);
        private static readonly Color32 SandPale = new(251, 228, 176, 255);

        [MenuItem("Friendship Warriors/Art/Build Pixel Terrain")]
        public static void Build()
        {
            var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            if (sheet == null)
            {
                Debug.LogWarning($"[PixelTerrain] Missing {SheetPath}; terrain keeps its flat colours.");
                return;
            }

            var atlas = BuildAtlas(sheet);
            var palette = AssetDatabase.LoadAssetAtPath<TerrainPalette>(ProjectAssets.PlaceholderPalettePath);
            if (palette != null)
            {
                palette.SetAtlas(atlas);
                EditorUtility.SetDirty(palette);
            }
            BuildProps(sheet);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ Atlas

        private static TerrainAtlas BuildAtlas(Texture2D sheet)
        {
            var source = new Sheet(sheet);
            var cells = new List<Color32[]>();
            int Add(Color32[] pixels)
            {
                cells.Add(pixels);
                return cells.Count - 1;
            }

            var dirt = new[] { source.Cell(1, 6), source.Cell(12, 6) };
            // Grass is drawn over dirt in the pack; the full grass cells are opaque, but lay them over dirt anyway so
            // any see-through pixel shows earth, as in the pack.
            int[] grassTops =
            {
                Add(Over(dirt[0], source.Cell(1, 1))), Add(Over(dirt[1], source.Cell(0, 4))),
                Add(Over(dirt[0], source.Cell(1, 4))), Add(Over(dirt[1], source.Cell(2, 4))),
            };
            int[] dirtTops = { Add(dirt[0]), Add(dirt[1]) };
            int[] waterTops = { Add(source.Cell(12, 1)) };
            int[] cliffs = { Add(source.Cell(1, 9)), Add(source.Cell(2, 9)) };
            int grassLip = Add(Lip(source.Cell(1, 2), 13, 10));
            int dirtLip = Add(Lip(source.Cell(1, 8), 22, 10));
            int[] stoneTops = { Add(Cobbles(11)), Add(Cobbles(23)), Add(Cobbles(37)) };
            int[] stoneSides = { Add(StoneWall(5)), Add(StoneWall(17)) };
            int[] sandTops = { Add(Sand(3)), Add(Sand(29)), Add(Sand(41)) };

            var tiles = new[]
            {
                new TerrainAtlas.Tiles(TerrainType.Grass, grassTops, true, cliffs, grassLip, 10),
                new TerrainAtlas.Tiles(TerrainType.Dirt, dirtTops, true, cliffs, dirtLip, 10),
                new TerrainAtlas.Tiles(TerrainType.Stone, stoneTops, true, stoneSides, -1, 0),
                new TerrainAtlas.Tiles(TerrainType.Sand, sandTops, true, cliffs, -1, 0),
                new TerrainAtlas.Tiles(TerrainType.Water, waterTops, true, cliffs, -1, 0),
            };

            var texture = WriteAtlas(cells);
            var asset = AssetDatabase.LoadAssetAtPath<TerrainAtlas>(AtlasAssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<TerrainAtlas>();
                AssetDatabase.CreateAsset(asset, AtlasAssetPath);
            }
            asset.Set(texture, Cell, Gutter, Columns, tiles);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>Packs the cells into a grid with each cell's edge pixels extruded into its gutter, then imports it.</summary>
        private static Texture2D WriteAtlas(List<Color32[]> cells)
        {
            int stride = Cell + Gutter * 2;
            int rows = (cells.Count + Columns - 1) / Columns;
            int width = Columns * stride;
            int height = rows * stride;
            var pixels = new Color32[width * height];

            for (int i = 0; i < cells.Count; i++)
            {
                int ox = i % Columns * stride;
                int oyTop = i / Columns * stride;
                for (int y = -Gutter; y < Cell + Gutter; y++)
                for (int x = -Gutter; x < Cell + Gutter; x++)
                {
                    int sx = Mathf.Clamp(x, 0, Cell - 1);
                    int sy = Mathf.Clamp(y, 0, Cell - 1);
                    // Cells are stored top row first; textures bottom row first.
                    int ty = height - 1 - (oyTop + Gutter + y);
                    pixels[ty * width + ox + Gutter + x] = cells[i][sy * Cell + sx];
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            Directory.CreateDirectory(AtlasFolder);
            File.WriteAllBytes(AtlasTexturePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(AtlasTexturePath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasTexturePath);
        }

        /// <summary>Source cells, top row first, so (column, row) and pixel rows read like the sheet.</summary>
        private sealed class Sheet
        {
            private readonly Color32[] _pixels;
            private readonly int _width;
            private readonly int _height;

            public Sheet(Texture2D texture)
            {
                _pixels = texture.GetPixels32();
                _width = texture.width;
                _height = texture.height;
            }

            public Color32 Pixel(int x, int yFromTop) => _pixels[(_height - 1 - yFromTop) * _width + x];

            public Color32[] Cell(int column, int row) => Rect(column * PixelTerrainBuilder.Cell, row * PixelTerrainBuilder.Cell, PixelTerrainBuilder.Cell, PixelTerrainBuilder.Cell);

            public Color32[] Rect(int x0, int y0, int w, int h)
            {
                var result = new Color32[w * h];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    result[y * w + x] = Pixel(x0 + x, y0 + y);
                return result;
            }
        }

        /// <summary><paramref name="top"/> over <paramref name="bottom"/> (alpha-over, cells top row first).</summary>
        private static Color32[] Over(Color32[] bottom, Color32[] top)
        {
            var result = new Color32[bottom.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = top[i].a == 255 ? top[i] : top[i].a == 0 ? bottom[i] : Color32.Lerp(bottom[i], top[i], top[i].a / 255f);
            return result;
        }

        /// <summary>A lip cell: <paramref name="rows"/> rows of <paramref name="source"/> from <paramref name="firstRow"/> at the cell's top, the rest clear.</summary>
        private static Color32[] Lip(Color32[] source, int firstRow, int rows)
        {
            var result = new Color32[Cell * Cell];
            for (int y = 0; y < rows; y++)
                Array.Copy(source, (firstRow + y) * Cell, result, y * Cell, Cell);
            return result;
        }

        /// <summary>Seamless cobbles: a wrapped Voronoi of jittered stones with dark gaps and a light upper-left rim.</summary>
        private static Color32[] Cobbles(int seed)
        {
            var rng = new System.Random(seed);
            const int grid = 4;
            int step = Cell / grid;
            var seeds = new List<Vector2Int>();
            var shades = new List<Color32>();
            for (int gy = 0; gy < grid; gy++)
            for (int gx = 0; gx < grid; gx++)
            {
                seeds.Add(new Vector2Int(gx * step + rng.Next(1, step - 1), gy * step + rng.Next(1, step - 1)));
                shades.Add(rng.Next(3) == 0 ? RockDark : RockMid);
            }

            var owner = new int[Cell * Cell];
            for (int y = 0; y < Cell; y++)
            for (int x = 0; x < Cell; x++)
                owner[y * Cell + x] = Nearest(seeds, x, y);

            var result = new Color32[Cell * Cell];
            for (int y = 0; y < Cell; y++)
            for (int x = 0; x < Cell; x++)
            {
                int me = owner[y * Cell + x];
                int right = owner[y * Cell + (x + 1) % Cell];
                int down = owner[(y + 1) % Cell * Cell + x];
                int left = owner[y * Cell + (x + Cell - 1) % Cell];
                int up = owner[(y + Cell - 1) % Cell * Cell + x];
                Color32 color = shades[me];
                if (right != me || down != me)
                    color = RockOutline;
                else if (left != me || up != me)
                    color = RockLight;
                result[y * Cell + x] = color;
            }
            return result;
        }

        private static int Nearest(List<Vector2Int> seeds, int x, int y)
        {
            int best = 0;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < seeds.Count; i++)
            {
                // Wrapped distance, so the tile repeats without seams.
                int dx = Mathf.Abs(seeds[i].x - x);
                int dy = Mathf.Abs(seeds[i].y - y);
                dx = Mathf.Min(dx, Cell - dx);
                dy = Mathf.Min(dy, Cell - dy);
                int d = dx * dx + dy * dy;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Seamless stone blocks for stone cliffs: offset courses, dark mortar, a light top edge.</summary>
        private static Color32[] StoneWall(int seed)
        {
            var rng = new System.Random(seed);
            var result = new Color32[Cell * Cell];
            const int course = 8;
            for (int row = 0; row < Cell / course; row++)
            {
                // Block joints for this course, wrapping round the cell.
                var joints = new List<int>();
                int x0 = rng.Next(0, 8);
                for (int x = x0; x < x0 + Cell; x += rng.Next(9, 15))
                    joints.Add(x % Cell);
                for (int y = 0; y < course; y++)
                for (int x = 0; x < Cell; x++)
                {
                    int py = row * course + y;
                    Color32 color = y == course - 1 || joints.Contains(x) ? RockOutline : y == 0 ? RockLight : (x + row) % 11 == 0 ? RockDark : RockMid;
                    result[py * Cell + x] = color;
                }
            }
            return result;
        }

        /// <summary>Speckled sand with a few short ripples, in the pack's warm yellows.</summary>
        private static Color32[] Sand(int seed)
        {
            var rng = new System.Random(seed);
            var result = new Color32[Cell * Cell];
            for (int i = 0; i < result.Length; i++)
            {
                int roll = rng.Next(100);
                result[i] = roll < 2 ? SandDark : roll < 5 ? SandLight : roll < 6 ? SandPale : SandMid;
            }
            for (int r = 0; r < 5; r++)
            {
                int x = rng.Next(Cell);
                int y = rng.Next(Cell);
                int length = rng.Next(4, 8);
                for (int i = 0; i < length; i++)
                {
                    result[y * Cell + (x + i) % Cell] = SandDark;
                    result[(y + Cell - 1) % Cell * Cell + (x + i) % Cell] = SandLight;
                }
            }
            return result;
        }

        // ------------------------------------------------------------------ Props

        private static void BuildProps(Texture2D sheet)
        {
            var pine = Prop("Pine", sheet, new RectInt(393, 323, 45, 60), 6, true);
            var oak = Prop("Oak", sheet, new RectInt(291, 291, 58, 60), 6, true);
            var shrub = Prop("Shrub", sheet, new RectInt(288, 129, 31, 30), 2, true);
            var rock = Prop("Mossy Rock", sheet, new RectInt(321, 3, 30, 26), 2, true);
            // Small decorations units can stand on.
            var mushroom = Prop("Mushroom", sheet, new RectInt(360, 552, 17, 17), 0, false);
            var flowers = Prop("Wildflowers", sheet, new RectInt(228, 324, 24, 24), 0, false);

            var settings = AssetDatabase.LoadAssetAtPath<MapGeneratorSettings>(ProjectAssets.DefaultGeneratorSettingsPath);
            if (settings != null)
            {
                // Random maps use the Green Wilderness props, so props match the terrain; the Tiny Tactics props stay
                // available in the map editor.
                settings.SetProps(new[]
                {
                    new MapGeneratorSettings.PropChoice(pine, 2f),
                    new MapGeneratorSettings.PropChoice(oak, 1f),
                    new MapGeneratorSettings.PropChoice(shrub, 1.5f),
                    new MapGeneratorSettings.PropChoice(rock, 1f),
                    new MapGeneratorSettings.PropChoice(mushroom, 0.4f),
                    new MapGeneratorSettings.PropChoice(flowers, 0.8f),
                });
                EditorUtility.SetDirty(settings);
            }

            UpdateHillside(pine, rock, mushroom, flowers);
        }

        /// <summary>The test map swaps its Tiny Tactics tree and rock for the matching Green Wilderness ones and gains a few flowers.</summary>
        private static void UpdateHillside(PropDefinition pine, PropDefinition rock, PropDefinition mushroom, PropDefinition flowers)
        {
            var map = AssetDatabase.LoadAssetAtPath<BattleMapData>(Maps.TestMapFactory.HillsidePath);
            if (map == null)
                return;
            var placements = new List<PropPlacement>();
            var taken = new HashSet<GridCoord>();
            foreach (var placement in map.Props)
            {
                if (placement.Prop == null || placement.Prop == mushroom || placement.Prop == flowers)
                    continue;
                var prop = placement.Prop.name switch
                {
                    "Tree" => pine,
                    "Rock" => rock,
                    _ => placement.Prop,
                };
                placements.Add(new PropPlacement(placement.Coord, prop));
                taken.Add(placement.Coord);
            }
            foreach (var (coord, prop) in new[]
            {
                (new GridCoord(3, 11), flowers), (new GridCoord(5, 9), mushroom), (new GridCoord(11, 6), flowers),
                (new GridCoord(1, 5), flowers), (new GridCoord(9, 2), mushroom), (new GridCoord(4, 0), flowers),
            })
            {
                if (map.Contains(coord) && !taken.Contains(coord) && map.GetDeploymentAt(coord) == DeploymentSide.None &&
                    map.GetTile(coord) is { Terrain: TerrainType.Grass, Slope: SlopeType.Flat })
                    placements.Add(new PropPlacement(coord, prop));
            }
            map.SetProps(placements.ToArray());
            EditorUtility.SetDirty(map);
        }

        private static PropDefinition Prop(string name, Texture2D texture, RectInt rect, int height, bool blocks)
        {
            string path = $"{ProjectAssets.PropsFolder}/{name}.asset";
            var prop = AssetDatabase.LoadAssetAtPath<PropDefinition>(path);
            if (prop == null)
            {
                prop = ScriptableObject.CreateInstance<PropDefinition>();
                AssetDatabase.CreateAsset(prop, path);
            }
            var so = new SerializedObject(prop);
            so.FindProperty("_displayName").stringValue = name;
            so.FindProperty("_blocksMovement").boolValue = blocks;
            so.FindProperty("_height").intValue = height;
            so.FindProperty("_color").colorValue = Color.gray;
            so.ApplyModifiedPropertiesWithoutUndo();
            prop.SetSprite(texture, rect, PropPixelsPerUnit);
            EditorUtility.SetDirty(prop);
            return prop;
        }
    }
}
