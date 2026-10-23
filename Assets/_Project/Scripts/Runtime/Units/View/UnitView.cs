using FriendshipWarriors.Cameras;
using FriendshipWarriors.Grid.View;
using UnityEngine;

namespace FriendshipWarriors.Units.View
{
    /// <summary>
    /// Shows a <see cref="Units.Unit"/>: a flat sprite that turns to face the camera (rotating only around the
    /// vertical axis so it stays upright), switches between front/back poses and mirrors them based on the unit's
    /// facing and the camera angle, plus a team-colored marker on the ground. Knocked-out units lie down, greyed.
    /// </summary>
    public sealed class UnitView : MonoBehaviour
    {
        private static readonly Color PlayerColor = new(0.35f, 0.65f, 1f);
        private static readonly Color EnemyColor = new(1f, 0.35f, 0.35f);

        private SpriteRenderer _sprite;
        private SpriteRenderer _marker;
        private TMPro.TextMeshPro _statusLabel;
        private TMPro.TextMeshPro _roleLabel;
        private int _shownStatusVersion = -1;
        private GridView _gridView;
        private IsoCameraRig _cameraRig;

        public Unit Unit { get; private set; }

        public static UnitView Create(Unit unit, GridView gridView, IsoCameraRig cameraRig, Transform parent)
        {
            var go = new GameObject(unit.Name);
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<UnitView>();
            view.Bind(unit, gridView, cameraRig);
            return view;
        }

        public void Bind(Unit unit, GridView gridView, IsoCameraRig cameraRig)
        {
            Unit = unit;
            _gridView = gridView;
            _cameraRig = cameraRig;

            _sprite = new GameObject("Sprite").AddComponent<SpriteRenderer>();
            _sprite.transform.SetParent(transform, false);

            _marker = new GameObject("Team Marker").AddComponent<SpriteRenderer>();
            _marker.transform.SetParent(transform, false);
            _marker.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            _marker.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _marker.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
            _marker.sprite = PlaceholderSprites.GroundMarker();
            _marker.color = unit.Team == Team.Player ? PlayerColor : EnemyColor;

            // Placeholder status indicator: short codes floating above the unit (PSN, HST...).
            _statusLabel = new GameObject("Statuses").AddComponent<TMPro.TextMeshPro>();
            _statusLabel.transform.SetParent(transform, false);
            _statusLabel.transform.localPosition = new Vector3(0f, 1.45f, 0f);
            _statusLabel.fontSize = 2.2f;
            _statusLabel.alignment = TMPro.TextAlignmentOptions.Center;
            _statusLabel.color = new Color(1f, 0.85f, 0.35f);
            _statusLabel.outlineWidth = 0.25f;
            _statusLabel.outlineColor = Color.black;
            _statusLabel.rectTransform.sizeDelta = new Vector2(4f, 1f);

            // Objective role (TARGET / PROTECT), above the status codes.
            _roleLabel = new GameObject("Role").AddComponent<TMPro.TextMeshPro>();
            _roleLabel.transform.SetParent(transform, false);
            _roleLabel.transform.localPosition = new Vector3(0f, 1.75f, 0f);
            _roleLabel.fontSize = 2.4f;
            _roleLabel.fontStyle = TMPro.FontStyles.Bold;
            _roleLabel.alignment = TMPro.TextAlignmentOptions.Center;
            _roleLabel.outlineWidth = 0.25f;
            _roleLabel.outlineColor = Color.black;
            _roleLabel.rectTransform.sizeDelta = new Vector2(4f, 1f);
            _roleLabel.text = "";

            SyncPosition();
            UpdateSprite();
        }

        /// <summary>Shows a role tag over the unit (e.g. TARGET, PROTECT); null or empty hides it.</summary>
        public void SetRole(string text, Color color)
        {
            _roleLabel.text = text ?? "";
            _roleLabel.color = color;
        }

        /// <summary>Snaps to the unit's tile; call after the unit moves.</summary>
        public void SyncPosition() => transform.position = _gridView.SurfaceCenter(Unit.Coord);

        /// <summary>A cheap fingerprint of the status set, so the label is only rebuilt when it changes.</summary>
        private int StatusVersion()
        {
            int hash = 17;
            foreach (var status in Unit.Statuses.Active)
                hash = hash * 31 + (int)status + 1;
            return hash;
        }

        private void LateUpdate() => UpdateSprite();

        private void UpdateSprite()
        {
            // A script reload during Play mode drops the (unserialized) unit binding; stay inert instead of throwing.
            if (Unit == null || _cameraRig == null)
                return;
            float yaw = _cameraRig.transform.eulerAngles.y;
            // Knocked-out units lie on their side and fade to grey until revived or lost.
            bool down = Unit.IsKnockedOut;
            _sprite.transform.rotation = Quaternion.Euler(0f, yaw, down ? 90f : 0f);
            _sprite.color = down ? new Color(0.55f, 0.55f, 0.6f) : Color.white;

            var pose = SpriteViewMath.Resolve(Unit.Facing, yaw);
            var job = Unit.Job;
            var art = pose.ShowsFront ? job.FrontSprite : job.BackSprite;
            _sprite.sprite = art != null ? art : PlaceholderSprites.Unit(job.PlaceholderColor, pose.ShowsFront);
            _sprite.flipX = pose.FlipX;

            _statusLabel.transform.rotation = Quaternion.Euler(_cameraRig.transform.eulerAngles.x, yaw, 0f);
            _roleLabel.transform.rotation = _statusLabel.transform.rotation;
            int version = StatusVersion();
            if (version != _shownStatusVersion)
            {
                _shownStatusVersion = version;
                var codes = new System.Text.StringBuilder();
                foreach (var status in Unit.Statuses.Active)
                    codes.Append(codes.Length > 0 ? " " : "").Append(Status.StatusRules.Abbreviation(status));
                _statusLabel.text = codes.ToString();
            }
        }
    }
}
