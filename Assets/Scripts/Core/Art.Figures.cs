using System;

namespace TheTrail.Core
{
    /// <summary>The wagon, the team, people and game animals, drawn from simple shapes so they can move.</summary>
    public static partial class Art
    {
        /// <summary>A covered wagon facing right. x is its left end, baseY the ground. wheel = turn angle.</summary>
        public static void Wagon(Canvas c, int x, int baseY, float wheel, float tilt = 0, bool worn = false)
        {
            int boxTop = baseY - 18, boxBot = baseY - 10;
            int bob = (int)Math.Round(Math.Sin(wheel * 2) * 0.6f);
            boxTop += bob; boxBot += bob;
            // Running gear.
            c.HLine(x + 8, x + 44, baseY - 8, Pal.WoodDark);
            // Box.
            c.Fill(x + 2, boxTop, 46, boxBot - boxTop, Pal.Wood);
            c.HLine(x + 2, x + 47, boxTop, Pal.GoldDark);
            c.HLine(x + 2, x + 47, boxBot - 1, Pal.WoodDark);
            for (int i = x + 8; i < x + 46; i += 9) c.VLine(i, boxTop + 1, boxBot - 2, Pal.WoodDark);
            // Bonnet: a cloth cover on hoops, flared at both ends.
            int top = boxTop - 20;
            for (int i = 0; i < 48; i++)
            {
                float u = i / 47f;
                int edge = top + (int)(Math.Pow(Math.Abs(u - 0.5f) * 2, 4) * 6);
                if (i < 3 || i > 44) edge -= 3 - Math.Min(i, 47 - i);
                byte col = (i % 9 == 4) ? Pal.ClothShade : (worn && Hash(i, 7) > 0.8f ? Pal.ClothShade : Pal.Cloth);
                c.VLine(x + 1 + i, edge, boxTop - 1, col);
                c.Set(x + 1 + i, edge, Pal.ClothShade);
            }
            c.VLine(x + 1, top + 3, boxTop - 1, Pal.ClothShade);
            c.Fill(x - 1, boxTop - 5, 3, 5, Pal.ClothShade);
            // Wheels: big at the back, small at the front.
            Wheel(c, x + 11, baseY - 8, 8, wheel);
            Wheel(c, x + 40, baseY - 6, 6, wheel * 1.33f);
            // Tongue toward the team.
            c.Line(x + 46, baseY - 7, x + 58, baseY - 9, Pal.WoodDark);
        }

        public static void Wheel(Canvas c, int cx, int cy, int r, float a)
        {
            c.Ring(cx, cy, r, Pal.WoodDark);
            c.Ring(cx, cy, r - 1, Pal.Wood);
            for (int k = 0; k < 6; k++)
            {
                double t = a + k * Math.PI / 3;
                c.Line(cx, cy, cx + (int)Math.Round(Math.Cos(t) * (r - 1)), cy + (int)Math.Round(Math.Sin(t) * (r - 1)), Pal.Wood);
            }
            c.Disc(cx, cy, 1, Pal.WoodDark);
        }

        /// <summary>An ox facing right. frame animates the legs. variant picks the coat.</summary>
        public static void Ox(Canvas c, int x, int baseY, float frame, int variant)
        {
            byte coat = variant % 3 == 0 ? Pal.Earth : variant % 3 == 1 ? Pal.WoodDark : Pal.RedRockDark;
            byte light = variant % 3 == 2 ? Pal.Cloth : Pal.Sand;
            int by = baseY - 10;
            Legs(c, x, baseY, by + 3, frame, 7, Pal.EarthDark);
            c.Ellipse(x, by, 9, 4, coat);
            c.Fill(x - 8, by + 2, 15, 2, coat);
            if (variant % 2 == 1) c.Ellipse(x - 3, by + 1, 3, 2, light);
            // Head and horns, low and forward.
            c.Fill(x + 8, by - 3, 5, 5, coat);
            c.Fill(x + 12, by - 1, 2, 3, Pal.EarthDark);
            c.HLine(x + 7, x + 9, by - 4, Pal.Cloth);
            c.Set(x + 6, by - 5, Pal.Cloth);
            // Tail.
            c.Line(x - 9, by - 1, x - 11, by + 5, coat);
        }

        public static void Mule(Canvas c, int x, int baseY, float frame, int variant)
        {
            byte coat = variant % 2 == 0 ? Pal.Earth : Pal.RockDark;
            int by = baseY - 11;
            Legs(c, x, baseY, by + 3, frame, 6, Pal.EarthDark);
            c.Ellipse(x, by, 7, 3, coat);
            c.Line(x + 6, by - 1, x + 9, by - 6, coat);
            c.Line(x + 7, by - 1, x + 10, by - 6, coat);
            c.Fill(x + 9, by - 7, 5, 3, coat);
            c.Set(x + 13, by - 6, Pal.Black);
            c.VLine(x + 9, by - 11, by - 7, coat);
            c.VLine(x + 11, by - 11, by - 7, coat);
            c.Line(x - 7, by - 1, x - 9, by + 4, Pal.Black);
        }

        static void Legs(Canvas c, int x, int baseY, int hip, float frame, int half, byte col)
        {
            float s = (float)Math.Sin(frame);
            int a = (int)Math.Round(s * 2), b = -a;
            c.Line(x - half + 1, hip, x - half + 1 + a, baseY, col);
            c.Line(x - half + 3, hip, x - half + 3 + b, baseY, col);
            c.Line(x + half - 3, hip, x + half - 3 + b, baseY, col);
            c.Line(x + half - 1, hip, x + half - 1 + a, baseY, col);
        }

        /// <summary>The wagon with its team in pairs ahead of it. x is the wagon's left end.</summary>
        public static void Outfit(Canvas c, int x, int baseY, int animals, TeamKind kind, float time, bool moving)
        {
            float frame = moving ? time * 7 : 0;
            int pairs = Math.Max(1, Math.Min(3, (animals + 1) / 2));
            for (int p = pairs - 1; p >= 0; p--)
            {
                int ax = x + 64 + p * 22;
                if (kind == TeamKind.Mules) { Mule(c, ax + 2, baseY - 1, frame + p, p); Mule(c, ax, baseY + 1, frame + p + 1.5f, p + 1); }
                else { Ox(c, ax + 2, baseY - 1, frame + p, p); Ox(c, ax, baseY + 1, frame + p + 1.5f, p + 1); }
            }
            c.Line(x + 57, baseY - 9, x + 64 + (pairs - 1) * 22 + 6, baseY - 10, Pal.WoodDark);
            Wagon(c, x, baseY, moving ? -time * 4 : 0);
            // A driver walking beside the team with a whip.
            Walker(c, x + 60, baseY + 3, moving ? time * 6 : 0, Pal.Blue, 0);
        }

        /// <summary>A walking person facing right, about 14 pixels tall. hat 0 slouch hat, 1 bonnet, 2 bare.</summary>
        public static void Walker(Canvas c, int x, int baseY, float frame, byte shirt, int hat, bool faceLeft = false)
        {
            int s = (int)Math.Round(Math.Sin(frame) * 2);
            c.Line(x, baseY - 5, x - s, baseY, Pal.EarthDark);
            c.Line(x + 1, baseY - 5, x + 1 + s, baseY, Pal.EarthDark);
            c.Fill(x - 1, baseY - 11, 4, 6, shirt);
            c.Line(x + (faceLeft ? -1 : 3), baseY - 10, x + (faceLeft ? -2 - s / 2 : 4 + s / 2), baseY - 6, Pal.Skin);
            c.Disc(x + 1, baseY - 13, 2, Pal.Skin);
            if (hat == 0) { c.HLine(x - 2, x + 4, baseY - 14, Pal.WoodDark); c.Fill(x - 1, baseY - 16, 4, 2, Pal.WoodDark); }
            else if (hat == 1) { c.Fill(x - 1, baseY - 16, 4, 3, Pal.Cloth); c.Set(faceLeft ? x + 3 : x - 1, baseY - 13, Pal.Cloth); }
            else c.HLine(x, x + 2, baseY - 15, Pal.WoodDark);
        }

        /// <summary>A standing figure for camp scenes, larger, with optional pan in hand.</summary>
        public static void Figure(Canvas c, int x, int baseY, byte shirt, int hat, int pose, float time)
        {
            // pose 0 stand, 1 crouch with pan, 2 swing pick
            if (pose == 1)
            {
                c.Fill(x - 2, baseY - 6, 6, 3, Pal.EarthDark);
                c.Fill(x - 1, baseY - 12, 5, 7, shirt);
                c.Disc(x + 3, baseY - 14, 2, Pal.Skin);
                c.HLine(x + 1, x + 6, baseY - 16, Pal.WoodDark);
                int sw = (int)Math.Round(Math.Sin(time * 5) * 1.5f);
                c.Ellipse(x + 8 + sw, baseY - 5, 4, 1, Pal.RockDark);
                c.HLine(x + 5 + sw, x + 11 + sw, baseY - 6, Pal.GoldDark);
                return;
            }
            c.Line(x, baseY - 7, x - 1, baseY, Pal.EarthDark);
            c.Line(x + 2, baseY - 7, x + 3, baseY, Pal.EarthDark);
            c.Fill(x - 1, baseY - 14, 5, 7, shirt);
            c.Disc(x + 1, baseY - 16, 2, Pal.Skin);
            if (hat == 0) { c.HLine(x - 2, x + 4, baseY - 18, Pal.WoodDark); c.Fill(x - 1, baseY - 20, 5, 2, Pal.WoodDark); }
            else if (hat == 1) c.Fill(x - 1, baseY - 20, 5, 3, Pal.Cloth);
            if (pose == 2)
            {
                double a = -1.2 + Math.Abs(Math.Sin(time * 3)) * 1.8;
                int hx = x + 2 + (int)(Math.Cos(a) * 9), hy = baseY - 13 + (int)(Math.Sin(a) * 9);
                c.Line(x + 3, baseY - 12, hx, hy, Pal.Wood);
                c.Line(hx - 3, hy + 2, hx + 3, hy - 2, Pal.Rock);
            }
        }

        // ---- Game ---------------------------------------------------------------------------------

        /// <summary>A buffalo facing right (or left), head down to graze or up and alert.</summary>
        public static void Buffalo(Canvas c, int x, int baseY, float frame, bool left, bool headUp, int scale = 1)
        {
            int d = left ? -1 : 1, s = scale;
            Legs4(c, x, baseY, baseY - 7 * s, frame, 7 * s, Pal.Black);
            c.Ellipse(x - d * 2 * s, baseY - 11 * s, 9 * s, 5 * s, Pal.EarthDark);
            c.Ellipse(x + d * 4 * s, baseY - 13 * s, 6 * s, 7 * s, Pal.WoodDark);   // hump and shaggy shoulders
            int hy = headUp ? baseY - 14 * s : baseY - 6 * s;
            c.Ellipse(x + d * 10 * s, hy, 3 * s, 3 * s, Pal.Black);
            c.Set(x + d * 10 * s, hy - 3 * s, Pal.Cloth);
            c.Line(x - d * 11 * s, baseY - 12 * s, x - d * 12 * s, baseY - 7 * s, Pal.Black);
        }

        public static void Antelope(Canvas c, int x, int baseY, float frame, bool left, bool headUp, int scale = 1)
        {
            int d = left ? -1 : 1, s = scale;
            Legs4(c, x, baseY, baseY - 6 * s, frame, 5 * s, Pal.EarthDark);
            c.Ellipse(x, baseY - 8 * s, 6 * s, 3 * s, Pal.Sand);
            c.Ellipse(x, baseY - 7 * s, 5 * s, 1 * s, Pal.Cloth);
            c.Fill(x - d * 6 * s - (left ? 0 : 1), baseY - 9 * s, 2 * s, 3 * s, Pal.Cloth);
            int nx = x + d * 5 * s, ny = baseY - 9 * s;
            int hx = x + d * 9 * s, hy = headUp ? baseY - 15 * s : baseY - 4 * s;
            c.Line(nx, ny, hx, hy, Pal.Sand);
            c.Line(nx + d, ny, hx + d, hy, Pal.Sand);
            c.Fill(hx - s, hy - s, 3 * s, 2 * s, Pal.Sand);
            c.Line(hx, hy - s, hx, hy - 4 * s, Pal.Black);
        }

        public static void Deer(Canvas c, int x, int baseY, float frame, bool left, bool headUp, int scale = 1)
        {
            int d = left ? -1 : 1, s = scale;
            Legs4(c, x, baseY, baseY - 6 * s, frame, 5 * s, Pal.EarthDark);
            c.Ellipse(x, baseY - 8 * s, 6 * s, 3 * s, Pal.Earth);
            c.Fill(x - d * 6 * s - (left ? 0 : 1), baseY - 10 * s, 2 * s, 2 * s, Pal.Cloth);
            int nx = x + d * 5 * s, ny = baseY - 9 * s;
            int hx = x + d * 8 * s, hy = headUp ? baseY - 15 * s : baseY - 4 * s;
            c.Line(nx, ny, hx, hy, Pal.Earth);
            c.Line(nx + d, ny, hx + d, hy, Pal.Earth);
            c.Fill(hx - s, hy - s, 3 * s, 2 * s, Pal.Earth);
            c.Line(hx, hy - s, hx - d * 2 * s, hy - 5 * s, Pal.WoodDark);
            c.Line(hx, hy - s, hx + d * 2 * s, hy - 5 * s, Pal.WoodDark);
        }

        static void Legs4(Canvas c, int x, int baseY, int hip, float frame, int half, byte col)
        {
            float s = (float)Math.Sin(frame);
            int a = (int)Math.Round(s * half * 0.35f), b = -a;
            c.Line(x - half + 1, hip, x - half + 1 + a, baseY, col);
            c.Line(x - half + 3, hip, x - half + 3 + b, baseY, col);
            c.Line(x + half - 3, hip, x + half - 3 + b, baseY, col);
            c.Line(x + half - 1, hip, x + half - 1 + a, baseY, col);
        }
    }
}
