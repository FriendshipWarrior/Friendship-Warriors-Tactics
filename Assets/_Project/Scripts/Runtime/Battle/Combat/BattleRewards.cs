using System;
using FriendshipWarriors.Units;

namespace FriendshipWarriors.Battle.Combat
{
    /// <summary>FFT's per-action rewards.</summary>
    public static class BattleRewards
    {
        /// <summary>
        /// EXP for an action on <paramref name="target"/>: 10 + the target's level − the actor's level, at least 1 (FFT,
        /// recalled). Note: tuned for FFT's flat 100 EXP per level, so it levels slowly on our exponential curve.
        /// </summary>
        public static int Experience(Unit actor, Unit target) => Math.Max(1, 10 + target.Level - actor.Level);

        /// <summary>Prof for a successful action: 8 + 2 × job level + level / 4 (FFT's JP formula).</summary>
        public static int Proficiency(Unit actor) => 8 + 2 * actor.JobLevel(actor.Job) + actor.Level / 4;

        /// <summary>Allies on the field get a quarter of an action's Prof in their own current job (FFT).</summary>
        public static int SpilloverProficiency(int earned) => earned / 4;
    }
}
