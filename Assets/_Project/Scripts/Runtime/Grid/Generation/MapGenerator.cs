using System;
using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Grid.Props;
using FriendshipWarriors.Grid.Validation;

namespace FriendshipWarriors.Grid.Generation
{
    /// <summary>
    /// Builds random battle maps in the same format as hand-made ones. Deterministic: the same settings and seed
    /// always give the same map. Each attempt is checked with <see cref="MapValidator"/>; attempts with errors are
    /// discarded and the next attempt uses a seed derived from the original, so retries stay reproducible.
    /// </summary>
    public static class MapGenerator
    {
        private const int H = GridMetrics.StepsPerH;

        /// <param name="deploymentSize">Tiles per deployment zone; null uses the settings (a battle setup's team size
        /// can ask for more or fewer).</param>
        public static GenerationResult Generate(MapGeneratorSettings settings, int seed, BattleMapData target, int? deploymentSize = null)
        {
            int zoneSize = deploymentSize ?? settings.DeploymentSize;
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (target == null) throw new ArgumentNullException(nameof(target));

            List<MapIssue> issues = null;
            for (int attempt = 0; attempt < settings.MaxAttempts; attempt++)
            {
                var rng = new Random(unchecked(seed * 486187739 + attempt * 16777619));
                BuildAttempt(settings, rng, target, zoneSize);
                target.DisplayName = $"Random Map {seed}";

                issues = MapValidator.Validate(target, settings.ReferenceJump);
                foreach (var (zone, side) in new[] { (target.PlayerDeployment, "Player"), (target.EnemyDeployment, "Enemy") })
                {
                    if (zone.Count < zoneSize)
                        issues.Add(new MapIssue(IssueSeverity.Error, $"{side} zone has {zone.Count} of {zoneSize} tiles."));
                }
                if (issues.All(i => i.Severity != IssueSeverity.Error))
                    return new GenerationResult(true, seed, attempt + 1, issues);
            }
            return new GenerationResult(false, seed, settings.MaxAttempts, issues);
        }

        private static void BuildAttempt(MapGeneratorSettings s, Random rng, BattleMapData target, int zoneSize)
        {
            int width = Range(rng, s.WidthRange);
            int depth = Range(rng, s.DepthRange);
            var grid = new BattleGrid(width, depth);

            var heights = ShapeTerrain(s, rng, width, depth);
            foreach (var c in grid.AllCoords())
                grid[c] = new Tile(heights[c.X, c.Y], PickTerrain(heights[c.X, c.Y], s.MaxHeight * H, rng));

            int ponds = Range(rng, s.PondCount);
            for (int i = 0; i < ponds; i++)
                CarvePond(grid, rng);

            int chasms = Range(rng, s.ChasmCount);
            for (int i = 0; i < chasms; i++)
                CarveChasm(grid, rng);

            AddRamps(grid, rng, s.RampChance);

            bool alongX = rng.NextDouble() < 0.5;
            var player = PickDeployment(grid, alongX, nearStart: true, zoneSize);
            var enemy = PickDeployment(grid, alongX, nearStart: false, zoneSize);

            target.CopyTilesFrom(grid);
            target.SetDeployment(player, enemy);
            target.SetProps(ScatterProps(grid, rng, s, player.Concat(enemy)));
        }

        // ------------------------------------------------------------------ Terrain shape

        /// <summary>Ground level plus a sum of rounded hills and an optional flat-topped plateau, in half-h steps.</summary>
        private static int[,] ShapeTerrain(MapGeneratorSettings s, Random rng, int width, int depth)
        {
            var field = new float[width, depth];
            int hills = Range(rng, s.HillCount);
            for (int i = 0; i < hills; i++)
            {
                double cx = rng.NextDouble() * (width - 1);
                double cy = rng.NextDouble() * (depth - 1);
                double radius = Lerp(s.HillRadius.x, s.HillRadius.y, rng.NextDouble());
                double amplitude = Lerp(s.HillAmplitude.x, s.HillAmplitude.y, rng.NextDouble());
                for (int x = 0; x < width; x++)
                for (int y = 0; y < depth; y++)
                {
                    double d = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / radius;
                    if (d < 1.0)
                        field[x, y] += (float)(amplitude * (1.0 - d * d));
                }
            }

            int maxSteps = s.MaxHeight * H;
            var heights = new int[width, depth];
            for (int x = 0; x < width; x++)
            for (int y = 0; y < depth; y++)
                heights[x, y] = Clamp(s.BaseHeight * H + (int)Math.Round(field[x, y] * H), 0, maxSteps);

            if (rng.NextDouble() < s.PlateauChance)
            {
                int pw = rng.Next(3, Math.Max(4, width / 3) + 1);
                int pd = rng.Next(3, Math.Max(4, depth / 3) + 1);
                int px = rng.Next(0, width - pw + 1);
                int py = rng.Next(0, depth - pd + 1);
                int top = 0;
                for (int x = px; x < px + pw; x++)
                for (int y = py; y < py + pd; y++)
                    top = Math.Max(top, heights[x, y]);
                top = Clamp(top + rng.Next(1, 3) * H, 0, maxSteps);
                for (int x = px; x < px + pw; x++)
                for (int y = py; y < py + pd; y++)
                    heights[x, y] = top;
            }
            return heights;
        }

        /// <summary>Grass on low ground, dirt on slopes, stone up high, with some jitter so bands aren't rings.</summary>
        private static TerrainType PickTerrain(int height, int maxSteps, Random rng)
        {
            double t = height / (double)Math.Max(1, maxSteps) + (rng.NextDouble() - 0.5) * 0.15;
            return t < 0.4 ? TerrainType.Grass : t < 0.7 ? TerrainType.Dirt : TerrainType.Stone;
        }

        /// <summary>A flat pond in a low spot: depth 1 at the edges, 2 in the middle when big enough, sandy shore.</summary>
        private static void CarvePond(BattleGrid grid, Random rng)
        {
            var lowest = grid.AllCoords().OrderBy(c => grid[c].Height).ThenBy(_ => rng.Next()).Take(Math.Max(1, grid.Width * grid.Depth / 4)).ToList();
            var center = lowest[rng.Next(lowest.Count)];
            int radius = rng.Next(1, 3);

            var pond = grid.AllCoords().Where(c => Chebyshev(c, center) <= radius && grid[c].Terrain != TerrainType.Void).ToList();
            int level = pond.Min(c => grid[c].Height);
            foreach (var c in pond)
            {
                int depth = radius >= 2 && Chebyshev(c, center) == 0 ? 2 : 1;
                grid[c] = new Tile(level, TerrainType.Water, depth);
            }

            foreach (var c in grid.AllCoords().Where(c => Chebyshev(c, center) == radius + 1))
            {
                var tile = grid[c];
                if (tile.Terrain != TerrainType.Water && tile.Terrain != TerrainType.Void)
                    grid[c] = new Tile(tile.Height, TerrainType.Sand);
            }
        }

        /// <summary>A straight one-tile-wide crack, leapable with Jump 2 or more.</summary>
        private static void CarveChasm(BattleGrid grid, Random rng)
        {
            bool horizontal = rng.NextDouble() < 0.5;
            int length = rng.Next(3, 7);
            var direction = horizontal ? GridCoord.East : GridCoord.North;
            var start = new GridCoord(rng.Next(1, Math.Max(2, grid.Width - 1)), rng.Next(1, Math.Max(2, grid.Depth - 1)));
            for (int i = 0; i < length; i++)
            {
                var c = start + direction * i;
                if (grid.Contains(c))
                    grid[c] = new Tile(0, TerrainType.Void);
            }
        }

        /// <summary>
        /// Turns some flat tiles sitting exactly 1h below a flat neighbor into ramps rising to meet it. Uses a 1h rise so
        /// the ramp's midpoint lands on a half height.
        /// </summary>
        private static void AddRamps(BattleGrid grid, Random rng, float chance)
        {
            var sloped = new HashSet<GridCoord>();
            foreach (var c in grid.AllCoords())
            {
                var tile = grid[c];
                if (tile.Terrain is TerrainType.Void or TerrainType.Water || rng.NextDouble() >= chance)
                    continue;

                foreach (var direction in GridCoord.CardinalDirections.OrderBy(_ => rng.Next()))
                {
                    var up = c + direction;
                    var down = c - direction;
                    if (!grid.TryGetTile(up, out var high) || sloped.Contains(up) || high.Slope != SlopeType.Flat)
                        continue;
                    if (high.Terrain is TerrainType.Void or TerrainType.Water || high.Height - tile.Height != H)
                        continue;
                    if (grid.TryGetTile(down, out var low) && low.Terrain != TerrainType.Void && low.StandingHeight > tile.Height)
                        continue;

                    grid[c] = new Tile(tile.Height, tile.Terrain, tile.Depth, RiseToward(direction), H);
                    sloped.Add(c);
                    break;
                }
            }
        }

        private static SlopeType RiseToward(GridCoord direction) =>
            direction == GridCoord.North ? SlopeType.RiseNorth
            : direction == GridCoord.East ? SlopeType.RiseEast
            : direction == GridCoord.South ? SlopeType.RiseSouth
            : SlopeType.RiseWest;

        // ------------------------------------------------------------------ Deployment and props

        /// <summary>
        /// Grows a cluster of standable tiles near the middle of one map edge. Neighbors must be within 1h so the zone
        /// reads as one area.
        /// </summary>
        private static List<GridCoord> PickDeployment(BattleGrid grid, bool alongX, bool nearStart, int size)
        {
            int span = alongX ? grid.Width : grid.Depth;
            int band = Math.Max(2, span / 4);
            bool InBand(GridCoord c)
            {
                int along = alongX ? c.X : c.Y;
                return nearStart ? along < band : along >= span - band;
            }
            bool Standable(GridCoord c) => grid.TryGetTile(c, out var t) && t.IsWalkable && t.AllowsActions && InBand(c);

            var edgeMiddle = alongX
                ? new GridCoord(nearStart ? 0 : grid.Width - 1, grid.Depth / 2)
                : new GridCoord(grid.Width / 2, nearStart ? 0 : grid.Depth - 1);
            var candidates = grid.AllCoords().Where(Standable).OrderBy(c => c.ManhattanDistance(edgeMiddle)).ThenBy(c => c.Y).ThenBy(c => c.X).ToList();
            if (candidates.Count == 0)
                return new List<GridCoord>();

            var zone = new List<GridCoord> { candidates[0] };
            var frontier = new Queue<GridCoord>(zone);
            while (frontier.Count > 0 && zone.Count < size)
            {
                var current = frontier.Dequeue();
                foreach (var direction in GridCoord.CardinalDirections)
                {
                    var next = current + direction;
                    if (zone.Count >= size || zone.Contains(next) || !Standable(next))
                        continue;
                    if (Math.Abs(grid[next].StandingHeight - grid[current].StandingHeight) > H)
                        continue;
                    zone.Add(next);
                    frontier.Enqueue(next);
                }
            }
            return zone;
        }

        private static List<PropPlacement> ScatterProps(BattleGrid grid, Random rng, MapGeneratorSettings s, IEnumerable<GridCoord> keepClear)
        {
            var placements = new List<PropPlacement>();
            var choices = s.Props.Where(p => p != null && p.Prop != null && p.Weight > 0f).ToArray();
            if (choices.Length == 0 || s.PropDensity <= 0f)
                return placements;

            var clear = new HashSet<GridCoord>(keepClear);
            float totalWeight = choices.Sum(p => p.Weight);
            foreach (var c in grid.AllCoords())
            {
                var tile = grid[c];
                if (clear.Contains(c) || tile.Terrain is TerrainType.Void or TerrainType.Water || tile.Slope != SlopeType.Flat)
                    continue;
                if (rng.NextDouble() >= s.PropDensity)
                    continue;

                double pick = rng.NextDouble() * totalWeight;
                var choice = choices[^1];
                foreach (var candidate in choices)
                {
                    pick -= candidate.Weight;
                    if (pick < 0)
                    {
                        choice = candidate;
                        break;
                    }
                }
                placements.Add(new PropPlacement(c, choice.Prop));
            }
            return placements;
        }

        // ------------------------------------------------------------------ Helpers

        /// <summary>Inclusive integer range from a min/max pair.</summary>
        private static int Range(Random rng, UnityEngine.Vector2Int range) =>
            rng.Next(Math.Min(range.x, range.y), Math.Max(range.x, range.y) + 1);

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
        private static int Chebyshev(GridCoord a, GridCoord b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    }
}
