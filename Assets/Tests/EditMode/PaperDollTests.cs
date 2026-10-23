using System.Collections.Generic;
using System.IO;
using FriendshipWarriors.Editor.Art;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Equipment;
using FriendshipWarriors.Units.View.PaperDoll;
using NUnit.Framework;
using UnityEngine;

namespace FriendshipWarriors.Tests
{
    public class PaperDollTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            PaperDollComposer.ClearCache();
            foreach (var obj in _created)
                Object.DestroyImmediate(obj);
            _created.Clear();
        }

        private static Color32 Rgb(byte r, byte g, byte b, byte a = 255) => new(r, g, b, a);

        // ------------------------------------------------------------------ Palette swap

        [Test]
        public void PaletteSwap_ReplacesExactKeysOnly()
        {
            var key = KeyPalette.Primary[1];
            var nearKey = Rgb(key.r, key.g, (byte)(key.b + 1));
            var other = KeyPalette.Metal[0];
            var pixels = new[] { key, nearKey, other, KeyPalette.Outline };
            var map = new Dictionary<int, Color32> { [PaletteSwap.Pack(key)] = Rgb(200, 10, 10) };

            PaletteSwap.Apply(pixels, map);

            Assert.AreEqual(Rgb(200, 10, 10), pixels[0]);
            Assert.AreEqual(nearKey, pixels[1], "A colour one step off a key must not be swapped.");
            Assert.AreEqual(other, pixels[2]);
            Assert.AreEqual(KeyPalette.Outline, pixels[3]);
        }

        [Test]
        public void PaletteSwap_KeepsAlphaAndSkipsTransparentPixels()
        {
            var key = KeyPalette.Skin[0];
            var pixels = new[] { Rgb(key.r, key.g, key.b, 128), Rgb(key.r, key.g, key.b, 0) };
            var map = new Dictionary<int, Color32> { [PaletteSwap.Pack(key)] = Rgb(1, 2, 3) };

            PaletteSwap.Apply(pixels, map);

            Assert.AreEqual(Rgb(1, 2, 3, 128), pixels[0]);
            Assert.AreEqual(Rgb(key.r, key.g, key.b, 0), pixels[1]);
        }

        [Test]
        public void DollPalette_SwapsEveryKeyRampAndNoFixedColour()
        {
            var unit = MakeUnit("Ada", Team.Enemy, Job(Color.red));
            var map = DollPalette.For(unit, 4).ToSwapMap();

            foreach (var ramp in new[] { KeyPalette.Skin, KeyPalette.Hair, KeyPalette.Primary, KeyPalette.Accent })
            foreach (var c in ramp)
                Assert.IsTrue(map.ContainsKey(PaletteSwap.Pack(c)), $"Key {c} missing from the swap map.");
            foreach (var c in new[] { KeyPalette.Outline, KeyPalette.Brass[2], KeyPalette.Metal[1], KeyPalette.Leather[0], KeyPalette.Undersuit[2] })
                Assert.IsFalse(map.ContainsKey(PaletteSwap.Pack(c)), $"Fixed colour {c} must not be swapped.");
        }

        [Test]
        public void DollPalette_IsStablePerName_AndTeamsGetDifferentAccents()
        {
            var job = Job(Color.green);
            var a = DollPalette.For(MakeUnit("Ada", Team.Player, job), 4);
            var again = DollPalette.For(MakeUnit("Ada", Team.Player, job), 4);
            var enemy = DollPalette.For(MakeUnit("Ada", Team.Enemy, job), 4);

            Assert.AreEqual(a.Key, again.Key);
            Assert.AreEqual(a.HairStyle, again.HairStyle);
            Assert.AreNotEqual(a.Accent[0], enemy.Accent[0]);
        }

        private static float Brightness(Color32 c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        [Test]
        public void ColorRamp_KeepsTheMidShade_AndRunsFromLightToDark()
        {
            foreach (var mid in new[] { Rgb(118, 136, 90), Rgb(230, 70, 66), Rgb(158, 166, 182), Rgb(40, 36, 48) })
            {
                var ramp = ColorRamp.From(mid);
                Assert.AreEqual(ColorRamp.Length, ramp.Length);
                Assert.AreEqual(mid, ramp[ColorRamp.Mid]);
                for (int i = 1; i < ramp.Length; i++)
                    Assert.Less(Brightness(ramp[i]), Brightness(ramp[i - 1]), $"{mid}: shade {i} should be darker than shade {i - 1}.");
            }
        }

        [Test]
        public void DollPalette_GivesEveryKeyShadeAReplacement_IncludingTheOutline()
        {
            var palette = DollPalette.For(MakeUnit("Ada", Team.Player, Job(Color.cyan)), 4);
            foreach (var ramp in new[] { palette.Skin, palette.Hair, palette.Primary, palette.Accent })
                Assert.AreEqual(ColorRamp.Length, ramp.Length);
            var map = palette.ToSwapMap();
            Assert.AreEqual(palette.Primary[KeyPalette.Line], map[PaletteSwap.Pack(KeyPalette.Primary[KeyPalette.Line])],
                "Coloured outlines follow the unit's colours.");
        }

        [Test]
        public void KeyPalette_ColoursAreAllDistinct_SoSwapsAreExact()
        {
            var seen = new Dictionary<int, string>();
            foreach (var (name, color) in KeyPalette.All())
            {
                Assert.IsFalse(seen.TryGetValue(PaletteSwap.Pack(color), out var other), $"{name} repeats {other}.");
                seen[PaletteSwap.Pack(color)] = name;
            }
            foreach (var ramp in KeyPalette.Ramps())
                Assert.AreEqual(ColorRamp.Length, ramp.Length);
        }

        // ------------------------------------------------------------------ Layout

        [Test]
        public void FrameRects_MatchTheGrid()
        {
            // Unity rects are bottom-left origin: row 0 (Idle) is the top 40 px of the 240 px sheet.
            Assert.AreEqual(new RectInt(0, 200, 40, 40), PaperDollLayout.FrameRect(DollAnimation.Idle, 0, false));
            Assert.AreEqual(new RectInt(40, 200, 40, 40), PaperDollLayout.FrameRect(DollAnimation.Idle, 1, false));
            Assert.AreEqual(new RectInt(160, 200, 40, 40), PaperDollLayout.FrameRect(DollAnimation.Idle, 0, true));
            Assert.AreEqual(new RectInt(120, 160, 40, 40), PaperDollLayout.FrameRect(DollAnimation.Walk, 3, false));
            Assert.AreEqual(new RectInt(80, 120, 40, 40), PaperDollLayout.FrameRect(DollAnimation.Attack, 2, false));
            Assert.AreEqual(new RectInt(160, 0, 40, 40), PaperDollLayout.FrameRect(DollAnimation.KnockedOut, 0, true));
            Assert.AreEqual(new Vector2Int(160, 200), PaperDollLayout.FrameTopLeft(DollAnimation.KnockedOut, 0, true));
        }

        [Test]
        public void FrameRects_StayInsideTheSheetAndNeverOverlap()
        {
            var seen = new List<RectInt>();
            for (int row = 0; row < PaperDollLayout.Rows; row++)
            {
                var animation = (DollAnimation)row;
                for (int frame = 0; frame < PaperDollLayout.FrameCount(animation); frame++)
                foreach (bool back in new[] { false, true })
                {
                    var rect = PaperDollLayout.FrameRect(animation, frame, back);
                    Assert.That(rect.xMin >= 0 && rect.yMin >= 0 && rect.xMax <= PaperDollLayout.SheetWidth && rect.yMax <= PaperDollLayout.SheetHeight);
                    foreach (var other in seen)
                        Assert.IsFalse(rect.Overlaps(other), $"{animation} {frame} overlaps another frame.");
                    seen.Add(rect);
                }
            }
            Assert.AreEqual(new[] { 2, 4, 3, 2, 1, 1 }, new[]
            {
                PaperDollLayout.FrameCount(DollAnimation.Idle), PaperDollLayout.FrameCount(DollAnimation.Walk),
                PaperDollLayout.FrameCount(DollAnimation.Attack), PaperDollLayout.FrameCount(DollAnimation.Cast),
                PaperDollLayout.FrameCount(DollAnimation.Hurt), PaperDollLayout.FrameCount(DollAnimation.KnockedOut),
            });
        }

        // ------------------------------------------------------------------ Composer

        [Test]
        public void Compose_UsesEachPosesLayerOrder()
        {
            // 2×1 image: x 0 is the front half, x 1 the back half.
            var weapon = Rgb(255, 0, 0);
            var shield = Rgb(0, 0, 255);
            var body = Rgb(0, 255, 0);
            var layers = new Dictionary<DollSlot, Color32[]>
            {
                [DollSlot.Body] = new[] { body, body },
                [DollSlot.Weapon] = new[] { weapon, weapon },
                [DollSlot.Shield] = new[] { shield, shield },
            };

            var result = PaperDollComposer.Compose(layers, 2, 1);

            Assert.AreEqual(weapon, result[0], "Front: the weapon hand is nearest the camera.");
            Assert.AreEqual(shield, result[1], "Back: the shield arm is nearest the camera.");
        }

        [Test]
        public void Compose_TransparentPixelsShowTheLayerBelow()
        {
            var body = Rgb(10, 20, 30);
            var hair = Rgb(90, 60, 30);
            var layers = new Dictionary<DollSlot, Color32[]>
            {
                [DollSlot.Body] = new[] { body, body },
                [DollSlot.Hair] = new[] { hair, default },
            };

            var result = PaperDollComposer.Compose(layers, 2, 1);

            Assert.AreEqual(hair, result[0]);
            Assert.AreEqual(body, result[1]);
        }

        [Test]
        public void LayerOrder_FrontAndBack()
        {
            CollectionAssert.AreEqual(new[]
            {
                DollSlot.Shield, DollSlot.Body, DollSlot.BodyGear, DollSlot.Head, DollSlot.Face, DollSlot.Hair, DollSlot.Headgear, DollSlot.Weapon,
            }, PaperDollLayout.LayerOrder(false));
            CollectionAssert.AreEqual(new[]
            {
                DollSlot.Weapon, DollSlot.Body, DollSlot.BodyGear, DollSlot.Head, DollSlot.Face, DollSlot.Hair, DollSlot.Headgear, DollSlot.Shield,
            }, PaperDollLayout.LayerOrder(true));
        }

        [Test]
        public void Composer_ReusesTheSheetForIdenticalLoadouts()
        {
            var catalog = Catalog(out var bodyKey);
            var job = Job(Color.blue);
            var cuirass = Gear(EquipmentCategory.Body, ArmorType.Armor);
            var first = MakeUnit("Ada", Team.Player, job);
            var twin = MakeUnit("Ada", Team.Player, job);
            first.TryEquipItem(cuirass);
            twin.TryEquipItem(cuirass);

            var a = PaperDollComposer.SheetFor(first, catalog);
            int count = PaperDollComposer.CachedSheetCount;
            var b = PaperDollComposer.SheetFor(twin, catalog);

            Assert.IsNotNull(a);
            Assert.AreSame(a, b);
            Assert.AreEqual(count, PaperDollComposer.CachedSheetCount);

            // A different loadout or team gets its own sheet.
            twin.Unequip(EquipmentSlot.Body);
            Assert.AreNotSame(a, PaperDollComposer.SheetFor(twin, catalog));
            Assert.AreNotSame(a, PaperDollComposer.SheetFor(MakeUnit("Ada", Team.Enemy, job), catalog));
        }

        [Test]
        public void Composer_PaintsTheJobColourAndTeamAccent()
        {
            var catalog = Catalog(out _);
            var unit = MakeUnit("Ada", Team.Enemy, Job(Color.red));
            unit.TryEquipItem(Gear(EquipmentCategory.Body, ArmorType.Armor));

            var pixels = PaperDollComposer.PixelsFor(unit, catalog);
            var palette = DollPalette.For(unit, catalog.HairStyleCount);
            int top = (PaperDollLayout.SheetHeight - 1) * PaperDollLayout.SheetWidth;

            Assert.AreEqual(palette.Primary[1], pixels[top + 0], "Body key colour becomes the job primary.");
            Assert.AreEqual(palette.Accent[0], pixels[top + 1], "Body gear accent key becomes the team accent.");
            Assert.AreEqual(KeyPalette.Outline, pixels[top + 2], "Fixed colours stay as painted.");
        }

        [Test]
        public void Catalog_FallsBackByTypeAndPrefersItemOverrides()
        {
            var catalog = Catalog(out _);
            var unit = MakeUnit("Ada", Team.Player, Job(Color.blue));
            Assert.IsNull(catalog.PartFor(DollSlot.BodyGear, unit, 0), "Nothing worn, nothing drawn.");
            Assert.IsNull(catalog.PartFor(DollSlot.Shield, unit, 0));

            var robe = Gear(EquipmentCategory.Body, ArmorType.Robe);
            unit.TryEquipItem(robe);
            Assert.IsNull(catalog.PartFor(DollSlot.BodyGear, unit, 0), "No robe part in this catalog.");

            unit.TryEquipItem(Gear(EquipmentCategory.Body, ArmorType.Armor));
            Assert.IsNotNull(catalog.PartFor(DollSlot.BodyGear, unit, 0));
        }

        // ------------------------------------------------------------------ Animator

        [Test]
        public void Animator_PlaysOneShotsThenReturnsToTheLoop()
        {
            var animator = new DollAnimator();
            Assert.AreEqual(DollAnimation.Idle, animator.Current(0f, false, false).animation);
            Assert.AreEqual(DollAnimation.Cast, animator.Current(0f, false, charging: true).animation);

            animator.Play(DollAnimation.Attack, 10f);
            float step = PaperDollLayout.FrameDuration(DollAnimation.Attack);
            Assert.AreEqual((DollAnimation.Attack, 0), animator.Current(10f, false, false));
            Assert.AreEqual((DollAnimation.Attack, 2), animator.Current(10f + step * 2.5f, false, false));
            Assert.AreEqual(DollAnimation.Idle, animator.Current(10f + step * 3.5f, false, false).animation);
            Assert.AreEqual((DollAnimation.KnockedOut, 0), animator.Current(10f, knockedOut: true, charging: true));
        }

        // ------------------------------------------------------------------ Generator

        [Test]
        public void Generator_DoesNotOverwriteByDefault()
        {
            string folder = Path.Combine(Path.GetTempPath(), "fwt-paperdoll-" + System.Guid.NewGuid().ToString("N"));
            string path = Path.Combine(folder, "Body", "body.png");
            try
            {
                Assert.IsTrue(PaperDollPartGenerator.WritePart(path, new byte[] { 1 }, overwrite: false), "Missing files are written.");
                Assert.IsFalse(PaperDollPartGenerator.WritePart(path, new byte[] { 2 }, overwrite: false));
                CollectionAssert.AreEqual(new byte[] { 1 }, File.ReadAllBytes(path), "A hand-edited part must survive a default run.");
                Assert.IsTrue(PaperDollPartGenerator.WritePart(path, new byte[] { 3 }, overwrite: true));
                CollectionAssert.AreEqual(new byte[] { 3 }, File.ReadAllBytes(path));
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }

        // ------------------------------------------------------------------ Helpers

        private JobDefinition Job(Color color)
        {
            var job = ScriptableObject.CreateInstance<JobDefinition>();
            job.Define("Tester", "", new StatFactors(100, 100, 100, 100, 100), new StatFactors(11, 15, 100, 60, 50), 4, 3, 10,
                new JobRequirement[0], color);
            job.SetEquipment(new[] { WeaponType.Sword }, canUseShields: true);
            job.SetArmorTypes(new[] { ArmorType.Armor, ArmorType.Robe, ArmorType.Helmet });
            _created.Add(job);
            return job;
        }

        private EquipmentDefinition Gear(EquipmentCategory category, ArmorType type)
        {
            var item = ScriptableObject.CreateInstance<EquipmentDefinition>();
            item.DefineGear(type.ToString(), "", category, type, 0, 0);
            _created.Add(item);
            return item;
        }

        private static Unit MakeUnit(string name, Team team, JobDefinition job) =>
            UnitFactory.CreateGeneric(name, team, job, 1, new System.Random(1));

        /// <summary>
        /// A catalog with a body painted in the primary key and an armor part with an accent key and an outline pixel,
        /// all along the sheet's top row.
        /// </summary>
        private SpritePartCatalog Catalog(out Texture2D body)
        {
            body = Sheet((0, KeyPalette.Primary[1]));
            var armor = Sheet((1, KeyPalette.Accent[0]), (2, KeyPalette.Outline));
            var catalog = ScriptableObject.CreateInstance<SpritePartCatalog>();
            catalog.Define(body, null, null, new Texture2D[0], new SpritePartCatalog.ArmorPart[0],
                new[] { new SpritePartCatalog.ArmorPart(ArmorType.Armor, armor) }, new SpritePartCatalog.WeaponPart[0], null);
            _created.Add(catalog);
            return catalog;
        }

        private Texture2D Sheet(params (int x, Color32 color)[] topRow)
        {
            var texture = new Texture2D(PaperDollLayout.SheetWidth, PaperDollLayout.SheetHeight, TextureFormat.RGBA32, false);
            var pixels = new Color32[PaperDollLayout.SheetWidth * PaperDollLayout.SheetHeight];
            int top = (PaperDollLayout.SheetHeight - 1) * PaperDollLayout.SheetWidth;
            foreach (var (x, color) in topRow)
                pixels[top + x] = color;
            texture.SetPixels32(pixels);
            texture.Apply();
            _created.Add(texture);
            return texture;
        }
    }
}
