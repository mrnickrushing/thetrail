using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    /// <summary>Moves the player between screens as the journey's phase changes.</summary>
    public static class Flow
    {
        public static void Resume(App app)
        {
            var g = app.Game;
            switch (g.Phase)
            {
                case Phase.Hire: app.Go(new HireScene()); break;
                case Phase.Outfit: app.Go(new StoreScene(true)); break;
                case Phase.Diggings: app.Go(new DiggingsScene()); break;
                case Phase.Stop:
                case Phase.Crossing:
                case Phase.Haul: app.Go(new StopScene(false)); break;
                default: app.Go(new RoadScene()); break;
            }
        }

        /// <summary>The wagon has reached the next stop.</summary>
        public static void Arrive(App app)
        {
            var g = app.Game;
            var s = Sim.Arrive(g);
            app.SaveGame();
            if (s.End) { app.Synth.Fanfare(); app.Go(new DiggingsScene(true)); return; }
            app.Synth.Arrive();
            app.Go(new StopScene(true));
        }

        /// <summary>Shows what happened, one card at a time, then the lost screen if the company is gone, else goes on.</summary>
        public static void Report(App app, List<Notice> log, Action then, bool all = false)
        {
            var show = new List<Notice>();
            foreach (var n in log) if (n.Stop || all) show.Add(n);
            app.SayAll(show, () =>
            {
                if (app.Game.Count == 0) { app.Game.Phase = Phase.Lost; app.ClearSave(); app.Go(new LostScene()); }
                else then?.Invoke();
            });
        }

        /// <summary>Asks one of the road's decisions and reports the outcome.</summary>
        public static void Decide(App app, EventDef e, Action then)
        {
            var g = app.Game;
            var answers = new List<(string, Action, bool)>();
            foreach (var ch in e.Choices(g))
            {
                var choice = ch;
                answers.Add((choice.Label, () =>
                {
                    if (choice.Hunt != null) { app.Push(new StalkScene(choice.Hunt, then)); return; }
                    string outcome = choice.Do(g);
                    g.morale = Sim.Clamp(g.morale, 0, 100);
                    g.strength = Sim.Clamp(g.strength, 0, 100);
                    g.wagon = Sim.Clamp(g.wagon, 0, 100);
                    g.food = Math.Max(0, g.food); g.powder = Math.Max(0, g.powder); g.kits = Math.Max(0, g.kits);
                    g.medicine = Math.Max(0, g.medicine); g.animals = Math.Max(0, g.animals);
                    foreach (var h in g.company) h.health = Sim.Clamp(h.health, 0, 100);
                    if (!string.IsNullOrEmpty(outcome)) Sim.Log(g, e.Title + ": " + outcome);
                    app.SaveGame();
                    if (string.IsNullOrEmpty(outcome)) then?.Invoke();
                    else app.Say(outcome, then, Tone.Info, e.Title.ToUpperInvariant());
                }, choice.Enabled));
            }
            app.Synth.Arrive();
            app.Push(new AskScene(app, e.Title.ToUpperInvariant(), e.Text(g), answers));
        }
    }

    /// <summary>The wagon rolling west a day at a time, with the company's state below. Tap to make camp.</summary>
    public sealed class RoadScene : Scene
    {
        float dayTimer, scroll, shown;
        bool paused;
        Btn camp;
        const int ViewH = 168;

        public override void Enter()
        {
            G.Phase = Phase.Road;
            scroll = G.miles * 8;
            camp = new Btn(Canvas.W - 96, ViewH + 8, 86, 20, "MAKE CAMP");
            if (!App.Synth.MusicPlaying) App.Synth.Music(Synth.Susanna, 0.16f, true);
        }

        bool Moving => !paused && App.Top == this && G.stuckDays == 0 && G.animals > 0;

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (App.Top != this || paused) return;
            float speed = App.Settings.fast ? 0.45f : 1f;
            if (G.stuckDays == 0) scroll += dt * Sim.MilesToday(G) * 8 / speed;
            dayTimer += dt;
            if (dayTimer < speed) return;
            dayTimer = 0;
            var res = Sim.Day(G, true);
            App.SaveGame();
            var then = (Action)(() =>
            {
                if (res.Arrived) { Flow.Arrive(App); return; }
                if (res.Event != null) Flow.Decide(App, res.Event, () => App.SaveGame());
            });
            bool any = false;
            foreach (var n in res.Notices) if (n.Stop) any = true;
            if (any) Flow.Report(App, res.Notices, then);
            else then();
        }

        public override void Draw(Canvas c)
        {
            var leg = Sim.CurrentLeg(G);
            DrawView(c, G, leg, scroll, App.Time, Moving, 0, ViewH);
            DrawDash(c);
        }

        /// <summary>The road picture: sky, ranges and ground for the country, the next stop coming into view, the outfit.</summary>
        public static void DrawView(Canvas c, GameState g, Leg leg, float scroll, float time, bool moving, int y, int h)
        {
            var ground = leg.Ground;
            int hy = y + (int)(h * 0.52f);
            bool dry = Sim.OnDryStretch(g);
            int tod = dry && g.hours >= 14 ? 1 : 0;
            Art.Sky(c, 0, y, Canvas.W, hy - y + 2, tod, time, 7 + g.visited.Count, g.Sky);
            int seed = (int)Sim.Hash(leg.To) & 0xFFF;
            switch (ground)
            {
                case Ground.Mountains:
                case Ground.Forest:
                    Art.Range(c, 0, Canvas.W, hy + 4, 40, 0.012f, seed, Pal.Far, Pal.FarLight, scroll * 0.05f, hy - 22);
                    Art.Hills(c, 0, Canvas.W, hy + 10, 22, 0.01f, seed + 1, Pal.PineRamp, 0.1f, 0.7f, scroll * 0.2f);
                    break;
                case Ground.Desert:
                case Ground.Sage:
                    Art.Range(c, 0, Canvas.W, hy + 4, 26, 0.01f, seed, Pal.Far, Pal.FarLight, scroll * 0.05f, -999, 0.4f);
                    Art.Hills(c, 0, Canvas.W, hy + 8, 14, 0.008f, seed + 1, Pal.RedRockRamp, 0.3f, 0.8f, scroll * 0.2f);
                    break;
                case Ground.Hills:
                    Art.Hills(c, 0, Canvas.W, hy + 4, 22, 0.008f, seed, Pal.FarRamp, 0.1f, 0.5f, scroll * 0.06f);
                    Art.Hills(c, 0, Canvas.W, hy + 10, 18, 0.01f, seed + 1, Pal.SageRamp, 0.2f, 0.7f, scroll * 0.2f);
                    break;
                default:
                    Art.Hills(c, 0, Canvas.W, hy + 4, 10, 0.006f, seed, Pal.FarRamp, 0.2f, 0.6f, scroll * 0.05f);
                    break;
            }
            // The next stop rising over the last miles of the leg.
            int left = g.legMiles - g.legDone;
            if (left < 30 && g.next != null)
            {
                float p = 1f - left / 30f;
                int sx = Canvas.W + 40 - (int)(p * 200);
                Landmark(c, Route.Get(g.next), sx, hy + 10, time);
            }
            Art.GroundPlane(c, 0, hy + 8, Canvas.W, y + h - hy - 8, ground, scroll, seed);
            int ry = y + h - 26;
            Art.Road(c, 0, ry, Canvas.W, 8, ground);
            Art.Outfit(c, 150, ry + 6, g.animals, g.Team, time, moving);
            // Walkers behind the wagon.
            int n = 0;
            for (int i = 1; i < g.company.Count && n < 3; i++)
                if (g.company[i].Here) { Art.Walker(c, 136 - n * 13, ry + 6 + (n % 2), moving ? time * 6 + i : 0, i % 3 == 0 ? Pal.Red : i % 3 == 1 ? Pal.Earth : Pal.Blue, i % 2 == 0 ? 1 : 0); n++; }
            if (g.Sky == Sky.Rain || g.Sky == Sky.Storm || g.Sky == Sky.Snow)
            {
                bool snow = g.Sky == Sky.Snow;
                for (int i = 0; i < 160; i++)
                {
                    float fx = (Art.Noise(i * 3.7f, 11) * Canvas.W + time * (snow ? 15 : 60)) % Canvas.W;
                    float fy = (Art.Noise(i * 1.3f, 12) * h + time * (snow ? 30 : 220)) % h;
                    if (snow) c.Set((int)fx, y + (int)fy, Pal.Snow);
                    else c.Line((int)fx, y + (int)fy, (int)fx - 1, y + (int)fy + 3, Pal.SkyPale);
                }
            }
        }

        /// <summary>A small version of a stop on the horizon.</summary>
        static void Landmark(Canvas c, Stop s, int x, int baseY, float time)
        {
            switch (s.Kind)
            {
                case StopKind.Fort:
                    c.Fill(x, baseY - 12, 60, 12, Pal.SandLight); c.Fill(x - 4, baseY - 18, 10, 18, Pal.Sand); c.Fill(x + 54, baseY - 18, 10, 18, Pal.Sand);
                    Art.Flag(c, x + 30, baseY - 36, time);
                    break;
                case StopKind.Town:
                case StopKind.Diggings:
                    for (int i = 0; i < 5; i++) c.Fill(x + i * 14, baseY - 10 - (i % 2) * 6, 12, 10 + (i % 2) * 6, i % 2 == 0 ? Pal.Wood : Pal.SandLight);
                    break;
                case StopKind.River:
                    for (int i = 0; i < 5; i++) Art.Cottonwood(c, x + i * 16, baseY, 16, i);
                    break;
                default:
                    if (s.Art == "courthouse" || s.Art == "gate" || s.Art == "rocks" || s.Art == "blackrock")
                    {
                        byte col = s.Art == "blackrock" ? Pal.Black : s.Art == "courthouse" ? Pal.Sand : Pal.RockLight;
                        c.Poly(new List<(int, int)> { (x, baseY), (x + 10, baseY - 24), (x + 50, baseY - 26), (x + 64, baseY) }, col);
                    }
                    else if (s.Art == "summit" || s.Art == "pass") Art.Range(c, x, 90, baseY, 30, 0.04f, 5, Pal.FarLight, Pal.SnowShade);
                    else for (int i = 0; i < 4; i++) Art.Pine(c, x + i * 14, baseY, 16);
                    break;
            }
        }

        void DrawDash(Canvas c)
        {
            int y = ViewH;
            c.Fill(0, y, Canvas.W, Canvas.H - y, Pal.Walnut);
            c.HLine(0, Canvas.W, y, Pal.Tan);
            c.HLine(0, Canvas.W, y + 1, Pal.Leather);
            // Where and when.
            c.Text(10, y + 8, Sim.DateLabel(G) + ", 1849", Pal.Gold);
            c.Text(10, y + 20, Sim.SkyName(G.Sky) + ", " + G.temp + " F", Pal.Cream);
            string to = G.next != null ? Route.Get(G.next).Name : "";
            c.Text(10, y + 32, (G.legMiles - G.legDone) + " mi to " + Short(to), Pal.Cream);
            c.Text(10, y + 44, G.miles + " miles from St. Joseph", Pal.Tan);
            if (G.stuckDays > 0) c.Text(10, y + 56, "Stopped: " + G.stuckDays + (G.stuckDays == 1 ? " day" : " days"), Pal.Dusk);
            else if (Sim.OnDryStretch(G)) c.Text(10, y + 56, "Dry stretch: " + (Sim.CurrentLeg(G).Dry - G.legDone) + " mi", Pal.Dusk);
            else c.Text(10, y + 56, "Grass: " + Sim.GrassLabel(Sim.Grass(G)), Pal.Tan);

            // Stores and the team.
            int bx = 172, bw = 120;
            int eat = Math.Max(1, G.portion * G.Count);
            int days = G.food / eat;
            Meter(c, bx, y + 8, bw, "Food " + days + " days", Math.Min(1f, days / 40f));
            if (G.kegs > 0 && G.water == 0 && !Sim.OnDryStretch(G)) Ui.Bar(c, bx, y + 22, bw, 12, 0, Pal.Tan, G.kegs + (G.kegs == 1 ? " keg" : " kegs") + ", empty");
            else if (G.kegs > 0) Meter(c, bx, y + 22, bw, "Water " + G.water + " gal", G.water / (float)(G.kegs * Content.KegGallons));
            else Meter(c, bx, y + 22, bw, "No water kegs", 0);
            Meter(c, bx, y + 36, bw, (G.Team == TeamKind.Mules ? "Mules " : "Oxen ") + G.animals + ", " + Sim.StrengthLabel(G.strength), G.strength / 100f);
            Meter(c, bx, y + 50, bw, "Wagon " + (int)G.wagon + "%", G.wagon / 100f);
            Meter(c, bx, y + 64, bw, "Spirits " + Sim.MoraleLabel(G.morale), G.morale / 100f);

            // The company.
            int px = 304;
            int row = 0;
            foreach (var h in G.company)
            {
                if (!h.Here || row >= 5) continue;
                int ry = y + 30 + row * 12;
                string nm = h.name.Length > 13 ? h.name.Substring(0, 12) + "." : h.name;
                c.Text(px, ry, nm, h.ill != Ill.None ? Pal.Dusk : Pal.Cream);
                Ui.Bar(c, px + 100, ry, 60, 8, h.health / 100f, Ui.LevelColour(h.health / 100f));
                row++;
            }
            camp.Draw(c, App.PointerDown && camp.Hit(App.PointerX, App.PointerY));
            if (G.journal.Count > 0)
            {
                string last = G.journal[G.journal.Count - 1];
                if (Canvas.TextWidth(last) > Canvas.W - 20) last = Canvas.Wrap(last, Canvas.W - 30)[0] + "...";
                c.Text(10, Canvas.H - 12, last, Pal.Tan);
            }
        }

        static string Short(string s)
        {
            int cut = s.IndexOfAny(new[] { ',', '(' });
            if (cut > 0) s = s.Substring(0, cut).TrimEnd();
            return Canvas.TextWidth(s) > 120 ? s.Substring(0, 16) + "." : s;
        }

        static void Meter(Canvas c, int x, int y, int w, string label, float f) => Ui.Bar(c, x, y, w, 12, f, Ui.LevelColour(f), label);

        public override void Tap(int x, int y) { if (T > 0.3f) OpenCamp(); }
        public override void Key(char k) { if (k == Keys.Space || k == Keys.Enter || k == Keys.Escape || k == 'c') OpenCamp(); }

        void OpenCamp()
        {
            S.Click();
            App.Push(new CampScene());
        }
    }

    /// <summary>The camp menu: orders, the company, supplies, the map, hunting, rest, repairs.</summary>
    public sealed class CampScene : Scene
    {
        Menu menu;
        public override bool Overlay => true;

        public override void Enter() => Build();

        void Build()
        {
            menu = new Menu(App) { X = Canvas.W - 214, Y = 34, W = 200, Pitch = 20, H = 17 };
            menu.Add("Back on the road", () => App.Pop());
            menu.Add("Orders", () => App.Push(new OrdersScene()), true, G.hours + " hrs, " + G.portion + " lb");
            menu.Add("The company", () => App.Push(new StatusScene(0)));
            menu.Add("Supplies", () => App.Push(new StatusScene(1)));
            menu.Add("Map", () => App.Push(new MapScene()));
            menu.Add("Journal", () => App.Push(new JournalScene()));
            bool dry = Sim.OnDryStretch(G);
            menu.Add("Rest a day", Rest);
            menu.Add("Go hunting", () => App.Push(new StalkScene(null, null)), G.powder >= 5 && !dry, G.powder + " shots");
            menu.Add("Mend the wagon", Mend, G.kits > 0 && G.wagon < 90, G.kits + " kits");
            menu.Add("Save and quit", () => { App.SaveGame(); App.Synth.StopMusic(); App.Go(new TitleScene()); });
        }

        void Rest()
        {
            var res = Sim.Rest(G);
            App.SaveGame();
            var log = new List<Notice> { new Notice("The company rests for a day. The animals graze and everyone mends their clothes.", Tone.Good) };
            log.AddRange(res.Notices);
            Flow.Report(App, log, () => Build(), false);
        }

        void Mend()
        {
            string s = Sim.Repair(G);
            App.SaveGame();
            App.Say(s, () => Build(), Tone.Good, "REPAIRS");
        }

        public override void Draw(Canvas c)
        {
            c.Shade(0, 0, Canvas.W, Canvas.H, 0.4f);
            Ui.Panel(c, Canvas.W - 226, 8, 220, menu.Height + 36, "CAMP");
            menu.Draw(c);
        }

        public override void Tap(int x, int y) { if (T > 0.2f && !menu.Tap(x, y) && x < Canvas.W - 230) App.Pop(); }
        public override void Key(char k) { if (k == Keys.Escape) App.Pop(); else menu.Key(k); }
    }

    /// <summary>Hours on the road and the food ration.</summary>
    public sealed class OrdersScene : Scene
    {
        readonly List<(Btn b, Action a)> buttons = new List<(Btn, Action)>();
        public override bool Overlay => true;

        public override void Enter()
        {
            buttons.Clear();
            int[] hrs = { 8, 10, 12, 14 };
            for (int i = 0; i < 4; i++) { int h = hrs[i]; buttons.Add((new Btn(70 + i * 88, 82, 80, 22, h + " hours"), () => { G.hours = h; S.Select(); })); }
            for (int i = 1; i <= 3; i++) { int p = i; buttons.Add((new Btn(70 + (i - 1) * 117, 168, 110, 22, p + " lb a day"), () => { G.portion = p; S.Select(); })); }
            buttons.Add((new Btn(Canvas.W / 2 - 50, 228, 100, 20, "DONE"), () => { App.SaveGame(); App.Pop(); }));
        }

        public override void Draw(Canvas c)
        {
            c.Shade(0, 0, Canvas.W, Canvas.H, 0.5f);
            Ui.Panel(c, 40, 16, Canvas.W - 80, 242, "ORDERS");
            c.TextCenter(46, "Hours on the road each day", Pal.Gold);
            c.TextCenter(60, "Longer days cover more ground but wear out the team.", Pal.Tan);
            for (int i = 0; i < 4; i++) buttons[i].b.Draw(c, G.hours == new[] { 8, 10, 12, 14 }[i]);
            c.TextCenter(110, "About " + Sim.MilesToday(G) + " miles a day at present.", Pal.Cream);
            c.TextCenter(132, "Food for each person each day", Pal.Gold);
            c.TextCenter(146, "Short rations save food but weaken everyone.", Pal.Tan);
            for (int i = 0; i < 3; i++) buttons[4 + i].b.Draw(c, G.portion == i + 1);
            int eat = Math.Max(1, (int)Math.Ceiling(G.portion * G.Count * (G.Has(Trade.Cook) ? 0.85f : 1f)));
            c.TextCenter(198, G.food + " lb of food lasts about " + (G.food / eat) + " days.", Pal.Cream);
            if (Sim.OnDryStretch(G)) c.TextCenter(210, "14 hours on a dry stretch means driving through the night.", Pal.Dusk);
            buttons[buttons.Count - 1].b.Draw(c);
        }

        public override void Tap(int x, int y) { foreach (var (b, a) in buttons) if (b.Hit(x, y)) { a(); return; } }
        public override void Key(char k)
        {
            if (k >= '1' && k <= '4') G.hours = 6 + (k - '0') * 2;
            else if (k == Keys.Escape || k == Keys.Enter) { App.SaveGame(); App.Pop(); }
        }
    }

    /// <summary>The company (tab 0) and the supplies (tab 1).</summary>
    public sealed class StatusScene : Scene
    {
        int tab;
        public override bool Overlay => true;
        public StatusScene(int tab) { this.tab = tab; }
        Btn A => new Btn(60, 22, 120, 18, "THE COMPANY");
        Btn B => new Btn(186, 22, 120, 18, "SUPPLIES");
        Btn Close => new Btn(Canvas.W - 120, 22, 80, 18, "CLOSE");

        public override void Draw(Canvas c)
        {
            c.Shade(0, 0, Canvas.W, Canvas.H, 0.5f);
            Ui.Panel(c, 30, 10, Canvas.W - 60, Canvas.H - 20);
            A.Draw(c, tab == 0); B.Draw(c, tab == 1); Close.Draw(c);
            if (tab == 0) Company(c); else Supplies(c);
        }

        void Company(Canvas c)
        {
            int y = 50;
            foreach (var h in G.company)
            {
                byte col = h.Here ? Pal.Cream : Pal.Leather;
                c.Text(48, y, h.name + (G.company.IndexOf(h) == 0 ? " (captain)" : ""), h.Here ? Pal.Gold : Pal.Tan);
                c.Text(48, y + 10, Content.TradeName(h.trade) + ", " + Content.TemperName(h.temper) + ", from " + h.home, col);
                if (h.Here)
                {
                    Ui.Bar(c, 300, y, 100, 9, h.health / 100f, Ui.LevelColour(h.health / 100f), Sim.HealthLabel(h.health));
                    c.Text(300, y + 11, h.ill != Ill.None ? "Has " + Content.IllName(h.ill) : "", Pal.Dusk);
                }
                else c.Paragraph(300, y, 130, h.fate ?? "", Pal.Tan, 9);
                y += 26;
                if (y > Canvas.H - 40) break;
            }
            c.Text(48, Canvas.H - 30, "Spirits: " + Sim.MoraleLabel(G.morale) + ".  " + (G.Has(Trade.Physician) ? "The physician tends the sick." : "No physician in the company."), Pal.Tan);
        }

        void Supplies(Canvas c)
        {
            int y = 50;
            void Row(string a, string b, string note = null)
            {
                c.Text(48, y, a, Pal.Cream); c.TextRight(250, y, b, Pal.Gold);
                if (note != null) c.Text(262, y, note, Pal.Tan);
                y += 13;
            }
            Row("Cash", Ui.Money(G.cents));
            Row(Content.GoodName(Good.Animals, G.Team), G.animals.ToString(), Sim.StrengthLabel(G.strength) + " (" + (int)G.strength + "%)");
            Row("Food", G.food + " lb", "about " + (G.food / Math.Max(1, G.portion * G.Count)) + " days");
            Row("Water", G.water + " of " + G.kegs * Content.KegGallons + " gal", G.kegs + " kegs");
            Row("Repair kits", G.kits.ToString(), "wagon at " + (int)G.wagon + "%");
            Row("Medicine", G.medicine + " doses");
            Row("Powder and shot", G.powder + " shots");
            Row("Gold pans", G.pans.ToString());
            Row("Picks and shovels", G.picks.ToString());
            Row("Rockers", G.rockers.ToString());
            if (G.gold > 0) Row("Gold dust", G.gold.ToString("0.0") + " oz");
            y += 6;
            int load = Sim.Load(G), cap = Sim.Capacity(G);
            Ui.Bar(c, 48, y, 380, 12, load / (float)Math.Max(1, cap), load > cap ? Pal.RedBright : Pal.GoldMid, "Load " + load + " of " + cap + " lb" + (load > cap ? ": too heavy!" : ""));
        }

        public override void Tap(int x, int y)
        {
            if (A.Hit(x, y)) { tab = 0; S.Click(); }
            else if (B.Hit(x, y)) { tab = 1; S.Click(); }
            else if (Close.Hit(x, y) || T > 0.3f && (y < 10 || y > Canvas.H - 10)) { S.Click(); App.Pop(); }
        }

        public override void Key(char k)
        {
            if (k == Keys.Left || k == Keys.Right || k == Keys.Space) tab = 1 - tab;
            else if (k == Keys.Escape || k == Keys.Enter) App.Pop();
        }
    }

    /// <summary>The whole road on a map, with where the company is.</summary>
    public sealed class MapScene : Scene
    {
        public override bool Overlay => true;
        const float Lon0 = -123.5f, Lon1 = -93.5f, Lat0 = 37.4f, Lat1 = 44.4f;
        const int MX = 30, MY = 28, MW = Canvas.W - 60, MH = Canvas.H - 64;

        static (int x, int y) P(Stop s) => (MX + (int)((s.Lon - Lon0) / (Lon1 - Lon0) * MW), MY + (int)((Lat1 - s.Lat) / (Lat1 - Lat0) * MH));

        public override void Draw(Canvas c)
        {
            c.Fill(0, 0, Canvas.W, Canvas.H, Pal.Ink);
            Ui.Title(c, "THE CALIFORNIA ROAD, 1849");
            c.Fill(MX, MY, MW, MH, Pal.SandLight);
            for (int j = MY; j < MY + MH; j += 2)
                for (int i = MX; i < MX + MW; i += 2)
                {
                    float m = Art.Fbm(i * 0.02f, 3) + Art.Fbm(j * 0.03f + 7, 4);
                    if (m > 1.15f) c.Set(i, j, Pal.Sand);
                }
            c.Rect(MX, MY, MW, MH, Pal.Earth);
            // Mountain ranges as hatched bands: the Sierra, the Wasatch, the Rockies.
            Hatch(c, -120.5f, 37.6f, -119.6f, 41.5f); Hatch(c, -112.0f, 39.8f, -111.3f, 42.2f); Hatch(c, -110.0f, 41.8f, -106.5f, 43.4f);
            // Every leg, the visited road drawn bold.
            foreach (var s in Route.All.Values)
                foreach (var l in s.Next)
                {
                    var a = P(s); var b = P(Route.Get(l.To));
                    bool done = Travelled(s.Id, l.To);
                    c.Line(a.x, a.y, b.x, b.y, done ? Pal.RedDark : Pal.Earth);
                    if (done) c.Line(a.x, a.y + 1, b.x, b.y + 1, Pal.RedDark);
                }
            foreach (var s in Route.All.Values)
            {
                var p = P(s);
                bool seen = G.visited.Contains(s.Id);
                c.Disc(p.x, p.y, 2, seen ? Pal.RedDark : Pal.EarthDark);
            }
            // Labels placed by hand so none overlap: offset from the dot, and which side the text runs.
            foreach (var (id, dx, dy, side) in new[] { ("stjoe", -5, 3, -1), ("kearny", 0, 5, 0), ("laramie", 0, 5, 0), ("southpass", 0, -12, 0), ("saltlake", 0, 5, 0),
                ("hall", 0, -12, 0), ("lassen", 0, -12, 0), ("ragtown", 5, 0, 1), ("hangtown", 5, -2, 1), ("lassenrancho", 0, 5, 0) })
            {
                var s = Route.Get(id); var p = P(s);
                string n = s.Name.Split(',', '(')[0].Trim().Replace("Great Salt Lake City", "Salt Lake City").Replace("Carson River", "Ragtown");
                int w = Canvas.TextWidth(n);
                int tx = side > 0 ? p.x + dx : side < 0 ? p.x + dx - w : p.x - w / 2;
                tx = Math.Max(MX + 2, Math.Min(MX + MW - w - 2, tx));
                c.Fill(tx - 1, p.y + dy - 1, w + 2, 10, Pal.SandLight);
                c.Text(tx, p.y + dy, n, Pal.WoodDark);
            }
            // Where the wagon is now.
            var here = P(G.HereStop);
            if (G.next != null && G.Phase == Phase.Road && G.legMiles > 0)
            {
                var to = P(Route.Get(G.next));
                float f = G.legDone / (float)G.legMiles;
                here = (here.x + (int)((to.x - here.x) * f), here.y + (int)((to.y - here.y) * f));
            }
            if (Blink) { c.Disc(here.x, here.y, 4, Pal.Gold); c.Ring(here.x, here.y, 5, Pal.Black); }
            c.Text(MX + 4, MY + MH - 12, G.miles + " miles travelled. " + Sim.DateLabel(G) + ".", Pal.WoodDark);
            Ui.Prompt(c, App.Touch ? "tap to close" : "press space to close", true, Canvas.H - 14);
        }

        static void Hatch(Canvas c, float lon0, float lat0, float lon1, float lat1)
        {
            int x0 = MX + (int)((lon0 - Lon0) / (Lon1 - Lon0) * MW), x1 = MX + (int)((lon1 - Lon0) / (Lon1 - Lon0) * MW);
            int y0 = MY + (int)((Lat1 - lat1) / (Lat1 - Lat0) * MH), y1 = MY + (int)((Lat1 - lat0) / (Lat1 - Lat0) * MH);
            for (int y = y0; y < y1; y += 5)
                for (int x = x0; x < x1; x += 7)
                {
                    int ox = (y / 5 % 2) * 3;
                    c.Tri(x + ox, y + 4, x + ox + 6, y + 4, x + ox + 3, y, Pal.Earth);
                }
        }

        bool Travelled(string a, string b)
        {
            for (int i = 0; i + 1 < G.visited.Count; i++) if (G.visited[i] == a && G.visited[i + 1] == b) return true;
            return false;
        }

        public override void Tap(int x, int y) { if (T > 0.2f) { S.Click(); App.Pop(); } }
        public override void Key(char k) { S.Click(); App.Pop(); }
    }

    /// <summary>The journal of the journey so far, newest first.</summary>
    public sealed class JournalScene : Scene
    {
        int top;
        public override bool Overlay => true;
        Btn Up => new Btn(Canvas.W - 96, 40, 50, 20, "UP");
        Btn Down => new Btn(Canvas.W - 96, Canvas.H - 66, 50, 20, "DOWN");
        Btn Close => new Btn(Canvas.W - 96, Canvas.H - 40, 50, 20, "CLOSE");

        List<string> Lines()
        {
            var l = new List<string>();
            for (int i = G.journal.Count - 1; i >= 0; i--) l.AddRange(Canvas.Wrap(G.journal[i], 370));
            return l;
        }

        public override void Draw(Canvas c)
        {
            c.Shade(0, 0, Canvas.W, Canvas.H, 0.5f);
            Ui.Panel(c, 20, 10, Canvas.W - 40, Canvas.H - 20, "JOURNAL");
            var l = Lines();
            int rows = (Canvas.H - 60) / 11;
            top = Math.Max(0, Math.Min(top, l.Count - rows));
            for (int i = 0; i < rows && top + i < l.Count; i++) c.Text(36, 36 + i * 11, l[top + i], i == 0 && top == 0 ? Pal.Gold : Pal.Cream);
            Up.Draw(c); Down.Draw(c); Close.Draw(c);
        }

        public override void Tap(int x, int y)
        {
            if (Up.Hit(x, y)) { top -= 10; S.Click(); }
            else if (Down.Hit(x, y)) { top += 10; S.Click(); }
            else if (Close.Hit(x, y) || T > 0.3f && x < 30) { S.Click(); App.Pop(); }
        }

        public override void Key(char k)
        {
            if (k == Keys.Up) top -= 3; else if (k == Keys.Down) top += 3; else if (k == Keys.Escape || k == Keys.Enter || k == Keys.Space) App.Pop();
        }
    }
}
