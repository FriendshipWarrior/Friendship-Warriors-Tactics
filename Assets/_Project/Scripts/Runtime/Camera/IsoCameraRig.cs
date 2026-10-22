using System;
using FriendshipWarriors.Controls;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FriendshipWarriors.Cameras
{
    /// <summary>
    /// Orthographic isometric camera in the style of FFT: always on a diagonal, rotating in 90° steps, with a low and
    /// a high tilt. The rig sits at the focus point; the camera is a child pulled back along the rig's forward axis.
    /// Gamepad: shoulders rotate, Y tilts, triggers zoom, right stick pans. Mouse/keyboard: Q/E, T, scroll or -/=,
    /// middle-drag.
    /// </summary>
    public sealed class IsoCameraRig : MonoBehaviour
    {
        [SerializeField] private BattleInput _input;
        [SerializeField] private Camera _camera;

        [Header("Angles")]
        [Tooltip("FFT's lower view. The exact FFT angles are approximated.")]
        [SerializeField, Range(10f, 80f)] private float _lowPitch = 30f;
        [SerializeField, Range(10f, 80f)] private float _highPitch = 45f;
        [Tooltip("Pull-back distance. Only needs to clear the map; orthographic size controls framing.")]
        [SerializeField, Min(1f)] private float _distance = 40f;

        [Header("Zoom (orthographic size)")]
        [SerializeField, Min(0.5f)] private float _minSize = 3f;
        [SerializeField, Min(0.5f)] private float _maxSize = 10f;
        [SerializeField, Min(0.01f)] private float _zoomStep = 0.75f;
        [Tooltip("Orthographic size per second while a zoom trigger or key is held.")]
        [SerializeField, Min(0.01f)] private float _zoomHoldSpeed = 6f;

        [Header("Motion")]
        [Tooltip("Right-stick pan speed in screen heights per second, so panning feels the same at any zoom.")]
        [SerializeField, Min(0f)] private float _stickPanSpeed = 1f;
        [SerializeField, Min(0.1f)] private float _smoothing = 12f;

        private float _yaw;
        private float _targetYaw;
        private float _pitch;
        private float _targetSize;
        private Vector3 _targetPosition;
        private Bounds? _panBounds;
        private bool _highTilt;

        /// <summary>Camera orientation as a count of clockwise quarter turns from the default view, 0–3.</summary>
        public int QuarterTurns { get; private set; }

        /// <summary>Settled yaw in degrees (45, 135, 225 or 315), ignoring any rotation still easing in.</summary>
        public float ViewYaw => 45f + 90f * QuarterTurns;

        /// <summary>Raised with the new <see cref="QuarterTurns"/> whenever the camera starts a rotation.</summary>
        public event Action<int> Rotated;

        private void Awake()
        {
            _camera.orthographic = true;
            _targetSize = Mathf.Clamp(_camera.orthographicSize, _minSize, _maxSize);
            _targetPosition = transform.position;
            _yaw = _targetYaw = ViewYaw;
            _pitch = _lowPitch;
            ApplyTransform();
        }

        private void OnEnable()
        {
            _input.RotateLeft.performed += OnRotateLeft;
            _input.RotateRight.performed += OnRotateRight;
            _input.ToggleTilt.performed += OnToggleTilt;
        }

        private void OnDisable()
        {
            _input.RotateLeft.performed -= OnRotateLeft;
            _input.RotateRight.performed -= OnRotateRight;
            _input.ToggleTilt.performed -= OnToggleTilt;
        }

        /// <summary>Moves the focus point, optionally jumping there instead of easing.</summary>
        public void Focus(Vector3 point, bool snap = false)
        {
            _targetPosition = ClampToBounds(point);
            if (snap)
                transform.position = _targetPosition;
        }

        /// <summary>Limits panning to a ground-plane area, typically the map.</summary>
        public void SetPanBounds(Bounds bounds)
        {
            _panBounds = bounds;
            _targetPosition = ClampToBounds(_targetPosition);
        }

        /// <param name="direction">+1 rotates the view clockwise, -1 counter-clockwise.</param>
        public void Rotate(int direction)
        {
            // Yaw is left unwrapped so easing always takes the short way around.
            _targetYaw += 90f * Math.Sign(direction);
            QuarterTurns = (QuarterTurns + Math.Sign(direction) + 4) % 4;
            Rotated?.Invoke(QuarterTurns);
        }

        public void ToggleTilt() => _highTilt = !_highTilt;

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            ReadPan(dt);
            ReadZoom(dt);

            float t = 1f - Mathf.Exp(-_smoothing * dt);
            _yaw = Mathf.Lerp(_yaw, _targetYaw, t);
            _pitch = Mathf.Lerp(_pitch, _highTilt ? _highPitch : _lowPitch, t);
            transform.position = Vector3.Lerp(transform.position, _targetPosition, t);
            _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, _targetSize, t);
            ApplyTransform();
        }

        private void ReadPan(float dt)
        {
            var planarRotation = Quaternion.Euler(0f, _yaw, 0f);
            float unitsPerPixel = _camera.orthographicSize * 2f / Mathf.Max(1, Screen.height);

            // Dragging moves the world with the pointer, so the camera goes the opposite way.
            var drag = _input.PanDrag.ReadValue<Vector2>();
            var stick = _input.PanStick.ReadValue<Vector2>();
            var move = -drag * unitsPerPixel + stick * (_stickPanSpeed * _camera.orthographicSize * 2f * dt);
            if (move != Vector2.zero)
                _targetPosition = ClampToBounds(_targetPosition + planarRotation * new Vector3(move.x, 0f, move.y));
        }

        private void ReadZoom(float dt)
        {
            // Positive input zooms in, which means a smaller orthographic size. Scroll deltas vary by platform and
            // device (wheel notches vs. trackpads), so clamp them to one step per frame.
            float scroll = Mathf.Clamp(_input.ZoomStep.ReadValue<float>(), -1f, 1f);
            float zoom = scroll * _zoomStep + _input.ZoomHold.ReadValue<float>() * _zoomHoldSpeed * dt;
            if (zoom != 0f)
                _targetSize = Mathf.Clamp(_targetSize - zoom, _minSize, _maxSize);
        }

        private void ApplyTransform()
        {
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            _camera.transform.SetLocalPositionAndRotation(new Vector3(0f, 0f, -_distance), Quaternion.identity);
        }

        private Vector3 ClampToBounds(Vector3 point)
        {
            if (_panBounds is not { } bounds)
                return point;
            return new Vector3(
                Mathf.Clamp(point.x, bounds.min.x, bounds.max.x),
                point.y,
                Mathf.Clamp(point.z, bounds.min.z, bounds.max.z));
        }

        private void OnRotateLeft(InputAction.CallbackContext _) => Rotate(-1);
        private void OnRotateRight(InputAction.CallbackContext _) => Rotate(1);
        private void OnToggleTilt(InputAction.CallbackContext _) => ToggleTilt();
    }
}
