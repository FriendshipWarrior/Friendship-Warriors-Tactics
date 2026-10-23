using FriendshipWarriors.Units.View.PaperDoll;
using UnityEngine;
using K = FriendshipWarriors.Units.View.PaperDoll.KeyPalette;

namespace FriendshipWarriors.Editor.Art
{
    /// <summary>The generated part sheets: each knows its file name and how to draw one frame for a rig.</summary>
    internal enum PartKind
    {
        Body, Head, Face,
        HairCrop, HairSwept, HairPonytail, HairLong,
        Cap, Helm,
        Fatigues, Cuirass, Robe,
        Saber, Knife, Rifle, Rod, Staff,
        Buckler,
    }

    /// <summary>
    /// Procedural first drafts of every paper-doll part, painted with the key palette so the game can recolour them.
    /// Each part draws one 40×40 frame for a <see cref="DollRig"/>; shading and outlines are applied per part. These
    /// are starting points for hand editing in Aseprite, not final art.
    /// </summary>
    internal static class PaperDollArt
    {
        public static string FileName(PartKind kind) => kind switch
        {
            PartKind.Body => "Body/body",
            PartKind.Head => "Head/head",
            PartKind.Face => "Face/face",
            PartKind.HairCrop => "Hair/hair_crop",
            PartKind.HairSwept => "Hair/hair_swept",
            PartKind.HairPonytail => "Hair/hair_ponytail",
            PartKind.HairLong => "Hair/hair_long",
            PartKind.Cap => "Headgear/headgear_hat_field_cap",
            PartKind.Helm => "Headgear/headgear_helmet_steel_helm",
            PartKind.Fatigues => "BodyGear/bodygear_clothing_fatigues",
            PartKind.Cuirass => "BodyGear/bodygear_armor_plate_cuirass",
            PartKind.Robe => "BodyGear/bodygear_robe_aether_robe",
            PartKind.Saber => "Weapons/weapon_sword_service_saber",
            PartKind.Knife => "Weapons/weapon_knife_field_knife",
            PartKind.Rifle => "Weapons/weapon_rifle_service_rifle",
            PartKind.Rod => "Weapons/weapon_rod_aether_rod",
            PartKind.Staff => "Weapons/weapon_staff_ward_staff",
            _ => "Shield/shield_buckler",
        };

        /// <summary>Draws one frame of a part. Back = the back-¾ pose.</summary>
        public static PixelCanvas Draw(PartKind kind, DollRig r, bool back)
        {
            var c = new PixelCanvas(PaperDollLayout.FrameWidth, PaperDollLayout.FrameHeight);
            switch (kind)
            {
                case PartKind.Body: Body(c, r, back); break;
                case PartKind.Head: Head(c, r, back); break;
                case PartKind.Face: if (!back) Face(c, r); break;
                case PartKind.HairCrop:
                case PartKind.HairSwept:
                case PartKind.HairPonytail:
                case PartKind.HairLong: Hair(c, r, back, kind); break;
                case PartKind.Cap: Cap(c, r, back); break;
                case PartKind.Helm: Helm(c, r, back); break;
                case PartKind.Fatigues: Fatigues(c, r, back); break;
                case PartKind.Cuirass: Cuirass(c, r, back); break;
                case PartKind.Robe: Robe(c, r, back); break;
                case PartKind.Saber: if (!r.KnockedOut) Saber(c, r); break;
                case PartKind.Knife: if (!r.KnockedOut) Knife(c, r); break;
                case PartKind.Rifle: if (!r.KnockedOut) Rifle(c, r); break;
                case PartKind.Rod: if (!r.KnockedOut) Rod(c, r); break;
                case PartKind.Staff: if (!r.KnockedOut) Staff(c, r); break;
                case PartKind.Buckler: Buckler(c, r, back); break;
            }
            return c;
        }

        // ------------------------------------------------------------------ Helpers

        private static Vector2Int Along(Vector2Int from, float degrees, float distance)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector2Int(Mathf.RoundToInt(from.x + Mathf.Cos(rad) * distance), Mathf.RoundToInt(from.y - Mathf.Sin(rad) * distance));
        }

        private static Vector2Int Lerp(Vector2Int a, Vector2Int b, float t) =>
            new(Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, t)), Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, t)));

        private static void ShadeRamp(PixelCanvas c, Color32[] ramp)
        {
            if (ramp.Length >= 3)
                c.Shade(ramp[1], ramp[0], ramp[2]);
            else
                c.Shade(ramp[0], ramp[0], ramp[1], lightEdges: false);
        }

        // ------------------------------------------------------------------ Body, head, face, hair

        private static void Body(PixelCanvas c, DollRig r, bool back)
        {
            // Legs and boots (the boots stay visible under every body gear).
            int far = 17 + r.FarFoot, near = 21 + r.NearFoot;
            c.Fill(far, r.Hip, far + 2, 36, K.UndersuitDark);
            c.Fill(far - 1, 37, far + 3, 39, K.Leather[1]);
            c.Fill(near, r.Hip, near + 2, 36, K.Undersuit);
            c.Fill(near, 37, near + 3, 39, K.Leather[0]);
            c.Set(near + 3, 39, K.Leather[1]);

            // Far arm behind the torso, then the torso, neck and near arm.
            c.Line(r.FarShoulder, r.FarHand, K.UndersuitDark, 2);
            c.Fill(r.FarHand.x, r.FarHand.y, r.FarHand.x + 1, r.FarHand.y + 1, K.Skin[2]);
            c.Fill(r.TorsoLeft, r.TorsoTop, r.TorsoRight, r.Hip, K.Undersuit);
            c.Fill(r.HeadCx - 1, r.TorsoTop - 1, r.HeadCx + 1, r.TorsoTop, K.Skin[2]);
            c.Line(r.NearShoulder, r.NearHand, K.Undersuit, 2);
            c.Fill(r.NearHand.x, r.NearHand.y, r.NearHand.x + 1, r.NearHand.y + 1, K.Skin[1]);

            c.Shade(K.Undersuit, K.Undersuit, K.UndersuitDark);
            c.Outline(K.Outline);
        }

        private static void Head(PixelCanvas c, DollRig r, bool back)
        {
            float cx = r.HeadCx + 0.5f, cy = r.HeadTop + 6.2f;
            c.Ellipse(cx, cy, 5.6f, 6.1f, K.Skin[1]);
            // Jaw toward the facing side; ear on the side away from it.
            if (!back)
                c.Fill(r.HeadCx + 2, r.HeadTop + 10, r.HeadCx + 4, r.HeadTop + 11, K.Skin[1]);
            int earX = back ? r.HeadCx + 3 : r.HeadCx - 3;
            c.Fill(earX, r.HeadTop + 6, earX + 1, r.HeadTop + 8, K.Skin[2]);
            ShadeRamp(c, K.Skin);
            c.Outline(K.Outline);
        }

        private static void Face(PixelCanvas c, DollRig r)
        {
            int x = r.HeadCx, y = r.HeadTop;
            bool closed = r.KnockedOut || r.Animation == DollAnimation.Hurt;
            // Brows in the darkest hair tone so they follow the unit's hair colour.
            c.Fill(x + 1, y + 4, x + 3, y + 4, K.Hair[2]);
            c.Set(x - 2, y + 4, K.Hair[2]);
            if (closed)
            {
                c.Fill(x + 1, y + 6, x + 3, y + 6, K.Eye);
                c.Set(x - 2, y + 6, K.Eye);
            }
            else
            {
                c.Set(x + 1, y + 6, K.EyeWhite);
                c.Fill(x + 2, y + 6, x + 2, y + 7, K.Eye);
                c.Fill(x - 2, y + 6, x - 2, y + 7, K.Eye);
            }
            c.Set(x + 4, y + 8, K.Skin[2]);
            c.Fill(x + 1, y + 10, x + 2, y + 10, K.Mouth);
        }

        private static void Hair(PixelCanvas c, DollRig r, bool back, PartKind style)
        {
            int x = r.HeadCx, y = r.HeadTop;
            var mid = K.Hair[1];
            if (back)
            {
                c.Ellipse(x + 0.5f, y + 5.5f, 6.0f, 5.9f, mid, (_, py) => py <= y + 9);
            }
            else
            {
                c.Ellipse(x - 0.5f, y + 3.8f, 6.1f, 4.2f, mid, (_, py) => py <= y + 4);
                c.Ellipse(x - 3.2f, y + 5.2f, 3.0f, 4.4f, mid, (px, _) => px <= x - 3);
            }

            switch (style)
            {
                case PartKind.HairCrop:
                    if (!back)
                        c.Fill(x + 1, y + 4, x + 3, y + 4, mid);
                    break;
                case PartKind.HairSwept:
                    c.Set(x - 1, y - 1, mid);
                    c.Set(x + 2, y - 1, mid);
                    c.Set(x + 1, y - 2, mid);
                    if (!back)
                    {
                        c.Fill(x + 2, y + 4, x + 5, y + 4, mid);
                        c.Set(x + 5, y + 5, mid);
                    }
                    break;
                case PartKind.HairPonytail:
                    if (back)
                    {
                        c.Line(x, y + 9, x - 1, y + 15, mid, 2);
                        c.Fill(x - 1, y + 9, x + 1, y + 9, K.Leather[1]);
                    }
                    else
                    {
                        c.Line(x - 6, y + 5, x - 9, y + 13, mid, 2);
                        c.Fill(x - 7, y + 5, x - 6, y + 6, K.Leather[1]);
                    }
                    break;
                case PartKind.HairLong:
                    if (back)
                        c.Fill(x - 5, y + 9, x + 5, y + 12, mid);
                    else
                    {
                        c.Fill(x - 6, y + 8, x - 3, y + 12, mid);
                        c.Fill(x + 3, y + 3, x + 4, y + 5, mid);
                    }
                    break;
            }

            ShadeRamp(c, K.Hair);
            c.Outline(K.Outline);
        }

        // ------------------------------------------------------------------ Headgear

        private static void Cap(PixelCanvas c, DollRig r, bool back)
        {
            int x = r.HeadCx, y = r.HeadTop;
            c.Ellipse(x - 0.3f, y + 1.8f, 6.3f, 3.4f, K.Primary[1], (_, py) => py <= y + 3);
            ShadeRamp(c, K.Primary);
            c.Fill(x - 6, y + 3, x + 5, y + 3, K.Accent[0]);
            if (!back)
            {
                // Peak toward the facing side, goggles pushed up on the crown.
                c.Fill(x + 3, y + 4, x + 8, y + 4, K.Leather[1]);
                c.Fill(x + 5, y + 3, x + 7, y + 3, K.Leather[0]);
                c.Fill(x - 5, y + 1, x + 0, y + 1, K.Leather[1]);
                c.Fill(x + 1, y, x + 3, y + 2, K.Brass);
                c.Set(x + 2, y + 1, K.Lens);
                c.Set(x + 1, y, K.BrassLight);
            }
            else
            {
                c.Fill(x - 5, y + 1, x + 5, y + 1, K.Leather[1]);
                c.Fill(x - 7, y + 4, x - 4, y + 4, K.Leather[1]);
            }
            c.Outline(K.Outline);
        }

        private static void Helm(PixelCanvas c, DollRig r, bool back)
        {
            int x = r.HeadCx, y = r.HeadTop;
            c.Ellipse(x - 0.3f, y + 3.4f, 6.6f, 4.9f, K.Metal[1], (_, py) => py <= y + 5);
            if (back)
                c.Fill(x - 5, y + 5, x + 6, y + 9, K.Metal[1]);
            else
                c.Fill(x - 7, y + 5, x - 4, y + 9, K.Metal[1]);
            ShadeRamp(c, K.Metal);
            c.Fill(x - 6, y + 5, x + 5, y + 5, K.Metal[2]);
            if (!back)
                c.Fill(x + 4, y + 5, x + 4, y + 7, K.Metal[2]);
            for (int rx = x - 5; rx <= x + 4; rx += 3)
                c.Set(rx, y + 5, K.Brass);
            // Crest in the team colour.
            c.Fill(x - 4, y - 2, x + 2, y - 1, K.Accent[0]);
            c.Fill(x - 4, y - 1, x + 2, y - 1, K.Accent[1]);
            c.Set(x + 3, y - 1, K.Accent[1]);
            c.Outline(K.Outline);
        }

        // ------------------------------------------------------------------ Body gear

        private static void Sleeves(PixelCanvas c, DollRig r, Color32 far, Color32 near, int thickness = 2, float reach = 0.72f)
        {
            c.Line(r.FarShoulder, Lerp(r.FarShoulder, r.FarHand, reach), far, thickness);
            c.Line(r.NearShoulder, Lerp(r.NearShoulder, r.NearHand, reach), near, thickness);
        }

        private static void Fatigues(PixelCanvas c, DollRig r, bool back)
        {
            int far = 17 + r.FarFoot, near = 21 + r.NearFoot;
            c.Fill(far, r.Hip, far + 2, 36, K.Primary[2]);
            c.Fill(near, r.Hip, near + 2, 36, K.Primary[1]);
            c.Line(r.FarShoulder, Lerp(r.FarShoulder, r.FarHand, 0.72f), K.Primary[2], 2);
            c.Fill(r.TorsoLeft, r.TorsoTop, r.TorsoRight, r.Hip - 1, K.Primary[1]);
            c.Line(r.NearShoulder, Lerp(r.NearShoulder, r.NearHand, 0.72f), K.Primary[1], 2);
            ShadeRamp(c, K.Primary);

            c.Fill(r.TorsoLeft, r.Hip - 2, r.TorsoRight, r.Hip - 2, K.Leather[1]);
            c.Fill(r.NearShoulder.x - 1, r.TorsoTop, r.NearShoulder.x + 1, r.TorsoTop, K.Accent[0]);
            if (back)
            {
                // Field pack on the back.
                c.Fill(r.HeadCx - 2, r.TorsoTop + 2, r.HeadCx + 2, r.TorsoTop + 7, K.Leather[0]);
                c.Fill(r.HeadCx - 2, r.TorsoTop + 7, r.HeadCx + 2, r.TorsoTop + 7, K.Leather[1]);
            }
            else
            {
                int bx = r.HeadCx + 2;
                c.Set(bx, r.TorsoTop + 2, K.Brass);
                c.Set(bx, r.TorsoTop + 4, K.Brass);
                c.Set(bx, r.TorsoTop + 6, K.Brass);
                c.Set(bx, r.Hip - 2, K.BrassLight);
                c.Fill(r.HeadCx - 1, r.TorsoTop, r.HeadCx + 1, r.TorsoTop, K.Primary[2]);
            }
            c.Outline(K.Outline);
        }

        private static void Cuirass(PixelCanvas c, DollRig r, bool back)
        {
            int far = 17 + r.FarFoot, near = 21 + r.NearFoot;
            // Greaves over the shins.
            c.Fill(far, 33, far + 2, 36, K.Metal[1]);
            c.Fill(near, 33, near + 2, 36, K.Metal[1]);
            // Far pauldron, tunic sleeves, breastplate, near pauldron.
            c.Ellipse(r.FarShoulder.x + 0.5f, r.FarShoulder.y + 0.5f, 2.6f, 2.1f, K.Metal[1]);
            Sleeves(c, r, K.Primary[2], K.Primary[1]);
            c.Fill(r.TorsoLeft, r.TorsoTop, r.TorsoRight, r.Hip - 1, K.Metal[1]);
            c.Ellipse(r.NearShoulder.x + 0.5f, r.NearShoulder.y + 0.5f, 2.8f, 2.2f, K.Metal[1]);
            ShadeRamp(c, K.Metal);
            ShadeRamp(c, K.Primary);

            // Faulds in the team colour, a centre ridge and rivets.
            c.Fill(r.TorsoLeft, r.Hip - 2, r.TorsoRight, r.Hip - 1, K.Accent[0]);
            c.Fill(r.TorsoLeft, r.Hip - 1, r.TorsoRight, r.Hip - 1, K.Accent[1]);
            if (!back)
            {
                c.Fill(r.HeadCx + 1, r.TorsoTop + 1, r.HeadCx + 1, r.Hip - 4, K.Metal[0]);
                c.Set(r.TorsoLeft + 1, r.TorsoTop + 2, K.Brass);
                c.Set(r.TorsoRight - 1, r.TorsoTop + 2, K.Brass);
                c.Set(r.TorsoLeft + 1, r.Hip - 4, K.Brass);
                c.Set(r.TorsoRight - 1, r.Hip - 4, K.Brass);
            }
            else
            {
                c.Fill(r.HeadCx, r.TorsoTop + 1, r.HeadCx, r.Hip - 4, K.Metal[2]);
            }
            c.Outline(K.Outline);
        }

        private static void Robe(PixelCanvas c, DollRig r, bool back)
        {
            Sleeves(c, r, K.Primary[2], K.Primary[1], thickness: 3, reach: 0.68f);
            for (int y = r.TorsoTop; y <= 36; y++)
            {
                int spread = (y - r.TorsoTop) / 5;
                c.Fill(r.TorsoLeft - spread, y, r.TorsoRight + spread, y, K.Primary[1]);
            }
            ShadeRamp(c, K.Primary);

            int hemLeft = r.TorsoLeft - (36 - r.TorsoTop) / 5, hemRight = r.TorsoRight + (36 - r.TorsoTop) / 5;
            c.Fill(hemLeft, 36, hemRight, 36, K.Copper);
            c.Fill(r.TorsoLeft, r.Hip - 3, r.TorsoRight, r.Hip - 2, K.Accent[0]);
            c.Set(r.HeadCx + 2, r.Hip - 1, K.Accent[1]);
            c.Set(Lerp(r.NearShoulder, r.NearHand, 0.68f).x, Lerp(r.NearShoulder, r.NearHand, 0.68f).y, K.Copper);
            if (back)
            {
                // Hood folded on the shoulders.
                c.Fill(r.HeadCx - 3, r.TorsoTop - 1, r.HeadCx + 3, r.TorsoTop + 1, K.Primary[2]);
            }
            else
            {
                c.Fill(r.HeadCx + 2, r.TorsoTop + 1, r.HeadCx + 2, 35, K.Copper);
                c.Fill(r.HeadCx - 1, r.TorsoTop, r.HeadCx + 2, r.TorsoTop, K.Copper);
            }
            c.Outline(K.Outline);
        }

        // ------------------------------------------------------------------ Weapons and shield

        private static void Saber(PixelCanvas c, DollRig r)
        {
            var hand = r.NearHand;
            float a = r.WeaponAngle;
            c.Line(Along(hand, a, -2), hand, K.Leather[1], 1);
            var guard = Along(hand, a, 1);
            c.Line(Along(guard, a + 90, 2), Along(guard, a - 90, 2), K.Brass, 1);
            c.Line(Along(hand, a, 2), Along(hand, a, 12), K.Metal[1], 2);
            c.Line(Along(hand, a, 2), Along(hand, a, 12), K.Metal[0], 1);
            c.Set(Along(hand, a, 13).x, Along(hand, a, 13).y, K.Metal[0]);
            c.Outline(K.Outline);
        }

        private static void Knife(PixelCanvas c, DollRig r)
        {
            var hand = r.NearHand;
            float a = r.WeaponAngle;
            c.Line(Along(hand, a, -1), hand, K.Leather[1], 1);
            var guard = Along(hand, a, 1);
            c.Line(Along(guard, a + 90, 1), Along(guard, a - 90, 1), K.Brass, 1);
            c.Line(Along(hand, a, 2), Along(hand, a, 6), K.Metal[0], 1);
            c.Line(Along(hand, a, 2), Along(hand, a, 5), K.Metal[1], 2);
            c.Outline(K.Outline);
        }

        private static float RifleAngle(DollRig r) => r.Animation switch
        {
            DollAnimation.Attack => r.Frame == 0 ? 5f : r.Frame == 1 ? 0f : 14f,
            DollAnimation.Cast => 70f,
            DollAnimation.Hurt => -60f,
            _ => -25f,
        };

        private static void Rifle(PixelCanvas c, DollRig r)
        {
            var hand = r.NearHand;
            float a = RifleAngle(r);
            c.Line(Along(hand, a, -6), Along(hand, a, -1), K.Leather[0], 2);
            c.Line(Along(hand, a, -1), Along(hand, a, 3), K.Leather[1], 2);
            c.Line(Along(hand, a, 3), Along(hand, a, 14), K.Metal[2], 1);
            var scope = Along(Along(hand, a, 1), a + 90, 2);
            c.Line(scope, Along(scope, a, 4), K.Metal[1], 1);
            c.Set(Along(hand, a, 5).x, Along(hand, a, 5).y, K.Brass);
            c.Set(Along(hand, a, 10).x, Along(hand, a, 10).y, K.Brass);
            c.Outline(K.Outline);
            if (r.Flash && r.Animation == DollAnimation.Attack)
            {
                var muzzle = Along(hand, a, 16);
                c.Fill(muzzle.x, muzzle.y - 1, muzzle.x + 1, muzzle.y + 1, K.Flash);
                c.Set(muzzle.x + 2, muzzle.y, K.Flash);
                c.Set(muzzle.x, muzzle.y, K.GlowCore);
            }
        }

        private static void Glow(PixelCanvas c, Vector2Int at, int size)
        {
            if (size >= 1)
                c.Ellipse(at.x + 0.5f, at.y + 0.5f, 1.6f + size, 1.6f + size, K.Glow, (px, py) => !c.Opaque(px, py));
            c.Fill(at.x, at.y, at.x + 1, at.y + 1, K.Glow);
            c.Set(at.x, at.y, K.GlowCore);
        }

        private static void Rod(PixelCanvas c, DollRig r)
        {
            var hand = r.NearHand;
            float a = r.Animation == DollAnimation.Attack ? r.WeaponAngle : r.Animation == DollAnimation.Cast ? 85f : 60f;
            c.Line(Along(hand, a, -1), Along(hand, a, 6), K.Copper, 1);
            c.Set(Along(hand, a, 3).x, Along(hand, a, 3).y, K.Brass);
            c.Outline(K.Outline);
            Glow(c, Along(hand, a, 7), r.Glow);
        }

        private static void Staff(PixelCanvas c, DollRig r)
        {
            var hand = r.NearHand;
            float a = r.Animation == DollAnimation.Attack ? r.WeaponAngle : 88f;
            float below = r.Animation == DollAnimation.Attack ? 3f : Mathf.Max(2f, 39 - hand.y - 1);
            c.Line(Along(hand, a, -below), Along(hand, a, 12), K.Leather[0], 1);
            var top = Along(hand, a, 13);
            c.Ellipse(top.x + 0.5f, top.y + 0.5f, 2.2f, 2.2f, K.Metal[1]);
            c.Outline(K.Outline);
            Glow(c, top, r.Glow);
        }

        private static void Buckler(PixelCanvas c, DollRig r, bool back)
        {
            float cx = r.FarHand.x - 0.5f, cy = r.FarHand.y - 1.5f;
            c.Ellipse(cx, cy, 3.7f, 4.1f, K.Metal[1]);
            ShadeRamp(c, K.Metal);
            c.Fill((int)cx, (int)cy, (int)cx + 1, (int)cy + 1, K.Brass);
            c.Set((int)cx, (int)cy, K.BrassLight);
            c.Outline(K.Outline);
        }
    }
}
