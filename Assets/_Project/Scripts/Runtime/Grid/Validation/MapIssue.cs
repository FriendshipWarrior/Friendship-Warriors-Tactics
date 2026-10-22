namespace FriendshipWarriors.Grid.Validation
{
    /// <summary>A problem found in a map, optionally pointing at the tile it concerns.</summary>
    public readonly struct MapIssue
    {
        public readonly IssueSeverity Severity;
        public readonly string Message;
        public readonly GridCoord? Coord;

        public MapIssue(IssueSeverity severity, string message, GridCoord? coord = null)
        {
            Severity = severity;
            Message = message;
            Coord = coord;
        }

        public override string ToString() => Coord is { } c ? $"{Severity} at {c}: {Message}" : $"{Severity}: {Message}";
    }
}
