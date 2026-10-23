using System;
using System.Collections.Generic;
using UnityEngine;

namespace FriendshipWarriors.Battle.Objectives
{
    /// <summary>
    /// Who fights and what wins a battle. Quick play uses the standard setup: 5 v 5, defeat all enemies. Campaign and
    /// special levels author their own: team sizes, rosters (with keys) and an objective. Each side fields the first
    /// <see cref="PlayerTeamSize"/> / <see cref="EnemyTeamSize"/> roster entries, as far as the map's deployment zone
    /// allows.
    /// </summary>
    [CreateAssetMenu(menuName = "Friendship Warriors/Battle Setup", fileName = "BattleSetup")]
    public sealed class BattleSetup : ScriptableObject
    {
        /// <summary>A standard match: five a side (user decision).</summary>
        public const int StandardTeamSize = 5;

        [SerializeField] private string _displayName = "Standard";
        [SerializeField, Range(1, 12)] private int _playerTeamSize = StandardTeamSize;
        [SerializeField, Range(1, 12)] private int _enemyTeamSize = StandardTeamSize;
        [SerializeField, Range(1, 99)] private int _defaultLevel = 5;
        [SerializeField] private RosterEntry[] _playerRoster = Array.Empty<RosterEntry>();
        [SerializeField] private RosterEntry[] _enemyRoster = Array.Empty<RosterEntry>();
        [SerializeField] private BattleObjective _objective = BattleObjective.Standard;

        public string DisplayName => _displayName;
        public int PlayerTeamSize => _playerTeamSize;
        public int EnemyTeamSize => _enemyTeamSize;
        public int DefaultLevel => _defaultLevel;
        public IReadOnlyList<RosterEntry> PlayerRoster => _playerRoster;
        public IReadOnlyList<RosterEntry> EnemyRoster => _enemyRoster;
        public BattleObjective Objective => _objective ?? BattleObjective.Standard;

        /// <summary>How many units a side actually fields: its team size, capped by its roster and the map's zone.</summary>
        public static int Fielded(int teamSize, int rosterCount, int deploymentTiles) =>
            Math.Max(0, Math.Min(teamSize, Math.Min(rosterCount, deploymentTiles)));

        public void Define(string displayName, int playerTeamSize, int enemyTeamSize, int defaultLevel,
            RosterEntry[] playerRoster, RosterEntry[] enemyRoster, BattleObjective objective)
        {
            _displayName = displayName;
            _playerTeamSize = playerTeamSize;
            _enemyTeamSize = enemyTeamSize;
            _defaultLevel = defaultLevel;
            _playerRoster = playerRoster;
            _enemyRoster = enemyRoster;
            _objective = objective;
        }
    }
}
