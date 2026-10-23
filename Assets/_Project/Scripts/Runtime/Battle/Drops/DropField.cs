using System.Collections.Generic;
using FriendshipWarriors.Grid;

namespace FriendshipWarriors.Battle.Drops
{
    /// <summary>The drops lying on the battlefield, in the order they appeared. They stay until someone takes them.</summary>
    public sealed class DropField
    {
        private readonly List<BattleDrop> _drops = new();
        private int _nextId = 1;

        public IReadOnlyList<BattleDrop> All => _drops;

        /// <summary>The id the next drop gets (ids are stable per battle, for recorded pickups).</summary>
        public int NextId() => _nextId++;

        public void Add(BattleDrop drop)
        {
            if (drop != null)
                _drops.Add(drop);
        }

        /// <summary>The oldest drop on the tile, or null.</summary>
        public BattleDrop At(GridCoord coord)
        {
            foreach (var drop in _drops)
            {
                if (drop.Coord == coord)
                    return drop;
            }
            return null;
        }

        public BattleDrop Find(int id)
        {
            foreach (var drop in _drops)
            {
                if (drop.Id == id)
                    return drop;
            }
            return null;
        }

        public bool Remove(BattleDrop drop) => _drops.Remove(drop);

        public void Clear()
        {
            _drops.Clear();
            _nextId = 1;
        }
    }
}
