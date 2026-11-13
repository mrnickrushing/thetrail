using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    /// <summary>
    /// Working the ropes: letting the wagon down Windlass Hill, or hauling it up the granite at the
    /// Sierra summit. The strain on the rope wanders as the wagon bucks on the rock; hold to take up
    /// rope, let go to pay it out. The wagon only moves while the strain is in the safe band; too much
    /// and the chain snaps, too little and the wagon runs.
    /// </summary>
    public sealed class HaulScene : Scene
    {
        float strain = 0.5f, progress, jolt, joltT, shake, endT = -1;
        int slips;
        bool done;
        Rng r;
        string msg = "";

        bool Up => G.HereStop.Haul == 2;
        float Diff => Sim.HaulDifficulty(G);
        float Half => 0.2f - Diff * 0.08f;
        const float Centre = 0.55f;

        public override void Enter()
        {
            r = new Rng(G.seed ^ 0xA11u ^ (uint)G.day);
            S.StopMusic();
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            shake = Math.Max(0, shake - dt * 3);
            if (done) { if (T - endT > 1.4f) Finish(); return; }
            if (T < 1.2f) return;
            bool pull = App.Held;
            strain += (pull ? 0.55f : -0.45f) * dt;
            // The rock: jolts come at random and push the strain one way or the other.
            joltT -= dt;
            if (joltT <= 0) { joltT = r.Range(0.5f, 1.4f) * (1.2f - Diff * 0.5f); jolt = r.Range(-1f, 1f) * (0.4f + Diff * 0.6f); }
            strain += jolt * dt * (Up ? 1.1f : 0.9f);
            strain += (Up ? 0.08f : -0.06f) * dt;   // the wagon's weight on the rope
            float d = Math.Abs(strain - Centre);
            if (d <= Half) progress += dt * 0.075f * (1.1f - Diff * 0.3f);
            else if (T % 0.6f < dt) S.Creak();
            if (strain >= 1 || strain <= 0)
            {
                slips++; shake = 1;
                msg = strain >= 1 ? (Up ? "The chain snapped! The wagon slid back." : "Too much strain! The rope burned through.") : (Up ? "Slack! The wagon rolled back down." : "The rope ran! The wagon slammed down the slope.");
                progress = Math.Max(0, progress - (Up ? 0.14f : 0.06f) + (Up ? 0 : 0.12f));
                strain = 0.5f; jolt = 0;
                S.Snap();
            }
            if (progress >= 1 || slips >= 4) { done = true; endT = T; if (progress >= 1) S.Good(); }
        }

        void Finish()
        {
            if (endT < -0.5f) return;
            endT = -2;
            var log = Sim.Hauled(G, slips);
            App.SaveGame();
            Flow.Report(App, log, () => App.Go(new StopScene(false)));
        }

        public override void Draw(Canvas c)
        {
            int sx = shake > 0 ? (int)(Math.Sin(T * 60) * shake * 3) : 0;
            Art.Sky(c, 0, 0, Canvas.W, 150, 0, App.Time, 11, G.Sky);
            c.Gradient(0, 120, Canvas.W, Canvas.H - 120, Up ? Pal.PineRamp : Pal.GrassRamp, 0.3f, 0.7f);
            Art.Range(c, 0, Canvas.W, 120, 50, 0.01f, 5, Pal.Far, Pal.FarLight, 0, Up ? 90 : -999);
            // The slope: a long diagonal from bottom left to top right.
            int x0 = 20, y0 = 236, x1 = 400, y1 = 70;
            var slope = new List<(int, int)> { (0, Canvas.H), (0, y0 + 6), (x0, y0), (x1, y1), (Canvas.W, y1 - 8), (Canvas.W, Canvas.H) };
            c.Poly(slope, Up ? Pal.RockLight : Pal.Sand);
            for (int i = 0; i < 40; i++)
            {
                float f = i / 40f;
                int bx = x0 + (int)((x1 - x0) * f) + 6, by = y0 + (int)((y1 - y0) * f) + 10 + (i * 13 % 9);
                c.Line(bx, by, bx + 8, by - 3, Up ? Pal.Rock : Pal.Earth);
            }
            c.Poly(new List<(int, int)> { (0, Canvas.H), (0, y0 + 30), (x1 + 40, y1 + 40), (Canvas.W, y1 + 30), (Canvas.W, Canvas.H) }, Up ? Pal.Rock : Pal.Earth);
            // The anchor at the top: a tree for the windlass hill, a windlass on the summit.
            if (Up) { c.Fill(x1 + 22, y1 - 22, 6, 18, Pal.WoodDark); c.Fill(x1 + 10, y1 - 14, 30, 4, Pal.Wood); Art.Pine(c, x1 + 54, y1 - 6, 40, G.Doy > 280); }
            else Art.Cottonwood(c, x1 + 26, y1 - 4, 34, 3);
            // Where the wagon is: up the slope as the haul goes on (or down it when lowering).
            float f0 = Up ? progress : 1f - progress;
            int wx = x0 + (int)((x1 - x0 - 60) * f0) + 30 + sx, wy = y0 + (int)((y1 - y0) * ((wx - sx - x0) / (float)(x1 - x0)));
            float tilt = (float)Math.Atan2(y1 - y0, x1 - x0);
            Art.Wagon(c, wx, wy - 1, -progress * 30, tilt, G.wagon < 50);
            // The rope from the wagon up to the anchor, taut or sagging.
            int ax = x1 + 25, ay = y1 - 12;
            float sag = Math.Max(0, Centre - strain) * 40;
            int prevx = wx + 10, prevy = wy - 14;
            for (int i = 1; i <= 12; i++)
            {
                float t = i / 12f;
                int px = wx + 10 + (int)((ax - wx - 10) * t), py = wy - 14 + (int)((ay - wy + 14) * t + Math.Sin(t * Math.PI) * sag);
                c.Line(prevx, prevy, px, py, strain > Centre + Half ? Pal.RedBright : Pal.Cloth);
                prevx = px; prevy = py;
            }
            // The company on the rope.
            for (int k = 0; k < Math.Min(4, G.Count); k++)
            {
                int mx = Up ? ax + 10 + k * 9 : ax - 14 - k * 9, my = ay + 10 + k * (Up ? 2 : 3);
                Art.Figure(c, mx, my, new[] { Pal.Blue, Pal.Red, Pal.Earth, Pal.Sage }[k], k % 3, 2, App.Time + k);
            }
            // The gauge.
            Ui.Panel(c, 6, 4, Canvas.W - 12, 26);
            c.Text(14, 9, Up ? "Hauling the wagon up the granite on chains." : "Letting the wagon down Windlass Hill on ropes.", Pal.Cream);
            c.Text(14, 19, (App.Touch ? "Hold" : "Hold space") + " to take up rope, let go to pay out. Keep the strain green.", Pal.Tan);
            int gx = Canvas.W - 34, gy = 44, gh = 140;
            c.Fill(gx - 2, gy - 2, 20, gh + 4, Pal.Ink);
            c.Fill(gx, gy, 16, gh, Pal.RedDark);
            int lo = gy + (int)((1 - (Centre + Half)) * gh), hi = gy + (int)((1 - (Centre - Half)) * gh);
            c.Fill(gx, lo, 16, hi - lo, Pal.Green);
            int my2 = gy + (int)((1 - Sim.Clamp(strain, 0, 1)) * gh);
            c.Fill(gx - 4, my2 - 1, 24, 3, Pal.Cream);
            c.Fill(gx - 12, gy + gh + 4, 40, 11, Pal.Ink);
            c.TextCenterIn(gx - 12, 40, gy + gh + 6, "STRAIN", Pal.Cream);
            Ui.Bar(c, 14, Canvas.H - 18, 200, 10, progress, Pal.GoldMid, (int)(progress * 100) + "%");
            c.Text(224, Canvas.H - 17, "Slips " + slips + " of 4", slips > 0 ? Pal.Dusk : Pal.Tan);
            if (msg.Length > 0 && !done) c.TextShadowCenter(40, msg, Pal.RedBright);
            if (T < 1.2f) c.TextShadowCenter(110, "Ready the ropes...", Pal.Gold, 2);
            if (done) c.TextShadowCenter(110, progress >= 1 ? (Up ? "Over the top!" : "Down safe!") : "The company gives up and drags it the last way.", progress >= 1 ? Pal.Green : Pal.Dusk, progress >= 1 ? 2 : 1);
        }

        public override void Tap(int x, int y) { if (done && T - endT > 0.4f) Finish(); }
        public override void Key(char k) { if (done && T - endT > 0.4f && k != Keys.Space) Finish(); }
    }
}
