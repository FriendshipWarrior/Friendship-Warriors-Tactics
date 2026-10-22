using System;
using System.Collections.Generic;
using FriendshipWarriors.Grid;
using FriendshipWarriors.Units.Abilities;
using FriendshipWarriors.Units.Equipment;

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

        /// <summary>Turns a knocked-out unit lasts before it is lost for good (FFT's count of 3; recalled).</summary>
        public const int DeathCounterStart = 3;

        private readonly Dictionary<JobDefinition, int> _proficiency = new();
        private readonly Dictionary<JobDefinition, int> _spentProficiency = new();
        private readonly HashSet<AbilityDefinition> _learned = new();
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

        /// <summary>FFT's Charge Time: fills by Speed every clock tick; the unit takes a turn at 100.</summary>
        public int ChargeTime { get; set; }

        /// <summary>The other job whose learned action abilities appear in the Act menu.</summary>
        public JobDefinition SecondaryJob { get; private set; }
        public AbilityDefinition Reaction { get; private set; }
        public AbilityDefinition Support { get; private set; }
        public AbilityDefinition Movement { get; private set; }

        public IReadOnlyCollection<AbilityDefinition> LearnedAbilities => _learned;

        private readonly EquipmentDefinition[] _equipment = new EquipmentDefinition[5];

        public EquipmentDefinition Equipped(EquipmentSlot slot) => _equipment[(int)slot];

        /// <summary>The main-hand weapon, or null for bare hands.</summary>
        public EquipmentDefinition Weapon => Equipped(EquipmentSlot.MainHand);

        /// <summary>The off-hand shield, if that's what the off hand holds.</summary>
        public EquipmentDefinition Shield =>
            Equipped(EquipmentSlot.OffHand) is { } offHand && offHand.Category == EquipmentCategory.Shield ? offHand : null;

        public EquipmentDefinition Head => Equipped(EquipmentSlot.Head);
        public EquipmentDefinition Body => Equipped(EquipmentSlot.Body);
        public EquipmentDefinition Accessory => Equipped(EquipmentSlot.Accessory);

        /// <summary>The weapon family used for attacks; bare hands without a weapon.</summary>
        public WeaponType WeaponType => Weapon != null ? Weapon.WeaponType : WeaponType.Unarmed;

        public bool IsKnockedOut => CurrentHp <= 0;

        /// <summary>Turns left before a knocked-out unit is gone. Only meaningful while knocked out.</summary>
        public int DeathCounter { get; private set; }

        /// <summary>The death counter ran out; the unit has left the battle.</summary>
        public bool IsGone => IsKnockedOut && DeathCounter <= 0;

        /// <summary>
        /// Equips an item the current job allows into its slot, replacing what was there. A two-handed weapon empties the
        /// off hand, and nothing can go in the off hand while one is held. Current HP and MP are capped at the new
        /// maximums.
        /// </summary>
        public bool TryEquipItem(EquipmentDefinition item)
        {
            if (item == null || !Job.CanEquip(item))
                return false;

            switch (item.Category)
            {
                case EquipmentCategory.Weapon:
                    _equipment[(int)EquipmentSlot.MainHand] = item;
                    if (item.TwoHanded)
                        _equipment[(int)EquipmentSlot.OffHand] = null;
                    break;
                case EquipmentCategory.Shield:
                    if (Weapon != null && Weapon.TwoHanded)
                        return false;
                    _equipment[(int)EquipmentSlot.OffHand] = item;
                    break;
                case EquipmentCategory.Head: _equipment[(int)EquipmentSlot.Head] = item; break;
                case EquipmentCategory.Body: _equipment[(int)EquipmentSlot.Body] = item; break;
                default: _equipment[(int)EquipmentSlot.Accessory] = item; break;
            }
            CapHpAndMp();
            return true;
        }

        /// <summary>Full HP and MP and no knock-out, as between battles.</summary>
        public void RestoreFully()
        {
            var stats = Stats;
            CurrentHp = stats.MaxHp;
            CurrentMp = stats.MaxMp;
            DeathCounter = 0;
        }

        public void Unequip(EquipmentSlot slot)
        {
            _equipment[(int)slot] = null;
            CapHpAndMp();
        }

        private void CapHpAndMp()
        {
            var stats = Stats;
            CurrentHp = Math.Min(CurrentHp, stats.MaxHp);
            CurrentMp = Math.Min(CurrentMp, stats.MaxMp);
        }

        private int GearBonus(Func<EquipmentDefinition, int> bonus)
        {
            int total = 0;
            foreach (var item in _equipment)
            {
                if (item != null)
                    total += bonus(item);
            }
            return total;
        }

        /// <summary>Removes HP (never below 0); dropping to 0 knocks the unit out and starts its death counter.</summary>
        public int TakeDamage(int amount)
        {
            if (IsKnockedOut || amount <= 0)
                return 0;
            int dealt = Math.Min(amount, CurrentHp);
            CurrentHp -= dealt;
            if (CurrentHp == 0)
                DeathCounter = DeathCounterStart;
            return dealt;
        }

        /// <summary>Counts down a knocked-out unit's remaining turns. Returns true once it is gone.</summary>
        public bool TickDeathCounter()
        {
            if (!IsKnockedOut || DeathCounter <= 0)
                return IsGone;
            DeathCounter--;
            return IsGone;
        }

        public UnitStats Stats
        {
            get
            {
                var m = Job.Multipliers;
                // Headgear and body gear add flat HP/MP on top of the job-scaled values, as in FFT.
                return new UnitStats(
                    Math.Min(StatFormulas.MaxHpOrMp, StatFormulas.Shown(Raw.Hp, m.Hp, StatFormulas.MaxHpOrMp) + GearBonus(i => i.HpBonus)),
                    Math.Min(StatFormulas.MaxHpOrMp, StatFormulas.Shown(Raw.Mp, m.Mp, StatFormulas.MaxHpOrMp) + GearBonus(i => i.MpBonus)),
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

        /// <summary>Total Proficiency (Prof) ever earned in a job. Job level follows this, spent or not, as in FFT.</summary>
        public int Proficiency(JobDefinition job) => _proficiency.TryGetValue(job, out int prof) ? prof : 0;

        /// <summary>Prof earned in a job that hasn't been spent on its abilities yet.</summary>
        public int AvailableProficiency(JobDefinition job) =>
            Proficiency(job) - (_spentProficiency.TryGetValue(job, out int spent) ? spent : 0);

        public bool HasLearned(AbilityDefinition ability) => _learned.Contains(ability);

        /// <summary>Spends Prof from the job that teaches the ability. Fails if already learned or unaffordable.</summary>
        public bool TryLearn(AbilityDefinition ability, JobDefinition fromJob)
        {
            if (ability == null || fromJob == null || !fromJob.Teaches(ability) || HasLearned(ability))
                return false;
            if (AvailableProficiency(fromJob) < ability.LearnCost)
                return false;
            _spentProficiency[fromJob] = (_spentProficiency.TryGetValue(fromJob, out int spent) ? spent : 0) + ability.LearnCost;
            _learned.Add(ability);
            return true;
        }

        /// <summary>
        /// Sets the secondary command: any other job with at least one learned action ability, or null to clear.
        /// </summary>
        public bool TrySetSecondary(JobDefinition job)
        {
            if (job == null)
            {
                SecondaryJob = null;
                return true;
            }
            if (job == Job)
                return false;
            foreach (var ability in job.Abilities)
            {
                if (ability != null && ability.Kind == AbilityKind.Action && HasLearned(ability))
                {
                    SecondaryJob = job;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Equips a learned reaction, support or movement ability into its slot.</summary>
        public bool TryEquip(AbilityDefinition ability)
        {
            if (ability == null || !HasLearned(ability))
                return false;
            switch (ability.Kind)
            {
                case AbilityKind.Reaction: Reaction = ability; return true;
                case AbilityKind.Support: Support = ability; return true;
                case AbilityKind.Movement: Movement = ability; return true;
                default: return false;
            }
        }

        public void Unequip(AbilityKind slot)
        {
            switch (slot)
            {
                case AbilityKind.Reaction: Reaction = null; break;
                case AbilityKind.Support: Support = null; break;
                case AbilityKind.Movement: Movement = null; break;
                case AbilityKind.Action: SecondaryJob = null; break;
            }
        }

        /// <summary>Whether an equipped ability grants a turn trade.</summary>
        public bool HasTurnTrade(TurnTrade trade) =>
            trade != TurnTrade.None && (Grants(Reaction, trade) || Grants(Support, trade) || Grants(Movement, trade));

        // Explicit null checks: '?.' skips Unity's check for destroyed assets.
        private static bool Grants(AbilityDefinition ability, TurnTrade trade) => ability != null && ability.TurnTrade == trade;

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
            if (SecondaryJob == job)
                SecondaryJob = null;
            // Equipment the new job can't use comes off, as in FFT.
            for (int i = 0; i < _equipment.Length; i++)
            {
                if (_equipment[i] != null && !job.CanEquip(_equipment[i]))
                    _equipment[i] = null;
            }
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
