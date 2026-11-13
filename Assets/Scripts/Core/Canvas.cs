using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    /// <summary>
    /// The game's whole picture: a 480x270 screen of palette indices (see <see cref="Pal"/>).
    /// 0-5 are the interface ramp (ink to cream, 5 gold); higher indices are picture colours.
    /// </summary>
    public sealed class Canvas
    {
        public const int W = 480, H = 270;
        public const byte Black = 0, Dim = 1, Dark = 2, Mid = 3, Bright = 4, Hot = 5;

        public readonly byte[] Px = new byte[W * H];

        int cx0, cy0, cx1 = W, cy1 = H;

        // 4x4 Bayer matrix for ordered dithering between two colours.
        static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        public void Clip(int x, int y, int w, int h)
        {
            cx0 = Math.Max(0, x); cy0 = Math.Max(0, y);
            cx1 = Math.Min(W, x + w); cy1 = Math.Min(H, y + h);
        }

        public void NoClip() { cx0 = 0; cy0 = 0; cx1 = W; cy1 = H; }

        public void Clear(byte c = Black) { for (int i = 0; i < Px.Length; i++) Px[i] = c; }

        public void Set(int x, int y, byte c)
        {
            if (x >= cx0 && x < cx1 && y >= cy0 && y < cy1) Px[y * W + x] = c;
        }

        public byte Get(int x, int y) => (uint)x < W && (uint)y < H ? Px[y * W + x] : Black;

        static bool Over(int x, int y, float f) => f * 16f > Bayer[(y & 3) * 4 + (x & 3)] + 0.5f;

        /// <summary>a or b by an ordered dither: f = 0 is all a, 1 all b.</summary>
        public static byte Mix(int x, int y, byte a, byte b, float f) => f <= 0 ? a : f >= 1 ? b : Over(x, y, f) ? b : a;

        /// <summary>A colour from a dark-to-light ramp at t (0..1), dithered between neighbours.</summary>
        public static byte Ramp(int x, int y, byte[] ramp, float t)
        {
            if (t <= 0) return ramp[0];
            if (t >= 1) return ramp[ramp.Length - 1];
            float p = t * (ramp.Length - 1);
            int lo = (int)p;
            return Mix(x, y, ramp[lo], ramp[Math.Min(lo + 1, ramp.Length - 1)], p - lo);
        }

        /// <summary>Interface shade 0..5, dithered.</summary>
        public static byte Dither(int x, int y, float shade)
        {
            if (shade <= 0) return 0;
            if (shade >= 5) return 5;
            int lo = (int)shade;
            return (byte)(Over(x, y, shade - lo) ? lo + 1 : lo);
        }

        public void SetShade(int x, int y, float shade) => Set(x, y, Dither(x, y, shade));
        public void SetRamp(int x, int y, byte[] ramp, float t) => Set(x, y, Ramp(x, y, ramp, t));

        public void Fill(int x, int y, int w, int h, byte c)
        {
            int x0 = Math.Max(x, cx0), y0 = Math.Max(y, cy0), x1 = Math.Min(x + w, cx1), y1 = Math.Min(y + h, cy1);
            for (int j = y0; j < y1; j++)
            {
                int row = j * W;
                for (int i = x0; i < x1; i++) Px[row + i] = c;
            }
        }

        public void FillMix(int x, int y, int w, int h, byte a, byte b, float f)
        {
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++) Set(i, j, Mix(i, j, a, b, f));
        }

        /// <summary>Vertical gradient along a ramp from t0 (top) to t1 (bottom).</summary>
        public void Gradient(int x, int y, int w, int h, byte[] ramp, float t0, float t1)
        {
            for (int j = 0; j < h; j++)
            {
                float t = t0 + (t1 - t0) * (h <= 1 ? 0 : j / (float)(h - 1));
                for (int i = x; i < x + w; i++) SetRamp(i, y + j, ramp, t);
            }
        }

        /// <summary>Darkens what is already drawn by a dither toward one colour (for panels over pictures).</summary>
        public void Shade(int x, int y, int w, int h, float f, byte to = Black)
        {
            int x0 = Math.Max(x, cx0), y0 = Math.Max(y, cy0), x1 = Math.Min(x + w, cx1), y1 = Math.Min(y + h, cy1);
            for (int j = y0; j < y1; j++)
                for (int i = x0; i < x1; i++)
                    if (Over(i, j, f)) Px[j * W + i] = to;
        }

        public void HLine(int x0, int x1, int y, byte c) { if (x1 < x0) (x0, x1) = (x1, x0); Fill(x0, y, x1 - x0 + 1, 1, c); }
        public void VLine(int x, int y0, int y1, byte c) { if (y1 < y0) (y0, y1) = (y1, y0); Fill(x, y0, 1, y1 - y0 + 1, c); }

        public void Rect(int x, int y, int w, int h, byte c)
        {
            HLine(x, x + w - 1, y, c); HLine(x, x + w - 1, y + h - 1, c);
            VLine(x, y, y + h - 1, c); VLine(x + w - 1, y, y + h - 1, c);
        }

        /// <summary>A filled rectangle with its corners cut by one pixel and an edge colour.</summary>
        public void RoundRect(int x, int y, int w, int h, byte fill, byte edge)
        {
            Fill(x + 1, y + 1, w - 2, h - 2, fill);
            HLine(x + 1, x + w - 2, y, edge); HLine(x + 1, x + w - 2, y + h - 1, edge);
            VLine(x, y + 1, y + h - 2, edge); VLine(x + w - 1, y + 1, y + h - 2, edge);
        }

        public void Line(int x0, int y0, int x1, int y1, byte c)
        {
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1, dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1, err = dx + dy;
            while (true)
            {
                Set(x0, y0, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        public void Disc(int cx, int cy, int r, byte c)
        {
            for (int j = -r; j <= r; j++)
                for (int i = -r; i <= r; i++)
                    if (i * i + j * j <= r * r + r / 2) Set(cx + i, cy + j, c);
        }

        public void Ellipse(int cx, int cy, int rx, int ry, byte c)
        {
            if (rx <= 0 || ry <= 0) { HLine(cx - rx, cx + rx, cy, c); return; }
            for (int j = -ry; j <= ry; j++)
                for (int i = -rx; i <= rx; i++)
                    if ((i * i) / (float)(rx * rx) + (j * j) / (float)(ry * ry) <= 1.05f) Set(cx + i, cy + j, c);
        }

        public void Ring(int cx, int cy, int r, byte c)
        {
            int x = r, y = 0, err = 1 - r;
            while (x >= y)
            {
                Set(cx + x, cy + y, c); Set(cx + y, cy + x, c); Set(cx - y, cy + x, c); Set(cx - x, cy + y, c);
                Set(cx - x, cy - y, c); Set(cx - y, cy - x, c); Set(cx + y, cy - x, c); Set(cx + x, cy - y, c);
                y++;
                if (err < 0) err += 2 * y + 1; else { x--; err += 2 * (y - x) + 1; }
            }
        }

        /// <summary>Scanline polygon fill in one colour.</summary>
        public void Poly(IList<(int x, int y)> pts, byte c)
        {
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var p in pts) { minY = Math.Min(minY, p.y); maxY = Math.Max(maxY, p.y); }
            var xs = new List<float>();
            for (int y = Math.Max(minY, cy0); y <= Math.Min(maxY, cy1 - 1); y++)
            {
                xs.Clear();
                float sy = y + 0.5f;
                for (int i = 0; i < pts.Count; i++)
                {
                    var a = pts[i]; var b = pts[(i + 1) % pts.Count];
                    if ((a.y <= sy && b.y > sy) || (b.y <= sy && a.y > sy))
                        xs.Add(a.x + (sy - a.y) / (b.y - a.y) * (b.x - a.x));
                }
                xs.Sort();
                for (int k = 0; k + 1 < xs.Count; k += 2)
                {
                    int x0 = (int)Math.Ceiling(xs[k] - 0.5f), x1 = (int)Math.Floor(xs[k + 1] - 0.5f);
                    for (int x = x0; x <= x1; x++) Set(x, y, c);
                }
            }
        }

        public void Tri(int x0, int y0, int x1, int y1, int x2, int y2, byte c) => Poly(new[] { (x0, y0), (x1, y1), (x2, y2) }, c);

        // ---- Text -------------------------------------------------------------------------------
        // Proportional lettering cut from the public-domain font8x8 glyphs: each glyph is trimmed to
        // its inked columns, with one column of space after it. Digits share one width so numbers line up.

        public const int LineH = 10;
        static readonly int[] gx = new int[128], gw = new int[128];

        static Canvas()
        {
            for (int ch = 0; ch < 128; ch++)
            {
                int lo = 8, hi = -1;
                for (int x = 0; x < 8; x++)
                    for (int y = 0; y < 8; y++)
                        if (Font8x8.Pixel((char)ch, x, y)) { lo = Math.Min(lo, x); hi = Math.Max(hi, x); }
                if (hi < 0) { gx[ch] = 0; gw[ch] = 3; }
                else { gx[ch] = lo; gw[ch] = hi - lo + 1; }
            }
            for (char d = '0'; d <= '9'; d++) { gx[d] = 0; gw[d] = 7; }
        }

        static int Glyph(char ch) => ch < 128 ? ch : '?';

        public static int CharWidth(char ch, int scale = 1) => (gw[Glyph(ch)] + 1) * scale;

        public static int TextWidth(string s, int scale = 1)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int w = 0;
            foreach (var ch in s) w += CharWidth(ch, scale);
            return w - scale;
        }

        public int Text(int x, int y, string s, byte c, int scale = 1)
        {
            if (s == null) return x;
            foreach (var ch in s)
            {
                int g = Glyph(ch);
                if (ch != ' ')
                    for (int j = 0; j < 8; j++)
                        for (int i = 0; i < gw[g]; i++)
                            if (Font8x8.Pixel((char)g, gx[g] + i, j))
                            {
                                if (scale == 1) Set(x + i, y + j, c);
                                else Fill(x + i * scale, y + j * scale, scale, scale, c);
                            }
                x += (gw[g] + 1) * scale;
            }
            return x;
        }

        public void TextCenter(int y, string s, byte c, int scale = 1) => Text((W - TextWidth(s, scale)) / 2, y, s, c, scale);
        public void TextCenterIn(int x, int w, int y, string s, byte c, int scale = 1) => Text(x + (w - TextWidth(s, scale)) / 2, y, s, c, scale);
        public void TextRight(int xRight, int y, string s, byte c, int scale = 1) => Text(xRight - TextWidth(s, scale), y, s, c, scale);

        /// <summary>Text with a dark drop shadow, for lettering over pictures.</summary>
        public void TextShadow(int x, int y, string s, byte c, int scale = 1, byte shadow = Pal.Black)
        {
            Text(x + scale, y + scale, s, shadow, scale);
            Text(x, y, s, c, scale);
        }

        public void TextShadowCenter(int y, string s, byte c, int scale = 1) => TextShadow((W - TextWidth(s, scale)) / 2, y, s, c, scale);

        /// <summary>Draws word-wrapped text within a pixel width and returns the y just below it.</summary>
        public int Paragraph(int x, int y, int width, string s, byte c, int pitch = LineH)
        {
            foreach (var line in Wrap(s, width)) { Text(x, y, line, c); y += pitch; }
            return y;
        }

        /// <summary>Splits text into lines no wider than width pixels. '\n' starts a new line.</summary>
        public static List<string> Wrap(string s, int width)
        {
            var lines = new List<string>();
            foreach (var para in (s ?? "").Split('\n'))
            {
                if (para.Length == 0) { lines.Add(""); continue; }
                string cur = "";
                foreach (var w in para.Split(' '))
                {
                    string next = cur.Length == 0 ? w : cur + " " + w;
                    if (cur.Length > 0 && TextWidth(next) > width) { lines.Add(cur); cur = w; }
                    else cur = next;
                    while (TextWidth(cur) > width && cur.Length > 1)
                    {
                        int n = cur.Length - 1;
                        while (n > 1 && TextWidth(cur.Substring(0, n)) > width) n--;
                        lines.Add(cur.Substring(0, n)); cur = cur.Substring(n);
                    }
                }
                lines.Add(cur);
            }
            return lines;
        }

        // ---- Sprites ----------------------------------------------------------------------------

        /// <summary>
        /// Draws string art with a key mapping characters to palette indices; characters not in the
        /// key (such as '.' and ' ') are transparent. flip mirrors horizontally.
        /// </summary>
        public void Sprite(int x, int y, string[] rows, IDictionary<char, byte> key, bool flip = false, int scale = 1)
        {
            for (int j = 0; j < rows.Length; j++)
            {
                string r = rows[j];
                for (int i = 0; i < r.Length; i++)
                {
                    if (!key.TryGetValue(r[i], out var col)) continue;
                    int px = flip ? x + (r.Length - 1 - i) * scale : x + i * scale;
                    if (scale == 1) Set(px, y + j, col); else Fill(px, y + j * scale, scale, scale, col);
                }
            }
        }

        public static int SpriteWidth(string[] rows) { int w = 0; foreach (var r in rows) w = Math.Max(w, r.Length); return w; }
    }
}
