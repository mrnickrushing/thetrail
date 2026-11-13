using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    /// <summary>
    /// Every picture in the game, drawn in code: skies, ranges, ground for each kind of country, the
    /// landmark scenes, and the people and animals. Scenes take a scroll offset so the road can move.
    /// </summary>
    public static partial class Art
    {
        // ---- Noise ------------------------------------------------------------------------------

        static float Hash(int x, int seed)
        {
            uint h = (uint)(x * 374761393 + seed * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }

        public static float Noise(float x, int seed)
        {
            int i = (int)Math.Floor(x);
            float f = x - i;
            float u = f * f * (3 - 2 * f);
            return Hash(i, seed) * (1 - u) + Hash(i + 1, seed) * u;
        }

        public static float Fbm(float x, int seed, int oct = 3)
        {
            float s = 0, a = 0.5f, t = 0;
            for (int o = 0; o < oct; o++) { s += Noise(x, seed + o * 97) * a; t += a; x *= 2.03f; a *= 0.5f; }
            return s / t;
        }

        // ---- Sky --------------------------------------------------------------------------------

        /// <summary>tod: 0 day, 1 dusk, 2 night.</summary>
        public static void Sky(Canvas c, int x, int y, int w, int h, int tod, float time, int seed, Sky weather = Core.Sky.Fair)
        {
            bool grey = weather == Core.Sky.Rain || weather == Core.Sky.Storm || weather == Core.Sky.Snow;
            if (tod == 2) c.Gradient(x, y, w, h, Pal.NightRamp, 0, 0.9f);
            else if (tod == 1) c.Gradient(x, y, w, h, Pal.DuskRamp, 0.05f, 1f);
            else if (grey) c.Gradient(x, y, w, h, new[] { Pal.RockDark, Pal.Rock, Pal.RockLight, Pal.SnowShade }, 0.1f, 0.85f);
            else c.Gradient(x, y, w, h, Pal.SkyRamp, 0.1f, 0.95f);

            if (tod == 2)
            {
                for (int i = 0; i < 70; i++)
                {
                    int sx = x + (int)(Hash(i, seed) * w), sy = y + (int)(Hash(i, seed + 5) * h * 0.8f);
                    bool tw = Hash(i + (int)(time * 2), seed + 9) > 0.85f;
                    c.Set(sx, sy, tw ? Pal.SkyPale : Pal.Far);
                }
                c.Disc(x + w * 3 / 4, y + h / 4, 6, Pal.Haze);
                c.Disc(x + w * 3 / 4 + 3, y + h / 4 - 2, 5, Pal.Night);
                return;
            }
            if (!grey)
            {
                int sx = tod == 1 ? x + w / 5 : x + w * 4 / 5, sy = tod == 1 ? y + h - 14 : y + h / 5;
                c.Disc(sx, sy, tod == 1 ? 12 : 8, tod == 1 ? Pal.Dusk : Pal.Sun);
                if (tod == 0) c.Disc(sx, sy, 6, Pal.Haze);
            }
            // Clouds drift slowly.
            int n = grey ? 9 : 4;
            for (int i = 0; i < n; i++)
            {
                float cx = (Hash(i, seed + 21) * (w + 160) + time * (4 + i % 3 * 2)) % (w + 160) - 80;
                int cy = y + 8 + (int)(Hash(i, seed + 33) * h * (grey ? 0.5f : 0.4f));
                int cw = 30 + (int)(Hash(i, seed + 44) * 50);
                Cloud(c, x + (int)cx, cy, cw, grey ? Pal.Rock : tod == 1 ? Pal.DuskPink : Pal.Haze, grey ? Pal.RockDark : tod == 1 ? Pal.Far : Pal.SkyPale);
            }
        }

        public static void Cloud(Canvas c, int x, int y, int w, byte col, byte shade)
        {
            int h = Math.Max(5, w / 5);
            c.Ellipse(x + w / 2, y + h / 2 + 1, w / 2, h / 2, shade);
            c.Ellipse(x + w / 2, y + h / 2, w / 2, h / 2, col);
            c.Ellipse(x + w / 3, y + 1, w / 5, h / 2 + 1, col);
            c.Ellipse(x + w * 3 / 5, y - 1, w / 4, h / 2 + 2, col);
        }

        /// <summary>A mountain range: ridge line from noise, filled down to baseY, with snow above snowY.</summary>
        public static void Range(Canvas c, int x, int w, int baseY, int height, float freq, int seed, byte col, byte shade, float scroll = 0, int snowY = -999, float jag = 1f)
        {
            for (int i = 0; i < w; i++)
            {
                float wx = (i + scroll) * freq;
                float n = Fbm(wx, seed, 4);
                n = (float)Math.Pow(n, 1.0 + 0.6 * jag);
                int top = baseY - (int)(n * height * 1.6f);
                float slope = Fbm(wx + 0.02f, seed, 4) - Fbm(wx - 0.02f, seed, 4);
                for (int y = Math.Max(0, top); y < baseY; y++)
                {
                    byte cc = slope > 0 ? shade : col;
                    if (y < snowY && y < top + 6 + (int)(Hash(i + (int)scroll, seed) * 5)) cc = slope > 0 ? Pal.SnowShade : Pal.Snow;
                    c.Set(x + i, y, cc);
                }
            }
        }

        /// <summary>Rolling hills, filled from a softer ridge down to baseY along a ramp.</summary>
        public static void Hills(Canvas c, int x, int w, int baseY, int height, float freq, int seed, byte[] ramp, float t0, float t1, float scroll = 0)
        {
            for (int i = 0; i < w; i++)
            {
                float n = Fbm((i + scroll) * freq, seed, 3);
                int top = baseY - (int)(n * height);
                for (int y = Math.Max(0, top); y < baseY; y++)
                {
                    float t = t0 + (t1 - t0) * (y - top) / (float)Math.Max(1, baseY - top);
                    c.SetRamp(x + i, y, ramp, t);
                }
            }
        }

        // ---- Ground -----------------------------------------------------------------------------

        public static byte[] GroundRamp(Ground g)
        {
            switch (g)
            {
                case Ground.Prairie: return Pal.GrassRamp;
                case Ground.Plains: return new[] { Pal.Grass, Pal.GrassLight, Pal.DryGrass, Pal.SandLight };
                case Ground.Sage: return Pal.SageRamp;
                case Ground.Desert: return new[] { Pal.Sand, Pal.SandLight, Pal.Haze };
                case Ground.Mountains: return new[] { Pal.GrassDeep, Pal.Grass, Pal.Sage, Pal.RockLight };
                case Ground.Forest: return new[] { Pal.PineDeep, Pal.GrassDeep, Pal.Grass, Pal.GrassLight };
                default: return new[] { Pal.Grass, Pal.GrassLight, Pal.DryGrass };
            }
        }

        /// <summary>The ground from the horizon down, with tufts that scroll with the road.</summary>
        public static void GroundPlane(Canvas c, int x, int y, int w, int h, Ground g, float scroll, int seed)
        {
            var ramp = GroundRamp(g);
            c.Gradient(x, y, w, h, ramp, 0.85f, 0.15f);
            // Tufts: closer rows move faster.
            for (int row = 0; row < 6; row++)
            {
                float depth = (row + 1) / 6f;
                int yy = y + (int)(h * depth * 0.9f);
                float sp = scroll * (0.3f + depth * 1.2f);
                int gap = (int)(26 - depth * 14);
                for (int k = -1; k < w / gap + 2; k++)
                {
                    int wx = (int)Math.Floor(sp / gap) + k;
                    int px = x + (int)(k * gap - (sp % gap)) + (int)(Hash(wx, seed + row) * gap);
                    if (Hash(wx, seed + row * 7) < 0.45f) continue;
                    int py = yy + (int)(Hash(wx, seed + 3) * 4);
                    Tuft(c, px, py, g, depth, Hash(wx, seed + 11));
                }
            }
        }

        static void Tuft(Canvas c, int x, int y, Ground g, float depth, float r)
        {
            int s = 1 + (int)(depth * 3);
            switch (g)
            {
                case Ground.Sage:
                case Ground.Desert:
                    if (g == Ground.Desert && r < 0.6f) { c.Fill(x, y, s + 1, 1, Pal.Earth); return; }
                    c.Ellipse(x, y - s / 2, s + 1, Math.Max(1, s / 2 + 1), Pal.SageDark);
                    c.Ellipse(x - 1, y - s / 2 - 1, s, Math.Max(1, s / 2), Pal.Sage);
                    break;
                case Ground.Mountains:
                    if (r < 0.5f) { c.Ellipse(x, y, s + 1, s / 2 + 1, Pal.RockDark); c.Ellipse(x - 1, y - 1, s, s / 2, Pal.Rock); }
                    else Blades(c, x, y, s, Pal.GrassDeep, Pal.Grass);
                    break;
                case Ground.Forest:
                    Blades(c, x, y, s, Pal.PineDeep, Pal.Grass);
                    break;
                case Ground.Plains:
                    Blades(c, x, y, s, Pal.Grass, Pal.DryGrass);
                    break;
                default:
                    Blades(c, x, y, s, Pal.GrassDeep, Pal.GrassLight);
                    if (r > 0.92f) c.Set(x, y - s - 1, Pal.Gold);
                    break;
            }
        }

        static void Blades(Canvas c, int x, int y, int s, byte dark, byte light)
        {
            for (int i = -s; i <= s; i++)
            {
                int hgt = s + 1 - Math.Abs(i) / 2;
                c.VLine(x + i, y - hgt, y, (i & 1) == 0 ? dark : light);
            }
        }

        /// <summary>Wagon ruts across the ground.</summary>
        public static void Road(Canvas c, int x, int y, int w, int h, Ground g)
        {
            byte a = g == Ground.Desert ? Pal.Sand : g == Ground.Sage ? Pal.DryGrass : Pal.Earth;
            byte b = g == Ground.Desert ? Pal.Earth : Pal.EarthDark;
            c.Fill(x, y, w, h, a);
            c.HLine(x, x + w, y, b);
            c.HLine(x, x + w, y + h - 1, b);
            c.HLine(x, x + w, y + h / 2, b);
        }

        // ---- Trees, rocks, water ----------------------------------------------------------------

        public static void Pine(Canvas c, int x, int baseY, int h, bool snow = false)
        {
            c.VLine(x, baseY - h / 5, baseY, Pal.WoodDark);
            int tiers = Math.Max(2, h / 7);
            for (int t = 0; t < tiers; t++)
            {
                int ty = baseY - h / 6 - t * (h - h / 6) / tiers;
                int hw = (tiers - t) * h / (tiers * 3) + 1;
                c.Tri(x - hw, ty, x + hw, ty, x, ty - (h - h / 6) / tiers - 3, Pal.PineDeep);
                c.Tri(x - hw + 1, ty - 1, x, ty - 1, x, ty - (h - h / 6) / tiers - 2, Pal.Pine);
                if (snow) c.HLine(x - hw / 2, x, ty - (h - h / 6) / tiers, Pal.Snow);
            }
        }

        public static void Cottonwood(Canvas c, int x, int baseY, int h, int seed)
        {
            c.Fill(x - 1, baseY - h / 2, 3, h / 2, Pal.WoodDark);
            c.Line(x, baseY - h / 2, x - h / 4, baseY - h * 3 / 4, Pal.WoodDark);
            c.Line(x, baseY - h / 2, x + h / 4, baseY - h * 3 / 4 + 2, Pal.WoodDark);
            for (int i = 0; i < 7; i++)
            {
                int dx = (int)((Hash(i, seed) - 0.5f) * h * 0.9f), dy = (int)(Hash(i, seed + 3) * h * 0.45f);
                int r = h / 6 + (int)(Hash(i, seed + 7) * h / 7);
                c.Disc(x + dx, baseY - h + dy + r / 2, r, Pal.GrassDeep);
                c.Disc(x + dx - 1, baseY - h + dy + r / 2 - 1, r - 2, i % 2 == 0 ? Pal.Grass : Pal.GrassLight);
            }
        }

        public static void Boulder(Canvas c, int x, int y, int r, byte light = Pal.RockLight, byte mid = Pal.Rock, byte dark = Pal.RockDark)
        {
            c.Ellipse(x, y - r / 2, r, r / 2 + 1, dark);
            c.Ellipse(x - 1, y - r / 2 - 1, r - 1, r / 2, mid);
            c.Ellipse(x - r / 3, y - r * 3 / 4, r / 3, Math.Max(1, r / 5), light);
        }

        public static void Water(Canvas c, int x, int y, int w, int h, float time, float flow = 1f)
        {
            c.Gradient(x, y, w, h, Pal.WaterRamp, 0.55f, 0.15f);
            for (int j = 0; j < h; j += 3)
                for (int i = 0; i < w; i++)
                {
                    float n = Noise((i + time * 22 * flow * (1 + j * 0.03f)) * 0.08f, j * 13 + 5);
                    if (n > 0.78f) c.Set(x + i, y + j, n > 0.9f ? Pal.Foam : Pal.WaterLight);
                }
        }

        // ---- Buildings --------------------------------------------------------------------------

        public static void Flag(Canvas c, int x, int y, float time)
        {
            c.VLine(x, y, y + 26, Pal.WoodDark);
            for (int j = 0; j < 9; j++)
                for (int i = 0; i < 14; i++)
                {
                    int dy = (int)Math.Round(Math.Sin(i * 0.5f - time * 5) * 1.2f);
                    byte col = j < 4 && i < 6 ? Pal.Blue : (j % 2 == 0 ? Pal.Red : Pal.Foam);
                    c.Set(x + 1 + i, y + j + dy, col);
                }
        }

        public static void Building(Canvas c, int x, int baseY, int w, int h, byte wall, byte trim, bool falseFront = true, bool sign = false)
        {
            c.Fill(x, baseY - h, w, h, wall);
            c.Rect(x, baseY - h, w, h, trim);
            if (falseFront) c.Fill(x - 1, baseY - h - 4, w + 2, 5, wall);
            for (int i = 3; i < w - 4; i += 7) c.Fill(x + i, baseY - h + 5, 3, 4, Pal.WaterDeep);
            c.Fill(x + w / 2 - 2, baseY - 8, 5, 8, Pal.WoodDark);
            if (sign) { c.Fill(x + 2, baseY - h - 2, w - 4, 3, Pal.Cream); }
        }

        public static void Tent(Canvas c, int x, int baseY, int w, byte col = Pal.Cloth)
        {
            int h = w * 2 / 3;
            c.Tri(x, baseY, x + w, baseY, x + w / 2, baseY - h, Pal.ClothShade);
            c.Tri(x, baseY, x + w / 2, baseY, x + w / 2, baseY - h, col);
            c.VLine(x + w / 2, baseY - h / 3, baseY, Pal.WoodDark);
        }

        public static void Tipi(Canvas c, int x, int baseY, int w)
        {
            int h = w * 3 / 2;
            c.Tri(x, baseY, x + w, baseY, x + w / 2, baseY - h, Pal.ClothShade);
            c.Tri(x + 1, baseY, x + w / 2, baseY, x + w / 2, baseY - h + 2, Pal.Cloth);
            c.Line(x + w / 2 - 2, baseY - h - 4, x + w / 2 + 1, baseY - h + 3, Pal.WoodDark);
            c.Line(x + w / 2 + 3, baseY - h - 4, x + w / 2, baseY - h + 3, Pal.WoodDark);
            c.Tri(x + w / 2 - 3, baseY, x + w / 2 + 3, baseY, x + w / 2, baseY - 7, Pal.WoodDark);
            c.HLine(x + w / 4, x + w * 3 / 4, baseY - h / 3, Pal.RedDark);
        }

        public static void Cabin(Canvas c, int x, int baseY, int w, int h)
        {
            for (int j = 0; j < h; j += 3) { c.Fill(x, baseY - h + j, w, 3, Pal.Wood); c.HLine(x, x + w - 1, baseY - h + j + 2, Pal.WoodDark); }
            c.Tri(x - 3, baseY - h, x + w + 3, baseY - h, x + w / 2, baseY - h - h / 2, Pal.WoodDark);
            c.Fill(x + w / 2 - 2, baseY - 7, 4, 7, Pal.Black);
            c.Fill(x + w - 5, baseY - h - h / 2, 3, h / 2, Pal.RockDark);
        }

        /// <summary>The app icon, 64x64 game pixels in the top-left corner: a wagon under a gold-rush sunset.</summary>
        public static void Icon(Canvas c)
        {
            c.Clip(0, 0, 64, 64);
            c.Gradient(0, 0, 64, 44, Pal.DuskRamp, 0.05f, 0.95f);
            c.Disc(53, 30, 8, Pal.Sun);
            Range(c, 0, 64, 44, 14, 0.09f, 7, Pal.Far, Pal.FarLight, 0, 34);
            c.Gradient(0, 44, 64, 20, Pal.DryRamp, 0.55f, 0.15f);
            c.HLine(0, 63, 44, Pal.EarthDark);
            Wagon(c, 7, 59, 0.5f);
            int w = Canvas.TextWidth("'49", 2);
            c.TextShadow(32 - w / 2, 2, "'49", Pal.Gold, 2);
            c.NoClip();
        }
    }
}
