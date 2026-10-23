using FriendshipWarriors.Cameras;
using FriendshipWarriors.Grid.View;
using FriendshipWarriors.Units.View.PaperDoll;
using FriendshipWarriors.Units.View.Sheets;
using FriendshipWarriors.Units.View.Walking;
using UnityEngine;

namespace FriendshipWarriors.Units.View
{
    /// <summary>
    /// Shows a <see cref="Units.Unit"/>: a flat sprite that turns to face the camera (rotating only around the
    /// vertical axis so it stays upright), switches between front/back poses and mirrors them based on the unit's
    /// facing and the camera angle, plus a team-colored marker on the ground. Knocked-out units lie down, greyed.
    /// Art comes from, in order: the job's hand-drawn front/back sprites, the job's animated whole-body
    /// <see cref="UnitSheet"/> (with a weapon swing over its attack), the paper-doll composite of the unit's job and gear
    /// (animated: idle, walk, attack, cast while charging, hurt, knocked out), or the generated placeholder.
    /// Moves play out along a <see cref="WalkPath"/> advanced by the battle (battle time, so pausing freezes them);
    /// the unit's tile has already changed by then, only the picture is catching up.
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
        private SpriteRenderer _shadow;
        private Color _shadowColor = Color.white;
        private int _shownStatusVersion = -1;
        private GridView _gridView;
        private IsoCameraRig _cameraRig;
        private readonly DollAnimator _animator = new();
        private readonly SheetAnimator _sheetAnimator = new();
        private SpriteRenderer _weapon;
        private ComposedSheet _sheet;
        private JobDefinition _sheetJob;
        private DollSheet _doll;
        private int _dollLoadout;
        private int _lastHp = -1;
        private bool _dollResolved;
        private static readonly Equipment.EquipmentSlot[] Slots =
            (Equipment.EquipmentSlot[])System.Enum.GetValues(typeof(Equipment.EquipmentSlot));

        private WalkPlayback _walk;

        public Unit Unit { get; private set; }

        /// <summary>The tile the unit is drawn on (or walking from); catches up with <see cref="Units.Unit.Coord"/> when a walk ends.</summary>
        public Grid.GridCoord ShownCoord { get; private set; }

        public bool IsWalking => _walk != null;

        /// <summary>The unit's art, for effects drawn over it (hit flashes). Its colour and frame are set every frame.</summary>
        public SpriteRenderer Sprite => _sprite;

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

            // Weapon swing over a unit sheet's attack frames; a child of the sprite so it turns and scales with it.
            _weapon = new GameObject("Weapon").AddComponent<SpriteRenderer>();
            _weapon.transform.SetParent(_sprite.transform, false);
            _weapon.enabled = false;

            _marker = new GameObject("Team Marker").AddComponent<SpriteRenderer>();
            _marker.transform.SetParent(transform, false);
            _marker.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            _marker.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _marker.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
            _marker.sprite = PlaceholderSprites.GroundMarker();
            _marker.color = unit.Team == Team.Player ? PlayerColor : EnemyColor;
            _marker.sortingOrder = 1;

            // A soft blob on the ground. It and the team marker stay on the surface below the unit while it hops or
            // leaps (see LateUpdate), so the arc reads against the ground, as in FFT.
            _shadow = new GameObject("Shadow").AddComponent<SpriteRenderer>();
            _shadow.transform.SetParent(transform, false);
            _shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _shadow.sprite = PlaceholderSprites.Shadow();

            SyncPosition();
            UpdateSprite();
            UpdateGroundMarks();
        }

        /// <summary>Shows a role tag over the unit (e.g. TARGET, PROTECT); null or empty hides it.</summary>
        public void SetRole(string text, Color color)
        {
            RoleTag = text ?? "";
            RoleColor = color;
        }

        /// <summary>The objective role shown over the unit (TARGET, PROTECT), or empty.</summary>
        public string RoleTag { get; private set; } = "";
        public Color RoleColor { get; private set; } = Color.white;

        /// <summary>Short codes of the unit's statuses (PSN HST ...), or empty. Drawn by the HUD's tag overlay in
        /// screen space (<see cref="FriendshipWarriors.UI.UnitTagOverlay"/>), so it stays crisp in pixel mode.</summary>
        public string StatusTag { get; private set; } = "";

        /// <summary>Plays the attack animation once (weapon swing or shot).</summary>
        public void PlayAttack()
        {
            _animator.Play(DollAnimation.Attack, Time.time);
            _sheetAnimator.Play(SheetAnimation.Attack, Time.time);
        }

        /// <summary>Plays the cast gesture once (charged spells then keep the cast loop while Charging).</summary>
        public void PlayCast()
        {
            _animator.Play(DollAnimation.Cast, Time.time);
            _sheetAnimator.Play(SheetAnimation.Release, Time.time);
        }

        /// <summary>Snaps to the unit's tile, ending any walk.</summary>
        public void SyncPosition()
        {
            _walk = null;
            ShownCoord = Unit.Coord;
            transform.position = _gridView.SurfaceCenter(Unit.Coord);
        }

        /// <summary>Starts showing the unit walking along <paramref name="path"/> (built in the grid's local space).</summary>
        public void Walk(WalkPath path)
        {
            if (path == null || path.Segments.Count == 0)
            {
                SyncPosition();
                return;
            }
            _walk = new WalkPlayback(path);
            PlaceOnWalk();
        }

        /// <summary>Plays <paramref name="seconds"/> of battle time of the current walk; at the end the unit stands on its tile.</summary>
        public void AdvanceWalk(float seconds, float rate = 1f)
        {
            if (_walk == null)
                return;
            _walk.Advance(seconds, rate);
            if (_walk.IsFinished)
                SyncPosition();
            else
                PlaceOnWalk();
        }

        private void PlaceOnWalk() => transform.position = _gridView.transform.TransformPoint(_walk.Position);

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

        /// <summary>The job's composed whole-body sheet, or null to use the paper doll; rebuilt when the job changes.</summary>
        private ComposedSheet CurrentSheet()
        {
            var job = Unit.Job;
            if (job.Sheet == null)
                return null;
            if (_sheet == null || !_sheet.IsAlive || _sheetJob != job)
            {
                _sheet = SheetComposer.SheetFor(Unit);
                _sheetJob = job;
            }
            return _sheet;
        }

        /// <summary>A cheap fingerprint of the status set, so the label is only rebuilt when it changes.</summary>
        private int StatusVersion()
        {
            int hash = 17;
            foreach (var status in Unit.Statuses.Active)
                hash = hash * 31 + (int)status + 1;
            return hash;
        }

        private void LateUpdate()
        {
            UpdateSprite();
            UpdateGroundMarks();
        }

        /// <summary>
        /// Puts the shadow and team marker on the surface straight below the unit, shrinking and fading the shadow with
        /// height. Over a void chasm both hide until the unit is over ground again.
        /// </summary>
        private void UpdateGroundMarks()
        {
            if (Unit == null || _gridView == null || _gridView.Grid == null)
                return;
            var feet = transform.position;
            float ground = feet.y;
            bool visible = true;
            if (_walk != null)
            {
                var local = _gridView.transform.InverseTransformPoint(feet);
                visible = GroundShadow.TrySurfaceHeight(_gridView.Grid, local, out float surface);
                if (visible)
                    ground = _gridView.transform.TransformPoint(new Vector3(local.x, surface, local.z)).y;
            }
            // Standing still, the unit's own height is the ground (an odd slope's standing height is rounded down).
            float above = Mathf.Max(0f, feet.y - ground);

            _shadow.enabled = visible;
            _marker.enabled = visible;
            if (!visible)
                return;
            _shadow.transform.position = new Vector3(feet.x, ground + 0.012f, feet.z);
            _marker.transform.position = new Vector3(feet.x, ground + 0.02f, feet.z);
            float size = 0.8f * GroundShadow.Scale(above);
            _shadow.transform.localScale = new Vector3(size, size, 1f);
            _shadowColor.a = GroundShadow.Opacity(above);
            _shadow.color = _shadowColor;
        }

        private void UpdateSprite()
        {
            // A script reload during Play mode drops the (unserialized) unit binding; stay inert instead of throwing.
            if (Unit == null || _cameraRig == null)
                return;
            float yaw = _cameraRig.transform.eulerAngles.y;
            bool down = Unit.IsKnockedOut;
            // Walking turns the unit with each step; pushes keep the way it faced.
            var facing = _walk != null && _walk.Current.TurnsUnit ? _walk.Current.Facing : Unit.Facing;
            var pose = SpriteViewMath.Resolve(facing, yaw);
            var job = Unit.Job;
            var art = pose.ShowsFront ? job.FrontSprite : job.BackSprite;
            var sheet = art == null ? CurrentSheet() : null;
            var doll = art == null && sheet == null ? CurrentDoll() : null;

            if (Unit.CurrentHp < _lastHp && !down)
            {
                _animator.Play(DollAnimation.Hurt, Time.time);
                _sheetAnimator.Play(SheetAnimation.Hurt, Time.time);
            }
            _lastHp = Unit.CurrentHp;

            // Knocked-out units lie down and fade to grey until revived or lost. Sheets and paper dolls have a lying
            // frame; other art is turned on its side.
            _sprite.transform.rotation = Quaternion.Euler(0f, yaw, down && doll == null && sheet == null ? 90f : 0f);
            _sprite.color = down ? new Color(0.55f, 0.55f, 0.6f) : Color.white;

            Sprite weapon = null;
            if (art != null)
            {
                _sprite.sprite = art;
            }
            else if (sheet != null)
            {
                bool charging = Unit.HasStatus(Status.StatusEffect.Charging);
                var (animation, frame) = _sheetAnimator.Current(Time.time, sheet, down, charging, CriticalHp.IsCritical(Unit),
                    _walk != null && !down ? _walk.WalkFrame : -1);
                _sprite.sprite = sheet.Get(animation, frame, back: !pose.ShowsFront);
                if (animation == SheetAnimation.Attack && Unit.Weapon != null)
                    weapon = sheet.Weapon(Unit.Weapon.WeaponType, frame, back: !pose.ShowsFront);
            }
            else if (doll != null)
            {
                var (animation, frame) = _animator.Current(Time.time, down, Unit.HasStatus(Status.StatusEffect.Charging),
                    _walk != null && !down ? _walk.WalkFrame : -1);
                _sprite.sprite = doll.Get(animation, frame, back: !pose.ShowsFront);
            }
            else
            {
                _sprite.sprite = PlaceholderSprites.Unit(job.PlaceholderColor, pose.ShowsFront);
            }
            _sprite.flipX = pose.FlipX;

            // The swing sits just in front of the body from the front and just behind it from the back, left to the
            // usual distance sorting.
            _weapon.enabled = weapon != null;
            if (weapon != null)
            {
                _weapon.sprite = weapon;
                _weapon.flipX = pose.FlipX;
                _weapon.color = _sprite.color;
                _weapon.transform.localPosition = new Vector3(0f, 0f, pose.ShowsFront ? -0.02f : 0.02f);
            }

            int version = StatusVersion();
            if (version != _shownStatusVersion)
            {
                _shownStatusVersion = version;
                var codes = new System.Text.StringBuilder();
                foreach (var status in Unit.Statuses.Active)
                    codes.Append(codes.Length > 0 ? " " : "").Append(Status.StatusRules.Abbreviation(status));
                StatusTag = codes.ToString();
            }
        }
    }
}
