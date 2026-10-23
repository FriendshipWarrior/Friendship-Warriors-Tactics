using System.Collections.Generic;
using FriendshipWarriors.Units.Status;

namespace FriendshipWarriors.Feedback
{
    /// <summary>
    /// What changed between two looks at a unit's statuses, for popups. A knock-out clears every status, which isn't
    /// news, so nothing counts as lifted on the look where the unit went down. Charging is left out (the cast pose
    /// shows it). Results are in enum order, so they're stable.
    /// </summary>
    public static class StatusDiff
    {
        public static void Compare(IReadOnlyCollection<StatusEffect> before, IReadOnlyCollection<StatusEffect> after,
            bool knockedOutNow, List<StatusEffect> gained, List<StatusEffect> lifted)
        {
            gained.Clear();
            lifted.Clear();
            for (var status = (StatusEffect)0; status <= StatusEffect.Charging; status++)
            {
                if (!FeedbackStyle.ShowsPopup(status))
                    continue;
                bool had = Contains(before, status), has = Contains(after, status);
                if (has && !had)
                    gained.Add(status);
                else if (had && !has && !knockedOutNow)
                    lifted.Add(status);
            }
        }

        private static bool Contains(IReadOnlyCollection<StatusEffect> set, StatusEffect status)
        {
            foreach (var s in set)
            {
                if (s == status)
                    return true;
            }
            return false;
        }
    }
}
