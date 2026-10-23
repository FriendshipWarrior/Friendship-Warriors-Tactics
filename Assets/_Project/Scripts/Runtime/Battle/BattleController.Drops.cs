using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Battle.Drops;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.View;
using FriendshipWarriors.UI;
using FriendshipWarriors.Units;
using UnityEngine;

namespace FriendshipWarriors.Battle
{
    /// <summary>
    /// FFT's crystals and treasure chests (see <see cref="DropRules"/>): a lost unit leaves a steel rod or a chest on
    /// its tile, and whoever ends a move there takes it. A human picking up a rod that can teach them something
    /// chooses from a small menu (learn or restore); the AI decides for itself. Gear from chests is kept as spoils
    /// and listed when the battle ends.
    /// </summary>
    public sealed partial class BattleController
    {
        private const string InheritId = "pickup.inherit", RestoreId = "pickup.restore";

        [Header("Drops")]
        [Tooltip("Rod and chest rules; falls back to Resources/Battle/DropTable, then to the defaults.")]
        [SerializeField] private DropTable _dropTable;

        private readonly DropField _drops = new();
        private readonly Spoils _spoils = new();
        private readonly MenuModel _pickupMenu = new("Steel Rod");
        private BattleDrop _pendingDrop;
        private System.Action _afterPickup;

        /// <summary>A lost unit left a rod or chest.</summary>
        public event System.Action<BattleDrop> DropPlaced;

        /// <summary>A drop was taken (raised after its effects are applied).</summary>
        public event System.Action<PickupResult> DropTaken;

        /// <summary>A pickup choice, as a recordable input (online play would send these).</summary>
        public event System.Action<PickupCommand> PickupChosen;

        public IReadOnlyList<BattleDrop> Drops => _drops.All;
        public Spoils Spoils => _spoils;
        public GridView GridView => _gridView;

        private DropTable Table
        {
            get
            {
                if (_dropTable == null)
                    _dropTable = Resources.Load<DropTable>(DropTable.ResourcePath);
                return _dropTable;
            }
        }

        private DropSettings DropSettings => Table != null ? Table.Settings : FriendshipWarriors.Battle.Drops.DropSettings.Default;

        private void ResetDrops()
        {
            _drops.Clear();
            _spoils.Clear();
            _pendingDrop = null;
            _afterPickup = null;
        }

        /// <summary>The lost unit's rod or chest appears where it fell.</summary>
        private void LeaveDrop(Unit unit)
        {
            var drop = DropRules.Create(_drops.NextId(), unit, _rng, DropSettings, Table != null ? Table.MonsterLoot : null);
            _drops.Add(drop);
            Debug.Log($"[Battle] {unit.Name} leaves {drop}.", this);
            DropPlaced?.Invoke(drop);
        }

        /// <summary>What each drop on the field is worth to this AI unit, for the planner.</summary>
        private Dictionary<GridCoord, double> DropValuesFor(Unit unit)
        {
            var values = new Dictionary<GridCoord, double>();
            var settings = DropSettings;
            foreach (var drop in _drops.All)
            {
                values.TryGetValue(drop.Coord, out double value);
                values[drop.Coord] = value + DropRules.AiValue(drop, unit, settings);
            }
            return values;
        }

        /// <summary>
        /// After a unit's move: it takes whatever lies on its tile, one drop at a time, then <paramref name="then"/>
        /// runs. A human at a rod with something to teach gets the choice menu; everything else resolves at once.
        /// </summary>
        private void TakeDropsAfterMove(System.Action then)
        {
            var unit = _active != null ? _active.Unit : null;
            var drop = unit != null && !unit.IsKnockedOut ? _drops.At(unit.Coord) : null;
            if (drop == null)
            {
                then();
                return;
            }
            var settings = DropSettings;
            if (!IsAiDriven(unit) && DropRules.NeedsChoice(drop, unit, settings))
            {
                OpenPickupMenu(drop, then);
                return;
            }
            var choice = IsAiDriven(unit) ? DropRules.AiChoice(drop, unit, settings) : PickupChoice.Restore;
            ExecutePickup(unit, new PickupCommand(drop.Id, drop.Kind == DropKind.Chest ? PickupChoice.None : choice));
            TakeDropsAfterMove(then);
        }

        private void OpenPickupMenu(BattleDrop drop, System.Action then)
        {
            _pendingDrop = drop;
            _afterPickup = then;
            _phase = Phase.PickupMenu;
            _cursor.DirectionalInputLocked = true;
            var unit = _active.Unit;
            var learnable = DropRules.Inheritable(drop, unit, DropSettings);
            _pickupMenu.Title = "Steel Rod";
            _pickupMenu.SetItems(new[]
            {
                new MenuItem(InheritId, $"Learn ({learnable.Count})"),
                new MenuItem(RestoreId, "Restore HP/MP"),
            });
            _hud.ShowMenu(_pickupMenu);
            _hud.SetHint($"{drop.FallenName}'s rod teaches: {string.Join(", ", learnable.Select(a => a.DisplayName))}   A / Enter: choose");
            Refresh();
        }

        /// <summary>Applies a pickup command for <paramref name="picker"/> and announces it.</summary>
        private void ExecutePickup(Unit picker, PickupCommand command)
        {
            var drop = _drops.Find(command.DropId);
            if (drop == null || picker == null)
                return;
            PickupChosen?.Invoke(command);
            _drops.Remove(drop);
            var result = DropRules.Take(drop, picker, command.Choice, InventoryOf(picker.Team), _spoils, _rng, DropSettings);
            Debug.Log($"[Battle] {result.Message}.", this);
            _hud.ShowBanner(result.Message);
            DropTaken?.Invoke(result);
        }

        private void ChoosePickup(string id)
        {
            var drop = _pendingDrop;
            var then = _afterPickup;
            _pendingDrop = null;
            _afterPickup = null;
            _hud.HideMenu();
            _phase = Phase.Idle;
            if (drop != null)
                ExecutePickup(_active.Unit, new PickupCommand(drop.Id, id == InheritId ? PickupChoice.Inherit : PickupChoice.Restore));
            // Another drop may lie on the same tile.
            TakeDropsAfterMove(then ?? AfterAction);
        }

        /// <summary>Gear the Player side found, for the end-of-battle banner.</summary>
        private string SpoilsText()
        {
            var gear = _spoils.Of(Team.Player);
            return gear.Count > 0 ? $"\nSpoils: {string.Join(", ", gear.Select(g => g.DisplayName))}" : "";
        }
    }
}
