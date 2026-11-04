using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    /// <summary>Rafting the Columbia: steer between the rocks. Touch and hold where you want the raft to go.</summary>
    public sealed class RaftScene : Scene
    {
        public const float Course = 2400f;
        const int RaftY = 196, Top = 16;
        const float Speed = 56f;

        public float Dist;
        public float X = Canvas.W / 2f;
        float vx, invuln, bankBump;
        bool started, over;
        int hits;
        readonly List<(float d, float x, int r)> rocks = new List<(float, float, int)>();
        readonly List<Notice> log = new List<Notice>();

        public static float Center(float d) => Canvas.W / 2f + 70 * (float)Math.Sin(d * 0.004f) + 26 * (float)Math.Sin(d * 0.011f + 1.3f);
        public static float Half(float d) => 92 + 26 * (float)Math.Sin(d * 0.007f + 0.4f);

        public override void Enter()
        {
            var r = new Rng(G.seed ^ 0xC0FFEEu);
            for (float d = 260; d < Course - 120; d += r.Range(70f, 130f))
            {
                int count = r.Chance(0.3f) ? 2 : 1;
                for (int k = 0; k < count; k++)
                {
                    float h = Half(d) - 20;
                    rocks.Add((d + k * 30, Center(d) + r.Range(-h, h), r.Range(5, 9)));
                }
            }
            App.SayAll(new[]
            {
                "You trade the oxen at the mission for a few supplies and lash your wagon onto a raft of logs.",
                App.Touch ? "Steer down the Columbia River. Touch and hold where you want the raft to go, and keep clear of the rocks."
                          : "Steer down the Columbia River with the arrow keys or the mouse, and keep clear of the rocks.",
            }, () => started = true);
            G.oxen = 0; G.oxenWeak = 0;
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (!started || over || App.Top != this) return;
            Dist += Speed * dt;
            invuln = Math.Max(0, invuln - dt);
            bankBump = Math.Max(0, bankBump - dt);

            // Steering: toward the finger, or by arrow keys. The current pulls toward the middle a little.
            float target = vx;
            if (App.PointerDown) target = Math.Max(-110, Math.Min(110, (App.PointerX - X) * 3f));
            else if (App.HeldLeft) target = -95; else if (App.HeldRight) target = 95;
            else target = vx * 0.92f + (Center(Dist) - X) * 0.05f;
            vx += (target - vx) * Math.Min(1f, dt * 6);
            X += vx * dt;

            float c = Center(Dist), h = Half(Dist) - 12;
            if (X < c - h || X > c + h)
            {
                X = Math.Max(c - h, Math.Min(c + h, X));
                vx = -vx * 0.4f;
                if (bankBump <= 0) { S.Thud(); bankBump = 0.8f; }
            }

            if (invuln <= 0)
                foreach (var rk in rocks)
                {
                    float sy = RaftY - (rk.d - Dist);
                    if (Math.Abs(sy - RaftY) < rk.r + 6 && Math.Abs(rk.x - X) < rk.r + 11) { Crash(rk.x); break; }
                }

            if (Dist >= Course) Finish();
        }

        void Crash(float rockX)
        {
            hits++; invuln = 1.6f;
            vx = (X < rockX ? -1 : 1) * 120;
            S.Thud();
            var r = G.rng;
            int k = r.Range(0, 4);
            if (k == 0 && G.food > 0) { int n = Math.Min(G.food, Math.Max(10, G.food * r.Range(10, 25) / 100)); G.food -= n; log.Add(new Notice("The raft hit a rock. " + n + " pounds of food went into the river.")); }
            else if (k == 1 && G.clothing > 0) { int n = Math.Min(G.clothing, r.Range(1, 3)); G.clothing -= n; log.Add(new Notice("The raft hit a rock. You lost " + (n == 1 ? "a set" : n + " sets") + " of clothing.")); }
            else if (k == 2 && G.bullets > 0) { int n = Math.Min(G.bullets, r.Range(10, 40)); G.bullets -= n; log.Add(new Notice("The raft hit a rock. You lost " + n + " bullets.")); }
            else log.Add(new Notice("The raft slammed into a rock, but the lashings held."));
            if (r.Chance(0.22f))
            {
                var alive = G.party.FindAll(m => m.alive);
                if (alive.Count > 0) Sim.Die(G, alive[r.Range(0, alive.Count)], "drowning", log);
            }
            if (G.Alive == 0) Finish();
        }

        void Finish()
        {
            if (over) return;
            over = true;
            G.day += 2;
            G.miles += G.legMiles - G.legDone; G.legDone = G.legMiles;
            if (G.Alive > 0)
            {
                log.Insert(0, new Notice(hits == 0 ? "You brought the raft down the Columbia without a scratch!" : "You made it down the Columbia, battered but afloat.", true, false, true));
                log.Add(new Notice("From the landing you follow the Willamette upstream to Oregon City.", true, false, true));
            }
            Flow.Report(App, log, () => Flow.Arrive(App), true);
        }

        public override void Draw(Canvas c)
        {
            // River banks with pines; water with moving ripples.
            for (int y = Top; y < Canvas.H; y++)
            {
                float d = Dist + (RaftY - y);
                float cx = Center(d), h = Half(d);
                for (int x = 0; x < Canvas.W; x++)
                {
                    float off = Math.Abs(x - cx);
                    if (off > h)
                    {
                        float s = 1.4f + Art.Noise(x * 0.15f, (int)d / 4) * 0.8f;
                        if (off < h + 3) s = 3f;
                        c.SetShade(x, y, s);
                    }
                    else
                    {
                        // Short ripple dashes drifting with the current.
                        int cell = ((int)d / 7) * 131 + (x / 9) * 17;
                        bool ripple = ((int)d % 7 == 0) && (cell * 2654435761u >> 28) < 3 && x % 9 < 5;
                        c.SetShade(x, y, ripple ? 2.6f : 0.55f + (off / h) * 0.35f);
                    }
                }
            }
            for (int i = 0; i < 24; i++)
            {
                float wd = (float)Math.Floor((Dist + 260) / 40) * 40 - i * 40;
                int y = (int)(RaftY - (wd - Dist));
                if (y < Top + 8 || y > Canvas.H + 20) continue;
                float cx = Center(wd), h = Half(wd);
                Art.Pine(c, (int)(cx - h - 14 - (i % 3) * 7), y, 14 + (i % 4) * 3, Canvas.Mid);
                Art.Pine(c, (int)(cx + h + 14 + (i % 2) * 9), y + 7, 12 + (i % 3) * 4, Canvas.Mid);
            }
            foreach (var rk in rocks)
            {
                int sy = (int)(RaftY - (rk.d - Dist));
                if (sy < Top - 12 || sy > Canvas.H + 12) continue;
                // White water piling up on the upstream side, then the rock itself.
                int foam = (int)(App.Time * 6) % 2;
                for (int i = -rk.r - 3; i <= rk.r + 3; i++)
                {
                    int fy = sy - rk.r + 1 - (int)Math.Sqrt(Math.Max(0, (rk.r + 3) * (rk.r + 3) - i * i)) / 2 - foam;
                    c.Set((int)rk.x + i, fy, (i + foam) % 3 == 0 ? Canvas.Hot : Canvas.Bright);
                }
                for (int j = -rk.r; j <= rk.r; j++)
                    for (int i = -rk.r - 2; i <= rk.r + 2; i++)
                    {
                        float dd = i * i / (float)((rk.r + 2) * (rk.r + 2)) + j * j / (float)(rk.r * rk.r);
                        if (dd > 1 - Art.Noise(i * 0.7f + j, (int)rk.d) * 0.25f) continue;
                        c.SetShade((int)rk.x + i, sy + j, 2.2f + (i < 0 ? 0.7f : -0.4f) + (j < 0 ? 0.5f : -0.3f));
                    }
            }
            // The raft: logs lashed together, the wagon bonnet on top. Blinks while you recover from a hit.
            if (invuln <= 0 || (int)(App.Time * 12) % 2 == 0)
            {
                int rx = (int)X - 11, ry = RaftY - 8;
                for (int i = 0; i < 6; i++) c.Fill(rx, ry + i * 3, 22, 2, i % 2 == 0 ? Canvas.Mid : Canvas.Bright);
                c.Fill(rx + 4, ry + 2, 14, 11, Canvas.Bright);
                for (int i = 0; i < 3; i++) c.HLine(rx + 4, rx + 17, ry + 4 + i * 3, Canvas.Mid);
                c.Fill(rx + 4, ry + 2, 14, 1, Canvas.Hot);
                int wake = (int)(App.Time * 10) % 3;
                c.HLine(rx + 2, rx + 19, ry + 19 + wake, Canvas.Dark);
            }

            c.Fill(0, 0, Canvas.W, Top, Canvas.Black);
            c.HLine(0, Canvas.W, Top - 1, Canvas.Mid);
            c.Text(6, 4, "THE COLUMBIA RIVER", Canvas.Bright);
            int left = Math.Max(0, (int)((Course - Dist) / Course * G.legMiles));
            c.TextRight(Canvas.W - 6, 4, left + " miles to go", Canvas.Hot);
            // Progress tick.
            c.HLine(4, Canvas.W - 4, Top - 1, Canvas.Dark);
            c.HLine(4, 4 + (int)((Canvas.W - 8) * Math.Min(1, Dist / Course)), Top - 1, Canvas.Hot);
        }

        public override void Tap(int x, int y) { }
    }
}
