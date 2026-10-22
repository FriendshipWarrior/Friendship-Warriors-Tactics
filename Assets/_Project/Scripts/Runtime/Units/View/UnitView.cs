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

            SyncPosition();
            UpdateSprite();
        }

        /// <summary>Snaps to the unit's tile; call after the unit moves.</summary>
        public void SyncPosition() => transform.position = _gridView.SurfaceCenter(Unit.Coord);

        private void LateUpdate() => UpdateSprite();

        private void UpdateSprite()
        {
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
        }
    }
}
