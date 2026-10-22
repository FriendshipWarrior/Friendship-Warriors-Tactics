using System;
using System.Collections.Generic;

namespace FriendshipWarriors.Grid.Pathfinding
{
    public static class Pathfinder
    {
        /// <summary>
        /// Finds every tile reachable from <paramref name="origin"/> within the unit's Move, following FFT's rules:
        /// <list type="bullet">
        /// <item>Steps are orthogonal and cost 1 Move; the standing heights of the two tiles may differ by at most
        /// Jump. (Whether FFT also limits stepping <i>down</i> by Jump is unverified; we assume it does.)</item>
        /// <item>A unit may leap straight across up to Jump / 2 gap tiles (void, or lower than the take-off tile),
        /// paying Move as if the gap were flat ground. Blocked tiles, higher tiles and enemies can't be leapt.</item>
        /// <item>Enemies block; allies can be passed but not stopped on.</item>
        /// </list>
        /// Water depth does not affect movement yet; FFT's exact water rules are unverified.
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

            var search = new Search(grid, profile, occupancy ?? (_ => Occupancy.Empty));
            search.Run(origin);
            return new MovementRange(origin, search.Nodes, search.Destinations);
        }

        /// <summary>Dijkstra over small integer costs, using one bucket per Move point instead of a heap.</summary>
        private sealed class Search
        {
            private readonly BattleGrid _grid;
            private readonly MovementProfile _profile;
            private readonly Func<GridCoord, Occupancy> _occupancy;
            private readonly List<GridCoord>[] _buckets;

            public readonly Dictionary<GridCoord, MovementRange.Node> Nodes = new();
            public readonly List<GridCoord> Destinations = new();

            public Search(BattleGrid grid, MovementProfile profile, Func<GridCoord, Occupancy> occupancy)
            {
                _grid = grid;
                _profile = profile;
                _occupancy = occupancy;
                _buckets = new List<GridCoord>[profile.Move + 1];
            }

            public void Run(GridCoord origin)
            {
                Nodes[origin] = new MovementRange.Node(0, origin);
                Enqueue(origin, 0);

                for (int cost = 0; cost <= _profile.Move; cost++)
                {
                    var bucket = _buckets[cost];
                    if (bucket == null)
                        continue;

                    foreach (var current in bucket)
                    {
                        // A cheaper route found later leaves a stale entry behind; skip it.
                        if (Nodes[current].Cost != cost)
                            continue;
                        if (current != origin && _occupancy(current) == Occupancy.Empty)
                            Destinations.Add(current);
                        if (cost < _profile.Move)
                            Expand(current, cost);
                    }
                }
            }

            private void Expand(GridCoord from, int cost)
            {
                float fromHeight = _grid[from].StandingHeight;
                foreach (var direction in GridCoord.CardinalDirections)
                {
                    TryStep(from, from + direction, cost + 1, fromHeight);

                    for (int gap = 1; gap <= _profile.HorizontalJump; gap++)
                    {
                        if (!IsLeapableGap(from + direction * gap, fromHeight))
                            break;
                        TryStep(from, from + direction * (gap + 1), cost + gap + 1, fromHeight);
                    }
                }
            }

            private bool IsLeapableGap(GridCoord coord, float takeOffHeight)
            {
                if (!_grid.TryGetTile(coord, out var tile) || tile.Blocked)
                    return false;
                if (_occupancy(coord) == Occupancy.Enemy)
                    return false;
                return tile.Terrain == TerrainType.Void || tile.StandingHeight < takeOffHeight;
            }

            private void TryStep(GridCoord from, GridCoord to, int cost, float fromHeight)
            {
                if (cost > _profile.Move)
                    return;
                if (!_grid.TryGetTile(to, out var tile) || !tile.IsWalkable)
                    return;
                if (Math.Abs(tile.StandingHeight - fromHeight) > _profile.JumpSteps)
                    return;
                if (_occupancy(to) == Occupancy.Enemy)
                    return;
                if (Nodes.TryGetValue(to, out var existing) && existing.Cost <= cost)
                    return;

                Nodes[to] = new MovementRange.Node(cost, from);
                Enqueue(to, cost);
            }

            private void Enqueue(GridCoord coord, int cost) => (_buckets[cost] ??= new List<GridCoord>()).Add(coord);
        }
    }
}
