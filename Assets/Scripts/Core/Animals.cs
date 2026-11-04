using System;

namespace TheTrail.Core
{
    /// <summary>
    /// A small shaded sprite built from ellipses and limbs, then stamped onto the screen with a black
    /// outline so it stands out from the grass. Drawn facing left; Blit mirrors it for facing right.
    /// </summary>
    public sealed class Sprite
    {
        public readonly int W, H;
        readonly float[] s;

        public Sprite(int w, int h)
        {
            W = w; H = h; s = new float[w * h];
            for (int i = 0; i < s.Length; i++) s[i] = -1;
        }

        public float Get(int x, int y) => x >= 0 && y >= 0 && x < W && y < H ? s[y * W + x] : -1;
        public void Put(int x, int y, float v) { if (x >= 0 && y >= 0 && x < W && y < H) s[y * W + x] = Math.Max(0.4f, v); }

        /// <summary>A filled ellipse, lit from above: lighter on top, darker underneath. rough adds a shaggy coat.</summary>
        public void Ellipse(float cx, float cy, float rx, float ry, float shade, float rough = 0)
        {
            for (int y = (int)Math.Floor(cy - ry); y <= (int)Math.Ceiling(cy + ry); y++)
                for (int x = (int)Math.Floor(cx - rx); x <= (int)Math.Ceiling(cx + rx); x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    float d = dx * dx + dy * dy;
                    float edge = rough > 0 ? 1 + (Art.Noise(x * 1.7f + y * 0.9f, 17) - 0.5f) * rough : 1;
                    if (d > edge) continue;
                    float v = shade - dy * 0.55f - dx * 0.15f;
                    if (rough > 0) v += (Art.Noise(x * 0.9f, y + 31) - 0.5f) * rough * 1.2f;
                    Put(x, y, v);
                }
        }

        /// <summary>A thick line (legs, necks, antlers).</summary>
        public void Limb(float x0, float y0, float x1, float y1, float width, float shade)
        {
            float len = (float)Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
            int steps = Math.Max(1, (int)(len * 2));
            float r = width / 2f;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps, px = x0 + (x1 - x0) * t, py = y0 + (y1 - y0) * t;
                for (int y = (int)Math.Floor(py - r); y <= (int)Math.Ceiling(py + r); y++)
                    for (int x = (int)Math.Floor(px - r); x <= (int)Math.Ceiling(px + r); x++)
                    {
                        float dx = x + 0.5f - px, dy = y + 0.5f - py;
                        if (dx * dx + dy * dy <= r * r + 0.15f) Put(x, y, shade);
                    }
            }
        }

        /// <summary>A two-part leg from the hip to the ground, swinging with the gait. Returns nothing; hooves are darker.</summary>
        public void Leg(float hipX, float hipY, float groundY, float width, float shade, float swing, float hoof = 1.2f)
        {
            float len = groundY - hipY;
            float a = swing * 0.42f;                         // forward/back swing of the upper leg
            float lift = Math.Max(0, -swing) * len * 0.22f;  // a leg swinging forward lifts its hoof
            float kx = hipX - (float)Math.Sin(a) * len * 0.5f, ky = hipY + (float)Math.Cos(a) * len * 0.5f;
            float fx = hipX - (float)Math.Sin(a) * len * 0.95f + Math.Max(0, -swing) * 1.2f, fy = groundY - lift;
            Limb(hipX, hipY, kx, ky, width, shade);
            Limb(kx, ky, fx, fy - 1, width * 0.8f, shade - 0.2f);
            Limb(fx, fy - 1, fx, fy, width * 0.85f, hoof);
        }

        public void Blit(Canvas c, int x, int y, bool flipH, bool flipV, float shift = 0, bool outline = true)
        {
            for (int j = 0; j < H; j++)
                for (int i = 0; i < W; i++)
                {
                    int sx = flipH ? W - 1 - i : i, sy = flipV ? H - 1 - j : j;
                    float v = Get(sx, sy);
                    // Solid shades (no dither): small sprites read cleaner as flat pixel-art tones.
                    if (v >= 0) { c.Set(x + i, y + j, (byte)Math.Max(1, Math.Min(5, (int)Math.Round(v + shift)))); continue; }
                    if (!outline) continue;
                    if (Get(sx - 1, sy) >= 0 || Get(sx + 1, sy) >= 0 || Get(sx, sy - 1) >= 0 || Get(sx, sy + 1) >= 0)
                        c.Set(x + i, y + j, Canvas.Black);
                }
        }
    }

    /// <summary>The game animals for the hunt, drawn facing left with a walking or running gait.</summary>
    public static class Animals
    {
        public static readonly string[] Kinds = { "buffalo", "elk", "deer", "antelope", "bear", "rabbit", "squirrel" };

        public static (int w, int h) Size(string kind)
        {
            switch (kind)
            {
                case "buffalo": return (48, 30);
                case "elk": return (40, 40);
                case "deer": return (32, 30);
                case "antelope": return (30, 27);
                case "bear": return (42, 25);
                case "rabbit": return (15, 13);
                default: return (15, 14);
            }
        }

        /// <summary>phase advances with time (radians of the gait); fleeing changes the deer's tail and the rabbit's hop.</summary>
        public static Sprite Draw(string kind, float phase, bool fleeing)
        {
            var (w, h) = Size(kind);
            var sp = new Sprite(w, h);
            float g = h - 1;
            float A(float off) => (float)Math.Sin(phase + off);
            switch (kind)
            {
                case "buffalo":
                    // Far legs, the long low body, the shaggy hump and head, then the near legs.
                    sp.Leg(17, 19, g, 3.2f, 1.4f, A(Pi));
                    sp.Leg(36, 19, g, 3.2f, 1.6f, A(0));
                    sp.Ellipse(29, 14, 15, 7, 2.6f);
                    sp.Ellipse(36, 12, 7, 6, 2.9f);
                    sp.Ellipse(16, 11, 10, 9.5f, 1.9f, 0.6f);
                    sp.Ellipse(19, 18, 7, 4, 1.7f, 0.5f);
                    sp.Leg(14, 19, g, 3.4f, 2.0f, A(0));
                    sp.Leg(33, 19, g, 3.4f, 2.2f, A(Pi));
                    sp.Ellipse(7, 16, 6, 5.5f, 1.8f, 0.5f);
                    sp.Ellipse(6, 22, 3, 3.5f, 1.4f, 0.6f);      // beard
                    sp.Ellipse(2.5f, 18, 2.5f, 2.2f, 2.4f);       // muzzle
                    sp.Limb(9, 12, 10, 9, 1.6f, 4.6f);           // horn
                    sp.Put(9, 8, 4.8f);
                    sp.Put(4, 15, 0.4f);                          // eye
                    sp.Limb(43, 11, 45, 18, 1.1f, 1.8f);          // tail
                    sp.Ellipse(45.5f, 19, 1.5f, 2, 1.4f);
                    break;

                case "elk":
                    sp.Leg(17, 23, g, 2.3f, 2.0f, A(Pi));
                    sp.Leg(30, 23, g, 2.3f, 2.2f, A(0));
                    sp.Ellipse(24, 20, 11.5f, 5.5f, 3.0f);
                    sp.Ellipse(33, 19, 3.5f, 4.5f, 4.5f);          // pale rump
                    sp.Leg(14, 23, g, 2.5f, 2.6f, A(0));
                    sp.Leg(27, 23, g, 2.5f, 2.8f, A(Pi));
                    sp.Limb(15, 19, 9, 11, 5.5f, 2.1f);           // dark maned neck
                    sp.Ellipse(6, 10.5f, 4.5f, 2.8f, 2.9f);
                    sp.Ellipse(2.5f, 11.5f, 2.2f, 1.9f, 2.6f);
                    sp.Limb(9, 8, 11, 5.5f, 1.4f, 2.8f);           // ear
                    // Antlers: a far one, darker, and the near one with its tines.
                    sp.Limb(10, 7, 14, 3, 1.1f, 3.6f); sp.Limb(14, 3, 19, 1, 1.1f, 3.6f); sp.Limb(16, 2, 16, 0, 1, 3.6f);
                    sp.Limb(8, 7, 11, 3, 1.3f, 4.7f); sp.Limb(11, 3, 16, 0.5f, 1.3f, 4.7f);
                    sp.Limb(9.5f, 5, 7, 2.5f, 1.1f, 4.7f); sp.Limb(12, 2.5f, 11, 0, 1.1f, 4.7f); sp.Limb(14.5f, 1, 15, -1, 1.1f, 4.7f);
                    sp.Put(4, 10, 0.4f);
                    sp.Limb(35, 17, 36, 19, 1.2f, 4.2f);
                    break;

                case "deer":
                    sp.Leg(13, 18, g, 1.8f, 2.3f, A(Pi));
                    sp.Leg(24, 18, g, 1.8f, 2.4f, A(0));
                    sp.Ellipse(19, 15, 9.5f, 4.6f, 3.2f);
                    sp.Ellipse(20, 18, 7, 1.6f, 4.2f);             // pale belly
                    sp.Leg(11, 18, g, 2.0f, 2.9f, A(0));
                    sp.Leg(22, 18, g, 2.0f, 3.0f, A(Pi));
                    sp.Limb(12, 14, 8, 7, 3.8f, 3.1f);
                    sp.Ellipse(5.5f, 6.5f, 3.6f, 2.2f, 3.3f);
                    sp.Ellipse(2.2f, 7.5f, 1.7f, 1.4f, 2.3f);
                    sp.Put(1, 7, 0.4f);
                    sp.Limb(8.5f, 5, 11, 2.5f, 1.6f, 3.2f);        // ears
                    sp.Limb(7, 5, 7.5f, 2.5f, 1.4f, 3.0f);
                    sp.Limb(8, 4, 9, 1, 1, 4.6f); sp.Limb(9, 1.5f, 11, 0, 1, 4.6f);   // a small rack
                    sp.Put(4, 6, 0.4f);
                    if (fleeing) { sp.Limb(28, 12, 30, 9, 2.2f, 4.9f); }
                    else sp.Limb(28, 12, 29, 15, 1.8f, 3.4f);
                    break;

                case "antelope":
                    sp.Leg(12, 16, g, 1.7f, 2.5f, A(Pi));
                    sp.Leg(22, 16, g, 1.7f, 2.6f, A(0));
                    sp.Ellipse(17.5f, 12.5f, 9, 4.3f, 3.5f);
                    sp.Ellipse(18, 15.2f, 7.5f, 1.8f, 4.9f);       // white belly
                    sp.Ellipse(25, 12, 2.6f, 3.2f, 4.9f);         // white rump
                    sp.Leg(10, 16, g, 1.9f, 3.0f, A(0));
                    sp.Leg(20, 16, g, 1.9f, 3.1f, A(Pi));
                    sp.Limb(11, 12, 8, 5, 3.2f, 3.4f);
                    sp.Limb(10.5f, 10, 9.5f, 8, 3.4f, 4.9f);       // throat bands
                    sp.Ellipse(5, 4.5f, 3.2f, 2, 3.5f);
                    sp.Ellipse(2.2f, 5.4f, 1.6f, 1.3f, 1.6f);      // dark face
                    sp.Limb(6, 3, 6, 0, 1.3f, 1.1f); sp.Put(5, 0, 1.1f);            // pronged horns
                    sp.Put(4, 4, 0.4f);
                    break;

                case "bear":
                    sp.Leg(16, 15, g, 4.0f, 1.4f, A(Pi) * 0.8f, 0.8f);
                    sp.Leg(33, 15, g, 4.0f, 1.5f, A(0) * 0.8f, 0.8f);
                    sp.Ellipse(24, 11, 14.5f, 7, 2.2f, 0.4f);
                    sp.Ellipse(16, 8, 7, 6, 2.4f, 0.4f);           // shoulder hump
                    sp.Leg(13, 15, g, 4.4f, 2.0f, A(0) * 0.8f, 0.9f);
                    sp.Leg(30, 15, g, 4.4f, 2.1f, A(Pi) * 0.8f, 0.9f);
                    sp.Ellipse(7, 13, 5.5f, 4.6f, 2.4f, 0.3f);
                    sp.Ellipse(2.5f, 14.5f, 2.6f, 2.1f, 3.4f);     // tan muzzle
                    sp.Put(0, 14, 0.4f);
                    sp.Ellipse(9, 8.5f, 1.8f, 1.8f, 2.7f);          // ear
                    sp.Put(5, 12, 0.4f);
                    break;

                case "rabbit":
                {
                    float hop = Math.Abs((float)Math.Sin(phase)) * (fleeing ? 3.5f : 2.5f);
                    float by = -hop;
                    sp.Ellipse(9, 8.5f + by, 4.8f, 3.2f, 3.4f);
                    sp.Ellipse(13, 7.5f + by, 1.8f, 1.8f, 4.9f);    // cotton tail
                    sp.Limb(10, 10 + by, 13, 12 + Math.Min(0, by + 1), 2.2f, 3.0f);  // hind legs
                    sp.Limb(5, 10 + by, 4, 12 + by * 0.5f, 1.2f, 3.0f);
                    sp.Ellipse(4, 6.5f + by, 2.8f, 2.2f, 3.5f);
                    sp.Limb(5, 4.5f + by, 7, 0.5f + by, 1.5f, 3.8f);  // long ears
                    sp.Limb(4, 4.5f + by, 5, 0.8f + by, 1.3f, 3.2f);
                    sp.Put(3, 6 + (int)by, 0.4f);
                    break;
                }

                default: // squirrel
                    sp.Limb(10, 10, 12, 6, 3.4f, 3.6f);            // bushy tail curling up
                    sp.Limb(12, 6, 12, 2, 3.6f, 3.8f);
                    sp.Limb(12, 2, 9.5f, 0.8f, 2.8f, 4.0f);
                    sp.Ellipse(7, 10, 3.8f, 2.6f, 3.2f);
                    sp.Limb(5, 12, 4 - A(0), 13, 1.2f, 2.8f);
                    sp.Limb(9, 12, 10 + A(0), 13, 1.4f, 2.8f);
                    sp.Ellipse(3.5f, 8, 2.3f, 2, 3.3f);
                    sp.Put(4, 5, 3.5f); sp.Put(4, 6, 3.5f);
                    sp.Put(2, 7, 0.4f);
                    break;
            }
            return sp;
        }

        const float Pi = (float)Math.PI;
    }
}
