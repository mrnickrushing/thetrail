using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    /// <summary>Everything drawn: the wagon and oxen, animals, and the landscape pictures.</summary>
    public static class Art
    {
        // ---- Noise ------------------------------------------------------------------------------

        static float Hash(int i, int seed)
        {
            uint h = (uint)(i * 374761393 + seed * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }

        /// <summary>Smooth 1D value noise in 0..1.</summary>
        public static float Noise(float x, int seed)
        {
            int i = (int)Math.Floor(x); float f = x - i;
            f = f * f * (3 - 2 * f);
            return Hash(i, seed) * (1 - f) + Hash(i + 1, seed) * f;
        }

        public static float Fbm(float x, int seed, int oct = 3)
        {
            float a = 0, amp = 0.5f, sum = 0;
            for (int o = 0; o < oct; o++) { a += Noise(x, seed + o * 31) * amp; sum += amp; x *= 2.1f; amp *= 0.5f; }
            return a / sum;
        }

        // ---- Landscape pieces -------------------------------------------------------------------

        /// <summary>A ridge line filled down to the bottom of the clip. offset scrolls it sideways.</summary>
        public static void Ridge(Canvas c, int x0, int x1, int baseY, int bottom, float height, float freq, int seed, float shade,
            float offset = 0, float sharp = 0, float topShade = -1, int snowBand = 0)
        {
            for (int x = x0; x < x1; x++)
            {
                float u = (x + offset) * freq;
                float n = Fbm(u, seed);
                if (sharp > 0) { float tri = 1 - Math.Abs(Noise(u * 0.7f, seed + 7) * 2 - 1); n = n * (1 - sharp) + tri * sharp; }
                int top = baseY - (int)(n * height);
                for (int y = top; y < bottom; y++)
                {
                    float s = shade;
                    if (topShade >= 0 && y - top < 2) s = topShade;
                    else if (snowBand > 0 && y < baseY - height * 0.62f && y - top < snowBand) s = 4.2f;
                    c.SetShade(x, y, s);
                }
            }
        }

        public static void Pine(Canvas c, int x, int baseY, int h, byte col, byte trunk = Canvas.Dark)
        {
            int w = Math.Max(2, h / 3);
            c.VLine(x, baseY - 2, baseY, trunk);
            for (int j = 0; j < h - 2; j++)
            {
                int y = baseY - 2 - j;
                // Stacked tiers: wider at the bottom of each tier.
                float tier = (j % Math.Max(3, h / 4)) / (float)Math.Max(3, h / 4);
                int half = (int)((w * (1f - j / (float)(h - 2))) * (0.65f + 0.35f * (1 - tier)) + 0.5f);
                c.HLine(x - half, x, y, col);
                if (half > 0) c.HLine(x + 1, x + half, y, col > 1 ? (byte)(col - 1) : col);
            }
            c.Set(x, baseY - h, col);
        }

        public static void RoundTree(Canvas c, int x, int baseY, int r, float shade)
        {
            c.VLine(x, baseY - r, baseY, Canvas.Dark);
            for (int j = -r; j <= r; j++)
                for (int i = -r - 1; i <= r + 1; i++)
                {
                    float d = (i * i) / (float)((r + 1) * (r + 1)) + (j * j) / (float)(r * r);
                    if (d > 1) continue;
                    float s = shade + (i > 0 ? -0.6f : 0.3f) + (j < 0 ? 0.3f : -0.3f);
                    c.SetShade(x + i, baseY - r * 2 + j, s);
                }
        }

        public static void Bush(Canvas c, int x, int y, int w, float shade)
        {
            for (int i = 0; i < w; i++)
            {
                int h = (int)(2 + 2 * Math.Sin(i / (float)w * Math.PI) + Hash(i, x) * 2);
                for (int j = 0; j < h; j++) c.SetShade(x + i, y - j, shade + (j == h - 1 ? 0.6f : 0));
            }
        }

        public static void Stars(Canvas c, int h, int seed, float time)
        {
            for (int i = 0; i < 40; i++)
            {
                int x = (int)(Hash(i, seed) * Canvas.W), y = (int)(Hash(i + 99, seed) * h);
                bool tw = Hash(i + 7, seed) > 0.8f && ((int)(time * 2 + i) % 5 == 0);
                c.Set(x, y, tw ? Canvas.Dark : Canvas.Dim);
            }
        }

        public static void Sun(Canvas c, int x, int y, int r)
        {
            c.Disc(x, y, r, Canvas.Bright);
            c.Ring(x, y, r + 3, Canvas.Dim);
        }

        public static void Cloud(Canvas c, int x, int y, int w)
        {
            for (int i = 0; i < w; i++)
            {
                float t = i / (float)w;
                int h = (int)(Math.Sin(t * Math.PI) * 5 + Noise(i * 0.3f, x) * 3);
                for (int j = 0; j < h; j++) c.SetShade(x + i, y - j, 1.2f + j * 0.25f);
            }
        }

        // ---- Wagon and oxen ---------------------------------------------------------------------

        /// <summary>A covered wagon facing left. (x,y) is the top-left of the bonnet; wheels sit on y+36.</summary>
        public static void Wagon(Canvas c, int x, int y, float wheelAngle, bool broken = false)
        {
            // Bonnet: flared hoops, a white canvas arch shaded toward the bottom.
            const int w = 44;
            for (int i = 0; i < w; i++)
            {
                float u = (i - w / 2f) / (w / 2f);
                int top = y + (int)(9 * (u * u)) - (Math.Abs(u) > 0.88f ? (int)((Math.Abs(u) - 0.88f) * 30) : 0);
                int bottom = y + 20;
                for (int j = top; j < bottom; j++)
                {
                    float s = 4.4f - (j - top) * 0.07f - (u < 0 ? 0.25f : 0);
                    if (j == top) s = 5;
                    c.SetShade(x + i, j, s);
                }
            }
            // Hoops (ribs) under the canvas.
            for (int k = 1; k < 5; k++)
            {
                int hx = x + k * 9;
                float u = (k * 9 - w / 2f) / (w / 2f);
                c.VLine(hx, y + (int)(9 * u * u) + 1, y + 19, Canvas.Mid);
            }
            // Puckered front opening.
            c.Fill(x + 1, y + 7, 3, 9, Canvas.Dim);
            c.VLine(x, y + 6, y + 17, Canvas.Bright);
            // Wagon box and running gear.
            c.Fill(x + 1, y + 20, w - 2, 7, Canvas.Mid);
            c.HLine(x + 1, x + w - 2, y + 20, Canvas.Bright);
            c.HLine(x + 1, x + w - 2, y + 23, Canvas.Dark);
            c.HLine(x + 1, x + w - 2, y + 26, Canvas.Dark);
            c.Fill(x + 4, y + 27, w - 8, 2, Canvas.Dark);
            // Tongue toward the oxen.
            c.Line(x + 2, y + 27, x - 10, y + 25, Canvas.Mid);
            // Wheels: the rear is bigger. Spokes turn as you go.
            Wheel(c, x + 33, y + 28, 8, wheelAngle, !broken);
            Wheel(c, x + 10, y + 30, 6, wheelAngle * 1.33f, true);
            // Water bucket hanging off the back.
            c.Fill(x + w, y + 22, 3, 4, Canvas.Dark);
        }

        public static void Wheel(Canvas c, int cx, int cy, int r, float a, bool whole = true)
        {
            c.Ring(cx, cy, r, Canvas.Bright);
            c.Ring(cx, cy, r - 1, Canvas.Mid);
            int spokes = r >= 8 ? 6 : 5;
            for (int k = 0; k < spokes; k++)
            {
                if (!whole && k % 2 == 0) continue;
                double ang = a + k * Math.PI * 2 / spokes;
                c.Line(cx, cy, cx + (int)Math.Round(Math.Cos(ang) * (r - 1)), cy + (int)Math.Round(Math.Sin(ang) * (r - 1)), Canvas.Mid);
            }
            c.Fill(cx - 1, cy - 1, 3, 3, Canvas.Hot);
        }

        /// <summary>An ox in profile facing left. (x,y) is the top-left; hooves rest on y+18. frame 0..3 walks.</summary>
        public static void Ox(Canvas c, int x, int y, int frame, int shift = 0)
        {
            byte B(int v) => (byte)Math.Max(1, Math.Min(5, v + shift));
            // Body: a deep barrel with a hump over the shoulders and a squared rump.
            for (int i = 6; i <= 26; i++)
            {
                int top = i >= 8 && i <= 12 ? y + 1 : i == 7 || i == 13 ? y + 2 : y + 3;
                if (i >= 24) top = y + 4;
                int bot = i <= 7 || i >= 25 ? y + 11 : y + 12;
                for (int j = top; j <= bot; j++)
                {
                    int v = j == top ? 4 : j >= bot - 1 ? 2 : 3;
                    c.Set(x + i, j, B(v));
                }
            }
            // A pale patch on the flank.
            c.Fill(x + 15, y + 5, 5, 3, B(4)); c.Fill(x + 16, y + 8, 3, 1, B(4));
            // Head held low: long face, pale muzzle, dewlap under the neck.
            c.Fill(x + 2, y + 4, 5, 6, B(3));
            c.Fill(x + 0, y + 8, 4, 4, B(4));
            c.HLine(x + 2, x + 6, y + 4, B(4));
            c.Set(x + 3, y + 6, Canvas.Black);
            c.Set(x + 0, y + 10, B(2));
            c.Fill(x + 6, y + 11, 3, 3, B(2));
            // Ear and a long curved horn sweeping up and forward.
            c.Fill(x + 6, y + 5, 2, 1, B(2));
            c.Set(x + 4, y + 3, B(5)); c.Set(x + 3, y + 2, B(5)); c.Set(x + 3, y + 1, B(5)); c.Set(x + 4, y + 0, B(5));
            c.Set(x + 6, y + 3, B(4)); c.Set(x + 7, y + 2, B(4));
            // Wooden yoke over the neck with its bow.
            c.Fill(x + 7, y + 0, 4, 2, B(4));
            c.VLine(x + 8, y + 2, y + 9, B(2));
            // Tail with a tuft.
            c.VLine(x + 27, y + 4, y + 12, B(2));
            c.Fill(x + 27, y + 12, 2, 2, B(3));
            // Legs: front pair and back pair swing in opposite phase.
            int f = frame & 3;
            int[] swing = { 0, 1, 0, -1 };
            Leg(c, x + 8 + swing[f], y + 13, B(3));
            Leg(c, x + 11 - swing[f], y + 13, B(2));
            Leg(c, x + 21 - swing[f], y + 13, B(3));
            Leg(c, x + 24 + swing[f], y + 13, B(2));
        }

        static void Leg(Canvas c, int x, int y, byte col)
        {
            c.Fill(x, y, 2, 5, col);
            c.Fill(x, y + 5, 2, 1, Canvas.Dark);
        }

        /// <summary>The whole outfit: oxen teams pulling the wagon west. Returns the wagon's x.</summary>
        public static void Outfit(Canvas c, int x, int groundY, int oxen, float time, bool moving, bool broken = false)
        {
            int frame = moving ? (int)(time * 6) : 0;
            float ang = moving ? -time * 3.2f : 0;
            int wy = groundY - 37;
            Wagon(c, x + 64, wy, ang, broken);
            int pairs = oxen <= 0 ? 0 : oxen < 4 ? 1 : 2;
            for (int p = pairs - 1; p >= 0; p--)
            {
                int ox = x + 26 - p * 32;
                int oy = groundY - 19;
                Ox(c, ox + 3, oy - 2, frame + 2, -1);    // far ox of the pair
                Ox(c, ox, oy, frame);                     // near ox
                if (p == 0) c.Line(ox + 9, oy + 2, x + 54, wy + 25, Canvas.Mid);   // chain to the tongue
                else c.Line(ox + 9, oy + 2, ox + 40, oy + 2, Canvas.Mid);
            }
            // A person walking beside the team (most emigrants walked).
            if (pairs > 0) Walker(c, x + 56, groundY - 15, frame);
        }

        public static void Walker(Canvas c, int x, int y, int frame)
        {
            c.Fill(x, y, 4, 1, Canvas.Mid);          // hat brim
            c.Fill(x + 1, y - 2, 2, 2, Canvas.Mid);  // hat
            c.Fill(x + 1, y + 1, 2, 2, Canvas.Bright); // face
            c.Fill(x, y + 3, 4, 6, Canvas.Mid);      // coat
            int s = (frame & 2) == 0 ? 1 : -1;
            c.VLine(x + 1 + (s > 0 ? -1 : 0), y + 9, y + 14, Canvas.Dark);
            c.VLine(x + 2 + (s > 0 ? 1 : 0), y + 9, y + 14, Canvas.Dark);
            c.Line(x - 1, y + 4, x - 3, y + 8, Canvas.Dark); // goad stick
        }

        public static void Tombstone(Canvas c, int x, int y, int w, int h)
        {
            int r = w / 2;
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    int dy = j - r, dx = i - r;
                    if (j < r && dx * dx + dy * dy > r * r) continue;
                    // Lit face: a near-solid light stone so carved text reads; darker edge on the right.
                    float s = i > w - 6 ? 2.4f : i < 4 ? 3.9f : 3.45f;
                    c.SetShade(x + i, y + j, s);
                }
            for (int i = -6; i < w + 6; i++) c.SetShade(x + i, y + h, 1.5f);
        }

        // ---- Animals for the hunt (side view, facing left; flip for right) ----------------------

        public static readonly Dictionary<string, string[][]> Animals = new Dictionary<string, string[][]>
        {
            ["buffalo"] = new[]
            {
                new[]
                {
                    ".....:::::::...............",
                    "...:++++++++:..............",
                    "..:+++#+++++++:::::::::....",
                    ".:++++++++++++++++++++++:..",
                    ":+@+++++++++++++++++++++++:",
                    ":++++++++++++++++++++++++++",
                    ".:+++++++++++++++++++++++++",
                    "..:++++++++++++++++++++++:+",
                    "..::+++++++++++++++++++++..",
                    "...:::++:+++::::::++:++....",
                    ".....++..++........++.++...",
                    ".....++..++........++.++...",
                    ".....::..::........::.::...",
                },
                new[]
                {
                    ".....:::::::...............",
                    "...:++++++++:..............",
                    "..:+++#+++++++:::::::::....",
                    ".:++++++++++++++++++++++:..",
                    ":+@+++++++++++++++++++++++:",
                    ":++++++++++++++++++++++++++",
                    ".:+++++++++++++++++++++++++",
                    "..:++++++++++++++++++++++:+",
                    "..::+++++++++++++++++++++..",
                    "...:::+++++::::::::++++....",
                    "....++....++......++...++..",
                    "...++......++....++.....++.",
                    "...::......::....::.....::.",
                },
            },
            ["deer"] = new[]
            {
                new[]
                {
                    "#..#..............",
                    ".##...............",
                    ".#+...............",
                    "++@+..............",
                    "+++++.............",
                    "..+++++++++++++#..",
                    "...+++++++++++++#.",
                    "...++++++++++++...",
                    "...:+++::::::++...",
                    "...+.+.......+.+..",
                    "...+.+.......+.+..",
                    "...:.:.......:.:..",
                },
                new[]
                {
                    "#..#..............",
                    ".##...............",
                    ".#+...............",
                    "++@+..............",
                    "+++++.............",
                    "..+++++++++++++#..",
                    "...+++++++++++++#.",
                    "...++++++++++++...",
                    "...:+++::::::++...",
                    "..+...+.....+...+.",
                    ".+.....+...+.....+",
                    ".:.....:...:.....:",
                },
            },
            ["elk"] = new[]
            {
                new[]
                {
                    "#.#.#...............",
                    ".###................",
                    "..#.#...............",
                    "..##................",
                    ".++++...............",
                    "+@+++...............",
                    "++++++..............",
                    "..++++++++++++++++..",
                    "..+++++++++++++++++#",
                    "...++++++++++++++++.",
                    "...:++++:::::::+++..",
                    "...+..+........+..+.",
                    "...+..+........+..+.",
                    "...:..:........:..:.",
                },
                new[]
                {
                    "#.#.#...............",
                    ".###................",
                    "..#.#...............",
                    "..##................",
                    ".++++...............",
                    "+@+++...............",
                    "++++++..............",
                    "..++++++++++++++++..",
                    "..+++++++++++++++++#",
                    "...++++++++++++++++.",
                    "...:++++:::::::+++..",
                    "..+....+......+....+",
                    ".+......+....+.....+",
                    ".:......:....:.....:",
                },
            },
            ["antelope"] = new[]
            {
                new[]
                {
                    ".#..............",
                    ".#..............",
                    "+@+.............",
                    "+++.............",
                    ".+++++++++++#...",
                    "..+##########+..",
                    "..++++++++++++..",
                    "..+.+......+.+..",
                    "..+.+......+.+..",
                    "..:.:......:.:..",
                },
                new[]
                {
                    ".#..............",
                    ".#..............",
                    "+@+.............",
                    "+++.............",
                    ".+++++++++++#...",
                    "..+##########+..",
                    "..++++++++++++..",
                    ".+...+....+...+.",
                    "+.....+..+.....+",
                    ":.....:..:.....:",
                },
            },
            ["bear"] = new[]
            {
                new[]
                {
                    "..:.................",
                    ".:++:++++++++++:....",
                    ":@+++++++++++++++:..",
                    "+++++++++++++++++++.",
                    "#++++++++++++++++++:",
                    ".:+++++++++++++++++:",
                    "..:++++:::::::+++++.",
                    "...++++.......++++..",
                    "...++++.......++++..",
                    "...::::.......::::..",
                },
                new[]
                {
                    "..:.................",
                    ".:++:++++++++++:....",
                    ":@+++++++++++++++:..",
                    "+++++++++++++++++++.",
                    "#++++++++++++++++++:",
                    ".:+++++++++++++++++:",
                    "..:++++:::::::+++++.",
                    "..++++.........++++.",
                    ".++++...........++++",
                    ".::::...........::::",
                },
            },
            ["rabbit"] = new[]
            {
                new[]
                {
                    ".#.#....",
                    ".#.#....",
                    "+@++....",
                    ".++++++#",
                    "..+++++.",
                    "..+...+.",
                },
                new[]
                {
                    "........",
                    ".##.##..",
                    "+@++....",
                    ".++++++#",
                    "+++++++.",
                    "+......+",
                },
            },
            ["squirrel"] = new[]
            {
                new[]
                {
                    "......##",
                    ".+...#.#",
                    "+@+..#..",
                    ".++++#..",
                    "..+++...",
                    "..+.+...",
                },
                new[]
                {
                    "......##",
                    ".+....##",
                    "+@+...#.",
                    ".+++++#.",
                    ".+++++..",
                    "+....+..",
                },
            },
        };

        // ---- Landmark pictures ------------------------------------------------------------------

        /// <summary>Draws a landmark's picture into (x,y,w,h). time animates water, flags, steam.</summary>
        public static void Scene(Canvas c, string art, int x, int y, int w, int h, float time, int seed = 1)
        {
            c.Clip(x, y, w, h);
            c.Fill(x, y, w, h, Canvas.Black);
            int horizon = y + h * 58 / 100;
            int bottom = y + h;
            Stars(c, horizon - 20, seed, time);
            switch (art)
            {
                case "town": Town(c, x, w, horizon, bottom, time); break;
                case "river": River(c, x, w, horizon, bottom, time, seed); break;
                case "fort": Fort(c, x, w, horizon, bottom, time, seed); break;
                case "hollow": Hollow(c, x, w, horizon, bottom, time); break;
                case "chimney": Chimney(c, x, w, horizon, bottom); break;
                case "bluff": Bluff(c, x, w, horizon, bottom); break;
                case "rock": Rock(c, x, w, horizon, bottom, time); break;
                case "pass": Pass(c, x, w, horizon, bottom, time); break;
                case "springs": Springs(c, x, w, horizon, bottom, time); break;
                case "mountains": Mountains(c, x, w, horizon, bottom); break;
                case "dalles": Dalles(c, x, w, horizon, bottom, time); break;
                case "valley": Valley(c, x, w, horizon, bottom, time); break;
                default: Plains(c, x, w, horizon, bottom); break;
            }
            c.NoClip();
        }

        static void Ground(Canvas c, int x, int w, int horizon, int bottom, float near = 1.6f, float far = 0.7f)
        {
            for (int yy = horizon; yy < bottom; yy++)
            {
                float t = (yy - horizon) / (float)Math.Max(1, bottom - horizon);
                for (int xx = x; xx < x + w; xx++) c.SetShade(xx, yy, far + (near - far) * t);
            }
            // Grass tufts, denser up close.
            for (int i = 0; i < w / 3; i++)
            {
                int gx = x + (int)(Hash(i, 5) * w);
                float t = Hash(i, 9);
                int gy = horizon + 4 + (int)(t * t * (bottom - horizon - 6));
                c.Set(gx, gy, t > 0.5f ? Canvas.Mid : Canvas.Dark);
                if (t > 0.6f) { c.Set(gx - 1, gy + 1, Canvas.Mid); c.Set(gx + 1, gy + 1, Canvas.Mid); }
            }
        }

        static void Plains(Canvas c, int x, int w, int horizon, int bottom)
        {
            Ridge(c, x, x + w, horizon, bottom, 10, 0.02f, 3, 1.1f);
            Ground(c, x, w, horizon, bottom);
        }

        static void Town(Canvas c, int x, int w, int horizon, int bottom, float time)
        {
            Ridge(c, x, x + w, horizon - 4, bottom, 12, 0.03f, 11, 1.0f);
            for (int i = 0; i < 22; i++) RoundTree(c, x + 8 + i * 18 + (int)(Hash(i, 3) * 8), horizon - 2, 5 + (int)(Hash(i, 4) * 3), 1.6f);
            Ground(c, x, w, horizon, bottom, 1.4f, 1.0f);
            // A muddy street running away from us.
            c.Poly(new List<(int, int)> { (x + w / 2 - 20, horizon + 2), (x + w / 2 + 20, horizon + 2), (x + w / 2 + 120, bottom), (x + w / 2 - 120, bottom) }, 0, 0.6f);
            // False-fronted stores either side.
            int[] lx = { 20, 70, 115 }; int[] lw = { 46, 40, 34 }; int[] lh = { 46, 38, 30 };
            for (int i = 0; i < 3; i++) Store(c, x + lx[i], horizon + 22 - i * 6, lw[i], lh[i], i == 0 ? "OUTFITTERS" : i == 1 ? "HOTEL" : null, true);
            int[] rx = { 300, 252, 214 };
            for (int i = 0; i < 3; i++) Store(c, x + rx[i], horizon + 22 - i * 6, lw[i], lh[i], i == 0 ? "LIVERY" : i == 1 ? "BANK" : null, false);
            // Courthouse with a cupola at the end of the street.
            int ch = x + w / 2 - 22;
            c.FillShade(ch, horizon - 16, 44, 20, 2.4f);
            c.Poly(new List<(int, int)> { (ch - 2, horizon - 16), (ch + 22, horizon - 26), (ch + 46, horizon - 16) }, 0, 3.2f);
            c.FillShade(ch + 17, horizon - 36, 10, 10, 3.4f);
            c.Set(ch + 22, horizon - 39, Canvas.Hot); c.VLine(ch + 22, horizon - 38, horizon - 36, Canvas.Bright);
            for (int k = 0; k < 4; k++) c.Fill(ch + 5 + k * 10, horizon - 10, 3, 5, Canvas.Black);
            // Wagons lined up, ready to go.
            Wagon(c, x + w / 2 - 70, horizon + 24, 0);
            Wagon(c, x + w / 2 + 24, horizon + 40, 1.2f);
            Walker(c, x + w / 2 - 4, horizon + 44, (int)(time * 4));
        }

        static void Store(Canvas c, int x, int baseY, int w, int h, string sign, bool left)
        {
            c.FillShade(x, baseY - h, w, h, left ? 2.6f : 2.2f);
            c.Rect(x, baseY - h, w, h, Canvas.Mid);
            c.HLine(x, x + w - 1, baseY - h, Canvas.Bright);
            // Porch roof and posts.
            c.Fill(x - 2, baseY - h / 2 + 2, w + 4, 2, Canvas.Bright);
            c.VLine(x + 1, baseY - h / 2 + 4, baseY, Canvas.Mid);
            c.VLine(x + w - 2, baseY - h / 2 + 4, baseY, Canvas.Mid);
            c.Fill(x + w / 2 - 3, baseY - h / 2 + 6, 7, h / 2 - 6, Canvas.Black);
            c.Fill(x + 4, baseY - h + 6, 6, 6, Canvas.Black);
            c.Fill(x + w - 10, baseY - h + 6, 6, 6, Canvas.Black);
            if (sign != null && Canvas.TextWidth(sign) <= w + 16)
            {
                int sx = x + (w - Canvas.TextWidth(sign)) / 2;
                c.Fill(sx - 2, baseY - h - 11, Canvas.TextWidth(sign) + 4, 10, Canvas.Black);
                c.Text(sx, baseY - h - 10, sign, Canvas.Bright);
            }
        }

        static void River(Canvas c, int x, int w, int horizon, int bottom, float time, int seed)
        {
            Ridge(c, x, x + w, horizon - 2, bottom, 16, 0.025f, seed + 21, 1.0f);
            for (int i = 0; i < 26; i++) RoundTree(c, x + 6 + i * 15 + (int)(Hash(i, seed) * 6), horizon + 2, 4 + (int)(Hash(i, seed + 1) * 3), 1.8f);
            // Far bank, the water, the near bank.
            c.FillShade(x, horizon, w, 5, 1.4f);
            int water0 = horizon + 5, water1 = bottom - 18;
            for (int yy = water0; yy < water1; yy++)
                for (int xx = x; xx < x + w; xx++)
                {
                    float t = (yy - water0) / (float)(water1 - water0);
                    float wave = (float)Math.Sin(xx * 0.09f + yy * 0.7f - time * 2.4f) * 0.5f + (float)Math.Sin(xx * 0.031f - time * 1.3f + yy) * 0.4f;
                    c.SetShade(xx, yy, 0.6f + t * 0.5f + (wave > 0.75f ? 1.6f : 0));
                }
            for (int xx = x; xx < x + w; xx++)
            {
                int top = water1 + (int)(Noise(xx * 0.05f, 4) * 4) - 2;
                for (int yy = top; yy < bottom; yy++) c.SetShade(xx, yy, 1.8f + (yy - top) * 0.04f);
                c.Set(xx, top, Canvas.Mid);
            }
            Bush(c, x + 20, bottom - 6, 14, 2.4f); Bush(c, x + w - 50, bottom - 4, 18, 2.4f);
        }

        static void Fort(Canvas c, int x, int w, int horizon, int bottom, float time, int seed)
        {
            Ridge(c, x, x + w, horizon - 8, bottom, 26, 0.012f, seed + 41, 1.0f, 0, 0, 1.6f);
            Ground(c, x, w, horizon, bottom, 1.5f, 0.9f);
            int fx = x + w / 2 - 110, fw = 220, top = horizon - 20, fb = horizon + 26;
            // Palisade of logs.
            for (int i = 0; i < fw; i++)
            {
                int pointed = (i % 5 == 2) ? 2 : (i % 5 == 1 || i % 5 == 3) ? 1 : 0;
                for (int yy = top - pointed; yy < fb; yy++)
                {
                    float s = i % 5 == 4 ? 1.2f : 2.4f + (i % 5 == 1 ? 0.6f : 0) - (yy - top) * 0.012f;
                    c.SetShade(fx + i, yy, s);
                }
            }
            // Corner blockhouses with roofs and gun ports.
            foreach (int bx in new[] { fx - 12, fx + fw - 22 })
            {
                c.FillShade(bx, top - 22, 34, 48, 2.9f);
                c.Rect(bx, top - 22, 34, 48, Canvas.Bright);
                c.Poly(new List<(int, int)> { (bx - 4, top - 22), (bx + 17, top - 34), (bx + 38, top - 22) }, 0, 3.6f);
                for (int k = 0; k < 3; k++) c.Fill(bx + 7 + k * 9, top - 10, 2, 5, Canvas.Black);
                for (int k = 0; k < 4; k++) c.HLine(bx + 1, bx + 32, top - 18 + k * 12, Canvas.Mid);
            }
            // Gate and flag.
            int gx = fx + fw / 2 - 14;
            c.Fill(gx, top + 10, 28, fb - top - 10, Canvas.Black);
            c.Rect(gx, top + 10, 28, fb - top - 10, Canvas.Bright);
            c.VLine(gx + 14, top + 10, fb - 1, Canvas.Mid);
            c.Fill(gx - 4, top + 6, 36, 3, Canvas.Bright);
            int px = fx + fw / 2 + 40;
            c.VLine(px, top - 50, top, Canvas.Bright);
            for (int i = 0; i < 18; i++)
            {
                int wave = (int)Math.Round(Math.Sin(i * 0.45f - time * 5f) * 1.6f);
                for (int j = 0; j < 11; j++)
                {
                    bool stripe = (j / 2) % 2 == 0;
                    bool canton = i < 7 && j < 6;
                    c.Set(px + 1 + i, top - 50 + j + wave, canton ? Canvas.Mid : stripe ? Canvas.Hot : Canvas.Dark);
                }
            }
            // Tipis and wagons camped outside.
            Tipi(c, x + 22, horizon + 30, 18); Tipi(c, x + 44, horizon + 34, 22);
            Wagon(c, x + w - 70, horizon + 30, 0.5f);
        }

        static void Tipi(Canvas c, int x, int baseY, int h)
        {
            c.Poly(new List<(int, int)> { (x, baseY), (x + h / 2, baseY - h), (x + h, baseY) }, 0, 3.4f);
            c.Line(x + h / 2 - 3, baseY - h - 4, x + h / 2 + 1, baseY - h + 2, Canvas.Mid);
            c.Line(x + h / 2 + 3, baseY - h - 4, x + h / 2 - 1, baseY - h + 2, Canvas.Mid);
            c.Poly(new List<(int, int)> { (x + h / 2 - 3, baseY), (x + h / 2, baseY - 7), (x + h / 2 + 3, baseY) }, Canvas.Black);
            c.HLine(x + 2, x + h - 2, baseY - h / 3, Canvas.Dark);
        }

        static void Hollow(Canvas c, int x, int w, int horizon, int bottom, float time)
        {
            // Steep canyon slopes coming down both sides, a glen of ash trees, a spring.
            Ridge(c, x, x + w, horizon - 18, bottom, 26, 0.02f, 51, 1.0f, 0, 0.2f, 1.6f);
            Ground(c, x, w, horizon, bottom, 1.6f, 1.0f);
            c.Poly(new List<(int, int)> { (x, horizon - 50), (x + 60, horizon - 46), (x + 150, horizon + 10), (x + 140, bottom), (x, bottom) }, 0, 2.2f);
            c.Poly(new List<(int, int)> { (x + w, horizon - 40), (x + w - 70, horizon - 36), (x + w - 160, horizon + 14), (x + w - 150, bottom), (x + w, bottom) }, 0, 1.6f);
            c.Line(x, horizon - 50, x + 60, horizon - 46, Canvas.Bright);
            c.Line(x + 60, horizon - 46, x + 150, horizon + 10, Canvas.Mid);
            // A wagon being let down the hill by rope.
            Wagon(c, x + 66, horizon - 34, 0);
            c.Line(x + 60 + 44, horizon - 16, x + 24, horizon - 48, Canvas.Bright);
            for (int i = 0; i < 12; i++) RoundTree(c, x + 150 + i * 8 + (int)(Hash(i, 2) * 6), horizon + 18 + (int)(Hash(i, 3) * 10), 5 + (int)(Hash(i, 4) * 3), 2.6f);
            // The spring.
            c.FillShade(x + 210, horizon + 34, 40, 6, 0.4f);
            for (int i = 0; i < 4; i++) c.Set(x + 214 + i * 9, horizon + 36 + (((int)(time * 3) + i) % 2), Canvas.Hot);
        }

        static void Chimney(Canvas c, int x, int w, int horizon, int bottom)
        {
            Ridge(c, x, x + w, horizon, bottom, 10, 0.02f, 61, 1.0f);
            int cx = x + w / 2 + 30;
            // Cone of clay, lit from the right.
            for (int yy = horizon - 30; yy < horizon + 4; yy++)
            {
                float t = (yy - (horizon - 30)) / 34f;
                int half = (int)(10 + t * t * 70);
                for (int xx = -half; xx <= half; xx++) c.SetShade(cx + xx, yy, 2.0f + xx / (float)half * 0.9f);
            }
            // The spire: tall, slightly tapered, with a capstone.
            int spireTop = Math.Max(6, horizon - 62);
            for (int yy = spireTop; yy < horizon - 26; yy++)
            {
                int half = 5 + (yy > horizon - 44 ? (yy - (horizon - 44)) / 5 : 0);
                for (int xx = -half; xx <= half; xx++) c.SetShade(cx + xx, yy, 2.4f + xx / (float)half * 1.2f);
            }
            c.Fill(cx - 4, spireTop - 2, 9, 2, Canvas.Bright);
            Ground(c, x, w, horizon, bottom);
            Wagon(c, x + 40, horizon + 20, 0);
        }

        static void Bluff(Canvas c, int x, int w, int horizon, int bottom)
        {
            var pts = new List<(int, int)> { (x, horizon + 4), (x + 30, horizon - 30), (x + 50, horizon - 64), (x + 90, horizon - 70), (x + 110, horizon - 58),
                (x + 132, horizon - 66), (x + 190, horizon - 74), (x + 214, horizon - 60), (x + 250, horizon - 40), (x + 300, horizon - 34),
                (x + 330, horizon - 18), (x + w, horizon - 8), (x + w, horizon + 4) };
            c.Poly(pts, 0, 1.9f);
            // Bands of sandstone and clay.
            for (int band = 0; band < 5; band++)
            {
                int by = horizon - 60 + band * 12;
                for (int xx = x; xx < x + w; xx++)
                    if (c.Get(xx, by) != 0 && c.Get(xx, by - 3) != 0) { c.Set(xx, by, Canvas.Mid); if (band % 2 == 0) c.Set(xx, by + 1, Canvas.Dark); }
            }
            for (int i = 1; i < pts.Count - 2; i++) c.Line(pts[i].Item1, pts[i].Item2, pts[i + 1].Item1, pts[i + 1].Item2, Canvas.Bright);
            Ground(c, x, w, horizon, bottom);
            Wagon(c, x + w - 120, horizon + 22, 0);
        }

        static void Rock(Canvas c, int x, int w, int horizon, int bottom, float time)
        {
            Ridge(c, x, x + w, horizon - 4, bottom, 22, 0.015f, 71, 1.0f);
            Ground(c, x, w, horizon, bottom);
            int cx = x + w / 2, rx = 120, ry = 52;
            for (int yy = -ry; yy <= 0; yy++)
                for (int xx = -rx; xx <= rx; xx++)
                {
                    float d = xx * xx / (float)(rx * rx) + yy * yy / (float)(ry * ry);
                    if (d > 1) continue;
                    float n = Noise(xx * 0.12f + yy * 0.05f, (yy / 6) + 50) * 0.8f;
                    float edge = d > 0.92f ? 0.9f : 0;
                    c.SetShade(cx + xx, horizon + 8 + yy, 1.6f + (-yy / (float)ry) * 0.9f - xx / (float)rx * 0.7f + n + edge);
                }
            // Names painted on the rock.
            for (int i = 0; i < 40; i++)
            {
                int nx = cx - 100 + (int)(Hash(i, 13) * 200), ny = horizon - 20 + (int)(Hash(i, 17) * 22);
                if (c.Get(nx, ny) != 0) c.HLine(nx, nx + 2 + (int)(Hash(i, 19) * 6), ny, Canvas.Dark);
            }
            // The Sweetwater River winding past.
            for (int xx = x; xx < x + w; xx++)
            {
                int yy = bottom - 22 + (int)(Math.Sin(xx * 0.03f) * 6);
                for (int j = 0; j < 4; j++) c.SetShade(xx, yy + j, 0.6f + ((xx + (int)(time * 10)) % 13 == 0 ? 2.5f : 0));
            }
        }

        static void Pass(Canvas c, int x, int w, int horizon, int bottom, float time)
        {
            Ridge(c, x, x + w, horizon - 30, bottom, 60, 0.012f, 81, 1.4f, 0, 0.6f, 2.6f, 6);
            Ridge(c, x, x + w, horizon - 6, bottom, 20, 0.01f, 83, 1.0f, 0, 0, 1.8f);
            Ground(c, x, w, horizon, bottom, 1.4f, 1.0f);
            // Wind streaks.
            for (int i = 0; i < 9; i++)
            {
                int wx = (int)((Hash(i, 3) * w + time * 60) % w) + x, wy = horizon + 6 + (int)(Hash(i, 4) * 40);
                c.HLine(wx, wx + 10, wy, Canvas.Mid);
            }
            Wagon(c, x + w / 2 - 22, horizon + 16, 0);
        }

        static void Springs(Canvas c, int x, int w, int horizon, int bottom, float time)
        {
            Ridge(c, x, x + w, horizon - 10, bottom, 40, 0.015f, 91, 1.2f, 0, 0.3f, 2.0f);
            Ground(c, x, w, horizon, bottom, 1.5f, 1.0f);
            for (int s = 0; s < 4; s++)
            {
                int sx = x + 60 + s * 80, sy = horizon + 20 + (s % 2) * 16;
                for (int i = -14; i <= 14; i++)
                    for (int j = -4; j <= 4; j++)
                        if (i * i / 196f + j * j / 16f <= 1) c.SetShade(sx + i, sy + j, i * i / 196f + j * j / 16f > 0.7f ? 3.2f : 0.7f);
                for (int b = 0; b < 4; b++)
                {
                    float ph = (time * 1.4f + b * 0.27f + s * 0.4f) % 1f;
                    c.Set(sx - 8 + b * 5, sy - (int)(ph * 3), Canvas.Hot);
                }
                // Steam rising.
                for (int k = 0; k < 6; k++)
                {
                    float ph = (time * 0.5f + k / 6f + s * 0.2f) % 1f;
                    int py = sy - 6 - (int)(ph * 34), px = sx + (int)(Math.Sin(ph * 6 + s) * 5);
                    c.SetShade(px, py, 2.4f * (1 - ph)); c.SetShade(px + 1, py, 2.0f * (1 - ph));
                }
            }
        }

        static void Mountains(Canvas c, int x, int w, int horizon, int bottom)
        {
            Ridge(c, x, x + w, horizon - 50, bottom, 60, 0.02f, 101, 1.3f, 0, 0.5f, 2.6f, 5);
            Ridge(c, x, x + w, horizon - 14, bottom, 34, 0.03f, 103, 1.0f, 0, 0.2f, 1.6f);
            for (int i = 0; i < 60; i++)
            {
                int px = x + (int)(Hash(i, 105) * w);
                int py = horizon - 6 + (int)(Hash(i, 107) * 30);
                Pine(c, px, py, 10 + (int)(Hash(i, 109) * 12), Hash(i, 111) > 0.5f ? Canvas.Mid : Canvas.Dark);
            }
            c.FillShade(x, horizon + 24, w, bottom - horizon - 24, 1.2f);
            for (int i = 0; i < 14; i++) Pine(c, x + 4 + i * 28 + (int)(Hash(i, 113) * 10), bottom - 2 - (int)(Hash(i, 115) * 8), 20 + (int)(Hash(i, 117) * 16), Canvas.Bright, Canvas.Mid);
        }

        static void Dalles(Canvas c, int x, int w, int horizon, int bottom, float time)
        {
            // Basalt cliffs and the Columbia rushing between them.
            Ridge(c, x, x + w, horizon - 24, bottom, 30, 0.02f, 121, 1.8f, 0, 0.1f, 3.0f);
            int water0 = horizon + 4, water1 = horizon + 34;
            for (int yy = water0; yy < water1; yy++)
                for (int xx = x; xx < x + w; xx++)
                {
                    float foam = Noise(xx * 0.08f - time * 4f, yy * 3) + Noise(xx * 0.21f - time * 7f, yy * 5 + 1) * 0.6f;
                    c.SetShade(xx, yy, 0.7f + (foam > 1.05f ? 3.0f : foam > 0.9f ? 1.2f : 0));
                }
            for (int i = 0; i < 7; i++)
            {
                int rx = x + 20 + i * 52 + (int)(Hash(i, 5) * 20), ry = water0 + 6 + (int)(Hash(i, 6) * 18);
                c.Disc(rx, ry, 3, Canvas.Mid); c.Set(rx - 1, ry - 2, Canvas.Bright);
            }
            Ground(c, x, w, water1, bottom, 1.6f, 1.2f);
            // The mission house on the bluff.
            int mx = x + 60, my = water1 + 22;
            c.FillShade(mx, my - 16, 40, 16, 2.8f);
            c.Poly(new List<(int, int)> { (mx - 3, my - 16), (mx + 20, my - 26), (mx + 43, my - 16) }, 0, 3.6f);
            c.Fill(mx + 17, my - 9, 6, 9, Canvas.Black);
            c.Fill(mx + 5, my - 12, 5, 4, Canvas.Black); c.Fill(mx + 30, my - 12, 5, 4, Canvas.Black);
            // A raft being built.
            int fx = x + w - 140, fy = water1 + 20;
            for (int i = 0; i < 9; i++) c.Fill(fx, fy + i * 3, 70, 2, i % 2 == 0 ? Canvas.Mid : Canvas.Bright);
            Wagon(c, fx + 14, fy - 30, 0);
        }

        static void Valley(Canvas c, int x, int w, int horizon, int bottom, float time)
        {
            // Mount Hood, snowcapped, above a green valley; a cabin, a field, the river.
            int px = x + w / 2 + 70;
            for (int yy = horizon - 96; yy < horizon; yy++)
            {
                float t = (yy - (horizon - 96)) / 96f;
                int half = (int)(4 + t * 95);
                for (int xx = -half; xx <= half; xx++)
                {
                    bool snow = t < 0.38f + Noise(xx * 0.3f, 7) * 0.12f;
                    c.SetShade(px + xx, yy, snow ? 4.2f + (xx > 0 ? -1.2f : 0) : 1.6f + (xx > 0 ? -0.5f : 0.2f));
                }
            }
            Ridge(c, x, x + w, horizon - 2, bottom, 18, 0.02f, 131, 1.6f, 0, 0, 2.4f);
            for (int i = 0; i < 30; i++) Pine(c, x + (int)(Hash(i, 133) * w), horizon + (int)(Hash(i, 135) * 6), 8 + (int)(Hash(i, 137) * 8), Canvas.Mid);
            Ground(c, x, w, horizon + 6, bottom, 2.2f, 1.6f);
            // Plowed field in rows.
            for (int r = 0; r < 8; r++) c.Line(x + 20, horizon + 22 + r * 4, x + 150, horizon + 14 + r * 5, r % 2 == 0 ? Canvas.Bright : Canvas.Dark);
            // Cabin with smoke.
            int cx = x + 200, cy = horizon + 38;
            c.FillShade(cx, cy - 14, 34, 14, 2.6f);
            for (int k = 0; k < 4; k++) c.HLine(cx, cx + 33, cy - 13 + k * 3, Canvas.Mid);
            c.Poly(new List<(int, int)> { (cx - 4, cy - 14), (cx + 17, cy - 24), (cx + 38, cy - 14) }, 0, 3.8f);
            c.Fill(cx + 14, cy - 8, 6, 8, Canvas.Black);
            c.Fill(cx + 26, cy - 27, 4, 8, Canvas.Mid);
            for (int k = 0; k < 7; k++)
            {
                float ph = (time * 0.4f + k / 7f) % 1f;
                c.SetShade(cx + 28 + (int)(Math.Sin(ph * 5) * 4 + ph * 10), cy - 30 - (int)(ph * 30), 3f * (1 - ph));
            }
            Sun(c, x + 50, horizon - 70, 9);
        }

        // ---- Distant landmark silhouettes for the travel screen ---------------------------------

        public static void Silhouette(Canvas c, string art, int cx, int horizon, float scale)
        {
            int S(float v) => (int)(v * scale);
            switch (art)
            {
                case "chimney":
                    c.Poly(new List<(int, int)> { (cx - S(40), horizon), (cx - S(6), horizon - S(20)), (cx + S(6), horizon - S(20)), (cx + S(40), horizon) }, 0, 1.8f);
                    c.FillShade(cx - S(3), horizon - S(56), Math.Max(2, S(6)), S(36), 2.2f);
                    break;
                case "bluff":
                    c.Poly(new List<(int, int)> { (cx - S(70), horizon), (cx - S(50), horizon - S(30)), (cx - S(20), horizon - S(36)), (cx + S(30), horizon - S(32)), (cx + S(70), horizon) }, 0, 1.6f);
                    break;
                case "rock":
                    for (int i = -S(60); i <= S(60); i++)
                    {
                        float u = i / (float)S(60);
                        int hgt = (int)(Math.Sqrt(Math.Max(0, 1 - u * u)) * S(22));
                        for (int j = 0; j < hgt; j++) c.SetShade(cx + i, horizon - j, 1.8f);
                    }
                    break;
                case "fort":
                    c.FillShade(cx - S(40), horizon - S(12), S(80), S(12), 1.8f);
                    c.FillShade(cx - S(46), horizon - S(20), S(14), S(20), 2.2f);
                    c.FillShade(cx + S(32), horizon - S(20), S(14), S(20), 2.2f);
                    c.VLine(cx + S(10), horizon - S(34), horizon - S(12), Canvas.Mid);
                    break;
                case "town":
                    for (int i = 0; i < 6; i++) c.FillShade(cx - S(50) + S(18) * i, horizon - S(8 + (i * 7) % 12), S(14), S(8 + (i * 7) % 12), 1.8f);
                    c.VLine(cx, horizon - S(30), horizon, Canvas.Mid);
                    break;
                case "pass":
                case "mountains":
                case "springs":
                    c.Poly(new List<(int, int)> { (cx - S(90), horizon), (cx - S(40), horizon - S(40)), (cx - S(10), horizon - S(26)), (cx + S(30), horizon - S(48)), (cx + S(90), horizon) }, 0, 1.6f);
                    break;
                case "dalles":
                case "hollow":
                    c.Poly(new List<(int, int)> { (cx - S(70), horizon), (cx - S(60), horizon - S(20)), (cx + S(60), horizon - S(22)), (cx + S(70), horizon) }, 0, 1.6f);
                    break;
                case "valley":
                    c.Poly(new List<(int, int)> { (cx - S(80), horizon), (cx, horizon - S(70)), (cx + S(80), horizon) }, 0, 2f);
                    c.Poly(new List<(int, int)> { (cx - S(20), horizon - S(52)), (cx, horizon - S(70)), (cx + S(20), horizon - S(52)) }, 0, 4f);
                    break;
            }
        }
    }
}
