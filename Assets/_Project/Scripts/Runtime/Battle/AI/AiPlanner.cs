using System;
using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Battle.Abilities;
using FriendshipWarriors.Battle.Combat;
using FriendshipWarriors.Battle.Magic;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Grid.Pathfinding;
using FriendshipWarriors.Battle.Reactions;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;
using FriendshipWarriors.Units.Status;

namespace FriendshipWarriors.Battle.AI
{
    /// <summary>
    /// Chooses an AI unit's turn, in the spirit of FFT's enemy AI (whose exact logic is not public; these are our own
    /// weights). Every tile the unit can stand on this turn — including staying put — is paired with every enemy its
    /// Attack can reach from there, and scored by expected damage, a large bonus for a likely knock-out, a preference
    /// for side and back attacks, and a small cost for ending within reach of many enemies. With nothing to attack, it
    /// closes in on the nearest enemy by walking distance. It always ends facing the nearest enemy. Statuses are
    /// respected: no moving when immobilized, no attacking when disabled, and a panicking unit only runs as far from
    /// the enemy as it can (FFT Chicken). Attacks into a likely counter-strike cost their expected counter damage.
    /// Spells are weighed too (see <see cref="SpellScore"/>): from each tile, every learned spell it can afford is tried
    /// on every tile in range and scored by its effect on everyone in the area — hurting or hexing enemies, healing,
    /// shielding or reviving allies — with friendly fire counted against it and charged spells discounted because
    /// targets can step out of the area before they land. Casting ends the turn's movement, so a spell is cast from
    /// the tile the unit moves to. Turn-trade abilities, items and job skills are not used by the AI yet.
    /// </summary>
    public static class AiPlanner
    {
        /// <summary>Extra score for a likely knock-out, as a share of the target's max HP (scaled by hit chance).</summary>
        public const double KnockOutBonus = 1.0;
        public const double SideBonus = 2.0;
        public const double BackBonus = 4.0;
        public const double ThreatCost = 1.5;

        /// <summary>Damage to allies counts this much more than damage to enemies.</summary>
        public const double FriendlyFireWeight = 1.5;

        /// <summary>Score of landing one status, before hit chance (helpful on allies, harmful on enemies).</summary>
        public const double StatusValue = 8.0;

        /// <summary>Charged spells may miss as targets walk away: harmful ones ×0.8, helpful ones ×0.9.</summary>
        public const double ChargedHarmDiscount = 0.8, ChargedHelpDiscount = 0.9;

        /// <summary>Heals barely count until an ally is down this share of max HP.</summary>
        public const double HealThreshold = 0.25;

        /// <param name="units">Everyone still on the field (knocked-out units included; they block tiles).</param>
        /// <param name="rng">The battle's seeded random, used only to break ties so plans stay deterministic.</param>
        public static AiPlan Plan(BattleGrid grid, Unit actor, IReadOnlyList<Unit> units, MovementRules rules, Random rng)
        {
            var origin = actor.Coord;
            var stats = actor.Stats;
            var positions = new List<GridCoord> { origin };
            if (actor.CanMove)
            {
                var range = Pathfinder.FindMovementRange(grid, origin, new MovementProfile(stats.Move, stats.Jump),
                    UnitOccupancy.For(actor, units), rules);
                positions.AddRange(range.Destinations);
            }
            var enemies = units.Where(u => u.Team != actor.Team && !u.IsKnockedOut).ToList();

            var plan = new AiPlan();
            if (actor.HasStatus(StatusEffect.Panic))
            {
                var flee = FarthestFromEnemies(positions, origin, enemies, rng);
                if (flee != origin)
                    plan.MoveBefore = flee;
                plan.Facing = FacingTowardNearest(flee, enemies, actor.Facing);
                return plan;
            }
            // A disabled unit can't attack, but still walks toward the enemy.
            var attackable = actor.CanAct ? enemies : new List<Unit>();
            var spells = actor.CanAct ? CastableSpells(actor) : new List<AbilityDefinition>();
            double bestScore = double.NegativeInfinity;
            GridCoord bestPosition = origin;
            AbilityDefinition bestSpell = null;
            GridCoord bestSpellTile = default;
            try
            {
                foreach (var position in positions)
                {
                    actor.Coord = position;
                    foreach (var spell in spells)
                    {
                        foreach (var tile in AbilityRules.TargetTiles(grid, spell, actor))
                        {
                            double score = SpellScore(grid, actor, spell, tile, units) - Threat(position, actor, enemies) * ThreatCost
                                           + rng.NextDouble() * 0.01;
                            if (score > bestScore && score > 0.5)
                            {
                                bestScore = score;
                                bestPosition = position;
                                bestSpell = spell;
                                bestSpellTile = tile;
                                plan.Target = null;
                            }
                        }
                    }
                    var targets = new HashSet<GridCoord>(AttackRange.Targets(grid, actor));
                    foreach (var enemy in attackable)
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
                            bestSpell = null;
                        }
                    }
                }
            }
            finally
            {
                actor.Coord = origin;
            }

            if (bestSpell != null)
            {
                // Casting ends the turn's movement, so the spell goes off from where the unit moves to.
                plan.Score = bestScore;
                plan.Spell = bestSpell;
                plan.SpellTile = bestSpellTile;
                if (bestPosition != origin)
                    plan.MoveBefore = bestPosition;
            }
            else if (plan.Target != null)
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

        /// <summary>Learned spells (current job and secondary) the unit has the MP and the voice to cast now.</summary>
        public static List<AbilityDefinition> CastableSpells(Unit actor)
        {
            if (actor.HasStatus(StatusEffect.Silence))
                return new List<AbilityDefinition>();
            return AbilityCommand.LearnedActions(actor, actor.Job)
                .Concat(AbilityCommand.LearnedActions(actor, actor.SecondaryJob))
                .Where(a => a.IsSpell && a.MpCost <= actor.CurrentMp)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// A spell's value at <paramref name="tile"/> for <paramref name="actor"/> (cast from its current tile): the sum
        /// over everyone the area would catch now. Damage counts (capped at HP, plus a knock-out bonus) for enemies and
        /// against allies at <see cref="FriendlyFireWeight"/>; heals count the HP actually restored on hurt allies;
        /// statuses count <see cref="StatusValue"/> × hit chance, signed by who benefits; revives count the HP restored
        /// on downed allies. Charged spells are discounted, and MP spent costs a little.
        /// </summary>
        public static double SpellScore(BattleGrid grid, Unit actor, AbilityDefinition spell, GridCoord tile, IReadOnlyList<Unit> units)
        {
            var area = new HashSet<GridCoord>(SpellArea.Tiles(grid, tile, spell.AreaRadius, spell.AreaVertical));
            double help = 0, harm = 0;
            foreach (var unit in units)
            {
                if (!area.Contains(unit.Coord) || !AbilityRules.IsValidTarget(spell, actor, unit))
                    continue;
                bool ally = unit.Team == actor.Team;
                switch (spell.Effect)
                {
                    case AbilityEffect.Damage:
                    {
                        int damage = Math.Min(MagicFormulas.Damage(actor, unit, spell.Power), unit.CurrentHp);
                        double value = damage + (damage >= unit.CurrentHp && damage > 0 ? unit.Stats.MaxHp * KnockOutBonus : 0);
                        if (ally) harm -= value * FriendlyFireWeight; else harm += value;
                        break;
                    }
                    case AbilityEffect.RestoreHp:
                    {
                        int missing = unit.Stats.MaxHp - unit.CurrentHp;
                        int healed = Math.Min(MagicFormulas.Heal(actor, unit, spell.Power), missing);
                        double weight = missing >= unit.Stats.MaxHp * HealThreshold ? 1.0 : 0.3;
                        if (ally) help += healed * weight; else help -= healed;
                        break;
                    }
                    case AbilityEffect.AddStatus:
                    {
                        double chance = MagicFormulas.HitChance(actor, unit, spell.Power) / 100.0;
                        foreach (var status in spell.Inflicts)
                        {
                            if (unit.HasStatus(status))
                                continue;
                            bool harmful = StatusRules.IsHarmful(status);
                            double value = chance * StatusValue;
                            if (harmful == ally) harm -= value; else if (harmful) harm += value; else help += value;
                        }
                        break;
                    }
                    case AbilityEffect.Revive:
                    {
                        double chance = MagicFormulas.HitChance(actor, unit, spell.Power) / 100.0;
                        double value = chance * (unit.Stats.MaxHp * spell.RevivePercent / 100.0 + StatusValue * 2);
                        if (ally) help += value; else help -= value;
                        break;
                    }
                    case AbilityEffect.CureStatus:
                    {
                        double chance = MagicFormulas.HitChance(actor, unit, spell.Power) / 100.0;
                        int bad = spell.Cures.Count(unit.HasStatus);
                        if (ally) help += chance * bad * StatusValue * 0.75; else help -= chance * bad * StatusValue * 0.75;
                        break;
                    }
                }
            }
            if (spell.ChargeSpeed > 0)
            {
                harm *= ChargedHarmDiscount;
                help *= ChargedHelpDiscount;
            }
            return harm + help - spell.MpCost * 0.1;
        }

        /// <summary>
        /// Expected damage (hit chance × damage, capped at the target's HP), plus a knock-out bonus worth a share of the
        /// target's HP scaled by hit chance, plus a bonus for side or back attacks. Uses the actor's current tile.
        /// </summary>
        public static double AttackScore(Unit actor, Unit target)
        {
            double hit = CombatFormulas.HitChance(actor, target) / 100.0;
            int damage = CombatFormulas.AttackDamage(actor, target);
            double score = hit * Math.Min(damage, target.CurrentHp);
            score -= CounterRisk(actor, target, hit, damage);
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

        /// <summary>
        /// Expected damage from the target's counter-strike reaction: trigger chance (its Morale) × average counter
        /// damage, if the attack would land, the target would survive it, and the attacker stands within its reach.
        /// </summary>
        public static double CounterRisk(Unit actor, Unit target, double hit, int damage)
        {
            var reaction = target.Reaction;
            if (reaction == null || reaction.Reaction != ReactionEffect.CounterStrike || !target.CanReact || damage >= target.CurrentHp)
                return 0.0;
            if (actor.Coord.ManhattanDistance(target.Coord) > Math.Max(1, reaction.MaxRange))
                return 0.0;
            double averageMultiplier = (reaction.RandomMultiplier.x + reaction.RandomMultiplier.y) / 2.0;
            double counter = CombatFormulas.PhysicalDamage(target, actor, (int)Math.Round(target.Stats.PhysicalAttack * averageMultiplier), melee: true);
            return hit * ReactionResolver.TriggerChance(target) * Math.Min(counter, actor.CurrentHp);
        }

        /// <summary>FFT's Chicken runs: the reachable tile farthest from the nearest enemy.</summary>
        private static GridCoord FarthestFromEnemies(List<GridCoord> positions, GridCoord origin, List<Unit> enemies, Random rng)
        {
            if (enemies.Count == 0)
                return origin;
            var best = origin;
            double bestDistance = Nearest(origin);
            foreach (var position in positions)
            {
                double distance = Nearest(position) + rng.NextDouble() * 0.01;
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    best = position;
                }
            }
            return best;

            double Nearest(GridCoord tile) => enemies.Min(e => e.Coord.ManhattanDistance(tile));
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
