using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    /// <summary>The landmark pictures: each stop on the road has its own scene.</summary>
    public static partial class Art
    {
        /// <summary>Draws the named scene into a rectangle. Unknown names get open prairie.</summary>
        public static void Scene(Canvas c, string art, int x, int y, int w, int h, float time, int seed = 1, int tod = 0, Sky weather = Core.Sky.Fair)
        {
            c.Clip(x, y, w, h);
            int hy = y + (int)(h * 0.56f);
            Sky(c, x, y, w, hy - y + 2, tod, time, seed, weather);
            switch (art)
            {
                case "town": Town(c, x, y, w, h, hy, time); break;
                case "river": RiverScene(c, x, y, w, h, hy, time, seed, false); break;
                case "ferry": RiverScene(c, x, y, w, h, hy, time, seed, true); break;
                case "fort": Fort(c, x, y, w, h, hy, time, seed); break;
                case "hollow": Hollow(c, x, y, w, h, hy, time); break;
                case "courthouse": Courthouse(c, x, y, w, h, hy); break;
                case "gate": Gate(c, x, y, w, h, hy, time); break;
                case "pass": Pass(c, x, y, w, h, hy, time); break;
                case "valley": Valley(c, x, y, w, h, hy, time); break;
                case "saltlake": SaltLake(c, x, y, w, h, hy, time); break;
                case "rocks": Rocks(c, x, y, w, h, hy); break;
                case "wells": Wells(c, x, y, w, h, hy, time); break;
                case "humboldt": Humboldt(c, x, y, w, h, hy, time); break;
                case "meadow": Meadow(c, x, y, w, h, hy); break;
                case "sink": Sink(c, x, y, w, h, hy, time); break;
                case "carson": Carson(c, x, y, w, h, hy, time); break;
                case "summit": Summit(c, x, y, w, h, hy); break;
                case "diggings": Diggings(c, x, y, w, h, hy, time); break;
                case "blackrock": BlackRock(c, x, y, w, h, hy, time); break;
                case "canyon": Canyon(c, x, y, w, h, hy, time); break;
                case "lake": Lake(c, x, y, w, h, hy, time); break;
                case "title": TitleArt(c, x, y, w, h, hy, time); break;
                default: Open(c, x, y, w, h, hy, Ground.Prairie, seed); break;
            }
            c.NoClip();
        }

        static void Open(Canvas c, int x, int y, int w, int h, int hy, Ground g, int seed)
        {
            Hills(c, x, w, hy + 2, 18, 0.012f, seed, Pal.FarRamp, 0.1f, 0.6f);
            GroundPlane(c, x, hy, w, y + h - hy, g, 0, seed);
        }

        static void Town(Canvas c, int x, int y, int w, int h, int hy, float time)
        {
            Hills(c, x, w, hy - 8, 14, 0.01f, 3, new[] { Pal.GrassDeep, Pal.Grass, Pal.Sage }, 0.2f, 0.7f);
            Water(c, x, hy - 8, w, 12, time, 0.6f);
            for (int i = 0; i < w; i += 9) Cottonwood(c, x + i + (int)(Hash(i, 5) * 6), hy - 8, 12 + (int)(Hash(i, 6) * 6), i);
            GroundPlane(c, x, hy + 4, w, y + h - hy - 4, Ground.Prairie, 0, 4);
            c.Fill(x, hy + 2, w, 3, Pal.Sand);
            // Steamboat at the landing.
            int sx = x + w - 150;
            c.Fill(sx, hy - 10, 90, 6, Pal.Cloth);
            c.Fill(sx + 6, hy - 16, 70, 6, Pal.Cloth);
            c.HLine(sx, sx + 90, hy - 4, Pal.RedDark);
            c.Fill(sx + 20, hy - 34, 4, 18, Pal.Black); c.Fill(sx + 30, hy - 34, 4, 18, Pal.Black);
            for (int k = 0; k < 6; k++)
            {
                int px = sx + 22 + (int)(k * 6 + time * 4) % 40 - k, py = hy - 38 - k * 4;
                c.Disc(px, py, 2 + k / 2, k < 3 ? Pal.Rock : Pal.RockLight);
            }
            c.Disc(sx + 70, hy - 7, 6, Pal.RedDark);
            // Main street.
            int bx = x + 10;
            byte[] walls = { Pal.Wood, Pal.Cloth, Pal.RedRockDark, Pal.Sand, Pal.Wood, Pal.ClothShade };
            for (int i = 0; i < 6; i++)
            {
                int bw = 30 + (int)(Hash(i, 2) * 14), bh = 26 + (int)(Hash(i, 3) * 16);
                Building(c, bx, hy + 18, bw, bh, walls[i], Pal.WoodDark, true, i % 2 == 0);
                bx += bw + 4;
            }
            Wagon(c, x + 60, y + h - 10, 0);
            Walker(c, x + 120, y + h - 8, time * 3, Pal.Red, 0);
            Walker(c, x + 160, y + h - 12, 0, Pal.Blue, 1, true);
            Walker(c, x + 210, y + h - 6, time * 4 + 1, Pal.Earth, 0);
        }

        static void RiverScene(Canvas c, int x, int y, int w, int h, int hy, float time, int seed, bool ferry)
        {
            Hills(c, x, w, hy + 2, 20, 0.012f, seed, Pal.FarRamp, 0.1f, 0.6f);
            GroundPlane(c, x, hy, w, y + h - hy, Ground.Plains, 0, seed);
            int ry = hy + 14, rh = ferry ? 38 : 30;
            c.Fill(x, ry - 3, w, 3, Pal.Sand);
            Water(c, x, ry, w, rh, time, ferry ? 1.6f : 1f);
            c.Fill(x, ry + rh, w, 3, Pal.Sand);
            for (int i = 0; i < w; i += 22) Cottonwood(c, x + i + (int)(Hash(i, seed) * 14), ry - 2, 18 + (int)(Hash(i, seed + 1) * 12), i + seed);
            if (ferry)
            {
                c.HLine(x, x + w, ry + 6, Pal.WoodDark);
                int fx = x + 150 + (int)(Math.Sin(time * 0.3f) * 60);
                c.Fill(fx - 6, ry + 14, 70, 7, Pal.Wood);
                c.HLine(fx - 6, fx + 63, ry + 14, Pal.GoldDark);
                Wagon(c, fx, ry + 14, 0);
                c.Line(fx + 30, ry + 6, fx + 30, ry + 12, Pal.WoodDark);
                for (int k = 0; k < 4; k++) Wagon(c, x + 20 + k * 58, y + h - 4 - (k % 2) * 4, 0);
            }
            else Boulder(c, x + w - 70, y + h - 10, 10);
        }

        static void Fort(Canvas c, int x, int y, int w, int h, int hy, float time, int seed)
        {
            Hills(c, x, w, hy + 2, 16, 0.01f, seed, Pal.FarRamp, 0.15f, 0.6f);
            GroundPlane(c, x, hy, w, y + h - hy, Ground.Plains, 0, seed);
            int fx = x + w / 2 - 90, fb = hy + 26;
            c.Fill(fx, fb - 26, 180, 26, Pal.SandLight);
            c.Fill(fx, fb - 26, 180, 3, Pal.Sand);
            for (int i = 0; i < 180; i += 6) c.VLine(fx + i, fb - 23, fb - 1, Pal.Sand);
            c.Fill(fx + 76, fb - 16, 26, 16, Pal.WoodDark);
            c.Fill(fx - 10, fb - 40, 26, 40, Pal.Sand); c.Fill(fx - 10, fb - 40, 26, 3, Pal.Earth);
            c.Fill(fx + 164, fb - 40, 26, 40, Pal.Sand); c.Fill(fx + 164, fb - 40, 26, 3, Pal.Earth);
            c.Fill(fx - 4, fb - 32, 4, 4, Pal.Black); c.Fill(fx + 172, fb - 32, 4, 4, Pal.Black);
            Flag(c, fx + 90, fb - 62, time);
            for (int k = 0; k < 4; k++) Tent(c, fx - 60 + k * 18, fb + 14 + (k % 2) * 3, 14);
            if (seed % 2 == 1) for (int k = 0; k < 4; k++) Tipi(c, fx + 200 + k * 24, fb + 12 + (k % 2) * 4, 16);
            Wagon(c, x + 26, y + h - 6, 0);
            Walker(c, x + w - 80, y + h - 8, time * 3, Pal.Blue, 0);
        }

        static void Hollow(Canvas c, int x, int y, int w, int h, int hy, float time)
        {
            Hills(c, x, w, hy + 4, 20, 0.012f, 8, Pal.FarRamp, 0.1f, 0.6f);
            GroundPlane(c, x, hy, w, y + h - hy, Ground.Plains, 0, 8);
            // Windlass Hill: a steep slope down from the left.
            var slope = new List<(int, int)> { (x, y + 30), (x + 70, y + 34), (x + 240, y + h), (x, y + h) };
            c.Poly(slope, Pal.DryGrass);
            c.Line(x + 70, y + 34, x + 240, y + h, Pal.Earth);
            c.Line(x + 70, y + 35, x + 240, y + h + 1, Pal.Earth);
            // A wagon on ropes halfway down.
            int wx = x + 120, wy = y + 34 + (int)((120 - 70) / 170f * (h - 34)) + 8;
            Wagon(c, wx - 20, wy, 0);
            c.Line(x + 40, y + 30, wx - 18, wy - 14, Pal.Cloth);
            c.Fill(x + 36, y + 24, 6, 8, Pal.WoodDark);
            Walker(c, x + 30, y + 31, 0, Pal.Red, 0);
            // The ash grove and spring at the bottom.
            for (int i = 0; i < 6; i++) Cottonwood(c, x + 270 + i * 34, hy + 30 + (i % 2) * 6, 34 + (i % 3) * 6, i * 3);
            c.Ellipse(x + w - 70, y + h - 16, 30, 5, Pal.Water);
            c.Ellipse(x + w - 74, y + h - 17, 18, 2, Pal.WaterLight);
        }

        static void Courthouse(Canvas c, int x, int y, int w, int h, int hy)
        {
            GroundPlane(c, x, hy, w, y + h - hy, Ground.Plains, 0, 12);
            // Chimney Rock, small, far to the west.
            int cx = x + w - 60;
            c.Tri(cx - 20, hy + 2, cx + 20, hy + 2, cx, hy - 12, Pal.FarLight);
            c.Fill(cx - 2, hy - 34, 4, 24, Pal.FarLight);
            // Courthouse Rock: a great block with a domed top; the Jail beside it.
            int bx = x + 90, by = hy + 30;
            var rock = new List<(int, int)> { (bx, by), (bx + 20, by - 50), (bx + 40, by - 62), (bx + 110, by - 64), (bx + 140, by - 50), (bx + 170, by) };
            c.Poly(rock, Pal.Sand);
            for (int j = 0; j < 64; j += 7) c.HLine(bx + 22, bx + 150, by - j, Pal.SandLight);
            var shade = new List<(int, int)> { (bx + 110, by - 64), (bx + 140, by - 50), (bx + 170, by), (bx + 120, by) };
            c.Poly(shade, Pal.Earth);
            c.Ellipse(bx + 75, by - 64, 26, 6, Pal.SandLight);
            var jail = new List<(int, int)> { (bx + 180, by), (bx + 192, by - 26), (bx + 220, by - 28), (bx + 234, by) };
            c.Poly(jail, Pal.Sand);
            c.Tri(bx + 220, by - 28, bx + 234, by, bx + 222, by, Pal.Earth);
            Wagon(c, x + 30, y + h - 8, 0);
        }

        static void Gate(Canvas c, int x, int y, int w, int h, int hy, float time)
        {
            // Independence Rock, a pale dome behind on the left.
            c.Ellipse(x + 80, hy + 6, 70, 18, Pal.FarLight);
            c.Ellipse(x + 80, hy + 2, 60, 14, Pal.RockLight);
            GroundPlane(c, x, hy + 6, w, y + h - hy - 6, Ground.Sage, 0, 14);
            // The granite ridge with its deep cleft.
            int rx = x + 180, ry = hy + 34;
            var left = new List<(int, int)> { (rx, ry), (rx + 20, ry - 50), (rx + 70, ry - 64), (rx + 108, ry - 58), (rx + 116, ry) };
            var right = new List<(int, int)> { (rx + 132, ry), (rx + 140, ry - 60), (rx + 190, ry - 68), (rx + 260, ry - 40), (rx + 300, ry) };
            c.Poly(left, Pal.Rock); c.Poly(right, Pal.Rock);
            for (int i = 0; i < 300; i += 3)
                for (int j = 0; j < 70; j += 4)
                    if (Hash(i * 31 + j, 3) > 0.7f && c.Get(rx + i, ry - j) == Pal.Rock) c.Set(rx + i, ry - j, Hash(i + j, 9) > 0.5f ? Pal.RockLight : Pal.RockDark);
            c.Poly(new List<(int, int)> { (rx + 108, ry - 58), (rx + 116, ry), (rx + 124, ry) }, Pal.RockDark);
            Water(c, rx + 112, ry - 4, 16, 8, time);
            c.Poly(new List<(int, int)> { (rx + 112, ry + 4), (rx + 128, ry + 4), (rx + 160, y + h), (rx + 70, y + h) }, Pal.Water);
            Water(c, rx + 90, y + h - 12, 60, 12, time);
            Wagon(c, x + 40, y + h - 8, 0);
        }

        static void Pass(Canvas c, int x, int y, int w, int h, int hy, float time)
        {
            Range(c, x + w / 2, w / 2, hy + 4, 34, 0.02f, 21, Pal.Far, Pal.FarLight, 0, hy - 16);
            Hills(c, x, w, hy + 8, 14, 0.006f, 22, Pal.SageRamp, 0.2f, 0.7f);
            GroundPlane(c, x, hy + 8, w, y + h - hy - 8, Ground.Sage, time * 2, 22);
            // Wind streaks across the saddle.
            for (int i = 0; i < 6; i++)
            {
                int sx = x + (int)((Hash(i, 3) * w + time * 90) % w), sy = hy + 16 + (int)(Hash(i, 4) * 40);
                c.HLine(sx, sx + 14, sy, Pal.Haze);
            }
            // A cairn and a sign.
            Boulder(c, x + 120, hy + 32, 8); Boulder(c, x + 124, hy + 26, 6); Boulder(c, x + 121, hy + 21, 4);
            Wagon(c, x + 260, y + h - 8, 0);
        }

        static void Valley(Canvas c, int x, int y, int w, int h, int hy, float time)
        {
            Range(c, x, w, hy + 2, 28, 0.015f, 31, Pal.Far, Pal.FarLight, 0, hy - 30);
            Hills(c, x, w, hy + 10, 18, 0.01f, 32, Pal.GrassRamp, 0.1f, 0.7f);
            GroundPlane(c, x, hy + 10, w, y + h - hy - 10, Ground.Prairie, 0, 33);
            // A winding river through the meadow.
            for (int j = hy + 12; j < y + h; j++)
            {
                float t = (j - hy) / (float)(h - (hy - y));
                int cxr = x + w / 2 + (int)(Math.Sin(t * 6) * 60 * t);
                int half = 2 + (int)(t * 14);
                for (int i = -half; i <= half; i++) c.SetRamp(cxr + i, j, Pal.WaterRamp, 0.3f + (Noise((i + time * 10) * 0.2f, j) * 0.6f));
            }
            for (int k = 0; k < 4; k++) Cottonwood(c, x + 40 + k * 110, hy + 22 + (k % 2) * 10, 20, k * 5);
        }

        static void SaltLake(Canvas c, int x, int y, int w, int h, int hy, float time)
        {
            Range(c, x, w, hy, 46, 0.018f, 41, Pal.Far, Pal.FarLight, 0, hy - 32);
            c.Fill(x, hy, w, 8, Pal.SkyPale);
            for (int i = 0; i < w; i += 2) if (Hash(i + (int)(time * 3), 4) > 0.8f) c.Set(x + i, hy + 3, Pal.Foam);
            GroundPlane(c, x, hy + 8, w, y + h - hy - 8, Ground.Prairie, 0, 42);
            // Fields in strips, and adobe houses along irrigation ditches.
            for (int i = 0; i < 8; i++)
            {
                int fx = x + i * 60 + 4;
                c.Fill(fx, hy + 12, 54, 10, i % 3 == 0 ? Pal.GoldMid : i % 3 == 1 ? Pal.GrassLight : Pal.Grass);
                for (int k = 0; k < 54; k += 4) c.VLine(fx + k, hy + 12, hy + 21, i % 3 == 0 ? Pal.GoldDark : Pal.GrassDeep);
            }
            c.HLine(x, x + w, hy + 23, Pal.Water);
            for (int i = 0; i < 7; i++)
            {
                int bx = x + 30 + i * 64, bw = 26 + (i % 2) * 8;
                Building(c, bx, hy + 50 + (i % 2) * 6, bw, 16, Pal.SandLight, Pal.Sand, false);
                c.Tri(bx - 2, hy + 34 + (i % 2) * 6, bx + bw + 2, hy + 34 + (i % 2) * 6, bx + bw / 2, hy + 26 + (i % 2) * 6, Pal.Earth);
            }
            Walker(c, x + 100, y + h - 6, time * 3, Pal.Blue, 1);
            Walker(c, x + 300, y + h - 10, 0, Pal.Earth, 0, true);
        }

        static void Rocks(Canvas c, int x, int y, int w, int h, int hy)
        {
            Hills(c, x, w, hy + 4, 18, 0.01f, 51, Pal.FarRamp, 0.1f, 0.6f);
            GroundPlane(c, x, hy + 4, w, y + h - hy - 4, Ground.Sage, 0, 52);
            int[] xs = { 60, 130, 190, 280, 350, 420 };
            int[] hs = { 60, 86, 50, 96, 64, 44 };
            for (int i = 0; i < xs.Length; i++)
            {
                int bx = x + xs[i], hh = hs[i], ww = 18 + hh / 5, by = hy + 34 + (i % 2) * 6;
                c.Fill(bx - ww / 2, by - hh + ww / 2, ww, hh - ww / 2, Pal.RockLight);
                c.Ellipse(bx, by - hh + ww / 2, ww / 2, ww / 2, Pal.RockLight);
                c.Fill(bx + ww / 6, by - hh + ww / 2, ww / 3, hh - ww / 2, Pal.Rock);
                for (int j = by - hh + 6; j < by; j += 9) c.HLine(bx - ww / 2, bx + ww / 2, j, Pal.Rock);
            }
            for (int k = 0; k < 5; k++) Pine(c, x + 30 + k * 95, hy + 48 + (k % 2) * 8, 14);
        }

        static void Wells(Canvas c, int x, int y, int w, int h, int hy, float time)
        {
            Range(c, x, w, hy + 2, 26, 0.016f, 61, Pal.Far, Pal.FarLight, 0, hy - 24);
            GroundPlane(c, x, hy + 2, w, y + h - hy - 2, Ground.Sage, 0, 62);
            for (int i = 0; i < 4; i++)
            {
                int px = x + 70 + i * 100, py = hy + 30 + (i % 2) * 18;
                c.Ellipse(px, py, 34, 9, Pal.Grass);
                c.Ellipse(px, py, 22, 5, Pal.WaterDeep);
                c.Ellipse(px - 3, py - 1, 12, 2, Pal.WaterLight);
            }
            Wagon(c, x + w - 120, y + h - 6, 0);
        }

        static void Humboldt(Canvas c, int x, int y, int w, int h, int hy, float time)
        {
            Hills(c, x, w, hy + 2, 26, 0.012f, 71, Pal.RedRockRamp, 0.2f, 0.8f);
            GroundPlane(c, x, hy + 2, w, y + h - hy - 2, Ground.Sage, 0, 72);
            // The shallow river over its gravel bar, willows along the bank, the road down to the ford.
            c.Fill(x, hy + 26, w, 4, Pal.Sand);
            Water(c, x, hy + 30, w, 18, time, 0.4f);
            for (int i = 0; i < w; i += 2) if (Hash(i, 73) > 0.55f) c.Set(x + i, hy + 36 + (int)(Hash(i, 74) * 8), Pal.SandLight);
            c.Fill(x, hy + 48, w, 3, Pal.Sand);
            for (int k = 0; k < 9; k++) Cottonwood(c, x + 14 + k * 52 + (int)(Hash(k, 75) * 14), hy + 28, 9 + (int)(Hash(k, 76) * 5), k + 40);
            c.Poly(new List<(int, int)> { (x + 250, y + h), (x + 300, hy + 48), (x + 318, hy + 48), (x + 290, y + h) }, Pal.Earth);
            Wagon(c, x + 330, hy + 44, time * 0.5f);
        }

        static void Meadow(Canvas c, int x, int y, int w, int h, int hy)
        {
            Hills(c, x, w, hy + 2, 24, 0.012f, 81, Pal.FarRamp, 0.1f, 0.6f);
            GroundPlane(c, x, hy + 2, w, y + h - hy - 2, Ground.Sage, 0, 82);
            // The fork: two roads and a post with boards pointing each way.
            c.Poly(new List<(int, int)> { (x + 200, y + h), (x + 240, y + h), (x + 236, hy + 2), (x + 232, hy + 2) }, Pal.Earth);
            c.Poly(new List<(int, int)> { (x + 236, hy + 40), (x + 244, hy + 40), (x + 360, hy + 6), (x + 356, hy + 6) }, Pal.Earth);
            c.Fill(x + 250, hy + 20, 3, 46, Pal.WoodDark);
            c.Fill(x + 220, hy + 22, 30, 7, Pal.Wood); c.Tri(x + 214, hy + 25, x + 220, hy + 21, x + 220, hy + 29, Pal.Wood);
            c.Fill(x + 253, hy + 32, 32, 7, Pal.Wood); c.Tri(x + 291, hy + 35, x + 285, hy + 31, x + 285, hy + 39, Pal.Wood);
            c.HLine(x + 224, x + 244, hy + 25, Pal.WoodDark); c.HLine(x + 257, x + 280, hy + 35, Pal.WoodDark);
        }

        static void Sink(Canvas c, int x, int y, int w, int h, int hy, float time)
        {
            Range(c, x, w, hy + 2, 22, 0.014f, 91, Pal.Far, Pal.FarLight);
            c.Fill(x, hy + 2, w, y + h - hy, Pal.Haze);
            for (int j = hy + 2; j < y + h; j += 2)
                for (int i = 0; i < w; i += 2)
                    if (Hash(i * 7 + j * 131, 3) > 0.82f) c.Set(x + i, j, Pal.SandLight);
            c.Ellipse(x + w / 2, hy + 26, 140, 10, Pal.WaterLight);
            c.Ellipse(x + w / 2, hy + 26, 120, 6, Pal.Water);
            for (int i = 0; i < 120; i++)
            {
                int rx = x + 30 + (int)(Hash(i, 92) * (w - 60)), ry = hy + 20 + (int)(Hash(i, 93) * 16);
                int rh = 4 + (int)(Hash(i, 94) * 8);
                c.VLine(rx, ry - rh, ry, Hash(i, 95) > 0.5f ? Pal.DryGrass : Pal.Sage);
            }
            // Bones of the animals that went before.
            c.Ellipse(x + 90, y + h - 16, 8, 3, Pal.Foam); c.HLine(x + 80, x + 100, y + h - 14, Pal.Cloth);
            for (int k = 0; k < 4; k++) c.Line(x + 84 + k * 4, y + h - 14, x + 86 + k * 4, y + h - 20, Pal.Cloth);
            c.Fill(x + w - 120, y + h - 30, 40, 12, Pal.WoodDark);
            Wheel(c, x + w - 112, y + h - 16, 7, 0.4f);
        }

        static void Carson(Canvas c, int x, int y, int w, int h, int hy, float time)
        {
            Range(c, x, w, hy + 2, 50, 0.017f, 101, Pal.Far, Pal.FarLight, 0, hy - 40);
            GroundPlane(c, x, hy + 2, w, y + h - hy - 2, Ground.Sage, 0, 102);
            Water(c, x, hy + 30, w, 12, time);
            for (int k = 0; k < 7; k++) Cottonwood(c, x + 10 + k * 70, hy + 30, 30 + (k % 3) * 6, k * 7);
            // Washing hung on the bushes: Ragtown.
            byte[] rags = { Pal.Red, Pal.Blue, Pal.Cloth, Pal.GoldMid, Pal.Cloth };
            for (int k = 0; k < 10; k++)
            {
                int bx = x + 20 + k * 46, by = y + h - 18 + (k % 2) * 6;
                c.Ellipse(bx, by, 10, 5, Pal.SageDark);
                c.Fill(bx - 4, by - 5, 6, 4, rags[k % rags.Length]);
            }
        }

        static void Summit(Canvas c, int x, int y, int w, int h, int hy)
        {
            Range(c, x, w, hy + 10, 60, 0.02f, 111, Pal.Far, Pal.FarLight, 0, hy - 20);
            c.Fill(x, hy + 10, w, y + h - hy, Pal.GrassDeep);
            c.Ellipse(x + 90, hy + 28, 70, 9, Pal.WaterDeep);
            c.Ellipse(x + 86, hy + 26, 50, 4, Pal.Water);
            for (int k = 0; k < 14; k++) Pine(c, x + 10 + k * 34, hy + 22 + (k % 3) * 8, 22 + (k % 4) * 6, true);
            // The granite pitch the wagons are hauled up.
            var slab = new List<(int, int)> { (x + 200, y + h), (x + 330, y + 40), (x + w, y + 30), (x + w, y + h) };
            c.Poly(slab, Pal.RockLight);
            Speckle(c, x + 200, y + 26, w - 200, h - 26, Pal.RockLight, new[] { Pal.Rock, Pal.SnowShade, Pal.Rock }, 0.22f, 113);
            c.Line(x + 200, y + h, x + 330, y + 40, Pal.Rock);
            for (int i = 0; i < 6; i++)
            {
                int x0 = x + 236 + i * 40, y0 = y + h - (int)(Hash(i, 114) * 20);
                c.Line(x0, y0, x0 + 30 + (int)(Hash(i, 115) * 20), y0 - 50 - (int)(Hash(i, 116) * 30), Pal.Rock);
            }
            for (int i = 0; i < 5; i++) Boulder(c, x + 250 + i * 46, y + h - 6 - (i % 2) * 10, 4 + i % 3);
            c.Fill(x + 340, y + 26, 18, 14, Pal.WoodDark);
            c.Line(x + 349, y + 34, x + 270, y + h - 50, Pal.RockDark);
            Wagon(c, x + 240, y + h - 40, 0);
            for (int k = 0; k < 4; k++) c.Fill(x + 370 + k * 20, y + 34 - k, 10, 3, Pal.Snow);
        }

        static void Diggings(Canvas c, int x, int y, int w, int h, int hy, float time)
        {
            Hills(c, x, w, hy + 6, 34, 0.013f, 121, new[] { Pal.PineDeep, Pal.Pine, Pal.Grass }, 0.1f, 0.6f);
            for (int k = 0; k < 16; k++) Pine(c, x + 6 + k * 30, hy + 2 + (k % 3) * 6, 18 + (k % 4) * 5);
            c.Fill(x, hy + 6, w, y + h - hy, Pal.Earth);
            // The gulch: a creek with dug-over banks.
            for (int j = hy + 6; j < y + h; j++)
            {
                int cxr = x + w / 2 + (int)(Math.Sin(j * 0.07f) * 30);
                int half = 4 + (j - hy) / 6;
                for (int i = -half - 8; i <= half + 8; i++)
                    c.Set(cxr + i, j, Math.Abs(i) <= half / 2 ? Canvas.Ramp(cxr + i, j, Pal.WaterRamp, 0.25f + Noise((i + time * 14) * 0.3f, j) * 0.5f) : Math.Abs(i) <= half ? Pal.Sand : Pal.EarthDark);
            }
            for (int k = 0; k < 9; k++) c.Ellipse(x + 30 + k * 52, y + h - 22 + (k % 3) * 6, 10, 4, k % 2 == 0 ? Pal.Sand : Pal.EarthDark);
            Tent(c, x + 40, hy + 30, 22); Tent(c, x + 80, hy + 34, 18); Cabin(c, x + 330, hy + 34, 30, 18); Tent(c, x + 390, hy + 30, 22);
            // A rocker and miners at work.
            int rx = x + w / 2 + 30, ry = y + h - 26;
            c.Fill(rx, ry - 8, 22, 8, Pal.Wood);
            c.Fill(rx + (int)(Math.Sin(time * 6) * 2), ry - 12, 10, 4, Pal.WoodDark);
            Figure(c, rx - 6, ry, Pal.Red, 0, 0, time);
            Figure(c, x + w / 2 - 60, y + h - 18, Pal.Blue, 0, 1, time);
            Figure(c, x + 120, y + h - 12, Pal.Earth, 0, 2, time);
        }

        /// <summary>Speckles every pixel of one colour inside a box with others, so flat shapes read as stone.</summary>
        static void Speckle(Canvas c, int x, int y, int w, int h, byte match, byte[] with, float amount, int seed)
        {
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++)
                {
                    if (c.Get(i, j) != match) continue;
                    float n = Hash(i * 31 + j * 1013, seed);
                    if (n < amount) c.Set(i, j, with[(int)(n / amount * with.Length) % with.Length]);
                }
        }

        static void BlackRock(Canvas c, int x, int y, int w, int h, int hy, float time)
        {
            c.Fill(x, hy, w, y + h - hy, Pal.Haze);
            for (int j = hy; j < y + h; j += 2) for (int i = 0; i < w; i += 3) if (Hash(i + j * 97, 5) > 0.85f) c.Set(x + i, j, Pal.SandLight);
            var point = new List<(int, int)> { (x + 160, hy + 20), (x + 230, hy - 50), (x + 270, hy - 56), (x + 330, hy - 20), (x + 380, hy + 20) };
            Range(c, x, w, hy + 2, 18, 0.02f, 381, Pal.Far, Pal.FarLight);
            c.Fill(x, hy + 2, w, y + h - hy - 2, Pal.Haze);
            for (int j = hy + 2; j < y + h; j += 2) for (int i = 0; i < w; i += 3) if (Hash(i + j * 97, 5) > 0.85f) c.Set(x + i, j, Pal.SandLight);
            c.Poly(point, Pal.Black);
            c.Poly(new List<(int, int)> { (x + 270, hy - 56), (x + 330, hy - 20), (x + 380, hy + 20), (x + 300, hy + 20) }, Pal.RockDark);
            Speckle(c, x + 150, hy - 60, 240, 82, Pal.Black, new[] { Pal.RockDark, Pal.EarthDark }, 0.18f, 382);
            Speckle(c, x + 150, hy - 60, 240, 82, Pal.RockDark, new[] { Pal.Rock, Pal.Black }, 0.18f, 383);
            for (int k = 0; k < 6; k++) Boulder(c, x + 150 + k * 40, hy + 20 + (k % 2) * 4, 3 + k % 3, Pal.RockDark, Pal.Black, Pal.Black);
            // Bones and a wheel left by the companies ahead.
            c.Line(x + 70, hy + 50, x + 86, hy + 48, Pal.Cloth); c.Line(x + 74, hy + 52, x + 90, hy + 51, Pal.Cloth);
            Wheel(c, x + 420, hy + 46, 7, 0.4f);
            c.Ellipse(x + 200, hy + 36, 30, 6, Pal.WaterLight);
            for (int k = 0; k < 10; k++)
            {
                float t = (time * 0.6f + k * 0.1f) % 1f;
                c.Disc(x + 195 + (int)(Math.Sin(t * 6 + k) * 6), hy + 32 - (int)(t * 44), 3 + (int)(t * 5), t > 0.6f ? Pal.SkyPale : Pal.Foam);
            }
        }

        static void Canyon(Canvas c, int x, int y, int w, int h, int hy, float time)
        {
            c.Fill(x, hy, w, y + h - hy, Pal.Sand);
            var left = new List<(int, int)> { (x, y), (x + 170, y), (x + 200, y + h), (x, y + h) };
            var right = new List<(int, int)> { (x + w, y), (x + w - 170, y), (x + w - 210, y + h), (x + w, y + h) };
            c.Poly(left, Pal.RedRock); c.Poly(right, Pal.RedRockDark);
            for (int j = y; j < y + h; j += 6)
            {
                c.HLine(x, x + 170 + (j - y) * 30 / h, j, Pal.RedRockDark);
                c.HLine(x + w - 170 - (j - y) * 40 / h, x + w, j + 3, Pal.RedRock);
            }
            Water(c, x + 210, y + h - 14, w - 420, 8, time);
            c.Text(x + 40, y + 70, "J.B. 1849", Pal.SandLight);
            c.Text(x + 30, y + 100, "OHIO CO.", Pal.SandLight);
        }

        static void Lake(Canvas c, int x, int y, int w, int h, int hy, float time)
        {
            Hills(c, x, w, hy, 26, 0.01f, 131, Pal.FarRamp, 0.1f, 0.5f);
            Water(c, x, hy, w, 26, time, 0.3f);
            GroundPlane(c, x, hy + 26, w, y + h - hy - 26, Ground.Forest, 0, 132);
            for (int k = 0; k < 9; k++) Pine(c, x + 14 + k * 54, hy + 50 + (k % 2) * 10, 26 + (k % 3) * 8);
        }

        static void TitleArt(Canvas c, int x, int y, int w, int h, int hy, float time)
        {
            Range(c, x, w, hy + 14, 40, 0.012f, 141, Pal.Far, Pal.DuskPink, 0, -999, 0.6f);
            Hills(c, x, w, hy + 34, 24, 0.008f, 142, new[] { Pal.RedDark, Pal.RedRockDark, Pal.EarthDark }, 0.2f, 0.9f);
            c.Fill(x, hy + 34, w, y + h - hy, Pal.EarthDark);
            for (int j = hy + 34; j < y + h; j++) for (int i = 0; i < w; i += 2) if (Hash(i + j * 53, 7) > 0.9f) c.Set(x + i, j, Pal.WoodDark);
            // A wagon train on the ridge against the evening.
            for (int k = 0; k < 4; k++)
            {
                int wx = x + 30 + k * 115 + (int)(time * 6) % 115 - 20;
                int wy = hy + 34 - (int)(Fbm((wx - x) * 0.008f, 142, 3) * 24) + 1;
                Silhouette(c, wx, wy, time + k);
            }
        }

        /// <summary>A wagon and team as a black shape against the sky.</summary>
        static void Silhouette(Canvas c, int x, int baseY, float t)
        {
            c.Fill(x, baseY - 12, 30, 5, Pal.Black);
            c.Ellipse(x + 15, baseY - 14, 16, 8, Pal.Black);
            c.Fill(x - 1, baseY - 14, 32, 4, Pal.Black);
            c.Disc(x + 7, baseY - 5, 5, Pal.Black); c.Disc(x + 25, baseY - 4, 4, Pal.Black);
            c.Line(x + 30, baseY - 6, x + 40, baseY - 7, Pal.Black);
            for (int k = 0; k < 2; k++)
            {
                int ox = x + 44 + k * 14;
                c.Ellipse(ox, baseY - 6, 6, 3, Pal.Black); c.Fill(ox + 5, baseY - 9, 4, 4, Pal.Black);
                int s = (int)(Math.Sin(t * 6 + k) * 1.5f);
                c.Line(ox - 4, baseY - 4, ox - 4 + s, baseY, Pal.Black); c.Line(ox + 4, baseY - 4, ox + 4 - s, baseY, Pal.Black);
            }
        }
    }
}
