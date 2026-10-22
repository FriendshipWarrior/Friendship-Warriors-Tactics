using System;
using System.Collections.Generic;

namespace FriendshipWarriors.Grid.Pathfinding
{
    public static class Pathfinder
    {
        /// <summary>
        /// Finds every tile reachable from <paramref name="origin"/> within the unit's Move, stepping orthogonally,
        /// never between tiles whose heights differ by more than its Jump. Enemies block; allies can be passed but
        /// not stopped on. Every step costs one tile, so a breadth-first search yields shortest paths.
        /// </summary>
        /// <param name="occupancy">Who stands on a tile, relative to the mover. Null means the field is empty.</param>
        public static MovementRange FindMovementRange(
            BattleGrid grid,
            GridCoord origin,
            MovementProfile profile,
            Func<GridCoord, Occupancy> occupancy = null)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (!grid.Contains(origin))
                throw new ArgumentOutOfRangeException(nameof(origin), origin, "Origin is outside the grid.");

            var nodes = new Dictionary<GridCoord, MovementRange.Node> { [origin] = new(0, origin) };
            var destinations = new List<GridCoord>();
            var frontier = new Queue<GridCoord>();
            frontier.Enqueue(origin);

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                int cost = nodes[current].Cost;
                if (cost >= profile.Move)
                    continue;

                int currentHeight = grid[current].Height;
                foreach (var direction in GridCoord.CardinalDirections)
                {
                    var next = current + direction;
                    if (nodes.ContainsKey(next) || !grid.TryGetTile(next, out var tile) || !tile.IsWalkable)
                        continue;
                    if (Math.Abs(tile.Height - currentHeight) > profile.Jump)
                        continue;

                    var occupant = occupancy?.Invoke(next) ?? Occupancy.Empty;
                    if (occupant == Occupancy.Enemy)
                        continue;

                    nodes[next] = new MovementRange.Node(cost + 1, current);
                    frontier.Enqueue(next);
                    if (occupant == Occupancy.Empty)
                        destinations.Add(next);
                }
            }

            return new MovementRange(origin, nodes, destinations);
        }
    }
}
