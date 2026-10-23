using System;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Status;
using UnityEngine;

namespace FriendshipWarriors.Dev
{
    /// <summary>A status a test-scene unit starts the battle with: which team, which unit in its roster, which status.</summary>
    [Serializable]
    public struct StatusSeed
    {
        [SerializeField] private Team _team;
        [SerializeField, Min(0)] private int _rosterIndex;
        [SerializeField] private StatusEffect _status;

        public StatusSeed(Team team, int rosterIndex, StatusEffect status)
        {
            _team = team;
            _rosterIndex = rosterIndex;
            _status = status;
        }

        public Team Team => _team;
        public int RosterIndex => _rosterIndex;
        public StatusEffect Status => _status;
    }
}
