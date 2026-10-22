using System;
using System.Collections.Generic;
using FriendshipWarriors.Grid;

namespace FriendshipWarriors.Units
{
    /// <summary>
    /// One combatant: identity, level and EXP, raw stats, Proficiency per job, Commendations, current HP/MP, and
    /// where it stands.
    /// Plain C# so battle rules can test it without a scene; <c>UnitView</c> presents it.
    /// </summary>
    public sealed class Unit
    {
        public const int MaxLevel = 99;

        private readonly Dictionary<JobDefinition, int> _proficiency = new();
        private readonly List<Commendation> _commendations = new();

        public Unit(string name, Team team, JobDefinition job, RawStats raw)
        {
            Name = name;
            Team = team;
            Job = job ?? throw new ArgumentNullException(nameof(job));
            Raw = raw;
            Level = 1;
            var stats = Stats;
            CurrentHp = stats.MaxHp;
            CurrentMp = stats.MaxMp;
        }

        public string Name { get; }
        public Team Team { get; }
        public int Level { get; private set; }

        /// <summary>EXP earned toward the next level; see <see cref="ExperienceCurve"/>.</summary>
        public int Experience { get; private set; }
        public RawStats Raw { get; private set; }
        public JobDefinition Job { get; private set; }
        public int CurrentHp { get; private set; }
        public int CurrentMp { get; private set; }
        public GridCoord Coord { get; set; }
        public Direction Facing { get; set; }

        public UnitStats Stats
        {
            get
            {
                var m = Job.Multipliers;
                return new UnitStats(
                    StatFormulas.Shown(Raw.Hp, m.Hp, StatFormulas.MaxHpOrMp),
                    StatFormulas.Shown(Raw.Mp, m.Mp, StatFormulas.MaxHpOrMp),
                    StatFormulas.Shown(Raw.Speed, m.Speed, StatFormulas.MaxOtherStat),
                    StatFormulas.Shown(Raw.PhysicalAttack, m.PhysicalAttack, StatFormulas.MaxOtherStat),
                    StatFormulas.Shown(Raw.MagicAttack, m.MagicAttack, StatFormulas.MaxOtherStat),
                    Job.Move,
                    Job.Jump,
                    Job.ClassEvasion);
            }
        }

        public IReadOnlyList<Commendation> Commendations => _commendations;

        /// <summary>Adds EXP and returns how many levels were gained. Growth follows the current job.</summary>
        public int GainExperience(int amount)
        {
            int gained = 0;
            Experience += amount;
            while (Level < MaxLevel && Experience >= ExperienceCurve.ToNextLevel(Level))
            {
                Experience -= ExperienceCurve.ToNextLevel(Level);
                LevelUp();
                gained++;
            }
            if (Level == MaxLevel)
                Experience = 0;
            return gained;
        }

        /// <summary>Total Proficiency (Prof) earned in a job.</summary>
        public int Proficiency(JobDefinition job) => _proficiency.TryGetValue(job, out int prof) ? prof : 0;

        public int JobLevel(JobDefinition job) => JobLevels.ForProficiency(Proficiency(job));

        /// <summary>A job is mastered at its top job level. (FFT masters a job by learning every ability; we have none yet.)</summary>
        public bool HasMastered(JobDefinition job) => JobLevel(job) >= JobLevels.MaxLevel;

        /// <summary>
        /// Adds Prof to a job (the current one if none is given). Mastering the job awards a Commendation, once.
        /// </summary>
        public void GainProficiency(int amount, JobDefinition job = null)
        {
            job ??= Job;
            bool wasMastered = HasMastered(job);
            _proficiency[job] = Proficiency(job) + amount;
            if (!wasMastered && HasMastered(job))
                _commendations.Add(new Commendation(CommendationSource.JobMastery, $"{job.DisplayName} Mastery", job));
        }

        /// <summary>Awards a Commendation for a field challenge.</summary>
        public void AwardCommendation(string title) =>
            _commendations.Add(new Commendation(CommendationSource.FieldChallenge, title));

        public bool MeetsRequirements(JobDefinition job)
        {
            foreach (var requirement in job.Requirements)
            {
                if (requirement.Job != null && JobLevel(requirement.Job) < requirement.Level)
                    return false;
            }
            return true;
        }

        /// <summary>Switches job if unlocked. Current HP and MP carry over, capped at the new maximums.</summary>
        public bool TryChangeJob(JobDefinition job)
        {
            if (job == null || !MeetsRequirements(job))
                return false;
            Job = job;
            var stats = Stats;
            CurrentHp = Math.Min(CurrentHp, stats.MaxHp);
            CurrentMp = Math.Min(CurrentMp, stats.MaxMp);
            return true;
        }

        private void LevelUp()
        {
            var before = Stats;
            var g = Job.Growth;
            Raw = new RawStats(
                StatFormulas.Grow(Raw.Hp, g.Hp, Level),
                StatFormulas.Grow(Raw.Mp, g.Mp, Level),
                StatFormulas.Grow(Raw.Speed, g.Speed, Level),
                StatFormulas.Grow(Raw.PhysicalAttack, g.PhysicalAttack, Level),
                StatFormulas.Grow(Raw.MagicAttack, g.MagicAttack, Level));
            Level++;

            // Whatever the maximums grew by is added to the current values, so a level-up never feels like a loss.
            var after = Stats;
            CurrentHp = Math.Min(after.MaxHp, CurrentHp + after.MaxHp - before.MaxHp);
            CurrentMp = Math.Min(after.MaxMp, CurrentMp + after.MaxMp - before.MaxMp);
        }

        public override string ToString() => $"{Name} ({Job.DisplayName} Lv{Level}, {Team})";
    }
}
