using System.Collections.Generic;
using System.IO;
using FriendshipWarriors.Units;
using FriendshipWarriors.Units.Equipment;
using FriendshipWarriors.Units.View.PaperDoll;
using UnityEditor;
using UnityEngine;

namespace FriendshipWarriors.Editor.Art
{
    /// <summary>
    /// Renders a contact sheet of composed units (every job in typical gear, both teams, a spread of frames) to a PNG,
    /// for checking the parts read clearly. Friendship Warriors → Art → Save Paper-Doll Preview.
    /// </summary>
    public static class PaperDollPreview
    {
        private static readonly string[] GearOrder =
        {
            "ServiceSaber", "ServiceRifle", "AetherRod", "WardStaff", "FieldKnife", "Buckler",
            "SteelHelm", "FieldCap", "PlateCuirass", "AetherRobe", "Fatigues",
        };

        private static readonly (DollAnimation animation, int frame, bool back)[] Frames =
        {
            (DollAnimation.Idle, 0, false), (DollAnimation.Idle, 1, false),
            (DollAnimation.Walk, 0, false), (DollAnimation.Walk, 2, false),
            (DollAnimation.Attack, 0, false), (DollAnimation.Attack, 1, false), (DollAnimation.Attack, 2, false),
            (DollAnimation.Cast, 0, false), (DollAnimation.Cast, 1, false),
            (DollAnimation.Hurt, 0, false), (DollAnimation.KnockedOut, 0, false),
            (DollAnimation.Idle, 0, true), (DollAnimation.Attack, 1, true), (DollAnimation.Cast, 1, true),
            (DollAnimation.KnockedOut, 0, true),
        };

        private static readonly Color32 Background = new(92, 112, 84, 255);
        private static readonly Color32 BackgroundAlt = new(84, 102, 78, 255);

        [MenuItem("Friendship Warriors/Art/Save Paper-Doll Preview...", priority = 120)]
        private static void SaveWithDialog()
        {
            string path = EditorUtility.SaveFilePanel("Save paper-doll preview", "", "paper_doll_preview", "png");
            if (!string.IsNullOrEmpty(path))
                Render(path, 3);
        }

        /// <summary>Every job, typical gear, Player then Enemy.</summary>
        public static List<Unit> SampleUnits()
        {
            var gear = new List<EquipmentDefinition>();
            foreach (var name in GearOrder)
            {
                var item = AssetDatabase.LoadAssetAtPath<EquipmentDefinition>($"Assets/_Project/Data/Equipment/{name}.asset");
                if (item != null)
                    gear.Add(item);
            }

            var jobs = ProjectAssets.FindAll<JobDefinition>();
            System.Array.Sort(jobs, (a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
            var units = new List<Unit>();
            foreach (var team in new[] { Team.Player, Team.Enemy })
            {
                int i = 0;
                foreach (var job in jobs)
                {
                    var unit = UnitFactory.CreateGeneric($"{job.DisplayName} {(char)('A' + i++)}{(team == Team.Enemy ? "e" : "")}",
                        team, job, 5, new System.Random(i));
                    foreach (var item in gear)
                    {
                        var slot = item.Category switch
                        {
                            EquipmentCategory.Weapon => EquipmentSlot.MainHand,
                            EquipmentCategory.Shield => EquipmentSlot.OffHand,
                            EquipmentCategory.Head => EquipmentSlot.Head,
                            EquipmentCategory.Body => EquipmentSlot.Body,
                            _ => EquipmentSlot.Accessory,
                        };
                        if (unit.Equipped(slot) == null)
                            unit.TryEquipItem(item);
                    }
                    units.Add(unit);
                }
            }
            return units;
        }

        /// <summary>Writes the contact sheet to <paramref name="path"/> at <paramref name="scale"/>× and returns the path.</summary>
        public static string Render(string path, int scale)
        {
            var catalog = PaperDollPartGenerator.UpdateCatalog();
            var units = SampleUnits();
            const int w = PaperDollLayout.FrameWidth, h = PaperDollLayout.FrameHeight;
            int width = Frames.Length * w * scale, height = units.Count * h * scale;
            var output = new Color32[width * height];

            for (int u = 0; u < units.Count; u++)
            {
                var sheet = PaperDollComposer.PixelsFor(units[u], catalog);
                for (int f = 0; f < Frames.Length; f++)
                {
                    var (animation, frame, back) = Frames[f];
                    var rect = PaperDollLayout.FrameRect(animation, frame, back);
                    var bg = (u + f) % 2 == 0 ? Background : BackgroundAlt;
                    // Output rows go top to bottom in unit order, so flip the unit index for texture order.
                    int oy0 = (units.Count - 1 - u) * h * scale, ox0 = f * w * scale;
                    for (int y = 0; y < h * scale; y++)
                    for (int x = 0; x < w * scale; x++)
                    {
                        var p = sheet[(rect.y + y / scale) * PaperDollLayout.SheetWidth + rect.x + x / scale];
                        output[(oy0 + y) * width + ox0 + x] = p.a > 0 ? p : bg;
                    }
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(output);
            texture.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            return path;
        }
    }
}
