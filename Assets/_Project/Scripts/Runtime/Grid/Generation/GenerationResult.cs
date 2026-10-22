using System.Collections.Generic;
using FriendshipWarriors.Grid.Validation;

namespace FriendshipWarriors.Grid.Generation
{
    public sealed class GenerationResult
    {
        public GenerationResult(bool succeeded, int seed, int attempts, IReadOnlyList<MapIssue> issues)
        {
            Succeeded = succeeded;
            Seed = seed;
            Attempts = attempts;
            Issues = issues;
        }

        /// <summary>True when the map passed validation with no errors (warnings are allowed).</summary>
        public bool Succeeded { get; }

        /// <summary>The seed passed in; regenerating with it reproduces the same map.</summary>
        public int Seed { get; }

        public int Attempts { get; }

        /// <summary>Validation results for the map that was produced.</summary>
        public IReadOnlyList<MapIssue> Issues { get; }
    }
}
