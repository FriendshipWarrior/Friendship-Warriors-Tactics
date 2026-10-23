using FriendshipWarriors.Cameras;
using FriendshipWarriors.Grid.View;
using FriendshipWarriors.Units.View.PaperDoll;
using UnityEngine;

namespace FriendshipWarriors.Units.View
{
    /// <summary>
    /// Shows a <see cref="Units.Unit"/>: a flat sprite that turns to face the camera (rotating only around the
    /// vertical axis so it stays upright), switches between front/back poses and mirrors them based on the unit's
    /// facing and the camera angle, plus a team-colored marker on the ground. Knocked-out units lie down, greyed.
    /// Art comes from, in order: the job's hand-drawn front/back sprites, the paper-doll composite of the unit's job
    /// and gear (animated: idle, attack, cast while charging, hurt, knocked out), or the generated placeholder.
    /// </summary>
    public sealed class UnitView : MonoBehaviour
    {
        /// <summary>
        /// World scale of unit art. Sprites are authored at 32 px per unit and shown 25% larger (user decision), so the
        /// pixel grid and frame layout stay the same.
        /// </summary>
        public const float SpriteScale = 1.25f;

        /// <summary>Height of the status codes above the unit's feet, scaled with the art so they clear the head.</summary>
        public const float StatusLabelHeight = 1.45f * SpriteScale;

        /// <summary>Height of the TARGET / PROTECT tag, above the status tags.</summary>
        public const float RoleLabelHeight = 1.75f * SpriteScale;

        private static readonly Color PlayerColor = new(0.35f, 0.65f, 1f);
        private static readonly Color EnemyColor = new(1f, 0.35f, 0.35f);

        private SpriteRenderer _sprite;
        private SpriteRenderer _marker;
        private TMPro.TextMeshPro _statusLabel;
        private TMPro.TextMeshPro _roleLabel;
        private int _shownStatusVersion = -1;
        private GridView _gridView;
        private IsoCameraRig _cameraRig;
        private readonly DollAnimator _animator = new();
        private DollSheet _doll;
        private int _dollLoadout;
        private int _lastHp = -1;
        private bool _dollResolved;
        private static readonly Equipment.EquipmentSlot[] Slots =
            (Equipment.EquipmentSlot[])System.Enum.GetValues(typeof(Equipment.EquipmentSlot));

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
            _sprite.transform.localScale = new Vector3(SpriteScale, SpriteScale, 1f);

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
            _statusLabel.transform.localPosition = new Vector3(0f, StatusLabelHeight, 0f);
            _statusLabel.fontSize = 2.2f;
            _statusLabel.alignment = TMPro.TextAlignmentOptions.Center;
            _statusLabel.color = new Color(1f, 0.85f, 0.35f);
            _statusLabel.outlineWidth = 0.25f;
            _statusLabel.outlineColor = Color.black;
            _statusLabel.rectTransform.sizeDelta = new Vector2(4f, 1f);

            // Objective role (TARGET / PROTECT), above the status codes.
            _roleLabel = new GameObject("Role").AddComponent<TMPro.TextMeshPro>();
            _roleLabel.transform.SetParent(transform, false);
            _roleLabel.transform.localPosition = new Vector3(0f, RoleLabelHeight, 0f);
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

        /// <summary>Plays the attack animation once (weapon swing or shot).</summary>
        public void PlayAttack() => _animator.Play(DollAnimation.Attack, Time.time);

        /// <summary>Plays the cast gesture once (charged spells then keep the cast loop while Charging).</summary>
        public void PlayCast() => _animator.Play(DollAnimation.Cast, Time.time);

        /// <summary>Snaps to the unit's tile; call after the unit moves.</summary>
        public void SyncPosition() => transform.position = _gridView.SurfaceCenter(Unit.Coord);

        /// <summary>The unit's paper-doll sheet, rebuilt only when its job or gear changes.</summary>
        private DollSheet CurrentDoll()
        {
            int loadout = Unit.Job.GetHashCode();
            foreach (var slot in Slots)
            {
                var item = Unit.Equipped(slot);
                loadout = loadout * 31 + (item != null ? item.GetHashCode() : 0);
            }
            bool stale = _doll != null && !_doll.IsAlive;
            if (!_dollResolved || stale || loadout != _dollLoadout)
            {
                _doll = PaperDollComposer.SheetFor(Unit);
                _dollLoadout = loadout;
                _dollResolved = true;
            }
            return _doll;
        }

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
            bool down = Unit.IsKnockedOut;
            var pose = SpriteViewMath.Resolve(Unit.Facing, yaw);
            var job = Unit.Job;
            var art = pose.ShowsFront ? job.FrontSprite : job.BackSprite;
            var doll = art == null ? CurrentDoll() : null;

            if (Unit.CurrentHp < _lastHp && !down)
                _animator.Play(DollAnimation.Hurt, Time.time);
            _lastHp = Unit.CurrentHp;

            // Knocked-out units lie down and fade to grey until revived or lost. Paper dolls have a lying frame; other
            // art is turned on its side.
            _sprite.transform.rotation = Quaternion.Euler(0f, yaw, down && doll == null ? 90f : 0f);
            _sprite.color = down ? new Color(0.55f, 0.55f, 0.6f) : Color.white;

            if (art != null)
            {
                _sprite.sprite = art;
            }
            else if (doll != null)
            {
                var (animation, frame) = _animator.Current(Time.time, down, Unit.HasStatus(Status.StatusEffect.Charging));
                _sprite.sprite = doll.Get(animation, frame, back: !pose.ShowsFront);
            }
            else
            {
                _sprite.sprite = PlaceholderSprites.Unit(job.PlaceholderColor, pose.ShowsFront);
            }
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
