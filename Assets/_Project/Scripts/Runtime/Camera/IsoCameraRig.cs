using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FriendshipWarriors.Cameras
{
    /// <summary>
    /// Orthographic isometric camera. The rig sits at the focus point on the ground; the camera is a child pulled back
    /// along the rig's forward axis. Rotates in 90° steps (Q/E), zooms with the scroll wheel, pans with WASD/arrows.
    /// </summary>
    public sealed class IsoCameraRig : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField, Range(10f, 80f)] private float _pitch = 30f;
        [Tooltip("Pull-back distance. Only needs to clear the map; orthographic size controls framing.")]
        [SerializeField, Min(1f)] private float _distance = 40f;

        [Header("Zoom (orthographic size)")]
        [SerializeField, Min(0.5f)] private float _minSize = 3f;
        [SerializeField, Min(0.5f)] private float _maxSize = 12f;
        [SerializeField, Min(0.01f)] private float _zoomStep = 0.75f;

        [Header("Motion")]
        [Tooltip("Pan speed in screen heights per second, so panning feels the same at any zoom.")]
        [SerializeField, Min(0f)] private float _panSpeed = 1.2f;
        [SerializeField, Min(0.1f)] private float _smoothing = 12f;

        private InputAction _rotateLeft;
        private InputAction _rotateRight;
        private InputAction _zoom;
        private InputAction _pan;

        private float _yaw;
        private float _targetYaw;
        private float _targetSize;
        private Vector3 _targetPosition;
        private Bounds? _panBounds;

        /// <summary>Camera orientation as a count of clockwise quarter turns from the default view, 0–3.</summary>
        public int QuarterTurns { get; private set; }

        /// <summary>Raised with the new <see cref="QuarterTurns"/> whenever the camera starts a rotation.</summary>
        public event Action<int> Rotated;

        private void Awake()
        {
            _rotateLeft = new InputAction("RotateLeft", InputActionType.Button, "<Keyboard>/q");
            _rotateRight = new InputAction("RotateRight", InputActionType.Button, "<Keyboard>/e");
            _zoom = new InputAction("Zoom", InputActionType.Value, "<Mouse>/scroll/y");
            _pan = new InputAction("Pan", InputActionType.Value);
            _pan.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _pan.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");

            _rotateLeft.performed += _ => Rotate(-1);
            _rotateRight.performed += _ => Rotate(1);

            _camera.orthographic = true;
            _targetSize = Mathf.Clamp(_camera.orthographicSize, _minSize, _maxSize);
            _targetPosition = transform.position;
            _yaw = _targetYaw = 45f;
            ApplyTransform();
        }

        private void OnEnable()
        {
            _rotateLeft.Enable();
            _rotateRight.Enable();
            _zoom.Enable();
            _pan.Enable();
        }

        private void OnDisable()
        {
            _rotateLeft.Disable();
            _rotateRight.Disable();
            _zoom.Disable();
            _pan.Disable();
        }

        private void OnDestroy()
        {
            _rotateLeft.Dispose();
            _rotateRight.Dispose();
            _zoom.Dispose();
            _pan.Dispose();
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

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;

            var pan = _pan.ReadValue<Vector2>();
            if (pan != Vector2.zero)
            {
                var planar = Quaternion.Euler(0f, _yaw, 0f) * new Vector3(pan.x, 0f, pan.y);
                float unitsPerSecond = _panSpeed * _camera.orthographicSize * 2f;
                _targetPosition = ClampToBounds(_targetPosition + planar * (unitsPerSecond * dt));
            }

            float scroll = _zoom.ReadValue<float>();
            if (scroll != 0f)
                _targetSize = Mathf.Clamp(_targetSize - scroll * _zoomStep, _minSize, _maxSize);

            float t = 1f - Mathf.Exp(-_smoothing * dt);
            _yaw = Mathf.Lerp(_yaw, _targetYaw, t);
            transform.position = Vector3.Lerp(transform.position, _targetPosition, t);
            _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, _targetSize, t);
            ApplyTransform();
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
    }
}
