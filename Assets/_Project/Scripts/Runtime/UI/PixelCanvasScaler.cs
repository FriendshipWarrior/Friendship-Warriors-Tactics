using UnityEngine;

namespace FriendshipWarriors.UI
{
    /// <summary>
    /// Keeps a screen-space canvas at a whole-number scale (<see cref="PixelUiScale"/>), so the pixel-art UI never draws
    /// one art pixel wider than another. Follows resolution changes (windowed resizes, docking a Steam Deck).
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class PixelCanvasScaler : MonoBehaviour
    {
        private UnityEngine.UI.CanvasScaler _scaler;
        private Canvas _canvas;
        private int _width, _height;

        /// <summary>Adds the scaler to <paramref name="canvas"/> if it isn't there yet (replacing scale-with-screen-size).</summary>
        public static PixelCanvasScaler Ensure(Canvas canvas)
        {
            var pixel = canvas.GetComponent<PixelCanvasScaler>();
            return pixel != null ? pixel : canvas.gameObject.AddComponent<PixelCanvasScaler>();
        }

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _canvas.pixelPerfect = true;
            _scaler = GetComponent<UnityEngine.UI.CanvasScaler>();
            if (_scaler == null)
                _scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            _scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;
            _scaler.referencePixelsPerUnit = 100f;
            Apply();
        }

        private void Update()
        {
            if (Screen.width != _width || Screen.height != _height)
                Apply();
        }

        private void Apply()
        {
            _width = Screen.width;
            _height = Screen.height;
            _scaler.scaleFactor = PixelUiScale.ScaleFor(_width, _height);
        }
    }
}
