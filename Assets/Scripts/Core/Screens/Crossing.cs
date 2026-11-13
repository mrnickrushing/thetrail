using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    /// <summary>At a river: the three scouted places to cross, the ferry if there is one, or wait.</summary>
    public sealed class CrossingScene : Scene
    {
        Ford[] fords;
        readonly List<(Btn b, Action a)> buttons = new List<(Btn, Action)>();

        Stop Here => G.HereStop;

        public override void Enter()
        {
            fords = Sim.Fords(G);
            buttons.Clear();
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                buttons.Add((new Btn(12 + i * 154, 196, 148, 20, "FORD HERE", Sim.Fordable(fords[i])), () => Ford(k)));
            }
            if (Here.Ferry > 0) buttons.Add((new Btn(12, Canvas.H - 26, 200, 20, "FERRY ($" + Here.Ferry + ", wait up to " + Here.FerryWait + " days)", G.Dollars >= Here.Ferry), Ferry));
            buttons.Add((new Btn(Here.Ferry > 0 ? 220 : 12, Canvas.H - 26, 120, 20, "WAIT A DAY"), Wait));
            buttons.Add((new Btn(Canvas.W - 100, Canvas.H - 26, 90, 20, "BACK"), () => App.Go(new StopScene(false))));
        }

        void Ford(int i)
        {
            if (!Sim.Fordable(fords[i])) { S.Bad(); return; }
            App.Go(new FordScene(fords[i]));
        }

        void Ferry()
        {
            if (G.Dollars < Here.Ferry) { S.Bad(); return; }
            int days = Sim.FerryDays(G);
            var log = new List<Notice>();
            for (int d = 0; d < days; d++) log.AddRange(Sim.Rest(G).Notices);
            if (G.Count == 0) { Flow.Report(App, log, null); return; }
            var done = Sim.Ferry(G);
            App.SaveGame();
            var all = new List<Notice>();
            if (days > 0) all.Add(new Notice("You wait " + days + (days == 1 ? " day" : " days") + " in the line for the ferry.", Tone.Info));
            all.AddRange(log); all.AddRange(done);
            Flow.Report(App, all, () => App.Go(new StopScene(false)));
        }

        void Wait()
        {
            var res = Sim.Rest(G);
            Sim.ReadRiver(G);
            App.SaveGame();
            var log = new List<Notice> { new Notice("You camp by the river for a day. It is running about " + Sim.RiverDepth(G, Here).ToString("0") + " feet deep now.", Tone.Info) };
            log.AddRange(res.Notices);
            Flow.Report(App, log, () => App.Go(new CrossingScene()));
        }

        public override void Draw(Canvas c)
        {
            Art.Scene(c, "river", 0, 0, Canvas.W, 96, App.Time, (int)(Sim.Hash(Here.Id) & 0xFF), 0, G.Sky);
            c.Fill(0, 96, Canvas.W, Canvas.H - 96, Pal.Ink);
            c.HLine(0, Canvas.W, 96, Pal.Tan);
            c.TextShadow(10, 8, "Crossing the " + Here.Name.Replace(", North Platte", "").Replace("Mormon Ferry", "North Platte"), Pal.Gold, 2);
            c.TextShadow(12, 28, Here.Width + " feet wide.  " + (G.Has(Trade.Guide) ? G.With(Trade.Guide).name + " has scouted the river." : "Your company has scouted three places."), Pal.Cream);
            for (int i = 0; i < 3; i++)
            {
                var f = fords[i];
                int x = 12 + i * 154, y = 104;
                c.RoundRect(x, y, 148, 88, Pal.Walnut, Pal.Leather);
                c.Text(x + 6, y + 6, Canvas.TextWidth(f.Name) > 136 ? Canvas.Wrap(f.Name, 130)[0] : f.Name, Pal.Gold);
                c.Text(x + 6, y + 22, "Depth: " + Sim.DepthReading(G, f), Pal.Cream);
                c.Text(x + 6, y + 34, "Current: " + Sim.CurrentName(f.Current), Pal.Cream);
                c.Text(x + 6, y + 46, "Bottom: " + Sim.BottomName(f.Bottom), f.Bottom == Bottom.Quicksand ? Pal.Dusk : Pal.Cream);
                string verdict = !Sim.Fordable(f) ? "Too deep to drive across." : f.Depth < 2.6f && f.Current < 0.45f ? "Should be easy." : f.Depth > 5 || f.Current > 0.65f ? "Dangerous." : "Can be done with care.";
                c.Paragraph(x + 6, y + 60, 136, verdict, !Sim.Fordable(f) || verdict == "Dangerous." ? Pal.Dusk : Pal.Tan, 9);
            }
            foreach (var (b, _) in buttons) b.Draw(c, App.PointerDown && b.Hit(App.PointerX, App.PointerY) && b.Enabled);
            c.Text(12, 222, "Cash " + Ui.Dollars(G.cents) + ".  A ford is free but risks the wagon; the ferry is safe.", Pal.Tan);
        }

        public override void Tap(int x, int y) { if (T < 0.25f) return; foreach (var (b, a) in buttons) if (b.Hit(x, y)) { if (b.Enabled) { S.Select(); a(); } else S.Bad(); return; } }
        public override void Key(char k)
        {
            if (k >= '1' && k <= '3') Ford(k - '1');
            else if (k == 'f' && Here.Ferry > 0) Ferry();
            else if (k == 'w') Wait();
            else if (k == Keys.Escape) App.Go(new StopScene(false));
        }
    }

    /// <summary>
    /// Driving the wagon across, seen from above. The team walks toward the far bank on its own; the
    /// current pushes the wagon downstream off the shallow bar. Hold to drive the team upstream.
    /// </summary>
    public sealed class FordScene : Scene
    {
        readonly Ford ford;
        float progress;     // 0 near bank .. 1 far bank
        float x;            // wagon's position across the screen
        float off, total;   // time off the bar, time in the water
        float surge;        // the current comes in surges
        bool done, started;
        const int FarBank = 40, NearBank = 230;

        public FordScene(Ford f) { ford = f; }

        float Drift => Sim.Drift(ford);
        float Speed => (ford.Bottom == Bottom.Mud ? 0.075f : ford.Bottom == Bottom.Quicksand ? 0.065f : 0.09f) * (G.Team == TeamKind.Mules ? 1.1f : 1f);

        /// <summary>Where the bar is at a given progress across: it angles upstream and bends.</summary>
        float BarX(float p) => Canvas.W * 0.5f - p * 70 + (float)Math.Sin(p * 3.2f + Sim.Hash(G.Here) % 7) * 34;
        float BarHalf => 26 - Math.Min(12, ford.Depth * 1.6f);

        public override void Enter() { x = BarX(0); S.StopMusic(); }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (done) return;
            if (!started) { if (T > 1.6f) started = true; else return; }
            progress += Speed * dt;
            surge = 0.6f + 0.6f * (float)(Math.Sin(T * 1.3f) * 0.5 + 0.5) + (Art.Noise(T * 0.8f, 3) - 0.5f) * 0.6f;
            float push = Drift * 70 * surge;
            if (App.Held || App.HeldLeft) push -= 105;
            x += push * dt;
            x = Math.Max(20, Math.Min(Canvas.W - 20, x));
            float d = Math.Abs(x - BarX(progress));
            total += dt;
            if (d > BarHalf) off += dt * Math.Min(2f, d / BarHalf / 1.2f);
            if ((int)(T * 3) % 2 == 0 && d > BarHalf * 1.6f && App.Time % 0.5f < dt) S.Splash();
            if (progress >= 1)
            {
                done = true;
                float frac = Math.Min(1f, off / Math.Max(0.1f, total));
                var log = Sim.Cross(G, ford, frac);
                App.SaveGame();
                Flow.Report(App, log, () => App.Go(new StopScene(false)));
            }
        }

        public override void Draw(Canvas c)
        {
            // Banks.
            c.Gradient(0, 0, Canvas.W, FarBank, Pal.GrassRamp, 0.2f, 0.6f);
            c.Gradient(0, NearBank, Canvas.W, Canvas.H - NearBank, Pal.GrassRamp, 0.6f, 0.2f);
            c.Fill(0, FarBank - 4, Canvas.W, 4, Pal.Sand); c.Fill(0, NearBank, Canvas.W, 4, Pal.Sand);
            // Water, flowing left to right, the bar lighter.
            for (int y = FarBank; y < NearBank; y++)
            {
                float p = 1f - (y - FarBank) / (float)(NearBank - FarBank);
                float bx = BarX(p);
                for (int i = 0; i < Canvas.W; i++)
                {
                    float d = Math.Abs(i - bx) / BarHalf;
                    float t = d < 1 ? 0.75f - d * 0.15f : Math.Max(0.05f, 0.45f - (d - 1) * 0.08f);
                    float ripple = Art.Noise((i - App.Time * 60 * (0.4f + ford.Current)) * 0.07f, y / 3);
                    if (ripple > 0.8f) t += 0.2f;
                    c.SetRamp(i, y, Pal.WaterRamp, t);
                }
            }
            // The wagon from above: cover and the team ahead of it.
            int wy = NearBank - (int)(progress * (NearBank - FarBank)) - 16;
            int wx = (int)x;
            byte col = G.Team == TeamKind.Mules ? Pal.RockDark : Pal.Earth;
            float step = (float)Math.Sin(App.Time * 6);
            c.Line(wx, wy - 12, wx, wy - 46, Pal.WoodDark);
            for (int k = 0; k < 3; k++)
            {
                int ay = wy - 20 - k * 12 + (k % 2 == 0 ? (int)step : -(int)step);
                c.Ellipse(wx - 6, ay, 3, 5, col); c.Ellipse(wx + 6, ay, 3, 5, col);
                c.Ellipse(wx - 6, ay - 5, 2, 2, Pal.EarthDark); c.Ellipse(wx + 6, ay - 5, 2, 2, Pal.EarthDark);
            }
            c.Fill(wx - 12, wy - 13, 24, 32, Pal.WoodDark);
            c.Fill(wx - 11, wy - 12, 22, 30, Pal.Cloth);
            for (int j = wy - 9; j < wy + 17; j += 5) c.HLine(wx - 11, wx + 10, j, Pal.ClothShade);
            c.Fill(wx - 14, wy - 10, 2, 7, Pal.Black); c.Fill(wx + 12, wy - 10, 2, 7, Pal.Black);
            c.Fill(wx - 14, wy + 9, 2, 7, Pal.Black); c.Fill(wx + 12, wy + 9, 2, 7, Pal.Black);
            float dd = Math.Abs(x - BarX(progress));
            if (dd > BarHalf) for (int i = -15; i <= 15; i += 2) c.Set(wx + i, wy + 20 + (int)(Math.Sin(App.Time * 9 + i) * 1.5f), Pal.Foam);
            for (int i = -14; i <= 14; i += 3) c.Set(wx - 16 - (int)(App.Time * 30 + i * 7) % 6, wy + i, Pal.Foam);
            // Guide marks and the readout.
            Ui.Panel(c, 6, 4, Canvas.W - 12, 26);
            c.Text(14, 9, Sim.DepthReading(G, ford) + ", " + Sim.CurrentName(ford.Current) + " current", Pal.Cream);
            bool onBar = dd <= BarHalf;
            c.TextRight(Canvas.W - 14, 9, onBar ? "ON THE BAR" : dd > BarHalf * 2 ? "DEEP WATER!" : "OFF THE BAR", onBar ? Pal.Green : Pal.RedBright);
            c.Text(14, 19, (App.Touch ? "Hold the screen" : "Hold space") + " to drive upstream, left, against the current.", Pal.Tan);
            if (!started) c.TextShadowCenter(110, "Keep the wagon on the pale shallow water.", Pal.Gold, 1);
        }
    }
}
