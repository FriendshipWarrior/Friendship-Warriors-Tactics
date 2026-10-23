using System.Collections.Generic;
using FriendshipWarriors.Audio;
using FriendshipWarriors.Battle;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Status;
using FriendshipWarriors.Units.View;
using TMPro;
using UnityEngine;

namespace FriendshipWarriors.Feedback
{
    /// <summary>
    /// FFT-style combat feedback, presentation only: floating numbers and words over units (damage, healing, MP,
    /// Miss, Guarded, Critical!, status names), a white hit flash and knock-shake on the struck sprite, a soft glow
    /// for healing, a short camera shake on criticals, a flash into the knocked-out pose with the death counter shown
    /// over the unit, and a fading ghost plus a placeholder crystal when a unit is lost. Sounds play with them.
    /// <para>
    /// It listens rather than being told: <see cref="Unit.Changed"/> for HP and MP, the units' statuses (compared
    /// each frame), and <see cref="BattleController"/>'s cues for what the numbers can't show. Everything is timed
    /// on the <see cref="BattleClock"/>, so pausing freezes it. The controller adds this component to itself.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(1000)] // after unit views and the camera rig, so overlays and shakes sit on top of them
    [RequireComponent(typeof(BattleController))]
    public sealed class BattleFeedback : MonoBehaviour
    {
        private const float CanvasFontSize = 46f;
        private const float RowHeight = 46f;
        private const float GhostTime = 0.9f;
        private const float CrystalTime = 3.5f;

        /// <summary>How long after an action its reactions' feedback starts (a counter follows the hit, FFT-style).</summary>
        public const float ReactionBeat = 0.45f;

        [Header("Camera Shake")]
        [Tooltip("Shake the camera on critical hits.")]
        [SerializeField] private bool _shakeOnCritical = true;
        [SerializeField, Min(0f)] private float _shakeStrength = 0.09f;
        [SerializeField, Min(0.01f)] private float _shakeDuration = 0.28f;

        private BattleController _battle;
        private Camera _camera;
        private RectTransform _canvas;
        private Canvas _canvasComponent;
        private Material _flashMaterial;
        private readonly Dictionary<Unit, Tracker> _trackers = new();
        private readonly List<Unit> _drop = new();
        private readonly List<Popup> _popups = new();
        private readonly Stack<TextMeshProUGUI> _freeTexts = new();
        private readonly List<Remnant> _remnants = new();
        private readonly PopupSchedule _schedule = new();
        private readonly List<StatusEffect> _gained = new(), _lifted = new(), _current = new();
        private float _shakeStart = float.NegativeInfinity;
        // Set when reactions start resolving; everything requested later in the same frame waits this long.
        private float _frameDelay;
        private Vector3 _appliedCameraShake;
        private static Sprite _crystal;

        /// <summary>Per-unit state: what it looked like last frame, pending effects and the flash overlay.</summary>
        private sealed class Tracker
        {
            public Unit Unit;
            public UnitView View;
            public System.Action<UnitChange> Handler;
            public readonly List<StatusEffect> Statuses = new();
            public bool WasDown;
            public bool CriticalPending;
            public float HitAt = float.NegativeInfinity;
            public float ShakeAt = float.NegativeInfinity;
            public float GlowAt = float.NegativeInfinity;
            public Color GlowColor;
            public float KnockOutAt = float.NegativeInfinity;
            public SpriteRenderer Flash;
            public Vector3 AppliedShake;
            public TextMeshProUGUI Counter;
            public Vector3 LastAnchor;
        }

        private sealed class Popup
        {
            public Tracker Owner;
            public Vector3 Anchor;
            public float Start;
            public int Row;
            public PopupStyle Style;
            public SoundCue Sound;
            public bool Started;
            public TextMeshProUGUI Text;
            public System.Action OnStart;
        }

        /// <summary>What's left on the tile of a lost unit: its fading ghost, then a crystal (FFT leaves one).</summary>
        private sealed class Remnant
        {
            public SpriteRenderer Ghost;
            public Vector3 GhostOrigin;
            public SpriteRenderer Crystal;
            public Vector3 Origin;
            public float Start;
        }

        private float Now => _battle.Clock.Now(Time.unscaledTime);

        private void Awake()
        {
            _battle = GetComponent<BattleController>();
            BuildCanvas();
            var material = Resources.Load<Material>("Feedback/SpriteFlash");
            _flashMaterial = material != null ? material : new Material(Shader.Find("Sprites/Default"));
        }

        private void OnEnable()
        {
            _battle.Began += OnBegan;
            _battle.Cue += OnCue;
            _battle.UnitLost += OnUnitLost;
        }

        private void OnDisable()
        {
            _battle.Began -= OnBegan;
            _battle.Cue -= OnCue;
            _battle.UnitLost -= OnUnitLost;
            RemoveShakes();
        }

        private void OnDestroy()
        {
            foreach (var tracker in _trackers.Values)
                tracker.Unit.Changed -= tracker.Handler;
            _trackers.Clear();
        }

        private void BuildCanvas()
        {
            var go = new GameObject("Battle Feedback", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            go.transform.SetParent(transform, false);
            _canvasComponent = go.GetComponent<Canvas>();
            _canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvasComponent.sortingOrder = -10; // under the HUD's menus and panels
            var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _canvas = (RectTransform)go.transform;
        }

        // ------------------------------------------------------------------ Battle events

        private void OnBegan()
        {
            foreach (var popup in _popups)
                Release(popup.Text);
            _popups.Clear();
            _schedule.Clear();
            foreach (var tracker in _trackers.Values)
                Forget(tracker);
            _trackers.Clear();
            foreach (var remnant in _remnants)
                DestroyRemnant(remnant);
            _remnants.Clear();
            Sync();
        }

        private void OnCue(Unit unit, BattleCue cue)
        {
            var tracker = unit != null ? Track(unit) : null;
            switch (cue)
            {
                case BattleCue.Miss when tracker != null:
                    Show(tracker, PopupKind.Miss);
                    break;
                case BattleCue.Guarded when tracker != null:
                    Show(tracker, PopupKind.Guarded, onStart: () => tracker.ShakeAt = Now); // a jolt, no flash: nothing got through
                    break;
                case BattleCue.Critical when tracker != null:
                    tracker.CriticalPending = true;
                    break;
                case BattleCue.Reactions:
                    _frameDelay = ReactionBeat;
                    break;
                default:
                    SoundPlayer.Play(FeedbackStyle.Sound(cue));
                    break;
            }
        }

        private void OnUnitChanged(Tracker tracker, UnitChange change)
        {
            switch (change.Kind)
            {
                case UnitChangeKind.HpLost:
                    bool critical = tracker.CriticalPending;
                    bool downed = tracker.Unit.IsKnockedOut;
                    tracker.CriticalPending = false;
                    // The flash, shake and knock-out land with the number, which may wait its turn (stagger, reactions).
                    Show(tracker, critical ? PopupKind.CriticalDamage : PopupKind.Damage, change.Amount, onStart: () =>
                    {
                        tracker.HitAt = tracker.ShakeAt = Now;
                        if (critical && _shakeOnCritical)
                            _shakeStart = Now;
                        if (downed)
                        {
                            tracker.KnockOutAt = Now;
                            SoundPlayer.Play(SoundCue.KnockOut);
                        }
                    });
                    break;
                case UnitChangeKind.HpRestored:
                case UnitChangeKind.Revived:
                    Show(tracker, change.Kind == UnitChangeKind.Revived ? PopupKind.Revived : PopupKind.Heal, change.Amount,
                        onStart: () => Glow(tracker, FeedbackStyle.HealColor));
                    break;
                case UnitChangeKind.MpRestored:
                    Show(tracker, PopupKind.MpRestore, change.Amount, onStart: () => Glow(tracker, FeedbackStyle.MpColor));
                    break;
                case UnitChangeKind.MpLost:
                    Show(tracker, PopupKind.MpLoss, change.Amount);
                    break;
            }
        }

        private void Glow(Tracker tracker, Color color)
        {
            tracker.GlowAt = Now;
            tracker.GlowColor = color;
        }

        private void OnUnitLost(UnitView view)
        {
            if (view == null)
                return;
            var sprite = view.Sprite;
            var remnant = new Remnant { Origin = view.transform.position, Start = Now };
            if (sprite != null && sprite.sprite != null)
            {
                remnant.Ghost = new GameObject("Lost Ghost").AddComponent<SpriteRenderer>();
                remnant.Ghost.transform.SetParent(transform, false);
                remnant.Ghost.transform.SetPositionAndRotation(sprite.transform.position, sprite.transform.rotation);
                remnant.Ghost.transform.localScale = sprite.transform.lossyScale;
                remnant.Ghost.sprite = sprite.sprite;
                remnant.Ghost.flipX = sprite.flipX;
                remnant.Ghost.color = sprite.color;
                remnant.GhostOrigin = sprite.transform.position;
            }
            remnant.Crystal = new GameObject("Lost Crystal").AddComponent<SpriteRenderer>();
            remnant.Crystal.transform.SetParent(transform, false);
            remnant.Crystal.sprite = CrystalSprite();
            remnant.Crystal.color = new Color(0.55f, 0.95f, 1f, 0f);
            _remnants.Add(remnant);
            if (_trackers.TryGetValue(view.Unit, out var tracker))
            {
                Forget(tracker);
                _trackers.Remove(view.Unit);
            }
        }

        // ------------------------------------------------------------------ Tracking

        private void Sync()
        {
            foreach (var view in _battle.Views)
            {
                if (view != null && view.Unit != null)
                    Track(view.Unit, view);
            }
        }

        private Tracker Track(Unit unit, UnitView view = null)
        {
            if (_trackers.TryGetValue(unit, out var tracker))
            {
                if (tracker.View == null && view != null)
                    Attach(tracker, view);
                return tracker;
            }
            tracker = new Tracker { Unit = unit, WasDown = unit.IsKnockedOut };
            tracker.Statuses.AddRange(unit.Statuses.Active);
            tracker.Handler = change => OnUnitChanged(tracker, change);
            unit.Changed += tracker.Handler;
            if (view == null)
            {
                foreach (var v in _battle.Views)
                {
                    if (v != null && v.Unit == unit)
                        view = v;
                }
            }
            if (view != null)
                Attach(tracker, view);
            _trackers[unit] = tracker;
            return tracker;
        }

        private void Attach(Tracker tracker, UnitView view)
        {
            tracker.View = view;
            tracker.LastAnchor = Anchor(tracker);
            var sprite = view.Sprite;
            if (sprite == null)
                return;
            var flash = new GameObject("Hit Flash").AddComponent<SpriteRenderer>();
            flash.transform.SetParent(sprite.transform, false);
            // A hair toward the camera, so it draws over the sprite it copies.
            flash.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            flash.sharedMaterial = _flashMaterial;
            flash.enabled = false;
            tracker.Flash = flash;
        }

        private void Forget(Tracker tracker)
        {
            tracker.Unit.Changed -= tracker.Handler;
            if (tracker.View != null && tracker.View.Sprite != null)
                tracker.View.Sprite.transform.localPosition -= tracker.AppliedShake;
            tracker.AppliedShake = Vector3.zero;
            if (tracker.Flash != null)
                Destroy(tracker.Flash.gameObject);
            if (tracker.Counter != null)
                Destroy(tracker.Counter.gameObject);
            foreach (var popup in _popups)
            {
                if (popup.Owner == tracker)
                    popup.Owner = null; // keeps playing where it was
            }
        }

        /// <summary>Where a unit's popups rise from: just over its head, or over the body when it lies knocked out.</summary>
        private static Vector3 Anchor(Tracker tracker)
        {
            if (tracker.View == null)
                return tracker.LastAnchor;
            float height = (tracker.Unit.IsKnockedOut ? 0.5f : 1.0f) * UnitView.SpriteScale;
            return tracker.View.transform.position + Vector3.up * height;
        }

        private void Show(Tracker tracker, PopupKind kind, int amount = 0, StatusEffect status = default, System.Action onStart = null)
        {
            var (start, row) = _schedule.Add(tracker.Unit.GetHashCode(), Now + _frameDelay);
            var popup = new Popup
            {
                Owner = tracker,
                Anchor = Anchor(tracker),
                Start = start,
                Row = row,
                Style = FeedbackStyle.Popup(kind, amount, status),
                Sound = FeedbackStyle.Sound(kind),
                OnStart = onStart,
                Text = Acquire(),
            };
            popup.Text.gameObject.SetActive(false);
            _popups.Add(popup);
        }

        // ------------------------------------------------------------------ Frame

        private void Update() => RemoveShakes();

        /// <summary>
        /// Shake offsets are added in LateUpdate and taken back out here, before anything else moves the sprite or
        /// camera this frame, so they never build up or fight other code that positions them.
        /// </summary>
        private void RemoveShakes()
        {
            foreach (var tracker in _trackers.Values)
            {
                if (tracker.View != null && tracker.View.Sprite != null)
                    tracker.View.Sprite.transform.localPosition -= tracker.AppliedShake;
                tracker.AppliedShake = Vector3.zero;
            }
            if (_camera != null)
                _camera.transform.localPosition -= _appliedCameraShake;
            _appliedCameraShake = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (_battle.Clock == null)
                return;
            if (_camera == null && _battle.CameraRig != null)
                _camera = _battle.CameraRig.GetComponentInChildren<Camera>();
            Sync();
            float now = Now;

            _drop.Clear();
            foreach (var tracker in _trackers.Values)
            {
                if (tracker.View == null && !ContainsUnit(tracker.Unit))
                {
                    // Its view went away without a loss (a new map): stop following it.
                    _drop.Add(tracker.Unit);
                    continue;
                }
                UpdateTracker(tracker, now);
            }
            foreach (var unit in _drop)
            {
                Forget(_trackers[unit]);
                _trackers.Remove(unit);
            }

            UpdatePopups(now);
            UpdateRemnants(now);
            UpdateCameraShake(now);
            _frameDelay = 0f;
        }

        private bool ContainsUnit(Unit unit)
        {
            foreach (var view in _battle.Views)
            {
                if (view != null && view.Unit == unit)
                    return true;
            }
            return false;
        }

        private void UpdateTracker(Tracker tracker, float now)
        {
            var unit = tracker.Unit;
            bool down = unit.IsKnockedOut;

            // Statuses gained or lifted since last frame.
            var current = _current;
            current.Clear();
            current.AddRange(unit.Statuses.Active);
            StatusDiff.Compare(tracker.Statuses, current, down && !tracker.WasDown, _gained, _lifted);
            foreach (var status in _gained)
                Show(tracker, PopupKind.StatusGained, status: status);
            foreach (var status in _lifted)
                Show(tracker, PopupKind.StatusLifted, status: status);
            tracker.Statuses.Clear();
            tracker.Statuses.AddRange(current);

            tracker.WasDown = down;
            // A critical cue is only good for the damage that follows it at once.
            tracker.CriticalPending = false;

            if (tracker.View == null)
                return;
            tracker.LastAnchor = Anchor(tracker);
            var sprite = tracker.View.Sprite;
            if (sprite == null)
                return;

            // Knocked out: dimmer as the death counter runs down (UnitView already greys and lays the unit down).
            if (down)
            {
                var c = sprite.color;
                c.a = 0.55f + 0.1f * Mathf.Clamp(unit.DeathCounter, 0, Unit.DeathCounterStart);
                sprite.color = c;
            }
            UpdateCounter(tracker, down);

            // White flash on a hit or a knock-out, otherwise the heal glow; drawn as a silhouette over the sprite.
            float flash = Mathf.Max(FeedbackCurves.Flash(now - tracker.HitAt), FeedbackCurves.KnockOutFlash(now - tracker.KnockOutAt));
            float glow = FeedbackCurves.Glow(now - tracker.GlowAt);
            if (tracker.Flash != null)
            {
                bool on = flash > 0f || glow > 0f;
                tracker.Flash.enabled = on;
                if (on)
                {
                    tracker.Flash.sprite = sprite.sprite;
                    tracker.Flash.flipX = sprite.flipX;
                    tracker.Flash.sortingLayerID = sprite.sortingLayerID;
                    tracker.Flash.sortingOrder = sprite.sortingOrder;
                    tracker.Flash.color = flash >= glow ? new Color(1f, 1f, 1f, flash) : WithAlpha(tracker.GlowColor, glow);
                }
            }

            float shake = FeedbackCurves.Shake(now - tracker.ShakeAt);
            tracker.AppliedShake = sprite.transform.right * shake;
            sprite.transform.localPosition += tracker.AppliedShake;
        }

        /// <summary>FFT shows the turns left over a knocked-out unit.</summary>
        private void UpdateCounter(Tracker tracker, bool down)
        {
            var unit = tracker.Unit;
            bool show = down && unit.DeathCounter > 0 && _camera != null;
            if (!show)
            {
                if (tracker.Counter != null)
                    tracker.Counter.gameObject.SetActive(false);
                return;
            }
            if (tracker.Counter == null)
            {
                tracker.Counter = NewText("Death Counter");
                tracker.Counter.fontSize = CanvasFontSize * 0.85f;
            }
            var text = tracker.Counter;
            text.gameObject.SetActive(true);
            text.text = unit.DeathCounter.ToString();
            // Red when it's the last turn.
            text.color = unit.DeathCounter <= 1 ? new Color(1f, 0.45f, 0.4f) : new Color(1f, 0.92f, 0.6f);
            if (!Place(text.rectTransform, tracker.View.transform.position + Vector3.up * (0.6f * UnitView.SpriteScale), 0f))
                text.gameObject.SetActive(false);
        }

        private void UpdatePopups(float now)
        {
            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                var popup = _popups[i];
                float t = now - popup.Start;
                if (PopupMotion.IsFinished(t))
                {
                    Release(popup.Text);
                    _popups.RemoveAt(i);
                    continue;
                }
                if (t < 0f)
                    continue;
                if (!popup.Started)
                {
                    popup.Started = true;
                    popup.Text.text = popup.Style.Text;
                    popup.Text.gameObject.SetActive(true);
                    SoundPlayer.Play(popup.Sound);
                    popup.OnStart?.Invoke();
                }
                if (popup.Owner != null)
                    popup.Anchor = popup.Owner.LastAnchor;

                var text = popup.Text;
                float punch = popup.Style.Size > 1f ? 0.45f : 0.25f;
                text.fontSize = CanvasFontSize * popup.Style.Size * UnitView.SpriteScale / 1.25f * PopupMotion.Scale(t, punch);
                var color = (Color)popup.Style.Color;
                color.a = PopupMotion.Alpha(t);
                text.color = color;
                bool visible = Place(text.rectTransform, popup.Anchor, PopupMotion.Height(t) + popup.Row * RowHeight);
                text.enabled = visible;
            }
        }

        private void UpdateRemnants(float now)
        {
            for (int i = _remnants.Count - 1; i >= 0; i--)
            {
                var remnant = _remnants[i];
                float t = now - remnant.Start;
                if (remnant.Ghost != null)
                {
                    float u = Mathf.Clamp01(t / GhostTime);
                    var c = remnant.Ghost.color;
                    c.a = 1f - u;
                    remnant.Ghost.color = c;
                    remnant.Ghost.transform.position = remnant.GhostOrigin + Vector3.up * (0.6f * u);
                    if (u >= 1f)
                    {
                        Destroy(remnant.Ghost.gameObject);
                        remnant.Ghost = null;
                    }
                }
                if (remnant.Crystal != null)
                {
                    float appear = Mathf.Clamp01((t - 0.3f) / 0.3f);
                    float fade = Mathf.Clamp01((CrystalTime - t) / 0.5f);
                    var c = remnant.Crystal.color;
                    c.a = appear * fade;
                    remnant.Crystal.color = c;
                    float bob = 0.08f * Mathf.Sin(t * 3f);
                    remnant.Crystal.transform.position = remnant.Origin + Vector3.up * (0.45f + bob);
                    if (_camera != null)
                        remnant.Crystal.transform.rotation = Quaternion.Euler(0f, _camera.transform.eulerAngles.y, 0f);
                }
                if (t >= CrystalTime)
                {
                    DestroyRemnant(remnant);
                    _remnants.RemoveAt(i);
                }
            }
        }

        private void UpdateCameraShake(float now)
        {
            if (_camera == null)
                return;
            var (x, y) = FeedbackCurves.CameraShake(now - _shakeStart, _shakeDuration, _shakeStrength);
            _appliedCameraShake = new Vector3(x, y, 0f);
            _camera.transform.localPosition += _appliedCameraShake;
        }

        // ------------------------------------------------------------------ Helpers

        /// <summary>Puts a canvas element over a world point, raised by <paramref name="pixelsUp"/>. False when off-screen or behind.</summary>
        private bool Place(RectTransform rect, Vector3 world, float pixelsUp)
        {
            if (_camera == null)
                return false;
            var screen = _camera.WorldToScreenPoint(world);
            if (screen.z < 0f)
                return false;
            float scale = _canvasComponent.scaleFactor > 0f ? _canvasComponent.scaleFactor : 1f;
            rect.anchoredPosition = new Vector2(screen.x / scale, screen.y / scale + pixelsUp);
            return true;
        }

        private TextMeshProUGUI Acquire()
        {
            var text = _freeTexts.Count > 0 ? _freeTexts.Pop() : NewText("Popup");
            text.enabled = true;
            return text;
        }

        private void Release(TextMeshProUGUI text)
        {
            if (text == null)
                return;
            text.gameObject.SetActive(false);
            _freeTexts.Push(text);
        }

        private TextMeshProUGUI NewText(string textName)
        {
            var go = new GameObject(textName, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_canvas, false);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(360f, 140f);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Bottom;
            text.fontStyle = FontStyles.Bold;
            text.fontSize = CanvasFontSize;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.lineSpacing = -18f;
            // A heavy dark outline keeps numbers readable over any terrain (FFT's numbers are outlined too).
            text.outlineWidth = 0.28f;
            text.outlineColor = new Color32(10, 10, 20, 255);
            return text;
        }

        private void DestroyRemnant(Remnant remnant)
        {
            if (remnant.Ghost != null)
                Destroy(remnant.Ghost.gameObject);
            if (remnant.Crystal != null)
                Destroy(remnant.Crystal.gameObject);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        /// <summary>
        /// Placeholder for FFT's crystal: a small pale diamond. Swap for real art later (and, like FFT, a crystal or
        /// a treasure chest the other units can pick up, once that's designed).
        /// </summary>
        private static Sprite CrystalSprite()
        {
            if (_crystal != null)
                return _crystal;
            const int w = 9, h = 15;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Abs(x - 4) / 4.5f, dy = Mathf.Abs(y - 7) / 7.5f;
                bool inside = dx + dy <= 1f;
                bool edge = inside && dx + dy > 0.78f;
                float shade = x < 4 ? 1f : 0.78f;
                pixels[y * w + x] = !inside ? Color.clear : edge ? new Color(0.15f, 0.25f, 0.4f) : new Color(shade, shade, shade);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            _crystal = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 14f);
            return _crystal;
        }
    }
}
