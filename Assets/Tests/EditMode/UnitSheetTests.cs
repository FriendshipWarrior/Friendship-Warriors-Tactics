using System.Collections.Generic;
using FriendshipWarriors.Grid.Props;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Equipment;
using FriendshipWarriors.Units.View.Sheets;
using NUnit.Framework;
using UnityEngine;

namespace FriendshipWarriors.Tests
{
    public class UnitSheetTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            SheetComposer.ClearCache();
            foreach (var obj in _created)
                Object.DestroyImmediate(obj);
            _created.Clear();
        }

        /// <summary>Timing for a sheet with only some animations, for the animator and fallback tests.</summary>
        private sealed class FakeTiming : ISheetTiming
        {
            private readonly Dictionary<SheetAnimation, (int count, float seconds)> _strips = new();

            public FakeTiming With(SheetAnimation animation, int count, float seconds)
            {
                _strips[animation] = (count, seconds);
                return this;
            }

            public int FrameCount(SheetAnimation animation) => _strips.TryGetValue(animation, out var s) ? s.count : 0;
            public float FrameSeconds(SheetAnimation animation) => _strips.TryGetValue(animation, out var s) ? s.seconds : 0f;
        }

        private static FakeTiming FullTiming() => new FakeTiming()
            .With(SheetAnimation.Idle, 4, 0.3f).With(SheetAnimation.Walk, 4, 0.15f).With(SheetAnimation.Attack, 4, 0.1f)
            .With(SheetAnimation.Charging, 1, 0.5f).With(SheetAnimation.Release, 1, 0.4f).With(SheetAnimation.Hurt, 1, 0.35f)
            .With(SheetAnimation.Weak, 1, 1f).With(SheetAnimation.KnockedOut, 1, 1f);

        // ------------------------------------------------------------------ Animator

        [Test]
        public void Animator_PlaysAOneShotOnceThenReturnsToIdle()
        {
            var timing = FullTiming();
            var animator = new SheetAnimator();
            animator.Play(SheetAnimation.Attack, 10f);

            Assert.AreEqual((SheetAnimation.Attack, 0), animator.Current(10.05f, timing, false, false, false));
            Assert.AreEqual((SheetAnimation.Attack, 3), animator.Current(10.35f, timing, false, false, false));
            Assert.AreEqual(SheetAnimation.Idle, animator.Current(10.45f, timing, false, false, false).animation);
        }

        [Test]
        public void Animator_KnockOutWinsOverEverything()
        {
            var animator = new SheetAnimator();
            animator.Play(SheetAnimation.Attack, 0f);

            Assert.AreEqual((SheetAnimation.KnockedOut, 0), animator.Current(0.05f, FullTiming(), true, true, true, walkFrame: 2));
        }

        [Test]
        public void Animator_LoopsChargingThenWeakThenIdle()
        {
            var timing = FullTiming();
            var animator = new SheetAnimator();

            Assert.AreEqual(SheetAnimation.Charging, animator.Current(1f, timing, false, charging: true, weak: true).animation);
            Assert.AreEqual(SheetAnimation.Weak, animator.Current(1f, timing, false, charging: false, weak: true).animation);
            Assert.AreEqual((SheetAnimation.Idle, 1), animator.Current(0.35f, timing, false, false, false));
        }

        [Test]
        public void Animator_ShowsTheWalkFrameItIsGiven()
        {
            Assert.AreEqual((SheetAnimation.Walk, 1), new SheetAnimator().Current(0f, FullTiming(), false, false, false, walkFrame: 5));
        }

        [Test]
        public void Fallback_UsesTheNearestPoseTheSheetHas()
        {
            var timing = new FakeTiming().With(SheetAnimation.Walk, 4, 0.15f).With(SheetAnimation.Attack, 2, 0.1f);

            Assert.AreEqual(SheetAnimation.Attack, SheetFallback.Resolve(SheetAnimation.Release, timing), "release → charging → attack");
            Assert.AreEqual(SheetAnimation.Walk, SheetFallback.Resolve(SheetAnimation.Weak, timing), "weak → hurt → idle → walk");
            Assert.AreEqual(SheetAnimation.Walk, SheetFallback.Resolve(SheetAnimation.KnockedOut, timing));
            Assert.AreEqual(SheetAnimation.Attack, SheetFallback.Resolve(SheetAnimation.Attack, timing));
        }

        [Test]
        public void Animator_WithoutAWeakPose_StandsIdle()
        {
            var timing = new FakeTiming().With(SheetAnimation.Idle, 2, 0.5f);

            Assert.AreEqual(SheetAnimation.Idle, new SheetAnimator().Current(0f, timing, false, false, weak: true).animation);
        }

        // ------------------------------------------------------------------ Critical HP

        [TestCase(20, 100, true)]
        [TestCase(21, 100, false)]
        [TestCase(1, 100, true)]
        [TestCase(0, 100, false)]
        [TestCase(9, 46, true)]
        [TestCase(10, 46, false)]
        public void CriticalHp_IsAFifthOfMaxOrLessWhileStanding(int hp, int max, bool critical)
        {
            Assert.AreEqual(critical, CriticalHp.IsCritical(hp, max));
        }

        // ------------------------------------------------------------------ Recolour

        [Test]
        public void Recolor_TurnsTheMiddleShadeIntoTheTargetAndKeepsTheRampOrder()
        {
            var ramp = new Color32[] { new(168, 227, 209, 255), new(132, 189, 195, 255), new(116, 145, 174, 255) };
            var target = new Color32(200, 60, 60, 255);

            var result = SheetRecolor.Remap(ramp, target);

            Assert.AreEqual(ramp[1], SheetRecolor.Reference(ramp));
            AssertClose(target, result[1]);
            Color.RGBToHSV(result[0], out _, out _, out float v0);
            Color.RGBToHSV(result[1], out _, out _, out float v1);
            Color.RGBToHSV(result[2], out _, out _, out float v2);
            Assert.That(v0, Is.GreaterThan(v1));
            Assert.That(v1, Is.GreaterThan(v2));
        }

        [Test]
        public void Recolor_ToTheGroupsOwnMiddleShade_ChangesNothing()
        {
            var ramp = new Color32[] { new(242, 24, 108, 255), new(201, 18, 110, 255), new(168, 13, 112, 255) };

            var result = SheetRecolor.Remap(ramp, ramp[1]);

            for (int i = 0; i < ramp.Length; i++)
                AssertClose(ramp[i], result[i]);
        }

        [Test]
        public void Palette_TintsOutfitByJobAccentByTeam_AndLeavesHairWithoutChoices()
        {
            var outfit = new Color32(132, 189, 195, 255);
            var accent = new Color32(67, 148, 214, 255);
            var hair = new Color32(201, 18, 110, 255);
            var groups = new[]
            {
                new UnitSheet.RecolorGroup(RecolorRole.JobOutfit, new[] { outfit }),
                new UnitSheet.RecolorGroup(RecolorRole.TeamAccent, new[] { accent }),
                new UnitSheet.RecolorGroup(RecolorRole.Hair, new[] { hair }),
            };
            var job = new Color32(60, 200, 90, 255);
            var team = new Color32(230, 70, 60, 255);

            var map = SheetPalette.BuildMap(groups, job, team, hair: null);

            AssertClose(job, map[Pack(outfit)]);
            AssertClose(team, map[Pack(accent)]);
            Assert.IsFalse(map.ContainsKey(Pack(hair)), "No hair colours: the painted hair stays.");
        }

        [Test]
        public void HairChoice_IsStablePerName()
        {
            Assert.AreEqual(SheetPalette.HairIndex("Vance", 6), SheetPalette.HairIndex("Vance", 6));
            Assert.AreEqual(-1, SheetPalette.HairIndex("Vance", 0));
            Assert.That(SheetPalette.HairIndex("Vance", 6), Is.InRange(0, 5));
        }

        // ------------------------------------------------------------------ Composed sheets

        private static readonly Color32 OutfitKey = new(132, 189, 195, 255);

        [Test]
        public void ComposedSheet_FindsEachFrameByAnimationAndFacing()
        {
            var sheet = TestSheet();
            var composed = new ComposedSheet(sheet, new Dictionary<int, Color32>());

            // Each frame of the test strips is filled with its own colour: red = frame, green = 100 front / 200 back.
            Assert.AreEqual(new Color32(0, 100, 10, 255), Centre(composed.Get(SheetAnimation.Walk, 0, back: false)));
            Assert.AreEqual(new Color32(1, 200, 10, 255), Centre(composed.Get(SheetAnimation.Walk, 1, back: true)));
            Assert.AreEqual(new Color32(0, 100, 20, 255), Centre(composed.Get(SheetAnimation.KnockedOut, 0, back: false)));
            Assert.AreEqual(new Color32(1, 100, 10, 255), Centre(composed.Get(SheetAnimation.Walk, 9, back: false)), "frames clamp");
            Assert.AreEqual(Centre(composed.Get(SheetAnimation.Walk, 0, back: false)),
                Centre(composed.Get(SheetAnimation.Idle, 0, back: false)), "a sheet without idle stands on its walk frames");
        }

        [Test]
        public void ComposedSheet_PivotSitsOnEachStripsLowestPixels()
        {
            var composed = new ComposedSheet(TestSheet(), new Dictionary<int, Color32>());

            Assert.AreEqual(4f / 8f, composed.Get(SheetAnimation.Walk, 0, false).pivot.y / 8f, 1e-4f, "walk feet at row 4");
            Assert.AreEqual(0f, composed.Get(SheetAnimation.KnockedOut, 0, false).pivot.y, 1e-4f, "the lying body reaches row 0");
        }

        [Test]
        public void ComposedSheet_RecoloursByExactColour()
        {
            var sheet = TestSheet(fill: OutfitKey);
            var map = new Dictionary<int, Color32> { [Pack(OutfitKey)] = new Color32(9, 8, 7, 255) };

            var composed = new ComposedSheet(sheet, map);

            Assert.AreEqual(new Color32(9, 8, 7, 255), Centre(composed.Get(SheetAnimation.Walk, 0, false)));
        }

        [Test]
        public void ComposedSheet_WeaponFramesFollowTheAttackAndSkipTypesWithoutArt()
        {
            var weapons = ScriptableObject.CreateInstance<WeaponArt>();
            _created.Add(weapons);
            var swing = Texture(48 * 2, 48, (x, y) => new Color32((byte)(x / 48), 50, 50, 255));
            weapons.Define(new Vector2Int(48, 48), new[] { new WeaponArt.Entry(WeaponType.Sword, swing, null) });
            var sheet = TestSheet(weapons: weapons);
            var composed = new ComposedSheet(sheet, new Dictionary<int, Color32>());

            Assert.AreEqual(new Color32(1, 50, 50, 255), Centre(composed.Weapon(WeaponType.Sword, 1, back: true)), "back falls back to front art");
            Assert.IsNull(composed.Weapon(WeaponType.Sword, 2, back: false), "past the strip");
            Assert.IsNull(composed.Weapon(WeaponType.Rifle, 0, back: false));
        }

        [Test]
        public void Composer_UsesTheJobsSheet_OrNoneSoThePaperDollShows()
        {
            var job = ScriptableObject.CreateInstance<JobDefinition>();
            _created.Add(job);
            job.Define("Tester", "", new StatFactors(100, 100, 100, 100, 100), new StatFactors(11, 15, 100, 60, 50), 4, 3, 10,
                new JobRequirement[0], Color.gray);
            var unit = UnitFactory.CreateGeneric("Tess", Team.Player, job, 1, new System.Random(1));

            Assert.IsNull(SheetComposer.SheetFor(unit), "no sheet: UnitView uses the paper doll");

            job.SetSheet(TestSheet());
            var first = SheetComposer.SheetFor(unit);
            Assert.IsNotNull(first);
            Assert.AreSame(first, SheetComposer.SheetFor(UnitFactory.CreateGeneric("Tess", Team.Player, job, 1, new System.Random(2))),
                "same job, team and hair share one composed sheet");
            Assert.AreNotSame(first, SheetComposer.SheetFor(UnitFactory.CreateGeneric("Tess", Team.Enemy, job, 1, new System.Random(1))));
        }

        // ------------------------------------------------------------------ Props

        [Test]
        public void PropSprite_IsCutFromImageCoordinatesWithItsBaseOnTheTile()
        {
            var texture = Texture(16, 16, (x, y) => new Color32(10, 10, 10, 255));
            var prop = ScriptableObject.CreateInstance<PropDefinition>();
            _created.Add(prop);
            Assert.IsNull(prop.Sprite, "no texture: placeholder shape");

            prop.SetSprite(texture, new RectInt(2, 3, 4, 5), 32f);

            var sprite = prop.Sprite;
            Assert.AreEqual(new Rect(2, 16 - 3 - 5, 4, 5), sprite.rect);
            Assert.AreEqual(new Vector2(2f, 0f), sprite.pivot);
            _created.Add(sprite);
        }

        // ------------------------------------------------------------------ Helpers

        /// <summary>
        /// A sheet with 8×8 frames: a two-frame walk strip (feet at row 4) and a one-frame knock-out strip (reaching row 0),
        /// front and back in separate textures.
        /// </summary>
        private UnitSheet TestSheet(Color32? fill = null, WeaponArt weapons = null)
        {
            Color32 Pixel(int frame, int strip, bool back) => fill ?? new Color32((byte)frame, (byte)(back ? 200 : 100), (byte)(10 * (strip + 1)), 255);
            Texture2D Strip(int frames, int strip, bool back, int lowestRow) =>
                Texture(8 * frames, 8, (x, y) => y >= lowestRow ? Pixel(x / 8, strip, back) : new Color32(0, 0, 0, 0));

            var sheet = ScriptableObject.CreateInstance<UnitSheet>();
            _created.Add(sheet);
            sheet.Define(new Vector2Int(8, 8), 8f, new[]
            {
                new UnitSheet.Strip(SheetAnimation.Walk, Strip(2, 0, false, 4), Strip(2, 0, true, 4), Vector2Int.zero, 2, 0.1f),
                new UnitSheet.Strip(SheetAnimation.KnockedOut, Strip(1, 1, false, 0), Strip(1, 1, true, 0), Vector2Int.zero, 1, 1f),
            }, new UnitSheet.RecolorGroup[0], new Color[0], weapons);
            return sheet;
        }

        /// <summary>A readable texture; <paramref name="pixel"/> takes Unity coordinates (origin bottom-left).</summary>
        private Texture2D Texture(int width, int height, System.Func<int, int, Color32> pixel)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = pixel(x, y);
            texture.SetPixels32(pixels);
            texture.Apply();
            _created.Add(texture);
            return texture;
        }

        /// <summary>The colour at the top-centre of a sprite (where every test frame is painted).</summary>
        private static Color32 Centre(Sprite sprite)
        {
            var rect = sprite.textureRect;
            return sprite.texture.GetPixel((int)(rect.x + rect.width / 2), (int)(rect.y + rect.height - 1));
        }

        private static int Pack(Color32 c) => FriendshipWarriors.Units.View.PaperDoll.PaletteSwap.Pack(c);

        private static void AssertClose(Color32 expected, Color32 actual)
        {
            Assert.That(Mathf.Abs(expected.r - actual.r), Is.LessThanOrEqualTo(2), $"{expected} vs {actual}");
            Assert.That(Mathf.Abs(expected.g - actual.g), Is.LessThanOrEqualTo(2), $"{expected} vs {actual}");
            Assert.That(Mathf.Abs(expected.b - actual.b), Is.LessThanOrEqualTo(2), $"{expected} vs {actual}");
        }
    }
}
