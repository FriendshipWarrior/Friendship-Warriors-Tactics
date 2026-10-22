using System;
using System.Collections.Generic;
using UnityEngine;

namespace FriendshipWarriors.Units
{
    /// <summary>Every job in the game, in menu order.</summary>
    [CreateAssetMenu(menuName = "Friendship Warriors/Job Catalog", fileName = "JobCatalog")]
    public sealed class JobCatalog : ScriptableObject
    {
        [SerializeField] private JobDefinition[] _jobs = Array.Empty<JobDefinition>();

        public IReadOnlyList<JobDefinition> Jobs => _jobs;

        public void SetJobs(JobDefinition[] jobs) => _jobs = jobs;
    }
}
