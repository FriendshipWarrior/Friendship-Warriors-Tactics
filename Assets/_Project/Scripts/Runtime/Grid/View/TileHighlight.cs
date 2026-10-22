namespace FriendshipWarriors.Grid.View
{
    /// <summary>Visual state of a tile's top. Later values take priority when combined by callers.</summary>
    public enum TileHighlight
    {
        None,
        MoveRange,
        Path,
        Hover,
        Selected,
    }
}
