using FriendshipWarriors.UI;
using NUnit.Framework;
using UnityEngine;

namespace FriendshipWarriors.Tests
{
    public class PixelUiTests
    {
        // ------------------------------------------------------------------ Integer UI scale

        [TestCase(1280, 800, 2)]   // Steam Deck: 640x400 logical
        [TestCase(1280, 720, 2)]
        [TestCase(1366, 768, 2)]
        [TestCase(1920, 1080, 3)]
        [TestCase(1920, 1200, 3)]
        [TestCase(2560, 1440, 4)]
        [TestCase(2560, 1600, 4)]
        [TestCase(3840, 2160, 6)]
        [TestCase(800, 600, 1)]
        [TestCase(320, 200, 1)]    // smaller than the reference still draws at 1x
        public void Scale_IsTheLargestWholeNumberThatFitsTheReference(int width, int height, int expected)
        {
            Assert.That(PixelUiScale.ScaleFor(width, height), Is.EqualTo(expected));
        }

        [TestCase(1280, 800)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(1366, 768)]
        public void LogicalCanvas_IsNeverSmallerThanTheReference(int width, int height)
        {
            int scale = PixelUiScale.ScaleFor(width, height);
            Assert.That(width / scale, Is.GreaterThanOrEqualTo(PixelUiScale.ReferenceWidth));
            Assert.That(height / scale, Is.GreaterThanOrEqualTo(PixelUiScale.ReferenceHeight));
        }

        // ------------------------------------------------------------------ Crisp font sizes

        [TestCase(16f, 3f, 16f)]     // 1080p: 48 px em, 4 screen px per font pixel
        [TestCase(16f, 2f, 18f)]     // Deck: 32 px would be 2.67 px per font pixel; 3 px is nearest
        [TestCase(12f, 2f, 12f)]     // native size stays native
        [TestCase(1f, 3f, 4f)]       // never below one screen pixel per font pixel
        public void CrispFontSize_LandsEveryFontPixelOnWholeScreenPixels(float wanted, float scale, float expected)
        {
            float size = PixelUiScale.CrispFontSize(wanted, 12, scale);
            Assert.That(size, Is.EqualTo(expected).Within(0.0001f));
            float screenPixelsPerFontPixel = size * scale / 12f;
            Assert.That(screenPixelsPerFontPixel, Is.EqualTo(Mathf.Round(screenPixelsPerFontPixel)).Within(0.0001f));
        }

        // ------------------------------------------------------------------ Bars

        [TestCase(0f, 80, 0)]
        [TestCase(1f, 80, 80)]
        [TestCase(0.5f, 80, 40)]
        [TestCase(0.001f, 80, 1)]    // anything left shows a sliver
        [TestCase(0.999f, 80, 79)]   // only a full bar is full
        [TestCase(2f, 80, 80)]
        [TestCase(-1f, 80, 0)]
        public void BarFill_IsWholePixels(float fraction, int interior, int expected)
        {
            Assert.That(PixelUiScale.FilledPixels(fraction, interior), Is.EqualTo(expected));
        }

        // ------------------------------------------------------------------ Sheets

        [Test]
        public void FrameRects_RunLeftToRight_FromTheTopOfTheTexture()
        {
            // A 384x22 button sheet: four 96x22 states in a row.
            Assert.That(UiSheet.FrameCountFor(384, 22, 96, 22), Is.EqualTo(4));
            Assert.That(UiSheet.FrameRect(384, 22, 96, 22, 0), Is.EqualTo(new RectInt(0, 0, 96, 22)));
            Assert.That(UiSheet.FrameRect(384, 22, 96, 22, 3), Is.EqualTo(new RectInt(288, 0, 96, 22)));
        }

        [Test]
        public void FrameRects_InAGrid_CountRowsFromTheTop()
        {
            // Unity textures start at the bottom-left; sheets are read from the top-left.
            Assert.That(UiSheet.FrameRect(64, 64, 32, 32, 0), Is.EqualTo(new RectInt(0, 32, 32, 32)));
            Assert.That(UiSheet.FrameRect(64, 64, 32, 32, 1), Is.EqualTo(new RectInt(32, 32, 32, 32)));
            Assert.That(UiSheet.FrameRect(64, 64, 32, 32, 2), Is.EqualTo(new RectInt(0, 0, 32, 32)));
        }

        [Test]
        public void FrameRects_ClampOutOfRangeIndices()
        {
            Assert.That(UiSheet.FrameRect(185, 14, 37, 14, 9), Is.EqualTo(new RectInt(148, 0, 37, 14)));
            Assert.That(UiSheet.FrameRect(185, 14, 37, 14, -1), Is.EqualTo(new RectInt(0, 0, 37, 14)));
        }

        // ------------------------------------------------------------------ Skin

        [Test]
        public void BuiltSkin_HasFontsAndFrames()
        {
            var skin = UiSkin.Current;
            Assume.That(skin, Is.Not.Null, "build it with Friendship Warriors > Art > Build Pixel UI Skin");
            Assert.That(skin.Body, Is.Not.Null);
            Assert.That(skin.Title, Is.Not.Null);
            Assert.That(skin.Numbers, Is.Not.Null);
            Assert.That(skin.Window.FrameCount, Is.EqualTo(1));
            Assert.That(skin.ItemFrames.FrameCount, Is.EqualTo(4), "normal, selected, pressed, disabled");
            Assert.That(skin.Cursor.FrameCount, Is.EqualTo(5));
            var border = skin.Window.Border;
            Assert.That(border.x + border.z, Is.LessThan(skin.Window.FrameWidth), "the window's middle must be stretchable");
        }

        [Test]
        public void BuiltSkin_FontSizesAreTheFontsNativePixelSizes()
        {
            var skin = UiSkin.Current;
            Assume.That(skin, Is.Not.Null);
            // Font Kit I draws on 2 font units per pixel: units per em / 2 is the native size.
            Assert.That(skin.Body.faceInfo.pointSize, Is.EqualTo(skin.BodySize));
            Assert.That(skin.Title.faceInfo.pointSize, Is.EqualTo(skin.TitleSize));
            Assert.That(skin.Body.atlasTexture.filterMode, Is.EqualTo(FilterMode.Point));
        }

        [Test]
        public void SpritesFromASheet_AreCachedAndCarryTheBorder()
        {
            var texture = new Texture2D(96, 22);
            try
            {
                var sheet = new UiSheet(texture, 48, 22, new Vector4(6, 4, 6, 4));
                var first = UiSkin.Frame(sheet, 1);
                Assert.That(UiSkin.Frame(sheet, 1), Is.SameAs(first));
                Assert.That(first.rect, Is.EqualTo(new Rect(48, 0, 48, 22)));
                Assert.That(first.border, Is.EqualTo(new Vector4(6, 4, 6, 4)));
                Assert.That(first.pixelsPerUnit, Is.EqualTo(100f), "one art pixel = one logical UI pixel");
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
