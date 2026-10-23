using System.Collections.Generic;
using System.Linq;
using FriendshipWarriors.Battle.Combat;
using FriendshipWarriors.Grid;
using FriendshipWarriors.UI;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Abilities;

namespace FriendshipWarriors.Battle.Abilities
{
    /// <summary>
    /// The Act menu's job commands (FFT's primary and secondary action sets): lists a job's learned action abilities,
    /// says which can be used now, and carries one out — spending MP or an item, resolving it, pushing the target back
    /// on knockback, and paying out EXP and Prof. Kept apart from <see cref="BattleController"/>, which only shows
    /// menus and targets.
    /// </summary>
    public sealed class AbilityCommand
    {
        public const string IdPrefix = "ability:";

        private readonly List<AbilityDefinition> _listed = new();

        /// <summary>The job whose abilities are currently listed.</summary>
        public JobDefinition Job { get; private set; }

        /// <summary>Learned action abilities a job gives, in the job's order.</summary>
        public static List<AbilityDefinition> LearnedActions(Unit user, JobDefinition job) =>
            job == null ? new List<AbilityDefinition>()
                : job.Abilities.Where(a => a != null && a.Kind == AbilityKind.Action && user.HasLearned(a)).ToList();

        /// <summary>Fills <paramref name="menu"/> with the job's learned actions; unusable ones are disabled with the reason.</summary>
        public void BuildMenu(MenuModel menu, JobDefinition job, Unit user, BattleGrid grid, IEnumerable<Unit> units, Inventory inventory)
        {
            Job = job;
            _listed.Clear();
            _listed.AddRange(LearnedActions(user, job));
            var unitList = units.ToList();
            menu.Title = job.CommandName;
            menu.SetItems(_listed.Select((ability, i) =>
            {
                string reason = AbilityRules.Unavailable(grid, ability, user, unitList, inventory);
                string label = ability.IsItem ? $"{ability.DisplayName}  x{(inventory != null ? inventory.Count(ability) : 0)}" : ability.DisplayName;
                if (ability.MpCost > 0)
                    label += $"  {ability.MpCost} MP";
                if (reason != null)
                    label += $"  ({reason})";
                return new MenuItem(IdPrefix + i, label, reason == null);
            }));
        }

        public AbilityDefinition Listed(string id) =>
            id != null && id.StartsWith(IdPrefix) && int.TryParse(id.Substring(IdPrefix.Length), out int i) && i < _listed.Count
                ? _listed[i]
                : null;

        /// <summary>What happened when an ability was used.</summary>
        public readonly struct Result
        {
            public readonly AbilityOutcome Outcome;
            public readonly GridCoord? KnockedTo;
            public readonly string Message;

            public Result(AbilityOutcome outcome, GridCoord? knockedTo, string message)
            {
                Outcome = outcome;
                KnockedTo = knockedTo;
                Message = message;
            }
        }

        /// <summary>
        /// Uses <paramref name="ability"/> on <paramref name="target"/>: pays its cost, resolves and applies it, rolls
        /// knockback, and grants rewards. Damage abilities give EXP by level difference; everything else gives the
        /// user's support EXP. Prof goes to the user, a quarter to standing allies.
        /// </summary>
        public static Result Execute(AbilityDefinition ability, Unit user, Unit target, BattleGrid grid, IEnumerable<Unit> units,
            Inventory inventory, System.Random rng)
        {
            var unitList = units.ToList();
            if (ability.IsItem)
                inventory.TryConsume(ability);
            user.TrySpendMp(ability.MpCost);

            var outcome = AbilityResolver.Apply(AbilityResolver.Resolve(ability, user, target, rng), target);

            GridCoord? knockedTo = null;
            if (outcome.Hit && ability.Knockback && !target.IsKnockedOut && Knockback.Roll(rng, user, target))
            {
                knockedTo = Knockback.Destination(grid, user.Coord, target.Coord, c => unitList.Any(u => u != target && u.Coord == c));
                if (knockedTo is { } landing)
                    target.Coord = landing;
            }

            if (outcome.Hit)
            {
                user.GainExperience(ability.IsSupportAction
                    ? BattleRewards.SupportExperience(user)
                    : BattleRewards.Experience(user, target));
                int prof = BattleRewards.Proficiency(user);
                user.GainProficiency(prof);
                foreach (var ally in unitList.Where(u => u != user && u.Team == user.Team && !u.IsKnockedOut))
                    ally.GainProficiency(BattleRewards.SpilloverProficiency(prof));
            }

            string who = target == user ? "" : $" > {target.Name}";
            string message = $"{user.Name}: {ability.DisplayName}{who}: {outcome}" +
                             (knockedTo != null ? ", knocked back" : "") +
                             (target.IsKnockedOut && outcome.Damage > 0 ? $"  {target.Name} is down!" : "");
            return new Result(outcome, knockedTo, message);
        }
    }
}
