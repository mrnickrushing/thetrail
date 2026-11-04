using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    /// <summary>
    /// The game's whole picture: a 384x240 screen of palette indices, like a monochrome monitor.
    /// 0 is the black glass, 1-4 are phosphor shades from dim to bright, 5 is the hot white-green of
    /// highlighted text. The Unity layer maps the indices to the chosen monitor colours.
    /// </summary>
    public sealed class Canvas
    {
        public const int W = 384, H = 240;
        public const byte Black = 0, Dim = 1, Dark = 2, Mid = 3, Bright = 4, Hot = 5;

        public readonly byte[] Px = new byte[W * H];

        // Clip rectangle (inclusive min, exclusive max).
        int cx0, cy0, cx1 = W, cy1 = H;

        // 4x4 Bayer matrix for ordered dithering between two shades.
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

        /// <summary>Continuous shade 0..5 drawn with an ordered dither, so gradients read as old-school stipple.</summary>
        public static byte Dither(int x, int y, float shade)
        {
            if (shade <= 0) return 0;
            if (shade >= 5) return 5;
            int lo = (int)shade;
            float f = shade - lo;
            return (byte)(f * 16f > Bayer[(y & 3) * 4 + (x & 3)] + 0.5f ? lo + 1 : lo);
        }

        public void SetShade(int x, int y, float shade) => Set(x, y, Dither(x, y, shade));

        public void Fill(int x, int y, int w, int h, byte c)
        {
            int x0 = Math.Max(x, cx0), y0 = Math.Max(y, cy0), x1 = Math.Min(x + w, cx1), y1 = Math.Min(y + h, cy1);
            for (int j = y0; j < y1; j++)
            {
                int row = j * W;
                for (int i = x0; i < x1; i++) Px[row + i] = c;
            }
        }

        public void FillShade(int x, int y, int w, int h, float shade)
        {
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++) SetShade(i, j, shade);
        }

        /// <summary>Vertical gradient from shade a (top) to b (bottom), dithered.</summary>
        public void Gradient(int x, int y, int w, int h, float a, float b)
        {
            for (int j = 0; j < h; j++)
            {
                float s = a + (b - a) * (h <= 1 ? 0 : j / (float)(h - 1));
                for (int i = x; i < x + w; i++) SetShade(i, y + j, s);
            }
        }

        public void HLine(int x0, int x1, int y, byte c) { if (x1 < x0) (x0, x1) = (x1, x0); Fill(x0, y, x1 - x0 + 1, 1, c); }
        public void VLine(int x, int y0, int y1, byte c) { if (y1 < y0) (y0, y1) = (y1, y0); Fill(x, y0, 1, y1 - y0 + 1, c); }

        public void Rect(int x, int y, int w, int h, byte c)
        {
            HLine(x, x + w - 1, y, c); HLine(x, x + w - 1, y + h - 1, c);
            VLine(x, y, y + h - 1, c); VLine(x + w - 1, y, y + h - 1, c);
        }

        /// <summary>A double-ruled frame, the classic look of the status boxes.</summary>
        public void Frame(int x, int y, int w, int h, byte c)
        {
            Rect(x, y, w, h, c);
            Rect(x + 2, y + 2, w - 4, h - 4, c);
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

        /// <summary>Scanline polygon fill. shade &lt; 0 means solid colour c, otherwise a dithered shade.</summary>
        public void Poly(IList<(int x, int y)> pts, byte c, float shade = -1)
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
                    for (int x = x0; x <= x1; x++)
                        if (shade < 0) Set(x, y, c); else SetShade(x, y, shade);
                }
            }
        }

        // ---- Text -------------------------------------------------------------------------------

        public const int CharW = 8, CharH = 8;
        public static int TextWidth(string s, int scale = 1) => s.Length * CharW * scale;

        public void Text(int x, int y, string s, byte c, int scale = 1)
        {
            for (int k = 0; k < s.Length; k++)
            {
                char ch = s[k];
                if (ch != ' ') Glyph(x + k * CharW * scale, y, ch, c, scale);
            }
        }

        public void Glyph(int x, int y, char ch, byte c, int scale = 1)
        {
            for (int j = 0; j < 8; j++)
                for (int i = 0; i < 8; i++)
                    if (Font8x8.Pixel(ch, i, j))
                    {
                        if (scale == 1) Set(x + i, y + j, c);
                        else Fill(x + i * scale, y + j * scale, scale, scale, c);
                    }
        }

        public void TextCenter(int y, string s, byte c, int scale = 1) => Text((W - TextWidth(s, scale)) / 2, y, s, c, scale);
        public void TextRight(int xRight, int y, string s, byte c) => Text(xRight - TextWidth(s), y, s, c);

        /// <summary>Text with a one-pixel black outline so it stays readable over pictures.</summary>
        public void TextOutlined(int x, int y, string s, byte c, byte outline = Black)
        {
            for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                    if (ox != 0 || oy != 0) Text(x + ox, y + oy, s, outline);
            Text(x, y, s, c);
        }

        /// <summary>Draws word-wrapped text and returns the y just below it.</summary>
        public int Paragraph(int x, int y, int cols, string s, byte c, int pitch = 10)
        {
            foreach (var line in Wrap(s, cols)) { Text(x, y, line, c); y += pitch; }
            return y;
        }

        public static List<string> Wrap(string s, int cols)
        {
            var lines = new List<string>();
            foreach (var para in s.Split('\n'))
            {
                if (para.Length == 0) { lines.Add(""); continue; }
                var words = para.Split(' ');
                string cur = "";
                foreach (var w in words)
                {
                    if (cur.Length == 0) cur = w;
                    else if (cur.Length + 1 + w.Length <= cols) cur += " " + w;
                    else { lines.Add(cur); cur = w; }
                    while (cur.Length > cols) { lines.Add(cur.Substring(0, cols)); cur = cur.Substring(cols); }
                }
                lines.Add(cur);
            }
            return lines;
        }

        // ---- Sprites ----------------------------------------------------------------------------

        /// <summary>
        /// Draws string art: '.' or ' ' is transparent, '0'-'5' are shades, '#' is Bright, '+' is Mid,
        /// ':' is Dark and ',' is Dim. flip mirrors horizontally.
        /// </summary>
        public void Art(int x, int y, string[] rows, bool flip = false, int shift = 0)
        {
            for (int j = 0; j < rows.Length; j++)
            {
                string r = rows[j];
                for (int i = 0; i < r.Length; i++)
                {
                    int v = ArtShade(r[i]);
                    if (v < 0) continue;
                    v = Math.Max(0, Math.Min(5, v + shift));
                    Set(flip ? x + r.Length - 1 - i : x + i, y + j, (byte)v);
                }
            }
        }

        /// <summary>String art drawn with each pixel as a scale x scale block (chunky sprites for the hunt).</summary>
        public void ArtScaled(int x, int y, string[] rows, int scale, bool flip = false, bool flipV = false, int shift = 0)
        {
            for (int j = 0; j < rows.Length; j++)
            {
                string r = rows[flipV ? rows.Length - 1 - j : j];
                for (int i = 0; i < r.Length; i++)
                {
                    int v = ArtShade(r[i]);
                    if (v < 0) continue;
                    v = Math.Max(0, Math.Min(5, v + shift));
                    int px = flip ? x + (r.Length - 1 - i) * scale : x + i * scale;
                    Fill(px, y + j * scale, scale, scale, (byte)v);
                }
            }
        }

        public static int ArtShade(char ch)
        {
            switch (ch)
            {
                case '0': return 0;
                case '1': case ',': return 1;
                case '2': case ':': return 2;
                case '3': case '+': return 3;
                case '4': case '#': return 4;
                case '5': case '@': return 5;
                default: return -1;
            }
        }

        public static int ArtWidth(string[] rows) { int w = 0; foreach (var r in rows) w = Math.Max(w, r.Length); return w; }
    }
}
