using System;
using UnityEngine;

namespace FriendshipWarriors.Units
{
    /// <summary>A job level a unit must reach in another job before this one unlocks.</summary>
    [Serializable]
    public struct JobRequirement
    {
        [SerializeField] private JobDefinition _job;
        [SerializeField, Range(1, 8)] private int _level;

        public JobRequirement(JobDefinition job, int level)
        {
            _job = job;
            _level = level;
        }

        public JobDefinition Job => _job;
        public int Level => _level;
    }
}
