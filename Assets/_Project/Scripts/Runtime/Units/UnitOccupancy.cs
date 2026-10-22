using System;
using System.Collections.Generic;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Pathfinding;

namespace FriendshipWarriors.Units
{
    public static class UnitOccupancy
    {
        /// <summary>
        /// Occupancy as seen by <paramref name="mover"/>: its teammates are allies (pass through, can't stop) and
        /// everyone else is an enemy (blocks, can be leapt from high enough). The mover's own tile counts as empty.
        /// </summary>
        public static Func<GridCoord, Occupancy> For(Unit mover, IEnumerable<Unit> units)
        {
            var teams = new Dictionary<GridCoord, Team>();
            foreach (var unit in units)
            {
                if (unit != mover)
                    teams[unit.Coord] = unit.Team;
            }
            return coord => !teams.TryGetValue(coord, out var team) ? Occupancy.Empty
                : team == mover.Team ? Occupancy.Ally
                : Occupancy.Enemy;
        }
    }
}
