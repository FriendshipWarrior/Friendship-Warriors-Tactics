using System;
using FriendshipWarriors.Units;
using UnityEngine;

namespace FriendshipWarriors.Battle.Objectives
{
    /// <summary>One unit a battle setup fields: its job, optional name and level, and the key objectives use.</summary>
    [Serializable]
    public sealed class RosterEntry
    {
        [SerializeField] private JobDefinition _job;
        [Tooltip("Shown name; empty uses \"<Job> <letter>\".")]
        [SerializeField] private string _displayName = "";
        [Tooltip("Stable key objectives refer to (e.g. \"commander\"). Leave empty if nothing needs to.")]
        [SerializeField] private string _key = "";
        [Tooltip("0 uses the setup's default level.")]
        [SerializeField, Range(0, 99)] private int _level;

        public RosterEntry() { }

        public RosterEntry(JobDefinition job, string key = "", string displayName = "", int level = 0)
        {
            _job = job;
            _key = key ?? "";
            _displayName = displayName ?? "";
            _level = level;
        }

        public JobDefinition Job => _job;
        public string DisplayName => _displayName;
        public string Key => _key;
        public int Level => _level;
    }
}
