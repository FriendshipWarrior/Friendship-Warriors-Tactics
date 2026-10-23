using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Generation;
using FriendshipWarriors.Grid.View;
using UnityEngine;

namespace FriendshipWarriors.Menus
{
    /// <summary>A freshly generated battlefield slowly turning behind the title screen.</summary>
    public sealed class MenuBackdrop : MonoBehaviour
    {
        [SerializeField] private GridView _gridView;
        [SerializeField] private Camera _camera;
        [SerializeField] private MapGeneratorSettings _generatorSettings;
        [SerializeField, Range(10f, 80f)] private float _pitch = 30f;
        [SerializeField, Min(0f)] private float _degreesPerSecond = 6f;
        [SerializeField, Min(1f)] private float _distance = 40f;

        private BattleMapData _map;
        private Vector3 _center;
        private float _yaw = 45f;

        private void Start()
        {
            if (_generatorSettings != null)
            {
                _map = ScriptableObject.CreateInstance<BattleMapData>();
                MapGenerator.Generate(_generatorSettings, System.Environment.TickCount, _map);
                _gridView.SetMap(_map);
                _gridView.Build();
            }
            var grid = _gridView.Grid;
            _center = _gridView.transform.TransformPoint(GridMetrics.GridCenter(grid));
            _camera.orthographic = true;
            _camera.orthographicSize = Mathf.Max(grid.Width, grid.Depth) * 0.55f;
        }

        private void OnDestroy()
        {
            if (_map != null)
                Destroy(_map);
        }

        private void LateUpdate()
        {
            _yaw += _degreesPerSecond * Time.unscaledDeltaTime;
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            _camera.transform.SetPositionAndRotation(_center - rotation * Vector3.forward * _distance, rotation);
        }
    }
}
