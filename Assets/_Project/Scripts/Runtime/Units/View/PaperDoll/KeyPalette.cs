using System.Collections.Generic;
using UnityEngine;

namespace FriendshipWarriors.Units.View.PaperDoll
{
    /// <summary>
    /// The colours part sheets are painted with. KEY ramps are recoloured per unit (skin, hair, job primary, team
    /// accent); metal, leather, outline and the fixed colours stay as painted. Keys are ordinary-looking defaults so
    /// parts look right in Aseprite too. Exported as Art/Palettes/PaperDollKeys.gpl for painting.
    /// </summary>
    public static class KeyPalette
    {
        public static readonly Color32 Outline = new(22, 18, 28, 255);

        public static readonly Color32[] Skin = { new(247, 206, 166, 255), new(222, 166, 120, 255), new(176, 116, 82, 255) };
        public static readonly Color32[] Hair = { new(130, 86, 52, 255), new(96, 60, 36, 255), new(64, 40, 26, 255) };
        public static readonly Color32[] Primary = { new(126, 140, 96, 255), new(96, 108, 70, 255), new(64, 72, 48, 255) };
        public static readonly Color32[] Accent = { new(90, 150, 230, 255), new(52, 96, 180, 255) };
        public static readonly Color32[] Metal = { new(206, 212, 222, 255), new(150, 158, 172, 255), new(96, 102, 118, 255) };
        public static readonly Color32[] Leather = { new(138, 92, 58, 255), new(96, 62, 40, 255) };

        // Fixed colours (never recoloured).
        public static readonly Color32 BrassLight = new(240, 200, 96, 255);
        public static readonly Color32 Brass = new(196, 150, 58, 255);
        public static readonly Color32 Copper = new(204, 110, 60, 255);
        public static readonly Color32 Lens = new(150, 230, 255, 255);
        public static readonly Color32 LensDark = new(60, 90, 110, 255);
        public static readonly Color32 Eye = new(30, 24, 36, 255);
        public static readonly Color32 EyeWhite = new(250, 248, 240, 255);
        public static readonly Color32 Mouth = new(150, 80, 70, 255);
        public static readonly Color32 Undersuit = new(78, 78, 92, 255);
        public static readonly Color32 UndersuitDark = new(52, 52, 64, 255);
        public static readonly Color32 Glow = new(130, 220, 255, 255);
        public static readonly Color32 GlowCore = new(232, 250, 255, 255);
        public static readonly Color32 Flash = new(255, 232, 120, 255);

        /// <summary>Every colour in painting order, with names, for the exported palettes.</summary>
        public static IEnumerable<(string name, Color32 color)> All()
        {
            yield return ("Outline", Outline);
            for (int i = 0; i < Skin.Length; i++) yield return ($"Skin {i + 1} (key)", Skin[i]);
            for (int i = 0; i < Hair.Length; i++) yield return ($"Hair {i + 1} (key)", Hair[i]);
            for (int i = 0; i < Primary.Length; i++) yield return ($"Job primary {i + 1} (key)", Primary[i]);
            for (int i = 0; i < Accent.Length; i++) yield return ($"Team accent {i + 1} (key)", Accent[i]);
            for (int i = 0; i < Metal.Length; i++) yield return ($"Metal {i + 1}", Metal[i]);
            for (int i = 0; i < Leather.Length; i++) yield return ($"Leather {i + 1}", Leather[i]);
            yield return ("Brass light", BrassLight);
            yield return ("Brass", Brass);
            yield return ("Copper", Copper);
            yield return ("Lens", Lens);
            yield return ("Lens dark", LensDark);
            yield return ("Eye", Eye);
            yield return ("Eye white", EyeWhite);
            yield return ("Mouth", Mouth);
            yield return ("Undersuit", Undersuit);
            yield return ("Undersuit dark", UndersuitDark);
            yield return ("Aether glow", Glow);
            yield return ("Aether glow core", GlowCore);
            yield return ("Muzzle flash", Flash);
        }
    }
}
