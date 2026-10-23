using FriendshipWarriors.Grid;
using FriendshipWarriors.Units;

namespace FriendshipWarriors.Battle.Deployment
{
    /// <summary>Where a unit starts the battle and which way it faces: one entry of a finished formation.</summary>
    public readonly struct UnitPlacement
    {
        public readonly Unit Unit;
        public readonly GridCoord Coord;
        public readonly Direction Facing;

        public UnitPlacement(Unit unit, GridCoord coord, Direction facing)
        {
            Unit = unit;
            Coord = coord;
            Facing = facing;
        }

        public override string ToString() => $"{Unit?.Name} at {Coord} facing {Facing}";
    }
}
