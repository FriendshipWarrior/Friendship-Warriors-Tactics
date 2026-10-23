using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// URP 17 marks upscalingFilter obsolete in favour of upscalerName, which is read-only unless the upscaler framework
// is enabled (it isn't here), so the filter is still the setting URP reads.
#pragma warning disable CS0618

namespace FriendshipWarriors.Cameras
{
    /// <summary>
    /// Optional low-resolution look: the 3D world (terrain, props, units) renders at about
    /// <see cref="ReferenceHeight"/> lines and is blown up by a whole-number factor with no smoothing, so everything
    /// shares one chunky pixel grid. Screen-space UI (HUD, menus, popups) draws afterwards at full resolution, and
    /// screen coordinates (mouse picking) are unchanged. Done with URP's render scale and point upscaling, which live
    /// on the pipeline asset: they're changed only in Play mode and put back when this is disabled. URP only upscales
    /// with point sampling inside its post-processing pass, so post-processing is switched on for this camera while
    /// pixelated, with a neutral volume on top so the pipeline's default effects (the URP template's bloom, vignette,
    /// tonemapping and motion blur) don't change the colours: pixel mode should differ only in resolution. Off by default.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class PixelRendering : MonoBehaviour
    {
        [Tooltip("Render the world at low resolution and scale it up crisply (pixel-art look). Changes how units look too.")]
        [SerializeField] private bool _pixelated;

        [Tooltip("Target height in pixels of the low-resolution image; the screen height is divided by a whole number to get near it (800 → 400 on Steam Deck, 1080 → 360).")]
        [SerializeField, Min(120)] private int _referenceHeight = 360;

        private UniversalRenderPipelineAsset _asset;
        private float _savedRenderScale;
        private UpscalingFilterSelection _savedFilter;
        private bool _applied;
        private int _appliedForHeight;
        private UniversalAdditionalCameraData _cameraData;
        private bool _savedPostProcessing;
        private Volume _neutralVolume;
        private VolumeProfile _neutralProfile;

        public int ReferenceHeight => _referenceHeight;

        public bool Pixelated
        {
            get => _pixelated;
            set
            {
                _pixelated = value;
                Apply();
            }
        }

        /// <summary>The whole-number upscale for a screen: how many screen pixels each world pixel covers (at least 1).</summary>
        public static int ScaleFor(int screenHeight, int referenceHeight) =>
            Mathf.Max(1, screenHeight / Mathf.Max(1, referenceHeight));

        /// <summary>URP render scale for a whole-number upscale (URP's minimum is 0.1).</summary>
        public static float RenderScaleFor(int scale) => Mathf.Max(0.1f, 1f / Mathf.Max(1, scale));

        private void OnEnable() => Apply();

        private void OnDisable() => Restore();

        private void OnValidate()
        {
            if (Application.isPlaying && isActiveAndEnabled)
                Apply();
        }

        private void Update()
        {
            if (_applied && _appliedForHeight != Screen.height)
                Apply();
        }

        private void Apply()
        {
            if (!_pixelated || !Application.isPlaying)
            {
                Restore();
                return;
            }

            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset == null)
                return;
            if (!_applied)
            {
                _asset = asset;
                _savedRenderScale = asset.renderScale;
                _savedFilter = asset.upscalingFilter;
                if (TryGetComponent<Camera>(out var camera))
                {
                    _cameraData = camera.GetUniversalAdditionalCameraData();
                    _savedPostProcessing = _cameraData.renderPostProcessing;
                }
                _applied = true;
            }
            _appliedForHeight = Screen.height;
            _asset.renderScale = RenderScaleFor(ScaleFor(Screen.height, _referenceHeight));
            _asset.upscalingFilter = UpscalingFilterSelection.Point;
            if (_cameraData != null)
                _cameraData.renderPostProcessing = true;
            if (_neutralVolume == null)
                CreateNeutralVolume();
        }

        /// <summary>A global, top-priority volume that switches off every effect the default profiles might turn on.</summary>
        private void CreateNeutralVolume()
        {
            _neutralProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _neutralProfile.hideFlags = HideFlags.DontSave;
            _neutralProfile.Add<Tonemapping>(true).mode.Override(TonemappingMode.None);
            _neutralProfile.Add<Bloom>(true).intensity.Override(0f);
            _neutralProfile.Add<Vignette>(true).intensity.Override(0f);
            _neutralProfile.Add<MotionBlur>(true).intensity.Override(0f);
            _neutralProfile.Add<FilmGrain>(true).intensity.Override(0f);
            _neutralProfile.Add<ChromaticAberration>(true).intensity.Override(0f);
            _neutralProfile.Add<LensDistortion>(true).intensity.Override(0f);
            _neutralProfile.Add<DepthOfField>(true).mode.Override(DepthOfFieldMode.Off);
            _neutralVolume = gameObject.AddComponent<Volume>();
            _neutralVolume.hideFlags = HideFlags.DontSave;
            _neutralVolume.isGlobal = true;
            _neutralVolume.priority = 1000f;
            _neutralVolume.sharedProfile = _neutralProfile;
        }

        private void Restore()
        {
            if (!_applied)
                return;
            if (_asset != null)
            {
                _asset.renderScale = _savedRenderScale;
                _asset.upscalingFilter = _savedFilter;
            }
            if (_cameraData != null)
                _cameraData.renderPostProcessing = _savedPostProcessing;
            if (_neutralVolume != null)
                Destroy(_neutralVolume);
            if (_neutralProfile != null)
                Destroy(_neutralProfile);
            _neutralVolume = null;
            _neutralProfile = null;
            _asset = null;
            _cameraData = null;
            _applied = false;
        }
    }
}
