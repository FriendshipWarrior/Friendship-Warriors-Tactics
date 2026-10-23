using System.Collections.Generic;
using FriendshipWarriors.Battle;
using FriendshipWarriors.Battle.Abilities;
using FriendshipWarriors.Battle.Deployment;
using FriendshipWarriors.Battle.Objectives;
using FriendshipWarriors.Cameras;
using FriendshipWarriors.Controls;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Generation;
using FriendshipWarriors.Grid.View;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;
using FriendshipWarriors.Units.Equipment;
using FriendshipWarriors.Units.View;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FriendshipWarriors.Dev
{
    /// <summary>
    /// Test-scene setup: spawns both teams from a <see cref="BattleSetup"/> (team sizes, rosters with keys, objective)
    /// onto the map's deployment zones and hands them to the <see cref="BattleController"/>. Each unit gets the first starter item for each slot its job can use, learns every
    /// ability its job teaches (passives get equipped), and Player units without items of their own take the item
    /// job's command as their secondary with its first item learned. Both teams get the same item stock. Human sides
    /// arrange their units in the formation phase before turn 1. R / Select rolls a random map and restarts.
    /// </summary>
    public sealed class BattleSandbox : MonoBehaviour
    {
        [SerializeField] private BattleInput _input;
        [SerializeField] private GridView _gridView;
        [SerializeField] private IsoCameraRig _cameraRig;
        [SerializeField] private BattleController _battle;
        [SerializeField] private DeploymentController _deployment;

        [Header("Battle")]
        [Tooltip("Team sizes, rosters and objective. Swap between the Standard, Commander and Escort setups to try each objective.")]
        [SerializeField] private BattleSetup _setup;
        [SerializeField] private int _rosterSeed = 7;
        [SerializeField] private EquipmentDefinition[] _starterGear = System.Array.Empty<EquipmentDefinition>();
        [Tooltip("The job whose command uses items (Corpsman), offered to Player units as a secondary.")]
        [SerializeField] private JobDefinition _itemJob;
        [SerializeField] private ItemStock[] _startingItems = System.Array.Empty<ItemStock>();

        [Header("Statuses (testing)")]
        [Tooltip("Statuses some units start with, to try statuses, cures and reactions. Panic comes from Morale instead.")]
        [SerializeField] private StatusSeed[] _startingStatuses = System.Array.Empty<StatusSeed>();
        [Tooltip("An enemy (by roster index) that starts at this Morale; below 10 it panics. -1 for none.")]
        [SerializeField] private int _lowMoraleEnemy = -1;
        [SerializeField, Range(0, 100)] private int _lowMorale = 8;

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
            // A reroll can come mid-battle or mid-formation: stop both before the old units go.
            _deployment.Cancel();
            _battle.Halt();
            foreach (var view in _views)
                Destroy(view.gameObject);
            _views.Clear();

            if (_setup == null)
            {
                Debug.LogError("[BattleSandbox] No Battle Setup assigned.", this);
                return;
            }
            foreach (var issue in BattleSetupValidator.Validate(_setup))
                Debug.LogWarning($"[BattleSandbox] {_setup.name}: {issue}", this);

            var map = _gridView.Map;
            var rng = new System.Random(_rosterSeed);
            Spawn(_setup.PlayerRoster, _setup.PlayerTeamSize, Team.Player, map.PlayerDeployment, map.EnemyDeployment, rng);
            Spawn(_setup.EnemyRoster, _setup.EnemyTeamSize, Team.Enemy, map.EnemyDeployment, map.PlayerDeployment, rng);

            var grid = _gridView.Grid;
            var center = GridMetrics.GridCenter(grid);
            _cameraRig.SetPanBounds(new Bounds(center, new Vector3(grid.Width, 0f, grid.Depth)));
            if (map.PlayerDeployment.Count > 0)
                _cameraRig.Focus(_gridView.SurfaceCenter(map.PlayerDeployment[0]), snap: true);

            foreach (var team in new[] { Team.Player, Team.Enemy })
            {
                var inventory = new Inventory();
                foreach (var stock in _startingItems)
                    inventory.Add(stock.Item, stock.Count);
                _battle.SetInventory(team, inventory);
            }
            // Human sides arrange their units first (the formation phase), then the battle begins.
            var objective = _setup.Objective;
            var teams = new List<Team>();
            foreach (var team in new[] { Team.Player, Team.Enemy })
            {
                if (_battle.ControlOf(team) == TeamControl.Human)
                    teams.Add(team);
            }
            _deployment.Run(_views, teams, objective, _ => _battle.Begin(_views, PrepareStatuses, objective));
        }

        /// <summary>Applies the test statuses and the low-Morale enemy once the battle has cleared old modifiers.</summary>
        private void PrepareStatuses(IReadOnlyList<Unit> units)
        {
            Unit At(Team team, int index)
            {
                int seen = 0;
                foreach (var unit in units)
                {
                    if (unit.Team != team)
                        continue;
                    if (seen++ == index)
                        return unit;
                }
                return null;
            }

            foreach (var seed in _startingStatuses)
                At(seed.Team, seed.RosterIndex)?.AddStatus(seed.Status);
            if (_lowMoraleEnemy >= 0 && At(Team.Enemy, _lowMoraleEnemy) is { } nervous)
                nervous.AdjustMorale(_lowMorale - nervous.Morale);
        }

        private void Spawn(IReadOnlyList<RosterEntry> roster, int teamSize, Team team, IReadOnlyList<GridCoord> zone,
            IReadOnlyList<GridCoord> facingZone, System.Random rng)
        {
            int fielded = BattleSetup.Fielded(teamSize, roster.Count, zone.Count);
            if (fielded < teamSize)
                Debug.LogWarning($"[BattleSandbox] {team} fields {fielded} of {teamSize} (roster {roster.Count}, deployment tiles {zone.Count}).", this);

            // Face the other side's zone, as units do at the start of an FFT battle.
            var target = facingZone.Count > 0 ? facingZone[0] : default;
            for (int i = 0; i < fielded; i++)
            {
                var entry = roster[i];
                if (entry == null || entry.Job == null)
                    continue;
                string unitName = string.IsNullOrEmpty(entry.DisplayName) ? $"{entry.Job.DisplayName} {(char)('A' + i)}" : entry.DisplayName;
                int level = entry.Level > 0 ? entry.Level : _setup.DefaultLevel;
                var unit = UnitFactory.CreateGeneric(unitName, team, entry.Job, level, rng);
                unit.Key = string.IsNullOrEmpty(entry.Key) ? null : entry.Key;
                unit.Coord = zone[i];
                unit.Facing = DirectionExtensions.Toward(zone[i], target);
                LearnJobAbilities(unit);
                if (team == Team.Player)
                    GiveItemSecondary(unit);
                EquipStarterGear(unit);
                unit.RestoreFully();
                _views.Add(UnitView.Create(unit, _gridView, _cameraRig, _unitRoot));
            }
        }

        /// <summary>Fills each empty slot with the first starter item the unit's job can use.</summary>
        private void EquipStarterGear(Unit unit)
        {
            foreach (var item in _starterGear)
            {
                if (item == null)
                    continue;
                var slot = item.Category switch
                {
                    EquipmentCategory.Weapon => EquipmentSlot.MainHand,
                    EquipmentCategory.Shield => EquipmentSlot.OffHand,
                    EquipmentCategory.Head => EquipmentSlot.Head,
                    EquipmentCategory.Body => EquipmentSlot.Body,
                    _ => EquipmentSlot.Accessory,
                };
                if (unit.Equipped(slot) == null)
                    unit.TryEquipItem(item);
            }
        }

        /// <summary>Grants the Prof to learn everything the job teaches, learns it, and equips passives into empty slots.</summary>
        private static void LearnJobAbilities(Unit unit)
        {
            foreach (var ability in unit.Job.Abilities)
            {
                if (ability == null)
                    continue;
                unit.GainProficiency(ability.LearnCost);
                if (!unit.TryLearn(ability, unit.Job) || ability.Kind == AbilityKind.Action)
                    continue;
                bool slotFree = ability.Kind switch
                {
                    AbilityKind.Support => unit.Support == null,
                    AbilityKind.Movement => unit.Movement == null,
                    _ => unit.Reaction == null,
                };
                if (slotFree)
                    unit.TryEquip(ability);
            }
        }

        /// <summary>Teaches the item job's first item and sets that job as the secondary, so Act lists it too.</summary>
        private void GiveItemSecondary(Unit unit)
        {
            if (_itemJob == null || unit.Job == _itemJob || unit.SecondaryJob != null)
                return;
            foreach (var ability in _itemJob.Abilities)
            {
                if (ability == null || !ability.IsItem)
                    continue;
                unit.GainProficiency(ability.LearnCost, _itemJob);
                unit.TryLearn(ability, _itemJob);
                unit.TrySetSecondary(_itemJob);
                return;
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
            // Zones big enough for the setup's larger side.
            int zone = _setup != null ? System.Math.Max(_setup.PlayerTeamSize, _setup.EnemyTeamSize) : BattleSetup.StandardTeamSize;
            var result = MapGenerator.Generate(_generatorSettings, seed, _generatedMap, zone);
            Debug.Log($"[BattleSandbox] Random map seed {seed}: {_generatedMap.Width}x{_generatedMap.Depth}, " +
                      $"{result.Attempts} attempt(s), {(result.Succeeded ? "valid" : "INVALID")}.", this);

            _gridView.SetMap(_generatedMap);
            _gridView.Build();
            SetUpBattle();
        }
    }
}
