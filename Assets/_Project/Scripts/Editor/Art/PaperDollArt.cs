using System;
using System.Collections.Generic;
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
    /// The style follows FFT's look (studied, never copied): chibi proportions with a big head, chunky hands and boots,
    /// volume shading lit from the upper left in 4 shades per material, and coloured outlines (each piece is outlined in
    /// its own material's darkest shade, never black). Every piece is drawn on its own layer, shaded, then stacked, so
    /// an arm in front of the torso keeps a dividing line. These are starting points for hand editing in Aseprite.
    /// </summary>
    internal static class PaperDollArt
    {
        private const int Hi = K.Highlight, Lt = K.Light, Md = K.Mid, Sh = K.Shadow, Ln = K.Line;

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
            var c = Blank();
            switch (kind)
            {
                case PartKind.Body: Body(c, r); break;
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
                case PartKind.Buckler: Buckler(c, r); break;
            }
            return c;
        }

        // ------------------------------------------------------------------ Helpers

        private static PixelCanvas Blank() => new(PaperDollLayout.FrameWidth, PaperDollLayout.FrameHeight);

        private static Vector2 V(float x, float y) => new(x, y);
        private static Vector2Int P(int x, int y) => new(x, y);

        private static Vector2Int Along(Vector2Int from, float degrees, float distance)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector2Int(Mathf.RoundToInt(from.x + Mathf.Cos(rad) * distance), Mathf.RoundToInt(from.y - Mathf.Sin(rad) * distance));
        }

        private static Vector2Int Lerp(Vector2Int a, Vector2Int b, float t) =>
            new(Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, t)), Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, t)));

        private static readonly Dictionary<int, Color32> OutlineShade = BuildOutlineShades();

        private static Dictionary<int, Color32> BuildOutlineShades()
        {
            var map = new Dictionary<int, Color32>();
            foreach (var ramp in K.Ramps())
                foreach (var shade in ramp)
                    map[PaletteSwap.Pack(shade)] = ramp[Ln];
            return map;
        }

        /// <summary>The outline colour next to a pixel: its material's darkest shade, or the general outline.</summary>
        private static Color32 OutlineOf(Color32 pixel) =>
            OutlineShade.TryGetValue(PaletteSwap.Pack(pixel), out var line) ? line : K.Outline;

        private static void Finish(PixelCanvas c) => c.Outline(OutlineOf);

        /// <summary>
        /// Draws one piece in its material's mid shade on a layer of its own, shades it as a volume and stacks it on
        /// <paramref name="c"/>. <paramref name="far"/> pieces (the limb on the far side of the body) use the material one
        /// step darker; <paramref name="separate"/> draws a dividing line where the piece overlaps what's already there.
        /// </summary>
        private static void Piece(PixelCanvas c, Color32[] ramp, Action<PixelCanvas, Color32> draw,
            bool far = false, bool separate = false, bool highlight = true)
        {
            var shades = far ? new[] { ramp[Md], ramp[Md], ramp[Sh], ramp[Sh] } : ramp;
            var piece = Blank();
            draw(piece, shades[Md]);
            piece.ShadeVolume(shades, highlight && !far);
            c.Layer(piece, separate ? ramp[Ln] : (Color32?)null);
        }

        private static void Leg(PixelCanvas p, DollRig r, bool near, Color32 m, int top = -1)
        {
            int x = r.LegX(near);
            p.Fill(x, top < 0 ? r.Hip : top, x + 2, DollRig.BootTop - 1, m);
        }

        private static void Boot(PixelCanvas p, DollRig r, bool near, Color32 m)
        {
            int x = r.LegX(near);
            p.Fill(x, DollRig.BootTop, x + 2, DollRig.BootTop, m);
            p.Fill(x - 1, DollRig.BootTop + 1, x + 3, 39, m);
            p.Fill(x + 4, 37, x + 4, 39, m);
        }

        private static void Hand(PixelCanvas p, Vector2Int at, Color32 m) => p.Ellipse(at.x + 0.5f, at.y + 0.5f, 1.8f, 1.8f, m);

        /// <summary>
        /// The trunk from the shoulders to <paramref name="bottom"/>: rounded shoulders, a slightly narrower waist, then a
        /// skirt below the hip that widens by a pixel every <paramref name="flareEvery"/> rows.
        /// </summary>
        private static void Trunk(PixelCanvas p, DollRig r, Color32 m, int bottom, int flareEvery = 2, int top = -1)
        {
            for (int y = top < 0 ? r.TorsoTop : top; y <= bottom; y++)
            {
                int inset = y == r.TorsoTop ? 1
                    : y > r.Hip ? -((y - r.Hip + flareEvery - 1) / flareEvery)
                    : y >= r.TorsoTop + 5 ? 1
                    : 0;
                p.Fill(r.TorsoLeft + inset, y, r.TorsoRight - inset, y, m);
            }
        }

        // ------------------------------------------------------------------ Body, head, face, hair

        private static void Body(PixelCanvas c, DollRig r)
        {
            Piece(c, K.Undersuit, (p, m) => Leg(p, r, false, m), far: true);
            Piece(c, K.Leather, (p, m) => Boot(p, r, false, m), far: true);
            Piece(c, K.Undersuit, (p, m) => p.Thick(r.FarShoulder, r.FarHand, m, 3), far: true);
            Piece(c, K.Skin, (p, m) => Hand(p, r.FarHand, m), far: true);
            Piece(c, K.Undersuit, (p, m) => Trunk(p, r, m, r.Hip));
            Piece(c, K.Skin, (p, m) => p.Fill(r.HeadCx - 1, r.TorsoTop - 2, r.HeadCx + 1, r.TorsoTop, m), far: true);
            Piece(c, K.Undersuit, (p, m) => Leg(p, r, true, m), separate: true);
            Piece(c, K.Leather, (p, m) => Boot(p, r, true, m), separate: true);
            Piece(c, K.Undersuit, (p, m) => p.Thick(r.NearShoulder, r.NearHand, m, 3), separate: true);
            Piece(c, K.Skin, (p, m) => Hand(p, r.NearHand, m), highlight: false);
            Finish(c);
        }

        private static void Head(PixelCanvas c, DollRig r, bool back)
        {
            float cx = r.HeadCx + 0.5f, cy = r.HeadTop + 7.5f;
            Piece(c, K.Skin, (p, m) =>
            {
                p.Ellipse(cx, cy, 6.6f, 7.0f, m);
                // Fuller cheek and chin toward the facing side.
                if (!back)
                    p.Ellipse(cx + 2.4f, cy + 3.0f, 4.0f, 3.4f, m);
            });
            int earX = back ? r.HeadCx + 3 : r.HeadCx - 3;
            Piece(c, K.Skin, (p, m) => p.Fill(earX, r.HeadTop + 9, earX + 1, r.HeadTop + 11, m), far: true);
            Finish(c);
        }

        private static void Face(PixelCanvas c, DollRig r)
        {
            int x = r.HeadCx, y = r.HeadTop;
            bool closed = r.KnockedOut || r.Animation == DollAnimation.Hurt;
            if (closed)
            {
                c.Fill(x + 2, y + 10, x + 4, y + 10, K.Eye);
                c.Fill(x - 2, y + 10, x - 1, y + 10, K.Eye);
            }
            else
            {
                // Big near eye with a catch-light, narrower far eye, as chibi tactics sprites draw them.
                c.Fill(x + 3, y + 8, x + 4, y + 10, K.Eye);
                c.Set(x + 4, y + 8, K.EyeWhite);
                c.Fill(x - 1, y + 8, x - 1, y + 10, K.Eye);
            }
            c.Set(x + 6, y + 11, K.Skin[Sh]);
            c.Fill(x + 3, y + 13, x + 4, y + 13, K.Mouth);
            c.Set(x - 2, y + 12, K.Skin[Lt]);
        }

        private static void Hair(PixelCanvas c, DollRig r, bool back, PartKind style)
        {
            int x = r.HeadCx, y = r.HeadTop;
            float cx = x + 0.5f;
            Piece(c, K.Hair, (p, m) =>
            {
                if (back)
                {
                    p.Ellipse(cx, y + 6.8f, 7.3f, 7.1f, m, (_, py) => py <= y + 12);
                    // Jagged ends at the nape instead of a smooth bowl.
                    for (int tx = x - 5; tx <= x + 4; tx += 3)
                        p.Triangle(V(tx, y + 12), V(tx + 3, y + 12), V(tx + 1.5f, y + 14.5f), m);
                    switch (style)
                    {
                        case PartKind.HairCrop:
                            p.Triangle(V(x - 5, y + 1), V(x - 1, y), V(x - 4, y - 2), m);
                            p.Triangle(V(x, y), V(x + 4, y + 1), V(x + 2, y - 2), m);
                            break;
                        case PartKind.HairSwept:
                            p.Triangle(V(x - 2, y + 1), V(x + 5, y + 1), V(x + 9, y + 4), m);
                            p.Triangle(V(x - 7, y + 4), V(x - 4, y + 1), V(x - 10, y + 2), m);
                            break;
                        case PartKind.HairPonytail:
                            p.Thick(P(x, y + 11), P(x - 1, y + 19), m, 3);
                            break;
                        case PartKind.HairLong:
                            p.Ellipse(cx, y + 13.5f, 6.8f, 4.8f, m);
                            break;
                    }
                }
                else
                {
                    p.Ellipse(cx - 0.4f, y + 5.6f, 7.4f, 6.0f, m, (_, py) => py <= y + 6);
                    p.Ellipse(cx - 4.4f, y + 8.0f, 3.6f, 6.4f, m, (px, _) => px <= x - 4);
                    p.Triangle(V(x - 8, y + 12), V(x - 4, y + 12), V(x - 6, y + 15.5f), m);
                    // Locks falling over the forehead.
                    p.Triangle(V(x - 3, y + 5), V(x + 2, y + 5), V(x, y + 8), m);
                    p.Triangle(V(x + 1, y + 5), V(x + 6, y + 5), V(x + 4, y + 8), m);
                    p.Triangle(V(x + 4, y + 4), V(x + 8, y + 5), V(x + 7, y + 9), m);
                    switch (style)
                    {
                        case PartKind.HairCrop:
                            p.Triangle(V(x - 5, y + 1), V(x - 1, y), V(x - 4, y - 2), m);
                            p.Triangle(V(x - 1, y), V(x + 3, y), V(x + 1, y - 2), m);
                            break;
                        case PartKind.HairSwept:
                            p.Triangle(V(x - 1, y + 1), V(x + 6, y + 1), V(x + 10, y + 5), m);
                            p.Triangle(V(x - 7, y + 3), V(x - 4, y + 1), V(x - 10, y + 2), m);
                            p.Triangle(V(x - 7, y + 7), V(x - 5, y + 4), V(x - 10, y + 7), m);
                            break;
                        case PartKind.HairPonytail:
                            p.Thick(P(x - 6, y + 7), P(x - 9, y + 17), m, 3);
                            break;
                        case PartKind.HairLong:
                            p.Ellipse(cx - 3.6f, y + 12.5f, 4.2f, 6.5f, m, (px, _) => px <= x - 3);
                            break;
                    }
                }
            });

            // Strands between the clumps, and the tie of a ponytail.
            var strand = K.Hair[Sh];
            if (!back)
            {
                c.Set(x + 1, y + 6, strand);
                c.Set(x + 4, y + 6, strand);
                c.Line(x - 3, y + 2, x - 2, y + 4, strand);
                c.Line(x + 2, y + 1, x + 3, y + 3, strand);
                c.Line(x - 6, y + 8, x - 6, y + 11, strand);
            }
            else
            {
                c.Line(x - 2, y + 2, x - 3, y + 8, strand);
                c.Line(x + 3, y + 2, x + 4, y + 8, strand);
                c.Line(x + 1, y + 6, x + 1, y + 11, strand);
            }
            if (style == PartKind.HairPonytail)
            {
                if (back)
                    c.Fill(x - 1, y + 11, x + 1, y + 12, K.Leather[Md]);
                else
                    c.Fill(x - 7, y + 7, x - 5, y + 8, K.Leather[Md]);
            }
            Finish(c);
        }

        // ------------------------------------------------------------------ Headgear

        private static void Cap(PixelCanvas c, DollRig r, bool back)
        {
            int x = r.HeadCx, y = r.HeadTop;
            float cx = x + 0.5f;
            Piece(c, K.Primary, (p, m) => p.Ellipse(cx - 0.3f, y + 3.6f, 7.7f, 4.9f, m, (_, py) => py <= y + 4));
            Piece(c, K.Accent, (p, m) => p.Fill(x - 7, y + 4, x + 7, y + 5, m), highlight: false);
            if (!back)
            {
                // Peak toward the facing side, goggles pushed up on the crown.
                Piece(c, K.Leather, (p, m) =>
                {
                    p.Fill(x + 5, y + 6, x + 10, y + 6, m);
                    p.Fill(x + 6, y + 7, x + 9, y + 7, m);
                }, highlight: false);
                Piece(c, K.Brass, (p, m) => p.Fill(x - 3, y + 1, x + 3, y + 3, m), highlight: false);
                c.Fill(x - 2, y + 2, x - 1, y + 2, K.Lens);
                c.Fill(x + 1, y + 2, x + 2, y + 2, K.Lens);
                c.Set(x - 1, y + 3, K.LensDark);
                c.Set(x + 2, y + 3, K.LensDark);
            }
            else
            {
                Piece(c, K.Leather, (p, m) => p.Fill(x - 6, y + 2, x + 6, y + 2, m), highlight: false);
            }
            Finish(c);
        }

        private static void Helm(PixelCanvas c, DollRig r, bool back)
        {
            int x = r.HeadCx, y = r.HeadTop;
            float cx = x + 0.5f;
            Piece(c, K.Accent, (p, m) => p.Ellipse(cx - 1.5f, y - 0.5f, 5.2f, 2.4f, m));
            Piece(c, K.Metal, (p, m) =>
            {
                p.Ellipse(cx - 0.2f, y + 5.2f, 7.6f, 6.4f, m, (_, py) => py <= y + 7);
                if (back)
                    p.Fill(x - 6, y + 7, x + 7, y + 12, m);
                else
                    p.Fill(x - 7, y + 7, x - 3, y + 12, m);
            }, separate: true);
            c.Fill(x - 7, y + 7, x + 7, y + 7, K.Metal[Sh]);
            for (int rx = x - 5; rx <= x + 6; rx += 3)
                c.Set(rx, y + 7, K.Brass[Lt]);
            Finish(c);
        }

        // ------------------------------------------------------------------ Body gear

        private static void Sleeve(PixelCanvas c, DollRig r, Color32[] ramp, bool near, int width = 3, float reach = 0.6f)
        {
            var shoulder = near ? r.NearShoulder : r.FarShoulder;
            var hand = near ? r.NearHand : r.FarHand;
            Piece(c, ramp, (p, m) => p.Thick(shoulder, Lerp(shoulder, hand, reach), m, width), far: !near, separate: near);
        }

        private static void Fatigues(PixelCanvas c, DollRig r, bool back)
        {
            Sleeve(c, r, K.Primary, near: false);
            Piece(c, K.Primary, (p, m) => Trunk(p, r, m, r.Hip + 2));
            Sleeve(c, r, K.Primary, near: true);
            Piece(c, K.Leather, (p, m) => p.Fill(r.TorsoLeft + 1, r.Hip - 1, r.TorsoRight - 1, r.Hip, m), highlight: false);
            if (back)
            {
                // Field pack on the back.
                Piece(c, K.Leather, (p, m) => p.Fill(r.HeadCx - 3, r.TorsoTop + 2, r.HeadCx + 2, r.TorsoTop + 7, m), separate: true);
                c.Fill(r.HeadCx - 3, r.TorsoTop + 4, r.HeadCx + 2, r.TorsoTop + 4, K.Leather[Sh]);
            }
            else
            {
                Piece(c, K.Accent, (p, m) => p.Fill(r.HeadCx - 2, r.TorsoTop, r.HeadCx + 2, r.TorsoTop + 1, m), highlight: false);
                c.Fill(r.HeadCx + 2, r.Hip - 1, r.HeadCx + 3, r.Hip, K.Brass[Lt]);
                c.Set(r.HeadCx + 1, r.TorsoTop + 3, K.Brass[Md]);
                c.Set(r.HeadCx + 1, r.TorsoTop + 5, K.Brass[Md]);
            }
            Finish(c);
        }

        private static void Cuirass(PixelCanvas c, DollRig r, bool back)
        {
            // Greaves over the shins.
            Piece(c, K.Metal, (p, m) => Leg(p, r, false, m, 31), far: true);
            Piece(c, K.Metal, (p, m) => Leg(p, r, true, m, 31));
            // Far pauldron and sleeve, tunic skirt, breastplate, near sleeve and pauldron.
            Piece(c, K.Metal, (p, m) => p.Ellipse(r.FarShoulder.x + 0.5f, r.FarShoulder.y - 0.5f, 3.0f, 2.6f, m), far: true);
            Sleeve(c, r, K.Primary, near: false);
            Piece(c, K.Primary, (p, m) => Trunk(p, r, m, r.Hip + 2, top: r.Hip - 1));
            Piece(c, K.Metal, (p, m) => Trunk(p, r, m, r.Hip - 1));
            Piece(c, K.Accent, (p, m) => p.Fill(r.TorsoLeft - 1, r.Hip + 2, r.TorsoRight + 1, r.Hip + 2, m), highlight: false);
            Sleeve(c, r, K.Primary, near: true);
            Piece(c, K.Metal, (p, m) => p.Ellipse(r.NearShoulder.x + 0.5f, r.NearShoulder.y - 0.5f, 3.4f, 2.9f, m), separate: true);

            if (!back)
            {
                c.Fill(r.HeadCx + 1, r.TorsoTop + 2, r.HeadCx + 1, r.Hip - 3, K.Metal[Hi]);
                c.Set(r.TorsoLeft + 2, r.TorsoTop + 3, K.Brass[Lt]);
                c.Set(r.TorsoLeft + 2, r.Hip - 3, K.Brass[Lt]);
                c.Set(r.TorsoRight - 3, r.Hip - 3, K.Brass[Lt]);
            }
            else
            {
                c.Fill(r.HeadCx, r.TorsoTop + 2, r.HeadCx, r.Hip - 3, K.Metal[Sh]);
            }
            Finish(c);
        }

        private static void Robe(PixelCanvas c, DollRig r, bool back)
        {
            int Spread(int y) => (y - r.TorsoTop) / 4;
            Sleeve(c, r, K.Primary, near: false, width: 4, reach: 0.62f);
            Piece(c, K.Primary, (p, m) =>
            {
                for (int y = r.TorsoTop; y <= 37; y++)
                {
                    int inset = y == r.TorsoTop ? 1 : 0;
                    p.Fill(r.TorsoLeft - Spread(y) + inset, y, r.TorsoRight + Spread(y) - inset, y, m);
                }
            });
            Piece(c, K.Copper, (p, m) =>
            {
                for (int y = 36; y <= 37; y++)
                    p.Fill(r.TorsoLeft - Spread(y), y, r.TorsoRight + Spread(y), y, m);
            }, highlight: false);
            Piece(c, K.Accent, (p, m) => p.Fill(r.TorsoLeft - Spread(r.Hip), r.Hip - 1, r.TorsoRight + Spread(r.Hip), r.Hip, m), highlight: false);
            // Folds in the skirt.
            for (int fx = r.TorsoLeft - 1; fx <= r.TorsoRight; fx += 4)
                c.Line(fx, r.Hip + 2, fx - (fx < r.HeadCx ? 1 : -1), 35, K.Primary[Sh]);
            Sleeve(c, r, K.Primary, near: true, width: 4, reach: 0.62f);
            if (back)
            {
                // Hood folded on the shoulders.
                Piece(c, K.Primary, (p, m) => p.Ellipse(r.HeadCx + 0.5f, r.TorsoTop + 0.5f, 4.6f, 2.3f, m), separate: true);
            }
            else
            {
                c.Fill(r.HeadCx + 2, r.TorsoTop + 1, r.HeadCx + 2, r.Hip - 2, K.Copper[Lt]);
                c.Fill(r.HeadCx + 2, r.Hip + 1, r.HeadCx + 2, 35, K.Copper[Lt]);
                c.Fill(r.HeadCx - 1, r.TorsoTop, r.HeadCx + 2, r.TorsoTop, K.Copper[Md]);
            }
            Finish(c);
        }

        // ------------------------------------------------------------------ Weapons and shield

        private static void Saber(PixelCanvas c, DollRig r)
        {
            var hand = r.NearHand;
            float a = r.WeaponAngle;
            c.Line(Along(hand, a, -3), hand, K.Leather[Md], 1);
            c.Set(Along(hand, a, -3).x, Along(hand, a, -3).y, K.Brass[Md]);
            var guard = Along(hand, a, 1);
            c.Line(Along(guard, a + 90, 2), Along(guard, a - 90, 2), K.Brass[Md], 1);
            c.Line(Along(hand, a, 2), Along(hand, a, 13), K.Metal[Md], 2);
            c.Line(Along(hand, a, 2), Along(hand, a, 13), K.Metal[Hi], 1);
            c.Set(Along(hand, a, 14).x, Along(hand, a, 14).y, K.Metal[Lt]);
            Finish(c);
        }

        private static void Knife(PixelCanvas c, DollRig r)
        {
            var hand = r.NearHand;
            float a = r.WeaponAngle;
            c.Line(Along(hand, a, -2), hand, K.Leather[Md], 1);
            var guard = Along(hand, a, 1);
            c.Line(Along(guard, a + 90, 1), Along(guard, a - 90, 1), K.Brass[Md], 1);
            c.Line(Along(hand, a, 2), Along(hand, a, 6), K.Metal[Md], 2);
            c.Line(Along(hand, a, 2), Along(hand, a, 7), K.Metal[Hi], 1);
            Finish(c);
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
            c.Line(Along(hand, a, -7), Along(hand, a, -1), K.Leather[Md], 2);
            c.Line(Along(hand, a, -7), Along(hand, a, -2), K.Leather[Lt], 1);
            c.Line(Along(hand, a, -1), Along(hand, a, 4), K.Leather[Sh], 2);
            c.Line(Along(hand, a, 4), Along(hand, a, 15), K.Metal[Sh], 1);
            var scope = Along(Along(hand, a, 1), a + 90, 2);
            c.Line(scope, Along(scope, a, 4), K.Metal[Md], 1);
            c.Set(Along(hand, a, 6).x, Along(hand, a, 6).y, K.Brass[Lt]);
            c.Set(Along(hand, a, 11).x, Along(hand, a, 11).y, K.Brass[Lt]);
            Finish(c);
            if (r.Flash && r.Animation == DollAnimation.Attack)
            {
                var muzzle = Along(hand, a, 17);
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
            c.Line(Along(hand, a, -1), Along(hand, a, 6), K.Copper[Md], 1);
            c.Set(Along(hand, a, 3).x, Along(hand, a, 3).y, K.Brass[Lt]);
            Finish(c);
            Glow(c, Along(hand, a, 7), r.Glow);
        }

        private static void Staff(PixelCanvas c, DollRig r)
        {
            var hand = r.NearHand;
            float a = r.Animation == DollAnimation.Attack ? r.WeaponAngle : 88f;
            float below = r.Animation == DollAnimation.Attack ? 3f : Mathf.Max(2f, 39 - hand.y - 1);
            c.Line(Along(hand, a, -below), Along(hand, a, 12), K.Leather[Md], 1);
            var top = Along(hand, a, 13);
            Piece(c, K.Metal, (p, m) => p.Ellipse(top.x + 0.5f, top.y + 0.5f, 2.3f, 2.3f, m));
            Finish(c);
            Glow(c, top, r.Glow);
        }

        private static void Buckler(PixelCanvas c, DollRig r)
        {
            float cx = r.FarHand.x - 0.5f, cy = r.FarHand.y - 1.5f;
            Piece(c, K.Metal, (p, m) => p.Ellipse(cx, cy, 4.0f, 4.6f, m));
            int bx = Mathf.FloorToInt(cx), by = Mathf.FloorToInt(cy);
            c.Fill(bx, by, bx + 1, by + 1, K.Brass[Md]);
            c.Set(bx, by, K.Brass[Hi]);
            Finish(c);
        }
    }
}
