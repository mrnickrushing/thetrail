using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    /// <summary>
    /// The outfitter's (or a fort's) counter: + and - for every good, with the weight the wagon can
    /// carry and the cash left. Taking back something bought this visit refunds it in full; selling
    /// what you came in with gets a third of the price.
    /// </summary>
    public sealed class StoreScene : Scene
    {
        readonly bool outfit;
        readonly Good[] goods;
        readonly Dictionary<Good, int> start = new Dictionary<Good, int>();
        int sel;
        float held; int heldBtn = -1;
        readonly List<(Btn b, int good, int dir)> steps = new List<(Btn, int, int)>();
        Btn done;
        const int Y0 = 30, Pitch = 22;

        public StoreScene(bool outfit)
        {
            this.outfit = outfit;
            goods = (Good[])Enum.GetValues(typeof(Good));
        }

        public override void Enter()
        {
            foreach (var k in goods) start[k] = Content.Have(G, k);
            steps.Clear();
            for (int i = 0; i < goods.Length; i++)
            {
                int y = Y0 + i * Pitch;
                steps.Add((new Btn(338, y, 28, 18, "-"), i, -1));
                steps.Add((new Btn(438, y, 28, 18, "+"), i, 1));
            }
            done = new Btn(Canvas.W - 120, Canvas.H - 26, 110, 20, outfit ? "SET OUT" : "DONE");
            if (outfit && !App.Synth.MusicPlaying) App.Synth.Music(Synth.Susanna, 0.2f, true);
        }

        int Price(Good k) => Sim.PriceCents(G, k);

        void Step(int i, int dir)
        {
            var k = goods[i];
            int n = Content.Step(k), have = Content.Have(G, k);
            if (dir > 0)
            {
                int add = Math.Min(n, Content.MaxOf(k) - have);
                if (add <= 0) { S.Bad(); return; }
                int cost = Price(k) * add / n;
                if (cost > G.cents) { S.Bad(); return; }
                G.cents -= cost; Content.Add(G, k, add);
                if (k == Good.Kegs && G.HereStop.Water) G.water += add * Content.KegGallons;
                S.Coin();
            }
            else
            {
                int take = Math.Min(n, have);
                if (take <= 0) { S.Bad(); return; }
                int bought = have - start[k];
                int refund = 0;
                for (int u = 0; u < take; u++) refund += (bought - u > 0 ? Price(k) : Sim.SellCents(G, k)) / n;
                G.cents += refund; Content.Add(G, k, -take);
                S.Click();
            }
            sel = i;
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            // Holding + or - repeats.
            if (App.PointerDown && heldBtn >= 0 && steps[heldBtn].b.Hit(App.PointerX, App.PointerY))
            {
                held += dt;
                if (held > 0.45f) { held -= 0.09f; Step(steps[heldBtn].good, steps[heldBtn].dir); }
            }
            else { heldBtn = -1; held = 0; }
        }

        public override void Draw(Canvas c)
        {
            c.Fill(0, 0, Canvas.W, Canvas.H, Pal.Ink);
            Ui.Title(c, outfit ? "OUTFITTING AT ST. JOSEPH" : "BUYING AT " + G.HereStop.Name.Split(',')[0].ToUpperInvariant());
            c.Text(12, 21, "GOODS", Pal.Tan); c.TextRight(250, 21, "HAVE", Pal.Tan); c.TextRight(326, 21, "PRICE", Pal.Tan); c.TextCenterIn(366, 72, 21, "BUY / SELL", Pal.Tan);
            for (int i = 0; i < goods.Length; i++)
            {
                var k = goods[i];
                int y = Y0 + i * Pitch;
                if (i == sel) c.Fill(4, y - 2, Canvas.W - 8, Pitch - 1, Pal.Walnut);
                c.Text(12, y + 5, Content.GoodName(k, G.Team), Pal.Cream);
                string unit = Content.GoodUnit(k);
                c.TextRight(250, y + 5, Content.Have(G, k) + (unit.Length > 0 ? " " + unit : ""), Pal.Gold);
                int n = Content.Step(k);
                c.TextRight(326, y + 5, Ui.Dollars(Price(k)) + (n > 1 ? "/" + n : ""), Pal.Cream);
                int delta = Content.Have(G, k) - start[k];
                c.TextCenterIn(366, 72, y + 5, delta == 0 ? "" : (delta > 0 ? "+" : "") + delta, delta > 0 ? Pal.Green : Pal.Dusk);
            }
            foreach (var (b, gi, dir) in steps)
            {
                bool can = dir < 0 ? Content.Have(G, goods[gi]) > 0 : Content.Have(G, goods[gi]) < Content.MaxOf(goods[gi]);
                var bb = b; bb.Enabled = can;
                bb.Draw(c, App.PointerDown && b.Hit(App.PointerX, App.PointerY));
            }
            int fy = Y0 + goods.Length * Pitch + 2;
            c.HLine(0, Canvas.W, fy - 2, Pal.Leather);
            c.Text(12, fy + 2, Content.GoodNote(goods[sel], G.Team), Pal.Tan);
            c.Text(12, Canvas.H - 22, "Cash " + Ui.Money(G.cents), Pal.Gold);
            int load = Sim.Load(G), cap = Math.Max(1, Sim.Capacity(G));
            if (Sim.Capacity(G) <= 0) Ui.Bar(c, 120, Canvas.H - 25, 200, 14, 0, Pal.Tan, "Load " + load + " lb, no team yet");
            else Ui.Bar(c, 120, Canvas.H - 25, 200, 14, load / (float)cap, load > cap ? Pal.RedBright : Pal.GoldMid, "Load " + load + " / " + cap + " lb");
            done.Draw(c, App.PointerDown && done.Hit(App.PointerX, App.PointerY));
        }

        public override void Tap(int x, int y)
        {
            for (int i = 0; i < steps.Count; i++)
                if (steps[i].b.Hit(x, y)) { Step(steps[i].good, steps[i].dir); heldBtn = i; held = 0; return; }
            if (done.Hit(x, y)) { Finish(); return; }
            int row = (y - Y0 + 2) / Pitch;
            if (row >= 0 && row < goods.Length && y >= Y0 - 2) { sel = row; S.Click(); }
        }

        public override void Key(char k)
        {
            if (k == Keys.Up) sel = (sel + goods.Length - 1) % goods.Length;
            else if (k == Keys.Down) sel = (sel + 1) % goods.Length;
            else if (k == Keys.Right || k == '+' || k == '=') Step(sel, 1);
            else if (k == Keys.Left || k == '-') Step(sel, -1);
            else if (k == Keys.Enter || k == Keys.Escape) Finish();
        }

        void Finish()
        {
            if (!outfit) { S.Select(); App.SaveGame(); App.Pop(); return; }
            string why = Sim.CannotLeave(G);
            if (why != null) { App.Say(why, null, Tone.Bad, "NOT READY"); return; }
            var warn = new List<string>();
            if (G.kegs == 0) warn.Add("no water kegs (there are dry stretches ahead)");
            if (G.pans + G.picks == 0) warn.Add("no mining tools (they cost far more in California)");
            if (G.kits == 0) warn.Add("no repair kits");
            if (Sim.Load(G) > Sim.Capacity(G)) warn.Add("a wagon heavier than the team can pull");
            if (warn.Count > 0)
            {
                App.Ask("BEFORE YOU GO", "You have " + Sim.Join(warn) + ". Set out anyway?", ("Set out", SetOut), ("Keep shopping", null));
                return;
            }
            SetOut();
        }

        void SetOut()
        {
            Sim.FillKegs(G);
            Sim.Depart(G);
            Sim.Log(G, "Left St. Joseph with " + G.animals + " " + (G.Team == TeamKind.Mules ? "mules" : "oxen") + " and " + G.food + " lb of food.");
            App.SaveGame();
            S.Fanfare();
            App.Go(new RoadScene());
        }
    }
}
