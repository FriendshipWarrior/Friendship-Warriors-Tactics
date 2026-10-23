using System.Collections.Generic;
using System.Linq;

namespace FriendshipWarriors.Battle.Objectives
{
    /// <summary>Checks a battle setup for references and sizes that would break or spoil the battle.</summary>
    public static class BattleSetupValidator
    {
        public static List<string> Validate(BattleSetup setup)
        {
            var issues = new List<string>();
            if (setup == null)
            {
                issues.Add("No battle setup.");
                return issues;
            }

            CheckRoster(setup.PlayerRoster, setup.PlayerTeamSize, "Player", issues);
            CheckRoster(setup.EnemyRoster, setup.EnemyTeamSize, "Enemy", issues);

            var objective = setup.Objective;
            var fieldedEnemies = setup.EnemyRoster.Take(setup.EnemyTeamSize).ToList();
            var fieldedPlayers = setup.PlayerRoster.Take(setup.PlayerTeamSize).ToList();
            if (objective.Victory == VictoryCondition.DefeatTarget)
            {
                if (string.IsNullOrEmpty(objective.TargetKey))
                    issues.Add("Defeat Target needs a target key.");
                else if (!fieldedEnemies.Any(e => e.Key == objective.TargetKey))
                    issues.Add($"Target \"{objective.TargetKey}\" isn't among the fielded enemies.");
            }
            foreach (var key in objective.ProtectedKeys)
            {
                if (string.IsNullOrEmpty(key))
                    issues.Add("A protected key is empty.");
                else if (!fieldedPlayers.Any(p => p.Key == key))
                    issues.Add($"Protected unit \"{key}\" isn't among the fielded Player units.");
            }
            return issues;
        }

        private static void CheckRoster(IReadOnlyList<RosterEntry> roster, int teamSize, string side, List<string> issues)
        {
            if (roster.Count < teamSize)
                issues.Add($"{side} roster has {roster.Count} of {teamSize} units.");
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i] == null || roster[i].Job == null)
                    issues.Add($"{side} roster entry {i} has no job.");
            }
            var keys = roster.Where(r => r != null && !string.IsNullOrEmpty(r.Key)).GroupBy(r => r.Key).Where(g => g.Count() > 1);
            foreach (var duplicate in keys)
                issues.Add($"{side} roster key \"{duplicate.Key}\" is used more than once.");
        }
    }
}
