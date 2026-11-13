using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    /// <summary>
    /// The diggings: stake one of three claims, then work it a week at a time until the winter rains
    /// close the river bars (December 20) or the company sells up and goes home.
    /// </summary>
    public sealed class DiggingsScene : Scene
    {
        readonly bool arrived;
        Menu menu;
        readonly List<Btn> stake = new List<Btn>();
        const int ViewH = 120;

        public DiggingsScene(bool arrived = false) { this.arrived = arrived; }

        bool Choosing => G.claim < 0;

        public override void Enter()
        {
            G.Phase = Phase.Diggings;
            if (G.offers.Count == 0) { Sim.OfferClaims(G); App.SaveGame(); }
            Build();
            if (arrived)
            {
                int days = G.day;
                App.Say("You have reached the diggings after " + days + " days and " + G.miles + " miles. " + G.Count + " of the " + G.company.Count + " who left St. Joseph are here to see it."
                    + " The camp is tents and brush shanties along the river, and every man you meet has a story of a strike. Stake a claim and go to work: the rains close the river bars on December 20.", null, Tone.Good, "THE DIGGINGS");
            }
            if (!App.Synth.MusicPlaying) App.Synth.Music(Synth.Susanna, 0.18f, true);
        }

        void Build()
        {
            stake.Clear();
            if (Choosing)
            {
                for (int i = 0; i < 3; i++) stake.Add(new Btn(12 + i * 154, 238, 148, 20, "STAKE THIS CLAIM"));
                menu = null;
                return;
            }
            menu = new Menu(App) { X = 290, Y = ViewH + 12, W = 180, Pitch = 19, H = 16 };
            bool flooded = Sim.Flooded(G);
            menu.Add("Work the claim a week", Work, !flooded && Sim.Workers(G) > 0, flooded ? "flooded" : null);
            menu.Add("Rest a week", Rest);
            menu.Add("Prospect for a new claim", Prospect);
            menu.Add("Buy fresh vegetables", () => { App.Say(Sim.BuyFresh(G), Build, Tone.Info, "THE CAMP STORE"); App.SaveGame(); }, true, Ui.Dollars(600 * G.Count));
            if (G.HereStop.Prices > 0) menu.Add("Buy supplies", () => App.Push(new StoreScene(false)));
            menu.Add("Camp", () => App.Push(new CampMenuAtStop()), true, "company, journal");
            menu.Add("Sell up and go home", () => App.Ask("SELL UP?", "Sell the claim, weigh the dust at the assay office and go home with what you have: " + Ui.Dollars(Sim.TakeDollars(G) * 100) + ".", ("Go home", Payout), ("Keep digging", null)));
        }

        void Work()
        {
            if (Sim.Flooded(G)) { Report(Sim.WorkWeek(G, true, 0)); return; }
            App.Push(new PanScene(q => Report(Sim.WorkWeek(G, true, q))));
        }

        void Rest() => Report(Sim.WorkWeek(G, false, 0));

        void Prospect()
        {
            var w = Sim.Prospect(G);
            w.Notices.Insert(0, new Notice("A week walking the creeks with a pan. There are three claims worth staking: choose one.", Tone.Info));
            Report(w);
        }

        void Report(Sim.Week w)
        {
            App.SaveGame();
            var log = new List<Notice>();
            if (w.Ounces > 0) log.Add(new Notice("Week " + G.week + ": the company washed out " + w.Ounces.ToString("0.0") + " ounces of gold, worth " + Ui.Dollars((int)(w.Ounces * Sim.DollarsPerOunce * 100)) + ".", Tone.Good));
            if (w.FoodCents > 0) log.Add(new Notice("The food in the wagon ran out. Feeding the company at camp prices cost " + Ui.Dollars(w.FoodCents) + ".", Tone.Bad));
            log.AddRange(w.Notices);
            Flow.Report(App, log, () =>
            {
                if (Sim.SeasonOver(G))
                {
                    if (G.Count == 0) return;
                    App.Say("The winter rains have come. The river bars are under water and the diggings are closing for the season. Time to weigh your gold.", Payout, Tone.Info, "THE RAINS");
                }
                else Build();
            });
        }

        void Payout() { App.Synth.StopMusic(); App.Go(new PayoutScene()); }

        void Stake(int i)
        {
            Sim.TakeClaim(G, i);
            App.SaveGame();
            S.Coin();
            Build();
        }

        public override void Draw(Canvas c)
        {
            Art.Scene(c, "diggings", 0, 0, Canvas.W, ViewH, App.Time, 5, 0, G.Sky);
            c.Fill(0, ViewH, Canvas.W, Canvas.H - ViewH, Pal.Walnut);
            c.HLine(0, Canvas.W, ViewH, Pal.Tan);
            c.TextShadow(10, 8, G.HereStop.Name, Pal.Gold, 2);
            c.TextShadow(12, 28, Sim.DateLabel(G) + ", 1849.  " + Math.Max(0, Sim.SeasonEnd - G.Doy) / 7 + " weeks until the rains.", Pal.Cream);
            if (Choosing)
            {
                c.Text(12, ViewH + 6, G.Has(Trade.Miner) ? "Three claims to stake. " + G.With(Trade.Miner).name + " has tried a pan of each." : "Three claims to stake. Nobody can judge gravel: these are guesses.", Pal.Cream);
                for (int i = 0; i < 3; i++)
                {
                    int x = 12 + i * 154, y = ViewH + 20;
                    var cl = (Claim)i;
                    c.RoundRect(x, y, 148, 94, Pal.Leather, Pal.Tan);
                    c.Text(x + 6, y + 6, Sim.ClaimName(cl), Pal.Gold);
                    c.Paragraph(x + 6, y + 20, 136, Sim.ClaimNote(cl), Pal.Cream, 10);
                    c.Text(x + 6, y + 80, Sim.ProspectLabel(G, G.offers[i]), Pal.GoldLight);
                }
                foreach (var b in stake) b.Draw(c, App.PointerDown && b.Hit(App.PointerX, App.PointerY));
                return;
            }
            var cl2 = (Claim)G.claim;
            int ty = ViewH + 10;
            c.Text(10, ty, "Claim: " + Sim.ClaimName(cl2).ToLowerInvariant() + ", " + Sim.ProspectLabel(G, G.claimRich) + ".", Pal.Cream);
            c.Text(10, ty + 14, "Gold: " + G.gold.ToString("0.00") + " oz (" + Ui.Dollars((int)(G.gold * Sim.DollarsPerOunce * 100)) + ")", Pal.Gold);
            c.Text(10, ty + 28, "Hands " + Sim.Workers(G) + " of " + G.Count + ".  Tools " + Sim.Tools(G) + ", rockers " + G.rockers + ".", Pal.Cream);
            c.Text(10, ty + 42, "Food " + G.food + " lb   Cash " + Ui.Dollars(G.cents), Pal.Tan);
            if (G.lastPan >= 0) c.Text(10, ty + 56, "Last week's washing: " + (G.lastPan > 0.75f ? "careful, little lost" : G.lastPan > 0.45f ? "fair" : "rough, gold washed away"), Pal.Tan);
            if (Sim.Flooded(G)) c.Text(10, ty + 70, "The river is over the bar this week.", Pal.Dusk);
            int y2 = ty + 86;
            foreach (var h in G.company)
            {
                if (!h.Here) continue;
                c.Text(10, y2, h.name, Pal.Cream);
                c.Text(130, y2, h.ill != Ill.None ? Content.IllName(h.ill) : Sim.HealthLabel(h.health), h.ill != Ill.None || h.health < 50 ? Pal.Dusk : Pal.Green);
                y2 += 10;
                if (y2 > Canvas.H - 10) break;
            }
            menu.Draw(c);
        }

        public override void Tap(int x, int y)
        {
            if (T < 0.3f) return;
            if (Choosing) { for (int i = 0; i < stake.Count; i++) if (stake[i].Hit(x, y)) { Stake(i); return; } return; }
            menu.Tap(x, y);
        }

        public override void Key(char k)
        {
            if (Choosing) { if (k >= '1' && k <= '3') Stake(k - '1'); return; }
            menu.Key(k);
        }
    }

    /// <summary>
    /// Washing the week's best pan: swirl it in time. A marker swings across the pan's lip; tap as it
    /// crosses the gold mark. Five swirls; the closer each tap, the less gold washes away with the sand.
    /// </summary>
    public sealed class PanScene : Scene
    {
        readonly Action<float> done;
        float phase, score, endT = -1;
        int swirls;
        readonly List<float> taps = new List<float>();
        string word = "";
        float wordT;
        const int Swirls = 5;

        public PanScene(Action<float> done) { this.done = done; }
        public override bool Overlay => false;

        float Speed => 1.6f + swirls * 0.25f;
        float Marker => (float)Math.Sin(phase);   // -1..1

        public override void Tick(float dt)
        {
            base.Tick(dt);
            wordT -= dt;
            if (endT >= 0) { if (T - endT > 1.3f) Finish(); return; }
            if (T > 0.8f) phase += dt * Speed;
        }

        void Swirl()
        {
            if (endT >= 0) { if (T - endT > 0.3f) Finish(); return; }
            if (T < 0.8f) return;
            float off = Math.Abs(Marker);
            float q = Sim.Clamp(1f - off * 1.4f, 0, 1);
            taps.Add(q); score += q; swirls++;
            word = q > 0.8f ? "PERFECT" : q > 0.5f ? "GOOD" : q > 0.2f ? "SLOPPY" : "SPILLED";
            wordT = 0.8f;
            if (q > 0.5f) S.Pluck(880 + q * 400, 0.1f, 0.6f); else S.Splash();
            if (swirls >= Swirls) { endT = T; if (score / Swirls > 0.6f) S.Coin(); }
        }

        void Finish()
        {
            if (endT < -0.5f) return;
            endT = -2;
            App.Pop();
            done(Sim.Clamp(score / Swirls, 0, 1));
        }

        public override void Draw(Canvas c)
        {
            c.Gradient(0, 0, Canvas.W, Canvas.H, Pal.WaterRamp, 0.1f, 0.45f);
            for (int i = 0; i < 30; i++)
            {
                int y = (i * 37) % Canvas.H, x = (int)((i * 71 + App.Time * 30) % (Canvas.W + 40)) - 20;
                c.HLine(x, x + 14, y, Pal.WaterLight);
            }
            int cx = Canvas.W / 2, cy = 140;
            float tilt = Marker * 10;
            c.Ellipse(cx + (int)tilt, cy + 4, 104, 58, Pal.Black);
            c.Ellipse(cx + (int)tilt, cy, 102, 56, Pal.RockDark);
            c.Ellipse(cx + (int)tilt, cy, 92, 48, Pal.Rock);
            // Sand left in the pan thins as the swirls go on; the gold stays behind if washed well.
            int grit = 900 - swirls * 170;
            float spin = phase * 0.15f;
            for (int i = 0; i < grit; i++)
            {
                uint hsh = Sim.Hash("grit" + i);
                double a = (hsh & 0xFFFF) / 65535.0 * Math.PI * 2 + spin;
                float rr = (float)Math.Sqrt(((hsh >> 16) & 0xFFFF) / 65535.0) * 84;
                int px = cx + (int)tilt + (int)(Math.Cos(a) * rr), py = cy + (int)(Math.Sin(a) * rr * 0.5f);
                c.Set(px, py, i % 5 == 0 ? Pal.SandLight : i % 2 == 0 ? Pal.Sand : Pal.Earth);
                if (i % 9 == 0) c.Set(px + 1, py, Pal.RockDark);
            }
            int flakes = (int)(score * 5);
            for (int i = 0; i < flakes; i++)
            {
                int px = cx + (int)tilt + (int)(Math.Cos(i * 1.7) * (10 + i % 5 * 4)), py = cy + 8 + (int)(Math.Sin(i * 1.3) * 6);
                c.Fill(px, py, 2, 1, Pal.GoldLight); c.Set(px, py + 1, Pal.Gold);
            }
            // The swing bar.
            int bx = 90, bw = Canvas.W - 180, by = 222;
            c.Fill(bx - 2, by - 2, bw + 4, 14, Pal.Ink);
            c.Fill(bx, by, bw, 10, Pal.Walnut);
            c.Fill(cx - 14, by, 28, 10, Pal.GoldMid);
            c.Fill(cx - 5, by, 10, 10, Pal.GoldLight);
            int mx = cx + (int)(Marker * bw / 2);
            c.Fill(mx - 1, by - 4, 3, 18, Pal.Cream);
            Ui.Panel(c, 6, 4, Canvas.W - 12, 26);
            c.Text(14, 9, "Washing the week's best pan.  Swirl " + Math.Min(swirls + 1, Swirls) + " of " + Swirls, Pal.Cream);
            c.Text(14, 19, (App.Touch ? "Tap" : "Press space") + " as the marker crosses the gold.", Pal.Tan);
            for (int i = 0; i < taps.Count; i++) c.Fill(Canvas.W - 80 + i * 12, 10, 9, 9, taps[i] > 0.8f ? Pal.Green : taps[i] > 0.5f ? Pal.GoldMid : Pal.RedBright);
            if (wordT > 0) c.TextShadowCenter(60, word, word == "PERFECT" ? Pal.GoldLight : word == "GOOD" ? Pal.Green : Pal.Dusk, 2);
            if (endT >= 0) c.TextShadowCenter(60, score / Swirls > 0.75f ? "A clean pan. Bright colour in the bottom." : score / Swirls > 0.45f ? "Some colour in the bottom." : "Most of the fine gold washed out.", Pal.Cream);
        }

        public override void Tap(int x, int y) => Swirl();
        public override void Key(char k) { if (k == Keys.Space || k == Keys.Enter) Swirl(); }
    }

    /// <summary>The assay office: gold weighed, debts paid, the company's take; then the ledger.</summary>
    public sealed class PayoutScene : Scene
    {
        List<Sim.Line> lines;
        int dollars, mark = -1;
        bool saved;

        public override void Enter()
        {
            lines = Sim.Payout(G);
            dollars = Sim.TakeDollars(G);
            G.Phase = Phase.Done;
            Sim.Log(G, "Sold up. The company's take: $" + dollars + ".");
            S.Fanfare();
            Save();
        }

        void Save()
        {
            if (saved) return;
            saved = true;
            string who = G.company.Count > 0 ? G.company[0].name : "A company";
            string route = G.visited.Contains("lassenrancho") ? "Lassen's Cutoff" : "Carson route";
            string note = G.gold.ToString("0.0") + " oz, " + G.Count + " of " + G.company.Count + " through, " + route;
            if (App.Ledger.Makes(dollars)) { mark = App.Ledger.Add(who, dollars, note); App.SaveLedger(); }
            App.ClearSave();
        }

        public override void Draw(Canvas c)
        {
            c.Fill(0, 0, Canvas.W, Canvas.H, Pal.SandLight);
            c.Rect(8, 8, Canvas.W - 16, Canvas.H - 16, Pal.WoodDark);
            c.TextCenter(18, "ASSAY OFFICE", Pal.WoodDark, 2);
            c.TextCenter(38, "Gold weighed and paid at " + "$" + Sim.DollarsPerOunce + " the ounce. " + Sim.DateLabel(G) + ", 1849.", Pal.Earth);
            int y = 62;
            foreach (var l in lines)
            {
                c.Text(40, y, l.What, Pal.Black);
                c.TextRight(Canvas.W - 40, y, Ui.Dollars(l.Cents), Pal.Black);
                y += 16;
            }
            c.HLine(40, Canvas.W - 40, y, Pal.WoodDark);
            c.Text(40, y + 6, "The company's take", Pal.Black);
            c.TextRight(Canvas.W - 40, y + 6, "$" + dollars.ToString("N0"), Pal.RedDark);
            c.TextCenter(y + 30, Sim.Rank(dollars).ToUpperInvariant(), Pal.WoodDark, 2);
            int lost = G.company.Count - G.Count;
            string epitaph = lost == 0 ? "Every soul who set out from St. Joseph is here to share it." : lost == 1 ? "One of the company did not live, or did not stay, to share it." : lost + " of the company did not live, or did not stay, to share it.";
            c.TextCenter(y + 54, epitaph, Pal.Earth);
            if (mark >= 0) c.TextCenter(y + 68, "Your company is entered in the Assay Ledger at number " + (mark + 1) + ".", Pal.Black);
            if (T > 1 && Blink) c.TextCenter(Canvas.H - 22, App.Touch ? "tap to see the ledger" : "press space", Pal.Earth);
        }

        public override void Tap(int x, int y) { if (T > 1) Next(); }
        public override void Key(char k) { if (T > 1) Next(); }
        void Next() { S.Click(); App.Game = null; App.Go(new LedgerScene(mark)); }
    }

    /// <summary>The whole company is dead or gone.</summary>
    public sealed class LostScene : Scene
    {
        string where, when;
        List<string> fates = new List<string>();

        public override void Enter()
        {
            if (G != null)
            {
                where = G.HereStop.Name.Split(',')[0];
                when = Sim.DateLabel(G);
                foreach (var h in G.company) fates.Add(h.name + ": " + (h.fate ?? (h.alive ? "turned back" : "died")));
            }
            App.ClearSave();
            S.Death();
        }

        public override void Draw(Canvas c)
        {
            Art.Scene(c, "sink", 0, 0, Canvas.W, 100, App.Time, 9, 1);
            c.Fill(0, 100, Canvas.W, Canvas.H - 100, Pal.Ink);
            c.HLine(0, Canvas.W, 100, Pal.Tan);
            c.TextShadowCenter(14, "THE COMPANY IS NO MORE", Pal.Cream, 2);
            if (where != null) c.TextCenter(108, "Somewhere past " + where + ", " + when + ", 1849.", Pal.Tan);
            for (int i = 0; i < fates.Count; i++) c.TextCenter(126 + i * 13, fates[i], Pal.Cream);
            c.TextCenter(Canvas.H - 40, "Some ninety thousand went west in 1849. Not all of them saw California.", Pal.Tan);
            if (T > 1.5f) Ui.Prompt(c, App.Touch ? "tap to return" : "press space", Blink);
        }

        public override void Tap(int x, int y) { if (T > 1.5f) Done(); }
        public override void Key(char k) { if (T > 1.5f) Done(); }
        void Done() { App.Game = null; App.Synth.StopMusic(); App.Go(new TitleScene()); }
    }
}
