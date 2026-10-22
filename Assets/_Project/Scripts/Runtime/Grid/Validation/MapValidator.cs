using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Grid.Pathfinding;

namespace FriendshipWarriors.Grid.Validation
{
    /// <summary>
    /// Checks a map for problems that would break or spoil a battle. Used by the map editor and the random map
    /// generator, so hand-made and generated maps meet the same bar.
    /// </summary>
    public static class MapValidator
    {
        /// <summary>FFT's most common Jump; reachability is judged for a unit with this Jump.</summary>
        public const int DefaultReferenceJump = 3;

        public static List<MapIssue> Validate(BattleMapData map, int referenceJump = DefaultReferenceJump)
        {
            var issues = new List<MapIssue>();
            var grid = map.CreateGrid();

            CheckDeployment(grid, map.PlayerDeployment, "Player", issues);
            CheckDeployment(grid, map.EnemyDeployment, "Enemy", issues);
            foreach (var coord in map.PlayerDeployment.Intersect(map.EnemyDeployment))
                issues.Add(new MapIssue(IssueSeverity.Error, "Tile is in both deployment zones.", coord));

            CheckProps(map, issues);
            CheckTiles(grid, issues);
            CheckReachability(grid, map, referenceJump, issues);
            return issues;
        }

        private static void CheckDeployment(BattleGrid grid, IReadOnlyList<GridCoord> zone, string side, List<MapIssue> issues)
        {
            if (zone.Count == 0)
                issues.Add(new MapIssue(IssueSeverity.Error, $"{side} deployment zone is empty."));

            foreach (var coord in zone)
            {
                if (!grid.TryGetTile(coord, out var tile))
                    issues.Add(new MapIssue(IssueSeverity.Error, $"{side} deployment tile is outside the map.", coord));
                else if (!tile.IsWalkable)
                    issues.Add(new MapIssue(IssueSeverity.Error, $"{side} deployment tile can't be stood on.", coord));
                else if (!tile.AllowsActions)
                    issues.Add(new MapIssue(IssueSeverity.Warning, $"{side} units deployed here start in deep water and can't act.", coord));
            }
        }

        private static void CheckProps(BattleMapData map, List<MapIssue> issues)
        {
            var seen = new HashSet<GridCoord>();
            foreach (var placement in map.Props)
            {
                var coord = placement.Coord;
                if (placement.Prop == null)
                    issues.Add(new MapIssue(IssueSeverity.Warning, "Prop placement has no prop assigned.", coord));
                if (!map.Contains(coord))
                {
                    issues.Add(new MapIssue(IssueSeverity.Error, "Prop is outside the map.", coord));
                    continue;
                }
                if (map.GetTile(coord).Terrain == TerrainType.Void)
                    issues.Add(new MapIssue(IssueSeverity.Error, "Prop stands on a void tile.", coord));
                if (!seen.Add(coord))
                    issues.Add(new MapIssue(IssueSeverity.Warning, "More than one prop on this tile.", coord));
                if (placement.Prop != null && placement.Prop.BlocksMovement && map.GetDeploymentAt(coord) != DeploymentSide.None)
                    issues.Add(new MapIssue(IssueSeverity.Error, "A blocking prop covers a deployment tile.", coord));
            }
        }

        private static void CheckTiles(BattleGrid grid, List<MapIssue> issues)
        {
            foreach (var coord in grid.AllCoords())
            {
                var tile = grid[coord];
                if (tile.Slope != SlopeType.Flat && tile.SlopeHeight % 2 != 0)
                {
                    issues.Add(new MapIssue(IssueSeverity.Warning,
                        $"Slope midpoint falls between half heights; it counts as {GridMetrics.FormatHeight(tile.StandingHeight)}.", coord));
                }
                if (tile.Slope != SlopeType.Flat && tile.SlopeHeight == 0)
                    issues.Add(new MapIssue(IssueSeverity.Warning, "Sloped tile has no slope height, so it renders flat.", coord));
                if (tile.Depth > 0 && tile.Terrain != TerrainType.Water)
                    issues.Add(new MapIssue(IssueSeverity.Warning, $"Water depth {tile.Depth} on {tile.Terrain} terrain.", coord));
            }
        }

        private static void CheckReachability(BattleGrid grid, BattleMapData map, int referenceJump, List<MapIssue> issues)
        {
            var starts = map.PlayerDeployment.Where(c => grid.TryGetTile(c, out var t) && t.IsWalkable).ToList();
            if (starts.Count == 0)
                return;

            // Unlimited Move: only terrain, Jump and gaps decide what can be reached.
            var profile = new MovementProfile(grid.Width * grid.Depth, referenceJump);
            var reachable = new HashSet<GridCoord>(starts);
            foreach (var start in starts)
                reachable.UnionWith(Pathfinder.FindMovementRange(grid, start, profile).Destinations);

            foreach (var coord in map.EnemyDeployment)
            {
                if (grid.TryGetTile(coord, out var tile) && tile.IsWalkable && !reachable.Contains(coord))
                {
                    issues.Add(new MapIssue(IssueSeverity.Error,
                        $"Enemy deployment tile can't be reached from the player zone with Jump {referenceJump}.", coord));
                }
            }

            var stranded = grid.AllCoords().Where(c => grid[c].IsWalkable && !reachable.Contains(c)).ToList();
            if (stranded.Count > 0)
            {
                issues.Add(new MapIssue(IssueSeverity.Warning,
                    $"{stranded.Count} walkable tile(s) can't be reached from the player zone with Jump {referenceJump}.", stranded[0]));
            }
        }
    }
}
