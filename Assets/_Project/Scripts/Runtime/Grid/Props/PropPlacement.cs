using System;
using UnityEngine;

namespace FriendshipWarriors.Grid.Props
{
    [Serializable]
    public struct PropPlacement
    {
        [SerializeField] private Vector2Int _coord;
        [SerializeField] private PropDefinition _prop;

        public PropPlacement(GridCoord coord, PropDefinition prop)
        {
            _coord = new Vector2Int(coord.X, coord.Y);
            _prop = prop;
        }

        public GridCoord Coord => new(_coord.x, _coord.y);
        public PropDefinition Prop => _prop;
    }
}
