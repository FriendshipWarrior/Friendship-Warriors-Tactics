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

        private static T At<T>(T[] values, int index, T fallback = default) =>
            index < values.Length ? values[index] : fallback;

        private static GridCoord[] ToCoords(Vector2Int[] cells) =>
            Array.ConvertAll(cells, c => new GridCoord(c.x, c.y));

        private void OnValidate()
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
