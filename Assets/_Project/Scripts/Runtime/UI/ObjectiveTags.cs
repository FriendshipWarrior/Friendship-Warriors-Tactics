using System.Collections.Generic;
using FriendshipWarriors.Battle.Objectives;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.View;
using UnityEngine;

namespace FriendshipWarriors.UI
{
    /// <summary>The role tags over units an objective names: a red TARGET over the enemy to defeat, a green PROTECT
    /// over player units that must not be lost. Shared by the formation phase and the battle.</summary>
    public static class ObjectiveTags
    {
        public static readonly Color TargetColor = new(1f, 0.35f, 0.3f);
        public static readonly Color ProtectColor = new(0.45f, 1f, 0.55f);

        public static void Apply(IEnumerable<UnitView> views, BattleObjective objective)
        {
            foreach (var view in views)
            {
                if (objective.IsTarget(view.Unit.Key) && view.Unit.Team == Team.Enemy)
                    view.SetRole("TARGET", TargetColor);
                else if (objective.IsProtected(view.Unit.Key) && view.Unit.Team == Team.Player)
                    view.SetRole("PROTECT", ProtectColor);
                else
                    view.SetRole(null, Color.white);
            }
        }
    }
}
