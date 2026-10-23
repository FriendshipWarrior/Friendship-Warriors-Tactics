using UnityEngine;

namespace FriendshipWarriors.UI
{
    /// <summary>
    /// Integer scaling for the pixel-art UI. The UI is laid out on a canvas of at least <see cref="ReferenceWidth"/> x
    /// <see cref="ReferenceHeight"/> logical pixels, and each logical pixel is drawn as a whole number of screen pixels,
    /// so pixel fonts and panels stay crisp (non-integer scales make some art pixels wider than others).
    /// 1280x800 (Steam Deck) → 2 (640x400 logical), 1920x1080 → 3 (640x360), 2560x1440 → 4 (640x360).
    /// </summary>
    public static class PixelUiScale
    {
        public const int ReferenceWidth = 640;
        public const int ReferenceHeight = 360;

        /// <summary>The largest whole-number scale that still fits the reference canvas on screen (at least 1).</summary>
        public static int ScaleFor(int screenWidth, int screenHeight) =>
            Mathf.Max(1, Mathf.Min(screenWidth / ReferenceWidth, screenHeight / ReferenceHeight));

        /// <summary>
        /// The font size nearest <paramref name="logicalSize"/> at which each pixel of a pixel font (drawn on a
        /// <paramref name="nativePixels"/> em) covers a whole number of screen pixels on a canvas at
        /// <paramref name="canvasScale"/>, so animated text (popups that punch in) stays crisp at every step.
        /// </summary>
        public static float CrispFontSize(float logicalSize, int nativePixels, float canvasScale)
        {
            if (nativePixels <= 0 || canvasScale <= 0f)
                return logicalSize;
            int screenPixelsPerArtPixel = Mathf.Max(1, Mathf.RoundToInt(logicalSize * canvasScale / nativePixels));
            return screenPixelsPerArtPixel * nativePixels / canvasScale;
        }

        /// <summary>How many of a bar's <paramref name="interior"/> pixels to fill for <paramref name="fraction"/>,
        /// rounded to whole pixels; anything above zero shows at least one pixel and only a full bar fills it all.</summary>
        public static int FilledPixels(float fraction, int interior)
        {
            if (interior <= 0 || fraction <= 0f)
                return 0;
            if (fraction >= 1f)
                return interior;
            return Mathf.Clamp(Mathf.RoundToInt(fraction * interior), 1, interior - 1);
        }
    }
}
