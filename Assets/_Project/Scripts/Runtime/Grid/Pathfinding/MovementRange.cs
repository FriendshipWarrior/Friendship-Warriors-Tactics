using System.Collections.Generic;

namespace FriendshipWarriors.Grid.Pathfinding
{
    /// <summary>
    /// Every tile a unit can reach this turn, with the shortest route to each.
    /// Produced by <see cref="Pathfinder.FindMovementRange"/>.
    /// </summary>
    public sealed class MovementRange
    {
        internal readonly struct Node
        {
            public readonly int Cost;
            public readonly GridCoord Previous;

            public Node(int cost, GridCoord previous)
            {
                Cost = cost;
                Previous = previous;
            }
        }

        private readonly Dictionary<GridCoord, Node> _nodes;
        private readonly List<GridCoord> _destinations;
        private readonly HashSet<GridCoord> _destinationSet;

        public GridCoord Origin { get; }

        /// <summary>Tiles the unit may end its move on, in discovery order (nearest first). Excludes the origin.</summary>
        public IReadOnlyList<GridCoord> Destinations => _destinations;

        internal MovementRange(GridCoord origin, Dictionary<GridCoord, Node> nodes, List<GridCoord> destinations)
        {
            Origin = origin;
            _nodes = nodes;
            _destinations = destinations;
            _destinationSet = new HashSet<GridCoord>(destinations);
        }

        public bool CanMoveTo(GridCoord coord) => _destinationSet.Contains(coord);

        /// <summary>Tiles needed to reach <paramref name="coord"/>, or -1 if it can't be reached (passed through or stopped on).</summary>
        public int CostTo(GridCoord coord) => _nodes.TryGetValue(coord, out var node) ? node.Cost : -1;

        /// <summary>
        /// Fills <paramref name="path"/> with the steps from the tile after the origin up to and including
        /// <paramref name="destination"/>. Returns false, leaving the list empty, if the unit can't stop there.
        /// Consecutive steps are adjacent except for leaps across gaps, which move in a straight line.
        /// </summary>
        public bool TryGetPath(GridCoord destination, List<GridCoord> path)
        {
            path.Clear();
            if (!CanMoveTo(destination))
                return false;

            for (var step = destination; step != Origin; step = _nodes[step].Previous)
                path.Add(step);
            path.Reverse();
            return true;
        }
    }
}
