using System;
using UnityEngine;

namespace FriendshipWarriors.Grid
{
    /// <summary>
    /// Authored map layout. Arrays are row-major (index = y * width + x); missing entries read as
    /// height 0 grass so a half-edited asset still loads.
    /// </summary>
    [CreateAssetMenu(menuName = "Friendship Warriors/Battle Map", fileName = "BattleMap")]
    public sealed class BattleMapData : ScriptableObject
    {
        [SerializeField, Min(1)] private int _width = 10;
        [SerializeField, Min(1)] private int _depth = 10;
        [SerializeField] private int[] _heights = Array.Empty<int>();
        [SerializeField] private TerrainType[] _terrain = Array.Empty<TerrainType>();

        public int Width => _width;
        public int Depth => _depth;

        public BattleGrid CreateGrid()
        {
            var grid = new BattleGrid(_width, _depth);
            foreach (var coord in grid.AllCoords())
            {
                int index = coord.Y * _width + coord.X;
                int height = index < _heights.Length ? _heights[index] : 0;
                var terrain = index < _terrain.Length ? _terrain[index] : TerrainType.Grass;
                grid[coord] = new Tile(height, terrain);
            }
            return grid;
        }

        public void CopyFrom(BattleGrid grid)
        {
            _width = grid.Width;
            _depth = grid.Depth;
            _heights = new int[_width * _depth];
            _terrain = new TerrainType[_width * _depth];
            foreach (var coord in grid.AllCoords())
            {
                int index = coord.Y * _width + coord.X;
                var tile = grid[coord];
                _heights[index] = tile.Height;
                _terrain[index] = tile.Terrain;
            }
        }

        private void OnValidate()
        {
            int size = _width * _depth;
            if (_heights.Length != size) Array.Resize(ref _heights, size);
            if (_terrain.Length != size) Array.Resize(ref _terrain, size);
        }
    }
}
