using System;
using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Units;

namespace FriendshipWarriors.Battle.Deployment
{
    /// <summary>
    /// One side's formation before the battle (FFT's formation screen): which of its units start, on which tiles of the
    /// deployment zone, facing which way. Pure and deterministic: the same zone, units and moves always give the same
    /// placements, so a formation can later be sent as an online input and rebuilt on the other side.
    /// <para>Units keep the tiles they were spawned on when those are free zone tiles; the rest fill the first free
    /// tiles in zone order. Placing a unit on another unit's tile swaps them. Up to <see cref="Capacity"/> units are
    /// deployed; the others wait on the bench (roster selection arrives with army building, but the rules are here).</para>
    /// </summary>
    public sealed class DeploymentPlan
    {
        private readonly List<GridCoord> _tiles;
        private readonly HashSet<GridCoord> _tileSet;
        private readonly List<Unit> _roster;
        private readonly Dictionary<Unit, GridCoord> _coords = new();
        private readonly Dictionary<Unit, Direction> _facings = new();

        /// <param name="zone">The side's deployment tiles, in the map's order. Tiles that aren't on the grid or can't
        /// be stood on are left out.</param>
        /// <param name="roster">Every unit that could deploy, in roster order.</param>
        /// <param name="capacity">How many may deploy; null for as many as the zone holds.</param>
        public DeploymentPlan(BattleGrid grid, IEnumerable<GridCoord> zone, IEnumerable<Unit> roster, int? capacity = null)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            _tiles = zone.Distinct().Where(c => grid.TryGetTile(c, out var tile) && tile.IsWalkable).ToList();
            _tileSet = new HashSet<GridCoord>(_tiles);
            _roster = roster.ToList();
            Capacity = Math.Min(capacity ?? _tiles.Count, _tiles.Count);

            // First pass: units already standing on a free zone tile keep it.
            var deployed = _roster.Take(Capacity).ToList();
            foreach (var unit in deployed)
            {
                _facings[unit] = unit.Facing;
                if (_tileSet.Contains(unit.Coord) && UnitAt(unit.Coord) == null)
                    _coords[unit] = unit.Coord;
            }
            // Second pass: the rest take the first free tiles.
            foreach (var unit in deployed.Where(u => !_coords.ContainsKey(u)))
                _coords[unit] = _tiles.First(c => UnitAt(c) == null);
        }

        /// <summary>The tiles units may start on.</summary>
        public IReadOnlyList<GridCoord> Tiles => _tiles;

        /// <summary>Every unit that could deploy, in roster order.</summary>
        public IReadOnlyList<Unit> Roster => _roster;

        public int Capacity { get; }

        public int DeployedCount => _coords.Count;

        public bool IsTile(GridCoord coord) => _tileSet.Contains(coord);

        public bool IsDeployed(Unit unit) => unit != null && _coords.ContainsKey(unit);

        public Unit UnitAt(GridCoord coord)
        {
            foreach (var pair in _coords)
            {
                if (pair.Value == coord)
                    return pair.Key;
            }
            return null;
        }

        public GridCoord? CoordOf(Unit unit) => IsDeployed(unit) ? _coords[unit] : null;

        public Direction FacingOf(Unit unit) => _facings.TryGetValue(unit, out var facing) ? facing : unit.Facing;

        /// <summary>
        /// Moves a deployed unit to a zone tile, swapping with whoever stands there. A benched unit is deployed there
        /// instead (see <see cref="Deploy"/>). False if the tile isn't in the zone or the unit isn't in the roster.
        /// </summary>
        public bool Place(Unit unit, GridCoord coord)
        {
            if (!_roster.Contains(unit) || !IsTile(coord))
                return false;
            if (!IsDeployed(unit))
                return Deploy(unit, coord);

            var other = UnitAt(coord);
            if (other != null && other != unit)
                _coords[other] = _coords[unit];
            _coords[unit] = coord;
            return true;
        }

        /// <summary>
        /// Puts a benched unit on a zone tile. If the tile is taken, the unit there goes to the bench in its place;
        /// otherwise there must be room left under <see cref="Capacity"/>.
        /// </summary>
        public bool Deploy(Unit unit, GridCoord coord)
        {
            if (!_roster.Contains(unit) || IsDeployed(unit) || !IsTile(coord))
                return false;
            var other = UnitAt(coord);
            if (other != null)
                _coords.Remove(other);
            else if (DeployedCount >= Capacity)
                return false;
            _coords[unit] = coord;
            if (!_facings.ContainsKey(unit))
                _facings[unit] = unit.Facing;
            return true;
        }

        /// <summary>Sends a deployed unit to the bench. At least one unit always stays deployed.</summary>
        public bool Withdraw(Unit unit)
        {
            if (!IsDeployed(unit) || DeployedCount <= 1)
                return false;
            _coords.Remove(unit);
            return true;
        }

        public bool SetFacing(Unit unit, Direction facing)
        {
            if (!IsDeployed(unit))
                return false;
            _facings[unit] = facing;
            return true;
        }

        /// <summary>The deployed units' placements, in roster order.</summary>
        public IReadOnlyList<UnitPlacement> Placements =>
            _roster.Where(IsDeployed).Select(u => new UnitPlacement(u, _coords[u], _facings[u])).ToList();

        /// <summary>Writes the placements onto the units (coordinates and facings).</summary>
        public void Apply()
        {
            foreach (var placement in Placements)
            {
                placement.Unit.Coord = placement.Coord;
                placement.Unit.Facing = placement.Facing;
            }
        }
    }
}
