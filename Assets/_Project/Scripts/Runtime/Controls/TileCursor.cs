using System;
using FriendshipWarriors.Cameras;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.View;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FriendshipWarriors.Controls
{
    /// <summary>
    /// The battlefield cursor. Moved tile by tile with the D-pad, left stick or WASD/arrows (with key-repeat), or
    /// placed by hovering the mouse. The camera follows directional moves so gamepad players never need to pan.
    /// Draws nothing itself; listeners highlight <see cref="Coord"/> as they see fit.
    /// </summary>
    public sealed class TileCursor : MonoBehaviour
    {
        [SerializeField] private BattleInput _input;
        [SerializeField] private GridView _gridView;
        [SerializeField] private IsoCameraRig _cameraRig;
        [SerializeField] private Camera _camera;

        [Header("Directional Repeat")]
        [SerializeField, Range(0.1f, 0.9f)] private float _deadZone = 0.5f;
        [SerializeField, Min(0.05f)] private float _repeatDelay = 0.3f;
        [SerializeField, Min(0.02f)] private float _repeatInterval = 0.1f;

        private GridCoord _heldStep;
        private float _nextRepeatTime;
        private Vector2 _lastPointer;

        public GridCoord Coord { get; private set; }

        /// <summary>
        /// While true the D-pad/stick/keys no longer move the cursor, so another mode (such as choosing a facing)
        /// can read them. The mouse still moves it.
        /// </summary>
        public bool DirectionalInputLocked { get; set; }

        public event Action<GridCoord> Moved;
        public event Action<GridCoord> Confirmed;
        public event Action Cancelled;

        private void OnEnable()
        {
            _input.Confirm.performed += OnConfirm;
            _input.Click.performed += OnClick;
            _input.Cancel.performed += OnCancel;
        }

        private void OnDisable()
        {
            _input.Confirm.performed -= OnConfirm;
            _input.Click.performed -= OnClick;
            _input.Cancel.performed -= OnCancel;
        }

        /// <summary>Places the cursor, optionally bringing the camera along.</summary>
        public void SetCoord(GridCoord coord, bool followCamera)
        {
            if (!_gridView.Grid.Contains(coord))
                return;
            if (followCamera)
                _cameraRig.Focus(_gridView.SurfaceCenter(coord));
            if (coord == Coord)
                return;
            Coord = coord;
            Moved?.Invoke(coord);
        }

        private void Update()
        {
            UpdateDirectional();
            UpdatePointer();
        }

        private void UpdateDirectional()
        {
            if (DirectionalInputLocked)
            {
                _heldStep = default;
                return;
            }

            var stick = _input.MoveCursor.ReadValue<Vector2>();
            var step = stick.magnitude >= _deadZone ? CursorMath.ToGridStep(stick, _cameraRig.ViewYaw) : default;

            if (step == default)
            {
                _heldStep = default;
                return;
            }

            float now = Time.unscaledTime;
            if (step != _heldStep)
            {
                _heldStep = step;
                _nextRepeatTime = now + _repeatDelay;
                Step(step);
            }
            else if (now >= _nextRepeatTime)
            {
                _nextRepeatTime = now + _repeatInterval;
                Step(step);
            }
        }

        private void UpdatePointer()
        {
            // Only react when the mouse actually moves, so a resting pointer doesn't fight the gamepad.
            var pointer = _input.Point.ReadValue<Vector2>();
            if ((pointer - _lastPointer).sqrMagnitude < 1f)
                return;
            _lastPointer = pointer;

            if (_gridView.TryPickTile(_camera.ScreenPointToRay(pointer), out var coord))
                SetCoord(coord, followCamera: false);
        }

        /// <summary>Moves one tile, hopping over holes in the map so the cursor never disappears.</summary>
        private void Step(GridCoord step)
        {
            var grid = _gridView.Grid;
            for (var next = Coord + step; grid.Contains(next); next += step)
            {
                if (grid[next].Terrain != TerrainType.Void)
                {
                    SetCoord(next, followCamera: true);
                    return;
                }
            }
        }

        private void OnConfirm(InputAction.CallbackContext _) => Confirmed?.Invoke(Coord);

        private void OnClick(InputAction.CallbackContext _)
        {
            if (_gridView.TryPickTile(_camera.ScreenPointToRay(_input.Point.ReadValue<Vector2>()), out var coord))
            {
                SetCoord(coord, followCamera: false);
                Confirmed?.Invoke(coord);
            }
        }

        private void OnCancel(InputAction.CallbackContext _) => Cancelled?.Invoke();
    }
}
