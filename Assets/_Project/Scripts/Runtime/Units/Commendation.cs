namespace FriendshipWarriors.Units
{
    /// <summary>A recognition earned by a unit, by mastering a job or completing a field challenge.</summary>
    public readonly struct Commendation
    {
        public readonly CommendationSource Source;
        public readonly string Title;

        /// <summary>The mastered job, for <see cref="CommendationSource.JobMastery"/>; otherwise null.</summary>
        public readonly JobDefinition Job;

        public Commendation(CommendationSource source, string title, JobDefinition job = null)
        {
            Source = source;
            Title = title;
            Job = job;
        }

        public override string ToString() => Title;
    }
}
