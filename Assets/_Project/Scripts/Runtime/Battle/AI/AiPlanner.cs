using System;
using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Battle.Combat;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Pathfinding;
using FriendshipWarriors.Units;

namespace FriendshipWarriors.Battle.AI
{
    /// <summary>
    /// Chooses an AI unit's turn, in the spirit of FFT's enemy AI (whose exact logic is not public; these are our own
    /// weights). Every tile the unit can stand on this turn — including staying put — is paired with every enemy its
    /// Attack can reach from there, and scored by expected damage, a large bonus for a likely knock-out, a preference
    /// for side and back attacks, and a small cost for ending within reach of many enemies. With nothing to attack, it
    /// closes in on the nearest enemy by walking distance. It always ends facing the nearest enemy. Turn-trade
    /// abilities are not used by the AI yet.
    /// </summary>
    public static class AiPlanner
    {
        /// <summary>Extra score for a likely knock-out, as a share of the target's max HP (scaled by hit chance).</summary>
        public const double KnockOutBonus = 1.0;
        public const double SideBonus = 2.0;
        public const double BackBonus = 4.0;
        public const double ThreatCost = 1.5;

        /// <param name="units">Everyone still on the field (knocked-out units included; they block tiles).</param>
        /// <param name="rng">The battle's seeded random, used only to break ties so plans stay deterministic.</param>
        public static AiPlan Plan(BattleGrid grid, Unit actor, IReadOnlyList<Unit> units, MovementRules rules, Random rng)
        {
            var origin = actor.Coord;
            var stats = actor.Stats;
            var range = Pathfinder.FindMovementRange(grid, origin, new MovementProfile(stats.Move, stats.Jump),
                UnitOccupancy.For(actor, units), rules);
            var positions = new List<GridCoord> { origin };
            positions.AddRange(range.Destinations);
            var enemies = units.Where(u => u.Team != actor.Team && !u.IsKnockedOut).ToList();

            var plan = new AiPlan();
            double bestScore = double.NegativeInfinity;
            GridCoord bestPosition = origin;
            try
            {
                foreach (var position in positions)
                {
                    actor.Coord = position;
                    var targets = new HashSet<GridCoord>(AttackRange.Targets(grid, actor));
                    foreach (var enemy in enemies)
                    {
                        if (!targets.Contains(enemy.Coord))
                            continue;
                        // Tiny random jitter breaks ties without letting chance override real differences.
                        double score = AttackScore(actor, enemy) - Threat(position, actor, enemies) * ThreatCost + rng.NextDouble() * 0.01;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestPosition = position;
                            plan.Target = enemy;
                        }
                    }
                }
            }
            finally
            {
                actor.Coord = origin;
            }

            if (plan.Target != null)
            {
                plan.Score = bestScore;
                if (bestPosition != origin)
                {
                    plan.MoveBefore = bestPosition;
                }
                else
                {
                    // Attacking from where it stands leaves the Move free: fall back somewhere safer if there is one.
                    var retreat = SafestPosition(positions, origin, actor, enemies, rng);
                    if (retreat != origin)
                        plan.MoveAfter = retreat;
                }
            }
            else
            {
                var approach = ClosestToEnemies(grid, positions, origin, actor, units, enemies, rules, rng);
                if (approach != origin)
                    plan.MoveBefore = approach;
            }

            var final = plan.MoveAfter ?? plan.MoveBefore ?? origin;
            plan.Facing = FacingTowardNearest(final, enemies, actor.Facing);
            return plan;
        }

        /// <summary>
        /// Expected damage (hit chance × damage, capped at the target's HP), plus a knock-out bonus worth a share of the
        /// target's HP scaled by hit chance, plus a bonus for side or back attacks. Uses the actor's current tile.
        /// </summary>
        public static double AttackScore(Unit actor, Unit target)
        {
            double hit = CombatFormulas.HitChance(actor, target) / 100.0;
            int damage = CombatFormulas.AttackFactors(actor).Damage;
            double score = hit * Math.Min(damage, target.CurrentHp);
            if (damage >= target.CurrentHp)
                score += hit * target.Stats.MaxHp * KnockOutBonus;
            score += FacingRules.AngleOfAttack(target.Coord, target.Facing, actor.Coord) switch
            {
                AttackAngle.Back => BackBonus,
                AttackAngle.Side => SideBonus,
                _ => 0.0,
            };
            return score;
        }

        /// <summary>How many enemies could plausibly reach and hit this tile next turn: within their Move plus weapon range.</summary>
        public static int Threat(GridCoord position, Unit actor, IReadOnlyList<Unit> enemies)
        {
            int threat = 0;
            foreach (var enemy in enemies)
            {
                int reach = enemy.Stats.Move + (enemy.Weapon != null ? enemy.Weapon.MaxRange : 1);
                if (enemy.Coord.ManhattanDistance(position) <= reach)
                    threat++;
            }
            return threat;
        }

        private static GridCoord SafestPosition(List<GridCoord> positions, GridCoord origin, Unit actor, List<Unit> enemies, Random rng)
        {
            var best = origin;
            double bestThreat = Threat(origin, actor, enemies);
            foreach (var position in positions)
            {
                double threat = Threat(position, actor, enemies) + rng.NextDouble() * 0.01;
                if (threat < bestThreat - 0.5)
                {
                    bestThreat = threat;
                    best = position;
                }
            }
            return best;
        }

        /// <summary>
        /// The reachable tile with the shortest walking distance to any enemy. Distances come from a search outward
        /// from each enemy using the actor's Jump and blocking rules (an approximation of the walk back, which is
        /// symmetric for climbing and dropping).
        /// </summary>
        private static GridCoord ClosestToEnemies(BattleGrid grid, List<GridCoord> positions, GridCoord origin, Unit actor,
            IReadOnlyList<Unit> units, List<Unit> enemies, MovementRules rules, Random rng)
        {
            var profile = new MovementProfile(grid.Width * grid.Depth, actor.Stats.Jump);
            var occupancy = UnitOccupancy.For(actor, units);
            var fields = enemies.Select(e => Pathfinder.FindMovementRange(grid, e.Coord, profile,
                c => c == e.Coord ? Occupancy.Empty : occupancy(c), rules)).ToList();

            var best = origin;
            double bestDistance = Distance(origin);
            foreach (var position in positions)
            {
                double distance = Distance(position) + rng.NextDouble() * 0.01;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = position;
                }
            }
            return best;

            double Distance(GridCoord tile)
            {
                double nearest = double.MaxValue;
                for (int i = 0; i < enemies.Count; i++)
                {
                    int cost = fields[i].CostTo(tile);
                    // Unreachable by walking: fall back to straight-line distance, heavily penalised.
                    double d = cost >= 0 ? cost : 1000 + enemies[i].Coord.ManhattanDistance(tile);
                    nearest = Math.Min(nearest, d);
                }
                return nearest;
            }
        }

        private static Direction FacingTowardNearest(GridCoord from, List<Unit> enemies, Direction fallback)
        {
            if (enemies.Count == 0)
                return fallback;
            var nearest = enemies.OrderBy(e => e.Coord.ManhattanDistance(from)).ThenBy(e => e.Coord.Y).ThenBy(e => e.Coord.X).First();
            return nearest.Coord == from ? fallback : DirectionExtensions.Toward(from, nearest.Coord);
        }
    }
}
