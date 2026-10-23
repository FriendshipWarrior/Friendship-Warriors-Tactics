using System.Collections.Generic;
using FriendshipWarriors.Units.View.PaperDoll;
using UnityEngine;

namespace FriendshipWarriors.Units.View.Sheets
{
    /// <summary>
    /// What a unit's sheet colours become: outfit groups take the job colour, accent groups the team colour, hair groups
    /// a hair colour picked from the unit's name (stable across runs, like paper dolls).
    /// </summary>
    public readonly struct SheetPalette
    {
        public readonly Dictionary<int, Color32> Map;

        /// <summary>Identifies this colour set for caching.</summary>
        public readonly string Key;

        private SheetPalette(Dictionary<int, Color32> map, string key)
        {
            Map = map;
            Key = key;
        }

        public static SheetPalette For(Unit unit, UnitSheet sheet)
        {
            var team = unit.Team == Team.Player ? sheet.PlayerTint : sheet.EnemyTint;
            int hair = HairIndex(unit.Name, sheet.HairColors.Count);
            Color? hairColor = hair >= 0 ? sheet.HairColors[hair] : null;
            var map = BuildMap(sheet.Recolors, unit.Job.PlaceholderColor, team, hairColor);
            string key = $"{ColorUtility.ToHtmlStringRGB(unit.Job.PlaceholderColor)}/{ColorUtility.ToHtmlStringRGB(team)}/{hair}";
            return new SheetPalette(map, key);
        }

        /// <summary>The hair colour a unit with this name gets, or -1 to keep the painted hair.</summary>
        public static int HairIndex(string unitName, int hairColorCount) =>
            hairColorCount > 0 ? (int)((DollPalette.StableHash(unitName) / 7u) % (uint)hairColorCount) : -1;

        /// <summary>Source colour → replacement for every group; a hair group is left alone when <paramref name="hair"/> is null.</summary>
        public static Dictionary<int, Color32> BuildMap(IReadOnlyList<UnitSheet.RecolorGroup> groups, Color job, Color team, Color? hair)
        {
            var map = new Dictionary<int, Color32>();
            foreach (var group in groups)
            {
                if (group == null)
                    continue;
                switch (group.Role)
                {
                    case RecolorRole.JobOutfit:
                        SheetRecolor.AddTo(map, group.Colors, job);
                        break;
                    case RecolorRole.TeamAccent:
                        SheetRecolor.AddTo(map, group.Colors, team);
                        break;
                    case RecolorRole.Hair when hair.HasValue:
                        SheetRecolor.AddTo(map, group.Colors, hair.Value);
                        break;
                }
            }
            return map;
        }
    }
}
