using System.Collections.Generic;
using FriendshipWarriors.Battle;
using FriendshipWarriors.Cameras;
using FriendshipWarriors.Controls;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Generation;
using FriendshipWarriors.Grid.View;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;
using FriendshipWarriors.Units.View;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FriendshipWarriors.Dev
{
    /// <summary>
    /// Test-scene setup: spawns both teams on their deployment zones and hands them to the
    /// <see cref="BattleController"/>. Units whose job teaches a turn-trade ability start with it learned and
    /// equipped, so the trades can be tried. R / Select rolls a random map and restarts.
    /// </summary>
    public sealed class BattleSandbox : MonoBehaviour
    {
        [SerializeField] private BattleInput _input;
        [SerializeField] private GridView _gridView;
        [SerializeField] private IsoCameraRig _cameraRig;
        [SerializeField] private BattleController _battle;

        [Header("Roster")]
        [SerializeField] private JobDefinition[] _playerJobs = System.Array.Empty<JobDefinition>();
        [SerializeField] private JobDefinition[] _enemyJobs = System.Array.Empty<JobDefinition>();
        [SerializeField, Range(1, 99)] private int _unitLevel = 5;
        [SerializeField] private int _rosterSeed = 7;

        [Header("Random Maps")]
        [SerializeField] private MapGeneratorSettings _generatorSettings;

        private readonly List<UnitView> _views = new();
        private Transform _unitRoot;
        private BattleMapData _generatedMap;

        private void OnEnable() => _input.DebugRegenerate.performed += OnRegenerate;
        private void OnDisable() => _input.DebugRegenerate.performed -= OnRegenerate;

        private void OnDestroy()
        {
            if (_generatedMap != null)
                Destroy(_generatedMap);
        }

        private void Start()
        {
            _unitRoot = new GameObject("Units").transform;
            SetUpBattle();
        }

        private void SetUpBattle()
        {
            foreach (var view in _views)
                Destroy(view.gameObject);
            _views.Clear();

            var map = _gridView.Map;
            var rng = new System.Random(_rosterSeed);
            Spawn(_playerJobs, Team.Player, map.PlayerDeployment, map.EnemyDeployment, rng);
            Spawn(_enemyJobs, Team.Enemy, map.EnemyDeployment, map.PlayerDeployment, rng);

            var grid = _gridView.Grid;
            var center = GridMetrics.GridCenter(grid);
            _cameraRig.SetPanBounds(new Bounds(center, new Vector3(grid.Width, 0f, grid.Depth)));
            if (map.PlayerDeployment.Count > 0)
                _cameraRig.Focus(_gridView.SurfaceCenter(map.PlayerDeployment[0]), snap: true);

            _battle.Begin(_views);
        }

        private void Spawn(JobDefinition[] jobs, Team team, IReadOnlyList<GridCoord> zone, IReadOnlyList<GridCoord> facingZone, System.Random rng)
        {
            // Face the other side's zone, as units do at the start of an FFT battle.
            var target = facingZone.Count > 0 ? facingZone[0] : default;
            for (int i = 0; i < jobs.Length && i < zone.Count; i++)
            {
                if (jobs[i] == null)
                    continue;
                var unit = UnitFactory.CreateGeneric($"{jobs[i].DisplayName} {(char)('A' + i)}", team, jobs[i], _unitLevel, rng);
                unit.Coord = zone[i];
                unit.Facing = DirectionExtensions.Toward(zone[i], target);
                EquipTurnTrades(unit);
                _views.Add(UnitView.Create(unit, _gridView, _cameraRig, _unitRoot));
            }
        }

        /// <summary>Grants enough Prof to learn the job's turn-trade abilities, then learns and equips them.</summary>
        private static void EquipTurnTrades(Unit unit)
        {
            foreach (var ability in unit.Job.Abilities)
            {
                if (ability == null || ability.TurnTrade == TurnTrade.None)
                    continue;
                unit.GainProficiency(ability.LearnCost);
                if (unit.TryLearn(ability, unit.Job))
                    unit.TryEquip(ability);
            }
        }

        private void OnRegenerate(InputAction.CallbackContext _)
        {
            if (_generatorSettings == null)
            {
                Debug.LogWarning("[BattleSandbox] No Map Generator Settings assigned.", this);
                return;
            }

            int seed = System.Environment.TickCount;
            if (_generatedMap == null)
                _generatedMap = ScriptableObject.CreateInstance<BattleMapData>();
            var result = MapGenerator.Generate(_generatorSettings, seed, _generatedMap);
            Debug.Log($"[BattleSandbox] Random map seed {seed}: {_generatedMap.Width}x{_generatedMap.Depth}, " +
                      $"{result.Attempts} attempt(s), {(result.Succeeded ? "valid" : "INVALID")}.", this);

            _gridView.SetMap(_generatedMap);
            _gridView.Build();
            SetUpBattle();
        }
    }
}
