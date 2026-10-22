using System;
using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Grid.Props;
using UnityEngine;

namespace FriendshipWarriors.Grid
{
    /// <summary>
    /// An authored or generated battle map: the single source of truth for campaign, quick-play and random maps.
    /// Per-tile arrays are row-major (index = y * width + x); missing entries read as flat height-0 grass so a
    /// half-edited asset still loads.
    /// </summary>
    [CreateAssetMenu(menuName = "Friendship Warriors/Battle Map", fileName = "BattleMap")]
    public sealed class BattleMapData : ScriptableObject
    {
        [SerializeField] private string _displayName = "Untitled Map";
        [SerializeField, Min(1)] private int _width = 10;
        [SerializeField, Min(1)] private int _depth = 10;

        [Header("Tiles")]
        [SerializeField] private int[] _heights = Array.Empty<int>();
        [SerializeField] private TerrainType[] _terrain = Array.Empty<TerrainType>();
        [SerializeField] private int[] _waterDepths = Array.Empty<int>();
        [SerializeField] private SlopeType[] _slopes = Array.Empty<SlopeType>();
        [SerializeField] private int[] _slopeHeights = Array.Empty<int>();

        [Header("Deployment")]
        [SerializeField] private Vector2Int[] _playerDeployment = Array.Empty<Vector2Int>();
        [SerializeField] private Vector2Int[] _enemyDeployment = Array.Empty<Vector2Int>();

        [Header("Decoration")]
        [SerializeField] private PropPlacement[] _props = Array.Empty<PropPlacement>();

        public string DisplayName
        {
            get => _displayName;
            set => _displayName = value;
        }

        public int Width => _width;
        public int Depth => _depth;
        public IReadOnlyList<PropPlacement> Props => _props;
        public IReadOnlyList<GridCoord> PlayerDeployment => ToCoords(_playerDeployment);
        public IReadOnlyList<GridCoord> EnemyDeployment => ToCoords(_enemyDeployment);

        /// <summary>Builds the rules-side grid. Tiles under props that block movement come out blocked.</summary>
        public BattleGrid CreateGrid()
        {
            var grid = new BattleGrid(_width, _depth);
            foreach (var coord in grid.AllCoords())
            {
                int i = coord.Y * _width + coord.X;
                grid[coord] = new Tile(
                    At(_heights, i),
                    At(_terrain, i, TerrainType.Grass),
                    At(_waterDepths, i),
                    At(_slopes, i, SlopeType.Flat),
                    At(_slopeHeights, i));
            }

            foreach (var placement in _props)
            {
                if (placement.Prop != null && placement.Prop.BlocksMovement && grid.Contains(placement.Coord))
                    grid[placement.Coord] = grid[placement.Coord].WithBlocked(true);
            }
            return grid;
        }

        /// <summary>Replaces the tile layout. Blocked flags are not copied; props are the source of blocking.</summary>
        public void CopyTilesFrom(BattleGrid grid)
        {
            _width = grid.Width;
            _depth = grid.Depth;
            int size = _width * _depth;
            _heights = new int[size];
            _terrain = new TerrainType[size];
            _waterDepths = new int[size];
            _slopes = new SlopeType[size];
            _slopeHeights = new int[size];
            foreach (var coord in grid.AllCoords())
            {
                int i = coord.Y * _width + coord.X;
                var tile = grid[coord];
                _heights[i] = tile.Height;
                _terrain[i] = tile.Terrain;
                _waterDepths[i] = tile.Depth;
                _slopes[i] = tile.Slope;
                _slopeHeights[i] = tile.SlopeHeight;
            }
        }

        public void SetDeployment(IEnumerable<GridCoord> player, IEnumerable<GridCoord> enemy)
        {
            _playerDeployment = player.Select(c => new Vector2Int(c.X, c.Y)).ToArray();
            _enemyDeployment = enemy.Select(c => new Vector2Int(c.X, c.Y)).ToArray();
        }

        public void SetProps(IEnumerable<PropPlacement> props) => _props = props.ToArray();

        public bool Contains(GridCoord coord) => coord.X >= 0 && coord.X < _width && coord.Y >= 0 && coord.Y < _depth;

        /// <summary>The authored tile at a coordinate, without blocking from props.</summary>
        public Tile GetTile(GridCoord coord)
        {
            RequireInBounds(coord);
            int i = coord.Y * _width + coord.X;
            return new Tile(At(_heights, i), At(_terrain, i, TerrainType.Grass), At(_waterDepths, i),
                At(_slopes, i, SlopeType.Flat), At(_slopeHeights, i));
        }

        /// <summary>Overwrites one tile. Its blocked flag is ignored; props are the source of blocking.</summary>
        public void SetTile(GridCoord coord, Tile tile)
        {
            RequireInBounds(coord);
            EnsureArraySizes();
            int i = coord.Y * _width + coord.X;
            _heights[i] = tile.Height;
            _terrain[i] = tile.Terrain;
            _waterDepths[i] = tile.Depth;
            _slopes[i] = tile.Slope;
            _slopeHeights[i] = tile.SlopeHeight;
        }

        /// <summary>
        /// Changes the map size, keeping tiles, props and deployment that still fit. New tiles are flat grass at
        /// <paramref name="fillHeight"/> half-h steps.
        /// </summary>
        public void Resize(int width, int depth, int fillHeight = 0)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be positive.");
            if (depth <= 0) throw new ArgumentOutOfRangeException(nameof(depth), depth, "Depth must be positive.");

            var resized = new BattleGrid(width, depth);
            foreach (var coord in resized.AllCoords())
                resized[coord] = Contains(coord) ? GetTile(coord) : new Tile(fillHeight, TerrainType.Grass);

            CopyTilesFrom(resized);
            _props = _props.Where(p => Contains(p.Coord)).ToArray();
            _playerDeployment = _playerDeployment.Where(c => Contains(new GridCoord(c.x, c.y))).ToArray();
            _enemyDeployment = _enemyDeployment.Where(c => Contains(new GridCoord(c.x, c.y))).ToArray();
        }

        public PropDefinition GetPropAt(GridCoord coord)
        {
            foreach (var placement in _props)
            {
                if (placement.Coord == coord)
                    return placement.Prop;
            }
            return null;
        }

        /// <summary>Places a prop on a tile, replacing any already there. Null removes it.</summary>
        public void SetPropAt(GridCoord coord, PropDefinition prop)
        {
            RequireInBounds(coord);
            var kept = _props.Where(p => p.Coord != coord);
            _props = prop == null ? kept.ToArray() : kept.Append(new PropPlacement(coord, prop)).ToArray();
        }

        public DeploymentSide GetDeploymentAt(GridCoord coord)
        {
            var cell = new Vector2Int(coord.X, coord.Y);
            if (Array.IndexOf(_playerDeployment, cell) >= 0)
                return DeploymentSide.Player;
            if (Array.IndexOf(_enemyDeployment, cell) >= 0)
                return DeploymentSide.Enemy;
            return DeploymentSide.None;
        }

        /// <summary>Assigns a tile to one side's deployment zone, removing it from the other.</summary>
        public void SetDeploymentAt(GridCoord coord, DeploymentSide side)
        {
            RequireInBounds(coord);
            var cell = new Vector2Int(coord.X, coord.Y);
            _playerDeployment = _playerDeployment.Where(c => c != cell).ToArray();
            _enemyDeployment = _enemyDeployment.Where(c => c != cell).ToArray();
            if (side == DeploymentSide.Player)
                _playerDeployment = _playerDeployment.Append(cell).ToArray();
            else if (side == DeploymentSide.Enemy)
                _enemyDeployment = _enemyDeployment.Append(cell).ToArray();
        }

        private void RequireInBounds(GridCoord coord)
        {
            if (!Contains(coord))
                throw new ArgumentOutOfRangeException(nameof(coord), coord, $"Outside the {_width}x{_depth} map.");
        }

        private static T At<T>(T[] values, int index, T fallback = default) =>
            index < values.Length ? values[index] : fallback;

        private static GridCoord[] ToCoords(Vector2Int[] cells) =>
            Array.ConvertAll(cells, c => new GridCoord(c.x, c.y));

        private void OnValidate() => EnsureArraySizes();

        private void EnsureArraySizes()
        {
            int size = _width * _depth;
            if (_heights.Length != size) Array.Resize(ref _heights, size);
            if (_terrain.Length != size) Array.Resize(ref _terrain, size);
            if (_waterDepths.Length != size) Array.Resize(ref _waterDepths, size);
            if (_slopes.Length != size) Array.Resize(ref _slopes, size);
            if (_slopeHeights.Length != size) Array.Resize(ref _slopeHeights, size);
        }
    }
}
