using System.Collections.Generic;
using FriendshipWarriors.Battle;
using FriendshipWarriors.Units.View;
using TMPro;
using UnityEngine;

namespace FriendshipWarriors.UI
{
    /// <summary>
    /// Draws each unit's status codes (PSN HST ...) and objective role (TARGET / PROTECT) over it, in screen space on a
    /// whole-number pixel canvas, in the pixel font. Screen space keeps the tags crisp and readable whatever the world
    /// camera does (it may render at a low resolution for the pixel look). Positions snap to whole logical pixels.
    /// The <see cref="BattleController"/> adds this component to itself.
    /// </summary>
    [DefaultExecutionOrder(1000)] // after unit views move, so tags follow walks and hops without lagging a frame
    [RequireComponent(typeof(BattleController))]
    public sealed class UnitTagOverlay : MonoBehaviour
    {
        private static readonly Color StatusColor = new(1f, 0.85f, 0.35f);

        private readonly Dictionary<UnitView, (TextMeshProUGUI status, TextMeshProUGUI role)> _tags = new();
        private readonly List<UnitView> _gone = new();
        private BattleController _battle;
        private Canvas _canvas;
        private Camera _camera;

        private void Awake()
        {
            _battle = GetComponent<BattleController>();
            var go = new GameObject("Unit Tags", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            go.transform.SetParent(transform, false);
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = -20; // under the battle feedback popups and the HUD
            PixelCanvasScaler.Ensure(_canvas);
        }

        private void LateUpdate()
        {
            if (_camera == null && _battle.CameraRig != null)
                _camera = _battle.CameraRig.GetComponentInChildren<Camera>();
            float scale = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;

            foreach (var view in _battle.Views)
            {
                if (view == null)
                    continue;
                if (!_tags.TryGetValue(view, out var tags))
                {
                    tags = (NewTag("Status", StatusColor), NewTag("Role", Color.white));
                    _tags[view] = tags;
                }
                bool shown = view.isActiveAndEnabled && _camera != null;
                Place(tags.status, shown ? view.StatusTag : "", view.transform.position + Vector3.up * UnitView.StatusLabelHeight, scale);
                tags.role.color = view.RoleColor;
                Place(tags.role, shown ? view.RoleTag : "", view.transform.position + Vector3.up * UnitView.RoleLabelHeight, scale);
            }

            // Views from a previous battle (a rerolled test map) take their tags with them.
            _gone.Clear();
            foreach (var view in _tags.Keys)
                if (view == null || !Contains(view))
                    _gone.Add(view);
            foreach (var view in _gone)
            {
                var (status, role) = _tags[view];
                Destroy(status.gameObject);
                Destroy(role.gameObject);
                _tags.Remove(view);
            }
        }

        private bool Contains(UnitView view)
        {
            foreach (var v in _battle.Views)
                if (v == view)
                    return true;
            return false;
        }

        private void Place(TextMeshProUGUI text, string value, Vector3 world, float scale)
        {
            bool visible = !string.IsNullOrEmpty(value);
            Vector3 screen = default;
            if (visible)
            {
                screen = _camera.WorldToScreenPoint(world);
                visible = screen.z > 0f;
            }
            text.gameObject.SetActive(visible);
            if (!visible)
                return;
            if (text.text != value)
                text.text = value;
            // Whole logical pixels, so the pixel font never straddles screen pixels.
            text.rectTransform.anchoredPosition = new Vector2(Mathf.Round(screen.x / scale), Mathf.Round(screen.y / scale));
        }

        private TextMeshProUGUI NewTag(string tagName, Color color)
        {
            var go = new GameObject(tagName, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_canvas.transform, false);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(96f, 12f);
            var text = go.GetComponent<TextMeshProUGUI>();
            var skin = UiSkin.Current;
            // The SDF numbers font (same letters as the body font) keeps a dark outline for reading over terrain.
            if (skin != null && skin.Numbers != null)
            {
                text.font = skin.Numbers;
                if (skin.NumbersOutline != null)
                    text.fontSharedMaterial = skin.NumbersOutline;
            }
            else
            {
                text.outlineWidth = 0.25f;
                text.outlineColor = new Color32(10, 10, 20, 255);
            }
            // Native size: every font pixel is a whole number of screen pixels at any whole-number canvas scale.
            text.fontSize = skin != null ? skin.BodySize : 12;
            text.alignment = TextAlignmentOptions.Bottom;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            text.color = color;
            go.SetActive(false);
            return text;
        }
    }
}
