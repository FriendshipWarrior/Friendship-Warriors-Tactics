namespace FriendshipWarriors.Battle.Drops
{
    /// <summary>
    /// A pickup as a player input: which drop and what was chosen. Battles stay replayable from their inputs, so an
    /// online match would send this rather than its result.
    /// </summary>
    public readonly struct PickupCommand
    {
        public readonly int DropId;
        public readonly PickupChoice Choice;

        public PickupCommand(int dropId, PickupChoice choice)
        {
            DropId = dropId;
            Choice = choice;
        }

        public override string ToString() => $"Pick up drop {DropId}: {Choice}";
    }
}
