using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    /// <summary>
    /// Stalking game. One animal grazes in the distance; creep toward it while its head is down and
    /// freeze when it looks up. Fire when close enough: the nearer, the surer the shot. Moving while
    /// it watches makes it uneasy, and an uneasy animal bolts.
    /// </summary>
    public sealed class StalkScene : Scene
    {
        readonly string animal;
        readonly Action then;
        readonly bool fromCamp;
        float yards, uneasy, cycle, nextLook, lookFor, flee, endT = -1, muzzle;
        bool headUp, fled, killed;
        int shots, pounds;
        string msg = "";
        Rng r;
        Btn creep, fire, quit;
        const int ViewH = 200, Horizon = 112;

        public StalkScene(string animal, Action then)
        {
            fromCamp = animal == null;
            this.animal = animal;
            this.then = then;
        }

        string Kind { get; set; }

        public override void Enter()
        {
            r = new Rng(G.seed ^ (uint)G.day * 977u ^ 0x5EEDu);
            var ground = Sim.GroundNow(G);
            Kind = animal ?? (ground == Ground.Mountains || ground == Ground.Forest || ground == Ground.Hills ? (r.Chance(0.7f) ? "deer" : "antelope")
                : ground == Ground.Prairie || ground == Ground.Plains ? (r.Chance(0.45f) ? "buffalo" : "antelope")
                : r.Chance(0.6f) ? "antelope" : "deer");
            yards = r.Range(230f, 290f);
            nextLook = r.Range(1.8f, 3.2f);
            creep = new Btn(10, Canvas.H - 30, 150, 24, App.Touch ? "HOLD TO CREEP" : "HOLD SPACE: CREEP");
            fire = new Btn(Canvas.W - 130, Canvas.H - 30, 120, 24, "FIRE");
            quit = new Btn(Canvas.W / 2 - 50, Canvas.H - 30, 100, 24, "GIVE UP");
            S.StopMusic();
        }

        int Meat => Kind == "buffalo" ? 420 : Kind == "deer" ? 110 : 60;
        bool Hunter => G.Has(Trade.Hunter);
        bool Over => endT >= 0;
        bool Creeping => !Over && (App.HeldSpace || App.HeldUp || (App.PointerDown && creep.Hit(App.PointerX, App.PointerY)));

        public override void Tick(float dt)
        {
            base.Tick(dt);
            muzzle = Math.Max(0, muzzle - dt);
            if (Over) { if (T - endT > 1.6f) Finish(); return; }
            // The animal grazes, then lifts its head to look about.
            cycle += dt;
            if (!headUp && cycle > nextLook) { headUp = true; cycle = 0; lookFor = r.Range(0.9f, 1.6f) + uneasy; }
            else if (headUp && cycle > lookFor) { headUp = false; cycle = 0; nextLook = r.Range(1.4f, 3.4f) * (1f - uneasy * 0.4f); }
            if (Creeping)
            {
                yards = Math.Max(25, yards - dt * 34);
                if (headUp) uneasy += dt * (Hunter ? 0.55f : 0.9f) * (1.4f - yards / 300f);
                if ((int)(T * 4) % 2 == 0 && T % 0.25f < dt) S.Click();
            }
            else uneasy = Math.Max(0, uneasy - dt * 0.08f);
            if (yards < 45) uneasy += dt * 0.25f;   // too close to stay hidden for long
            if (uneasy >= 1) Bolt("The " + Kind + " caught sight of you and ran.");
        }

        void Bolt(string why) { fled = true; msg = why; endT = T; S.Bad(); }

        float HitChance => Sim.Clamp(1.1f - yards / 190f + (Hunter ? 0.15f : 0) - (headUp ? 0 : 0.05f), 0.04f, 0.95f);

        void Fire()
        {
            if (Over) return;
            if (G.powder - shots <= 0) { msg = "Out of powder and shot."; S.Bad(); return; }
            shots++; muzzle = 0.15f;
            S.Shot();
            if (r.Chance(HitChance))
            {
                killed = true; pounds = Meat; endT = T;
                msg = "A clean shot at " + (int)yards + " yards. You'll need the whole company to carry the meat.";
                if (Kind != "buffalo") msg = "A clean shot at " + (int)yards + " yards.";
                return;
            }
            // A buffalo herd may only shift; lighter animals are gone at the first shot.
            if (Kind == "buffalo" && r.Chance(0.5f)) { msg = "Missed. The herd shifts and keeps grazing, uneasy."; uneasy = Math.Min(0.95f, uneasy + 0.4f); headUp = true; cycle = 0; lookFor = 1.6f; }
            else Bolt("Missed at " + (int)yards + " yards. The " + Kind + " is gone.");
        }

        void Finish()
        {
            if (endT < -0.5f) return;
            endT = -2;
            string outcome = Sim.HuntResult(G, Kind, pounds, shots);
            var log = new List<Notice> { new Notice(outcome, pounds > 0 ? Tone.Good : Tone.Info) };
            if (fromCamp)
            {
                var day = Sim.Rest(G);
                log[0] = new Notice("A day spent hunting. " + outcome, log[0].Tone);
                log.AddRange(day.Notices);
            }
            App.SaveGame();
            App.Pop();
            Flow.Report(App, log, then);
        }

        public override void Draw(Canvas c)
        {
            var ground = Sim.GroundNow(G);
            Art.Sky(c, 0, 0, Canvas.W, Horizon, 0, App.Time, 7, G.Sky);
            var ramp = Art.GroundRamp(ground);
            Art.Hills(c, 0, Canvas.W, Horizon, 18, 0.012f, 4, Pal.FarRamp, 0.2f, 0.7f);
            c.Gradient(0, Horizon, Canvas.W, ViewH - Horizon, ramp, 0.75f, 0.35f);
            // Tufts get bigger toward the viewer.
            for (int i = 0; i < 90; i++)
            {
                uint hsh = Sim.Hash("tuft" + i);
                float d = (hsh & 0xFFFF) / 65535f;
                int y = Horizon + 2 + (int)(d * d * (ViewH - Horizon - 4));
                int x = (int)((hsh >> 16) % Canvas.W);
                int h = 1 + (int)(d * 6);
                c.VLine(x, y - h, y, ramp[0]); c.VLine(x + 2, y - h + 1, y, ramp[0]);
            }
            // The animal, nearer = larger and lower.
            float near = 1f - (yards - 25f) / 270f;
            int scale = near > 0.7f ? 3 : near > 0.35f ? 2 : 1;
            int ay = Horizon + 6 + (int)(near * near * 60);
            int ax = Canvas.W / 2 + 40 + (int)(Math.Sin(T * 0.05f) * 6);
            if (killed) ay += 0;
            if (fled && !killed) ax += (int)((T - endT) * 260);
            float frame = fled && !killed ? T * 18 : (headUp ? 0 : (float)Math.Sin(T * 0.6f) * 0.3f);
            bool up = headUp || fled;
            if (killed) { c.Ellipse(ax, ay - 4 * scale, 9 * scale, 3 * scale, Kind == "buffalo" ? Pal.EarthDark : Pal.Earth); }
            else if (Kind == "buffalo")
            {
                Art.Buffalo(c, ax - 40 * scale, ay - 2, frame + 1, false, false, scale);
                Art.Buffalo(c, ax, ay, frame, false, up, scale);
                Art.Buffalo(c, ax + 34 * scale, ay - 1, frame + 2, true, false, scale);
            }
            else if (Kind == "deer") Art.Deer(c, ax, ay, frame, false, up, scale);
            else Art.Antelope(c, ax, ay, frame, false, up, scale);
            if (headUp && !Over) c.TextShadow(ax - 2, ay - 22 * scale - 8, "!", uneasy > 0.6f ? Pal.RedBright : Pal.Gold, 2);
            // The rifle barrel at the foot of the view.
            c.Fill(0, ViewH - 10, Canvas.W, 10, ramp[0]);
            c.Line(70, ViewH - 2, 150, ViewH - 30, Pal.Black); c.Line(71, ViewH - 2, 151, ViewH - 30, Pal.RockDark);
            c.Fill(40, ViewH - 6, 40, 6, Pal.WoodDark);
            if (muzzle > 0) c.Disc(152, ViewH - 31, 5, Pal.Sun);
            // Readout.
            c.Fill(0, ViewH, Canvas.W, Canvas.H - ViewH, Pal.Ink);
            c.HLine(0, Canvas.W, ViewH, Pal.Tan);
            Ui.Panel(c, 6, 4, Canvas.W - 12, 26);
            c.Text(14, 9, "Stalking " + (Kind == "buffalo" ? "buffalo" : "a " + (Kind == "deer" ? "mule deer" : "pronghorn antelope")) + ".  " + (int)yards + " yards.  Hit chance " + (int)(HitChance * 100) + "%", Pal.Cream);
            c.Text(14, 19, headUp ? "It's looking. Keep still!" : "Head down, grazing. Creep closer.", headUp ? Pal.Dusk : Pal.Green);
            c.TextRight(Canvas.W - 14, 9, "Shots " + (G.powder - shots), Pal.Gold);
            c.TextRight(326, 19, "WARY", Pal.Tan);
            Ui.Bar(c, 330, 19, 136, 8, uneasy, uneasy > 0.6f ? Pal.RedBright : Pal.GoldMid);
            if (msg.Length > 0) c.TextShadowCenter(ViewH + 6, msg, killed ? Pal.Green : Pal.Dusk);
            if (!Over)
            {
                creep.Draw(c, Creeping);
                quit.Draw(c, App.PointerDown && quit.Hit(App.PointerX, App.PointerY));
                var f = fire; f.Enabled = G.powder - shots > 0;
                f.Draw(c, App.PointerDown && fire.Hit(App.PointerX, App.PointerY));
            }
        }

        public override void Tap(int x, int y)
        {
            if (T < 0.3f) return;
            if (Over) { if (T - endT > 0.4f) Finish(); return; }
            if (fire.Hit(x, y)) Fire();
            else if (quit.Hit(x, y)) { msg = "You give up the stalk."; endT = T - 1.2f; S.Click(); }
        }

        public override void Key(char k)
        {
            if (Over) { if (T - endT > 0.4f) Finish(); return; }
            if (k == 'f' || k == Keys.Enter) Fire();
            else if (k == Keys.Escape) { msg = "You give up the stalk."; endT = T - 1.2f; }
        }
    }
}
