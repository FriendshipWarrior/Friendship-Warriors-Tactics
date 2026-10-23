using System.Collections.Generic;
using FriendshipWarriors.Battle;
using FriendshipWarriors.Battle.Abilities;
using FriendshipWarriors.Battle.Deployment;
using FriendshipWarriors.Cameras;
using FriendshipWarriors.Controls;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Generation;
using FriendshipWarriors.Grid.View;
using FriendshipWarriors.UI;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Equipment;
using FriendshipWarriors.Units.View;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace FriendshipWarriors.QuickPlay
{
    /// <summary>
    /// Runs a quick-play match: a freshly generated map and two random teams (Player = Human, Enemy = AI), all from
    /// one seed shown in the corner so a match can be reported and rebuilt. The player arranges their units in the
    /// formation phase (<see cref="DeploymentController"/>), then the battle begins. Start / P pauses (Resume, Quit to Main
    /// Menu). When the battle ends the result banner shows briefly — or until Confirm — then it's back to the main
    /// menu; another match is just Quick Play again.
    /// </summary>
    public sealed class QuickPlayDirector : MonoBehaviour
    {
        private const string ResumeId = "resume", QuitId = "quit";

        [Header("Scene")]
        [SerializeField] private BattleInput _battleInput;
        [SerializeField] private MenuInput _menuInput;
        [SerializeField] private GridView _gridView;
        [SerializeField] private IsoCameraRig _cameraRig;
        [SerializeField] private BattleController _battle;
        [SerializeField] private DeploymentController _deployment;
        [SerializeField] private BattleHud _hud;
        [SerializeField] private MenuScreen _pauseMenu;
        [SerializeField] private string _mainMenuScene = "MainMenu";

        [Header("Match")]
        [SerializeField] private MapGeneratorSettings _generatorSettings;
        [SerializeField] private JobCatalog _jobs;
        [SerializeField] private EquipmentCatalog _equipment;
        [SerializeField] private ItemStock[] _startingItems = System.Array.Empty<ItemStock>();
        [Tooltip("Units per side. A standard match is 5 v 5.")]
        [SerializeField, Range(1, 12)] private int _teamSize = Battle.Objectives.BattleSetup.StandardTeamSize;
        [Tooltip("Every unit's level, the same for both sides so matches are fair.")]
        [SerializeField, Range(1, 99)] private int _unitLevel = 5;
        [Tooltip("0 = a new random seed every match. Set one to rebuild a reported match.")]
        [SerializeField] private int _fixedSeed;

        [Header("Battle End")]
        [SerializeField, Min(0f)] private float _resultMinSeconds = 1f;
        [SerializeField, Min(0.5f)] private float _resultMaxSeconds = 6f;

        private readonly List<UnitView> _views = new();
        private readonly MenuModel _pauseModel = new("Paused");
        private BattleMapData _map;
        private TextMeshProUGUI _seedLabel;
        private bool _paused;
        private bool _ended;
        private float _endedAt;

        /// <summary>The seed this match was built from (map and both rosters).</summary>
        public int Seed { get; private set; }

        public bool IsPaused => _paused;
        public IReadOnlyList<UnitView> Units => _views;

        private void Awake()
        {
            // Bottom left, above the hint strip at its tallest (two lines); the objective owns the top-left corner.
            _seedLabel = UiFactory.Text(_pauseMenu.transform, "Seed", alignment: TextAlignmentOptions.BottomLeft);
            var rect = (RectTransform)_seedLabel.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(UiFactory.Margin + 2, UiFactory.Margin + 38);
            rect.sizeDelta = new Vector2(200f, 12f);
            _seedLabel.color = UiFactory.MutedTextColor;

            _pauseModel.SetItems(new[] { new MenuItem(ResumeId, "Resume"), new MenuItem(QuitId, "Quit to Main Menu") });
        }

        private void OnEnable()
        {
            _menuInput.Pause.performed += OnPause;
            _pauseMenu.ItemActivated += OnPauseItem;
            _pauseMenu.BackPressed += Resume;
            _battle.BattleEnded += OnBattleEnded;
        }

        private void OnDisable()
        {
            _menuInput.Pause.performed -= OnPause;
            _pauseMenu.ItemActivated -= OnPauseItem;
            _pauseMenu.BackPressed -= Resume;
            _battle.BattleEnded -= OnBattleEnded;
        }

        private void OnDestroy()
        {
            if (_map != null)
                Destroy(_map);
        }

        private void Start()
        {
            Seed = _fixedSeed != 0 ? _fixedSeed : System.Environment.TickCount;
            var rng = new System.Random(Seed);

            _map = ScriptableObject.CreateInstance<BattleMapData>();
            int mapSeed = rng.Next();
            var result = MapGenerator.Generate(_generatorSettings, mapSeed, _map);
            Debug.Log($"[QuickPlay] Seed {Seed}: map {_map.Width}x{_map.Depth} ({result.Attempts} attempt(s), " +
                      $"{(result.Succeeded ? "valid" : "INVALID")}).", this);
            _seedLabel.text = $"Seed {Seed}";
            _gridView.SetMap(_map);
            _gridView.Build();

            // One name pool for both sides, so no two units share a name.
            var names = new HashSet<string>();
            var players = RandomRoster.Build(Team.Player, _teamSize, _unitLevel, _jobs.Jobs, _equipment.Items, rng, taken: names);
            var enemies = RandomRoster.Build(Team.Enemy, _teamSize, _unitLevel, _jobs.Jobs, _equipment.Items, rng, taken: names);
            var unitRoot = new GameObject("Units").transform;
            Place(players, _map.PlayerDeployment, _map.EnemyDeployment, unitRoot);
            Place(enemies, _map.EnemyDeployment, _map.PlayerDeployment, unitRoot);

            var grid = _gridView.Grid;
            var center = GridMetrics.GridCenter(grid);
            _cameraRig.SetPanBounds(new Bounds(center, new Vector3(grid.Width, 0f, grid.Depth)));
            if (_map.PlayerDeployment.Count > 0)
                _cameraRig.Focus(_gridView.SurfaceCenter(_map.PlayerDeployment[0]), snap: true);

            foreach (var team in new[] { Team.Player, Team.Enemy })
            {
                var inventory = new Inventory();
                foreach (var stock in _startingItems)
                    inventory.Add(stock.Item, stock.Count);
                _battle.SetInventory(team, inventory);
            }
            // Quick play is a standard match: 5 v 5, defeat all enemies. The player arranges their side first.
            var objective = Battle.Objectives.BattleObjective.Standard;
            _deployment.Run(_views, HumanTeams(), objective, _ => _battle.Begin(_views, objective: objective));
        }

        /// <summary>Sides that get a formation phase: the human-controlled ones (AI sides keep their spawn positions).</summary>
        private IEnumerable<Team> HumanTeams()
        {
            foreach (var team in new[] { Team.Player, Team.Enemy })
            {
                if (_battle.ControlOf(team) == TeamControl.Human)
                    yield return team;
            }
        }

        /// <summary>Puts units on their deployment tiles, facing the other side as FFT battles start.</summary>
        private void Place(List<Unit> units, IReadOnlyList<GridCoord> zone, IReadOnlyList<GridCoord> facingZone, Transform root)
        {
            var target = facingZone.Count > 0 ? facingZone[0] : default;
            for (int i = 0; i < units.Count && i < zone.Count; i++)
            {
                units[i].Coord = zone[i];
                units[i].Facing = DirectionExtensions.Toward(zone[i], target);
                _views.Add(UnitView.Create(units[i], _gridView, _cameraRig, root));
            }
            if (units.Count > zone.Count)
                Debug.LogWarning($"[QuickPlay] Only {zone.Count} deployment tiles for {units.Count} units.", this);
        }

        private void Update()
        {
            if (!_ended)
                return;
            // The result shows for a moment, then Confirm (or a click) or the timeout goes back to the menu.
            float shown = Time.unscaledTime - _endedAt;
            bool confirmed = shown >= _resultMinSeconds &&
                             (_menuInput.Submit.WasPressedThisFrame() || _battleInput.Click.WasPressedThisFrame());
            if (confirmed || shown >= _resultMaxSeconds)
                ReturnToMainMenu();
        }

        // ------------------------------------------------------------------ Pause

        private void OnPause(InputAction.CallbackContext _)
        {
            if (_ended)
                return;
            if (_paused)
                Resume();
            else
                Pause();
        }

        public void Pause()
        {
            if (_paused || _ended)
                return;
            _paused = true;
            // Switching off the battle flow and its input freezes the battle, AI turns included.
            _battle.enabled = false;
            _deployment.enabled = false;
            _battleInput.enabled = false;
            _pauseMenu.Show(_pauseModel, "Paused", $"Seed {Seed}");
        }

        public void Resume()
        {
            if (!_paused)
                return;
            _paused = false;
            _pauseMenu.Hide();
            _battleInput.enabled = true;
            _deployment.enabled = true;
            _battle.enabled = true;
        }

        private void OnPauseItem(MenuItem item)
        {
            if (item.Id == ResumeId)
                Resume();
            else if (item.Id == QuitId)
                ReturnToMainMenu();
        }

        // ------------------------------------------------------------------ End

        private void OnBattleEnded(bool victory)
        {
            if (_ended)
                return;
            _ended = true;
            _endedAt = Time.unscaledTime;
            _hud.SetHint("A / Enter: return to the main menu");
            Debug.Log($"[QuickPlay] Seed {Seed}: {(victory ? "victory" : "defeat")}.", this);
        }

        private void ReturnToMainMenu() => SceneManager.LoadScene(_mainMenuScene);
    }
}
