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
                case Phase.Store: app.Go(new StoreScene(true)); break;
                case Phase.River: app.Go(new RiverScene()); break;
                case Phase.Branch: app.Go(new BranchScene()); break;
                case Phase.Raft: app.Go(new RaftScene()); break;
                case Phase.Arrived: app.Go(new TrailMenuScene()); break;
                default: app.Go(new TravelScene(false)); break;
            }
        }

        /// <summary>The wagon has reached the next landmark.</summary>
        public static void Arrive(App app)
        {
            var g = app.Game;
            var l = Sim.Arrive(g);
            app.SaveGame();
            if (g.Phase == Phase.Won) { app.Go(new WinScene()); return; }
            app.Synth.Arrive();
            app.Go(new LandmarkScene(l, () => AfterArrival(app)));
        }

        public static void AfterArrival(App app)
        {
            var g = app.Game;
            if (g.Phase == Phase.River) app.Go(new RiverScene());
            else if (g.Phase == Phase.Branch) app.Go(new BranchScene());
            else app.Go(new TrailMenuScene());
        }

        /// <summary>Shows what happened (deaths, illness, luck), then either the game-over screen or the next step.</summary>
        public static void Report(App app, List<Notice> log, Action then, bool all = false)
        {
            var texts = new List<string>();
            bool death = false, bad = false, good = false;
            foreach (var n in log)
            {
                if (!n.Stop && !all) continue;
                texts.Add(n.Text);
                death |= n.Death; good |= n.Good; bad |= !n.Good;
            }
            if (death) app.Synth.Death(); else if (bad) app.Synth.Bad(); else if (good) app.Synth.Good();
            app.SayAll(texts, () =>
            {
                if (app.Game.Alive == 0) { app.Game.Phase = Phase.Lost; app.ClearSave(); app.Go(new GameOverScene()); }
                else then?.Invoke();
            });
        }
    }

    /// <summary>The wagon rolling west, a day at a time. Tap to stop and size up the situation.</summary>
    public sealed class TravelScene : Scene
    {
        float dayTimer, scroll;
        bool paused;
        int lastMiles;
        readonly bool startPaused;

        public TravelScene(bool startPaused) { this.startPaused = startPaused; }

        public override void Enter()
        {
            G.Phase = Phase.Traveling;
            lastMiles = G.miles;
            scroll = G.miles * 6;
            paused = startPaused;
        }

        bool Moving => !paused && G.broken == Part.None && G.oxen > 0 && G.lostDays == 0 && App.Top == this;

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (paused || App.Top != this) return;
            if (Moving) scroll += dt * 16;
            dayTimer += dt;
            float dayLen = App.Settings.fast ? 0.32f : 0.6f;
            if (dayTimer < dayLen) return;
            dayTimer = 0;
            NextDay();
        }

        void NextDay()
        {
            int before = G.miles;
            var log = Sim.Day(G, true);
            if (G.day % 4 == 0) App.SaveGame();
            bool stop = G.Alive == 0;
            foreach (var n in log) stop |= n.Stop;

            // A grave left by an earlier party, passed today.
            Tomb tomb = null;
            foreach (var t in App.Records.tombs) if (t.miles > before && t.miles <= G.miles) { tomb = t; break; }

            Action after = () =>
            {
                if (tomb != null) App.Push(new TombScene(tomb, Proceed));
                else Proceed();
            };
            if (stop) { paused = true; Flow.Report(App, log, () => { paused = false; after(); }); }
            else after();
        }

        void Proceed()
        {
            if (Sim.AtNext(G)) { Flow.Arrive(App); return; }
        }

        public override void Draw(Canvas c)
        {
            DrawScene(c, G, App.Time, scroll, Moving);
            // Status panel.
            Ui.Box(c, 16, 144, 352, 82);
            var l = Trail.Get(G.next);
            int rem = G.legMiles - G.legDone;
            string[] labels = { "Date:", "Weather:", "Health:", "Food:", "Next landmark:", "Miles traveled:" };
            string[] values =
            {
                Sim.DateLabel(G), Sim.WeatherLabel(G), Sim.HealthLabel(G.hardship), G.food + " pounds",
                rem + (rem == 1 ? " mile" : " miles"), G.miles + " miles",
            };
            for (int i = 0; i < labels.Length; i++)
            {
                c.TextRight(188, 154 + i * 11, labels[i], Canvas.Mid);
                byte col = Canvas.Bright;
                if (i == 2 && G.hardship >= 60) col = Blink ? Canvas.Hot : Canvas.Bright;
                if (i == 3 && G.food < G.Alive * 30) col = Blink ? Canvas.Hot : Canvas.Bright;
                c.Text(196, 154 + i * 11, values[i], col);
            }
            string prompt = G.broken != Part.None ? "BROKEN " + Sim.PartName(G.broken).ToUpper() + "! STOP TO TRADE"
                : G.oxen <= 0 ? "NO OXEN TO PULL THE WAGON"
                : G.lostDays > 0 ? "LOST TIME: " + G.lostDays + (G.lostDays == 1 ? " DAY" : " DAYS")
                : App.Touch ? "TAP TO SIZE UP THE SITUATION" : "PRESS SPACE TO SIZE UP THE SITUATION";
            if (Blink || G.broken == Part.None) c.TextCenter(Canvas.H - 11, prompt, G.broken != Part.None || G.oxen <= 0 ? Canvas.Hot : Canvas.Dark);
        }

        /// <summary>The travel picture: sky, distant land for the terrain, the next landmark rising, the wagon.</summary>
        public static void DrawScene(Canvas c, GameState g, float time, float scroll, bool moving)
        {
            const int horizon = 100, ground = 134, bottom = 140;
            c.Clip(0, 0, Canvas.W, bottom);
            Art.Stars(c, 70, 9, time);
            for (int i = 0; i < 4; i++)
            {
                int cxp = (int)((i * 113 + scroll * 0.05f + time * 2) % (Canvas.W + 80)) - 40;
                Art.Cloud(c, cxp, 22 + (i * 17) % 40, 34 + (i * 13) % 30);
            }
            var t = Sim.TerrainNow(g);
            float far = scroll * 0.15f, mid = scroll * 0.4f;
            switch (t)
            {
                case Terrain.Plains:
                    Art.Ridge(c, 0, Canvas.W, horizon, bottom, 10, 0.012f, 3, 1.0f, far, 0, 1.5f);
                    break;
                case Terrain.Hills:
                    Art.Ridge(c, 0, Canvas.W, horizon - 6, bottom, 26, 0.01f, 5, 1.0f, far, 0.2f, 1.6f);
                    Art.Ridge(c, 0, Canvas.W, horizon, bottom, 10, 0.02f, 6, 1.3f, mid, 0, 1.8f);
                    break;
                case Terrain.Mountains:
                    Art.Ridge(c, 0, Canvas.W, horizon - 22, bottom, 56, 0.012f, 7, 1.2f, far, 0.6f, 2.4f, 4);
                    Art.Ridge(c, 0, Canvas.W, horizon, bottom, 18, 0.02f, 8, 1.0f, mid, 0.2f, 1.6f);
                    break;
                case Terrain.Desert:
                    Art.Ridge(c, 0, Canvas.W, horizon - 4, bottom, 22, 0.008f, 9, 1.0f, far, 0.8f, 1.8f);
                    break;
                default:
                    Art.Ridge(c, 0, Canvas.W, horizon - 16, bottom, 44, 0.014f, 10, 1.2f, far, 0.5f, 2.2f, 3);
                    break;
            }

            // The next landmark rises out of the west as you approach.
            var next = Trail.Get(g.next);
            int rem = g.legMiles - g.legDone;
            if (rem <= 60 && next.Kind != Kind.River && next.Kind != Kind.End)
            {
                float p = 1 - rem / 60f;
                Art.Silhouette(c, next.Art, (int)(-50 + p * 150), horizon + 1, 0.5f + p * 0.7f);
            }
            else if (rem <= 60 && next.Kind == Kind.End) Art.Silhouette(c, "valley", (int)(-40 + (1 - rem / 60f) * 140), horizon + 1, 0.8f);

            // Ground: dithered prairie, a worn track, tufts that slide past.
            for (int y = horizon; y < bottom; y++)
            {
                float s = 0.9f + (y - horizon) / (float)(bottom - horizon) * 0.8f;
                if (t == Terrain.Desert) s -= 0.2f;
                for (int x = 0; x < Canvas.W; x++) c.SetShade(x, y, s);
            }
            c.HLine(0, Canvas.W, ground + 1, Canvas.Dim);
            c.HLine(0, Canvas.W, ground - 3, Canvas.Dim);
            if (t == Terrain.Forest || t == Terrain.Mountains)
                for (int i = 0; i < 16; i++)
                {
                    float px = (i * 41 + mid * 1.4f) % (Canvas.W + 40) - 20;
                    Art.Pine(c, (int)px, horizon + 6 + (i * 5) % 7, 12 + (i * 7) % 10, i % 2 == 0 ? Canvas.Mid : Canvas.Bright);
                }
            float near = scroll;
            for (int i = 0; i < 34; i++)
            {
                int gx = (int)((i * 53 + near * (0.6f + (i % 3) * 0.2f)) % (Canvas.W + 20)) - 10;
                int gy = horizon + 4 + (i * 13) % 30;
                byte col = gy > horizon + 18 ? Canvas.Mid : Canvas.Dark;
                if (t == Terrain.Desert && i % 3 == 0) { Art.Bush(c, gx, gy, 6, 2.2f); continue; }
                c.Set(gx, gy, col); c.Set(gx - 1, gy + 1, col); c.Set(gx + 1, gy + 1, col);
            }

            // A river ahead crosses the trail.
            if (next.Kind == Kind.River && rem <= 25)
            {
                int rx = (int)(-60 + (1 - rem / 25f) * 150);
                for (int y = horizon; y < bottom; y++)
                {
                    int w = 6 + (y - horizon) / 2;
                    for (int x = rx - w; x <= rx + w; x++)
                    {
                        float ripple = Art.Noise(x * 0.25f + time * 3f, y);
                        c.SetShade(x + (y - horizon) / 3, y, x == rx - w || x == rx + w ? 2.6f : 0.5f + (ripple > 0.82f ? 2.2f : 0));
                    }
                }
            }

            // Weather.
            if (g.sky == Sky.Rain || g.sky == Sky.HeavyRain)
            {
                int n = g.sky == Sky.HeavyRain ? 90 : 40;
                for (int i = 0; i < n; i++)
                {
                    int x = (int)(i * 97 % Canvas.W + time * 40) % Canvas.W, y = (int)((i * 61 % bottom) + time * 180) % bottom;
                    c.Line(x, y, x - 2, y + 5, Canvas.Dark);
                }
            }
            else if (g.sky == Sky.Snow || g.sky == Sky.HeavySnow)
            {
                int n = g.sky == Sky.HeavySnow ? 140 : 60;
                for (int i = 0; i < n; i++)
                {
                    int x = (int)(i * 97 % Canvas.W + Math.Sin(time + i) * 6 + time * 10) % Canvas.W, y = (int)((i * 61 % bottom) + time * 30) % bottom;
                    c.Set(x, y, i % 3 == 0 ? Canvas.Hot : Canvas.Bright);
                }
            }

            Art.Outfit(c, 166, ground + 2, g.oxen, time, moving, g.broken == Part.Wheel);
            c.NoClip();
            c.HLine(0, Canvas.W - 1, bottom, Canvas.Mid);
        }

        public override void Tap(int x, int y) { if (!paused) OpenMenu(); }
        public override void Key(char k) { if (!paused && (k == ' ' || k == Keys.Enter || k == Keys.Escape)) OpenMenu(); }

        void OpenMenu()
        {
            S.Click();
            paused = true;
            App.Go(new TrailMenuScene());
        }
    }

    /// <summary>"Size up the situation": the main menu while stopped, on the trail or at a landmark.</summary>
    public sealed class TrailMenuScene : Scene
    {
        Menu menu;
        Btn quit = new Btn(Canvas.W - 64, 2, 60, 14, "SAVE");

        public override void Enter()
        {
            var l = Trail.Get(G.Here);
            bool atLandmark = G.Phase == Phase.Arrived;
            menu = new Menu(App) { X = 40, Y = 78, Pitch = 15, W = 304 };
            menu.Add("Continue on trail", () => { G.Phase = Phase.Traveling; App.SaveGame(); App.Go(new TravelScene(false)); });
            menu.Add("Check supplies", () => App.Go(new SuppliesScene()));
            menu.Add("Look at map", () => App.Go(new MapScene(() => App.Go(new TrailMenuScene()))));
            menu.Add("Change pace", () => App.Go(new PaceScene()), true, Sim.PaceName(G.pace));
            menu.Add("Change food rations", () => App.Go(new RationsScene()), true, Sim.RationName(G.rations));
            menu.Add("Stop to rest", () => App.Push(new NumberScene("How many days would you like to rest?", 9, Rest)));
            menu.Add("Attempt to trade", Trade);
            if (atLandmark && l.Talk.Length > 0) menu.Add("Talk to people", () => App.Go(new TalkScene()));
            if (atLandmark && l.HasStore) menu.Add("Buy supplies", () => App.Go(new StoreScene(false)));
            menu.Add("Hunt for food", Hunt, true, G.bullets > 0 ? G.bullets + " bullets" : "no bullets");
        }

        void Rest(int days)
        {
            if (days <= 0) return;
            var log = Sim.Rest(G, days);
            App.SaveGame();
            log.Insert(0, new Notice("You rest for " + (days == 1 ? "a day" : days + " days") + ". The party's health is now " + Sim.HealthLabel(G.hardship) + ".", true, false, true));
            Flow.Report(App, log, () => App.Go(new TrailMenuScene()));
        }

        void Trade()
        {
            var offer = Sim.MakeOffer(G);
            var log = Sim.Day(G, false);
            if (offer == null)
            {
                log.Insert(0, new Notice("You spent the day looking for someone to trade with. Nobody wanted to trade.", true));
                Flow.Report(App, log, () => App.Go(new TrailMenuScene()));
                return;
            }
            Flow.Report(App, log, () => App.Push(new AskScene(App,
                "A traveler offers you " + Sim.GoodName(offer.give, offer.giveN) + " for " + Sim.GoodName(offer.want, offer.wantN) + ".\n\nYou have " + Sim.GoodName(offer.want, Sim.Have(G, offer.want)) + ".",
                ("Accept the trade", () => { Sim.Accept(G, offer); S.Good(); App.SaveGame(); App.Go(new TrailMenuScene()); }),
                ("Decline", () => App.Go(new TrailMenuScene())))));
        }

        void Hunt()
        {
            if (G.bullets <= 0) { App.Say("You have no bullets. You can't hunt without them."); return; }
            App.Push(new AskScene(App, App.Touch
                    ? "Tap where you want to shoot. Each shot uses a bullet. Hunting takes the rest of the day."
                    : "Click where you want to shoot. Each shot uses a bullet. Hunting takes the rest of the day.",
                ("Go hunting", () => App.Go(new HuntScene())),
                ("Not now", null)));
        }

        public override void Draw(Canvas c)
        {
            var l = Trail.Get(G.Here);
            string where = G.Phase == Phase.Arrived ? l.Name : "On the trail";
            Ui.Title(c, where.ToUpper());
            quit.Draw(c);
            c.Text(40, 24, Sim.DateLabel(G), Canvas.Bright);
            c.Text(40, 36, "Weather: " + Sim.WeatherLabel(G), Canvas.Mid);
            c.Text(40, 48, "Health:  " + Sim.HealthLabel(G.hardship), G.hardship >= 60 ? Canvas.Hot : Canvas.Mid);
            c.Text(232, 36, "Pace:", Canvas.Mid); c.Text(280, 36, Sim.PaceName(G.pace), Canvas.Mid);
            c.Text(232, 48, "Food:", Canvas.Mid); c.Text(280, 48, G.food + " lb", Canvas.Mid);
            c.HLine(32, 352, 62, Canvas.Dark);
            c.Text(40, 66, "You may:", Canvas.Mid);
            menu.Draw(c);
        }

        public override void Tap(int x, int y)
        {
            if (quit.Hit(x, y)) { SaveQuit(); return; }
            menu.Tap(x, y);
        }

        public override void Key(char k) { if (k == Keys.Escape) SaveQuit(); else menu.Key(k); }

        void SaveQuit() => App.Push(new AskScene(App, "Save your journey and return to the title screen? You can continue it later.",
            ("Save and quit", () => { App.SaveGame(); App.Go(new TitleScene()); }),
            ("Keep playing", null)));
    }

    /// <summary>Arriving somewhere named: its picture, its name, a line about it.</summary>
    public sealed class LandmarkScene : Scene
    {
        readonly Landmark l; readonly Action then;
        public LandmarkScene(Landmark l, Action then) { this.l = l; this.then = then; }

        public override void Draw(Canvas c)
        {
            Art.Scene(c, l.Art, 0, 0, Canvas.W, 150, App.Time, l.Name.Length);
            c.HLine(0, Canvas.W, 150, Canvas.Mid);
            c.TextCenter(158, l.Name, Canvas.Hot);
            c.TextCenter(170, Sim.DateLabel(G), Canvas.Mid);
            c.Paragraph(24, 186, 42, l.Blurb, Canvas.Bright);
            Ui.Prompt(c, Continue, Blink, Canvas.H - 10);
        }

        public override void Tap(int x, int y) { if (T > 0.3f) Done(); }
        public override void Key(char k) { if (k == ' ' || k == Keys.Enter || k == Keys.Escape) Done(); }
        void Done() { S.Click(); then(); }
    }

    public sealed class SuppliesScene : Scene
    {
        public override void Draw(Canvas c)
        {
            Ui.Title(c, "YOUR SUPPLIES");
            string[] n = { "Oxen", "Sets of clothing", "Bullets", "Wagon wheels", "Wagon axles", "Wagon tongues", "Pounds of food", "Money left" };
            string[] v = { G.oxen + (G.oxenWeak > 0 ? " (" + G.oxenWeak + " hurt)" : ""), G.clothing.ToString(), G.bullets.ToString(), G.wheels.ToString(),
                G.axles.ToString(), G.tongues.ToString(), G.food.ToString(), Ui.Money(G.cents) };
            for (int i = 0; i < n.Length; i++)
            {
                c.Text(24, 24 + i * 11, n[i], Canvas.Mid);
                c.TextRight(200, 24 + i * 11, v[i], Canvas.Bright);
            }
            c.Text(224, 24, "The party", Canvas.Mid);
            c.HLine(224, 360, 33, Canvas.Dark);
            for (int i = 0; i < G.party.Count; i++)
            {
                var m = G.party[i];
                c.Text(224, 38 + i * 22, m.name, m.alive ? Canvas.Bright : Canvas.Dark);
                string st = !m.alive ? "died: " + m.cause : m.ailment != Ailment.None ? Sim.AilmentName(m.ailment).Replace("a ", "") : "well";
                if (st.Length > 17) st = st.Substring(0, 17);
                c.Text(232, 48 + i * 22, st, !m.alive ? Canvas.Dim : m.ailment != Ailment.None ? Canvas.Hot : Canvas.Mid);
            }
            c.HLine(16, 368, 152, Canvas.Dark);
            c.Text(24, 160, "Party health:", Canvas.Mid); c.Text(144, 160, Sim.HealthLabel(G.hardship), Canvas.Bright);
            c.Text(24, 172, "Pace:", Canvas.Mid); c.Text(144, 172, Sim.PaceName(G.pace), Canvas.Bright);
            c.Text(24, 184, "Rations:", Canvas.Mid); c.Text(144, 184, Sim.RationName(G.rations), Canvas.Bright);
            int days = G.Alive > 0 ? G.food / Math.Max(1, G.Alive * Sim.FoodPerPerson(G.rations)) : 0;
            c.Text(24, 196, "Food lasts:", Canvas.Mid); c.Text(144, 196, "about " + days + (days == 1 ? " day" : " days"), Canvas.Bright);
            c.Text(24, 208, "Way of life:", Canvas.Mid); c.Text(144, 208, Sim.JobName(G.job), Canvas.Bright);
            Ui.Prompt(c, Continue, Blink, Canvas.H - 11);
        }

        public override void Tap(int x, int y) { if (T > 0.2f) Back(); }
        public override void Key(char k) => Back();
        void Back() { S.Click(); App.Go(new TrailMenuScene()); }
    }

    /// <summary>The route across the continent, what's behind you and what's ahead.</summary>
    public sealed class MapScene : Scene
    {
        readonly Action back;
        public MapScene(Action back) { this.back = back; }

        // Longitude and latitude to the map: 124W..93W across, 48N..38N down.
        static (int x, int y) Geo(float lat, float lon) => (14 + (int)((lon + 124f) * 11.6f), 26 + (int)((48f - lat) * 17.5f));
        static (int x, int y) P(Landmark l) => Geo(l.Lat, l.Lon);

        static void Path(Canvas c, byte col, params (float lat, float lon)[] pts)
        {
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                var a = Geo(pts[i].lat, pts[i].lon); var b = Geo(pts[i + 1].lat, pts[i + 1].lon);
                c.Line(a.x, a.y, b.x, b.y, col);
            }
        }

        public override void Draw(Canvas c)
        {
            Ui.Title(c, "MAP OF THE TRAIL");
            // The Pacific coast.
            for (int y = 16; y < 212; y++)
            {
                int x = 6 + (int)(Math.Sin(y * 0.09f) * 2 + Math.Sin(y * 0.031f) * 3);
                c.SetShade(x, y, 2.4f);
                for (int i = 0; i < x; i++) c.SetShade(i, y, 0.5f);
            }
            // Rivers: the Missouri, the Platte, the Sweetwater, the Snake, the Columbia, the Willamette.
            Path(c, Canvas.Dim, (38.6f, -90.2f), (39.1f, -94.6f), (40.0f, -95.4f), (41.3f, -95.9f), (42.5f, -96.4f), (43.5f, -98.5f));
            Path(c, Canvas.Dim, (41.0f, -95.9f), (40.7f, -98.4f), (41.1f, -100.8f), (41.6f, -102.6f), (42.1f, -104.4f), (42.85f, -106.3f), (42.4f, -107.6f));
            Path(c, Canvas.Dim, (46.2f, -123.9f), (45.6f, -122.7f), (45.65f, -121.2f), (45.9f, -119.3f), (46.25f, -119.1f), (46.4f, -117.0f));
            Path(c, Canvas.Dim, (46.4f, -117.0f), (45.0f, -116.8f), (43.8f, -117.0f), (42.9f, -115.3f), (42.6f, -114.4f), (42.9f, -112.6f), (43.4f, -111.4f));
            Path(c, Canvas.Dim, (45.4f, -122.6f), (44.6f, -123.1f), (44.0f, -123.0f));
            Path(c, Canvas.Dim, (41.95f, -110.05f), (41.2f, -109.5f), (40.5f, -109.3f));
            // Mountains: the Rockies, the Blue Mountains, the Cascades.
            for (int i = 0; i < 12; i++) Peak(c, Geo(40.3f + i * 0.45f, -105.8f - i * 0.45f - (i % 2) * 0.5f));
            for (int i = 0; i < 5; i++) Peak(c, Geo(44.6f + i * 0.25f, -118.8f + i * 0.35f));
            for (int i = 0; i < 7; i++) Peak(c, Geo(43.6f + i * 0.5f, -121.9f + (i % 2) * 0.2f));

            // Every road; the ones you've traveled bright.
            foreach (var l in Trail.All.Values)
                foreach (var r in l.Next)
                {
                    var a = P(l); var b = P(Trail.Get(r.To));
                    int ia = G.visited.IndexOf(l.Id), ib = G.visited.IndexOf(r.To);
                    bool done = ia >= 0 && ib == ia + 1;
                    if (done) { c.Line(a.x, a.y, b.x, b.y, Canvas.Hot); c.Line(a.x, a.y + 1, b.x, b.y + 1, Canvas.Bright); }
                    else DottedLine(c, a.x, a.y, b.x, b.y);
                }
            foreach (var l in Trail.All.Values)
            {
                var p = P(l);
                byte col = G.visited.Contains(l.Id) ? Canvas.Hot : Canvas.Mid;
                if (l.Kind == Kind.Fort || l.Kind == Kind.Town || l.Kind == Kind.End) { c.Fill(p.x - 2, p.y - 2, 5, 5, Canvas.Black); c.Rect(p.x - 2, p.y - 2, 5, 5, col); }
                else c.Disc(p.x, p.y, 1, col);
            }
            // Where you are now.
            var here = P(Trail.Get(G.Here)); var nx = P(Trail.Get(G.next));
            float f = G.Phase == Phase.Traveling && G.legMiles > 0 ? G.legDone / (float)G.legMiles : 0;
            int wx = (int)(here.x + (nx.x - here.x) * f), wy = (int)(here.y + (nx.y - here.y) * f);
            if (Blink) { c.Ring(wx, wy, 5, Canvas.Hot); c.Fill(wx - 1, wy - 1, 3, 3, Canvas.Hot); }

            c.Text(28, 140, "OREGON", Canvas.Dark);
            c.Text(36, 150, "COUNTRY", Canvas.Dark);
            c.Text(118, 184, "ROCKY MTS", Canvas.Dark);
            c.Text(290, 196, "MISSOURI", Canvas.Dark);
            c.Text(250, 120, "PLATTE R.", Canvas.Dark);
            c.Fill(0, 212, Canvas.W, 28, Canvas.Black);
            c.HLine(0, Canvas.W, 212, Canvas.Dark);
            c.Text(12, 218, "Last stop: " + Trail.Get(G.Here).Name, Canvas.Mid);
            c.Text(12, 229, "Next: " + Trail.Get(G.next).Name + ", " + (G.legMiles - G.legDone) + " mi", Canvas.Bright);
        }

        static void Peak(Canvas c, (int x, int y) p)
        {
            int x = p.x, y = p.y;
            c.Line(x - 4, y + 4, x, y, Canvas.Mid);
            c.Line(x, y, x + 4, y + 4, Canvas.Dark);
        }

        static void DottedLine(Canvas c, int x0, int y0, int x1, int y1)
        {
            int n = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
            for (int i = 0; i <= n; i += 3) c.Set(x0 + (x1 - x0) * i / Math.Max(1, n), y0 + (y1 - y0) * i / Math.Max(1, n), Canvas.Mid);
        }

        public override void Tap(int x, int y) { if (T > 0.2f) { S.Click(); back(); } }
        public override void Key(char k) { S.Click(); back(); }
    }

    public sealed class PaceScene : Scene
    {
        Menu menu;
        public override void Enter()
        {
            menu = new Menu(App) { X = 40, Y = 52, Pitch = 18, W = 300 };
            menu.Add("A steady pace", () => Set(Pace.Steady));
            menu.Add("A strenuous pace", () => Set(Pace.Strenuous));
            menu.Add("A grueling pace", () => Set(Pace.Grueling));
            menu.Sel = (int)G.pace - 1;
        }
        void Set(Pace p) { G.pace = p; App.Go(new TrailMenuScene()); }

        public override void Draw(Canvas c)
        {
            Ui.Title(c, "CHANGE PACE");
            c.Text(40, 30, "The current pace is " + Sim.PaceName(G.pace) + ".", Canvas.Bright);
            menu.Draw(c);
            c.Paragraph(32, 120, 42,
                "Steady: about eight hours a day, with rest stops. The party stays healthiest.\n" +
                "Strenuous: dawn to dusk. You will be tired.\n" +
                "Grueling: every waking hour. The party's health will suffer badly.", Canvas.Mid);
        }
        public override void Tap(int x, int y) => menu.Tap(x, y);
        public override void Key(char k) { if (k == Keys.Escape) App.Go(new TrailMenuScene()); else menu.Key(k); }
    }

    public sealed class RationsScene : Scene
    {
        Menu menu;
        public override void Enter()
        {
            menu = new Menu(App) { X = 40, Y = 52, Pitch = 18, W = 300 };
            menu.Add("Filling: meals are large", () => Set(Rations.Filling));
            menu.Add("Meager: meals are small", () => Set(Rations.Meager));
            menu.Add("Bare bones: barely enough", () => Set(Rations.BareBones));
            menu.Sel = (int)G.rations - 1;
        }
        void Set(Rations r) { G.rations = r; App.Go(new TrailMenuScene()); }

        public override void Draw(Canvas c)
        {
            Ui.Title(c, "CHANGE FOOD RATIONS");
            c.Text(40, 30, "The current rations are " + Sim.RationName(G.rations) + ".", Canvas.Bright);
            menu.Draw(c);
            c.Paragraph(32, 120, 42,
                "Filling: 3 pounds of food per person a day.\n" +
                "Meager: 2 pounds. Hunger wears on everyone.\n" +
                "Bare bones: 1 pound. Your party will weaken fast.", Canvas.Mid);
        }
        public override void Tap(int x, int y) => menu.Tap(x, y);
        public override void Key(char k) { if (k == Keys.Escape) App.Go(new TrailMenuScene()); else menu.Key(k); }
    }

    public sealed class TalkScene : Scene
    {
        static int next;
        (string who, string say) quote;

        public override void Enter()
        {
            var l = Trail.Get(G.Here);
            quote = l.Talk[next++ % l.Talk.Length];
        }

        public override void Draw(Canvas c)
        {
            var l = Trail.Get(G.Here);
            Art.Scene(c, l.Art, 0, 0, Canvas.W, 96, App.Time, 4);
            c.HLine(0, Canvas.W, 96, Canvas.Mid);
            c.Text(24, 108, quote.who + " tells you:", Canvas.Mid);
            c.Paragraph(32, 126, 40, "\"" + quote.say + "\"", Canvas.Bright, 11);
            Ui.Prompt(c, Continue, Blink, Canvas.H - 11);
        }

        public override void Tap(int x, int y) { if (T > 0.2f) Back(); }
        public override void Key(char k) => Back();
        void Back() { S.Click(); App.Go(new TrailMenuScene()); }
    }

    /// <summary>A fork in the trail.</summary>
    public sealed class BranchScene : Scene
    {
        Menu menu;
        public override void Enter()
        {
            var l = Trail.Get(G.Here);
            menu = new Menu(App) { X = 32, Y = 100, Pitch = 18, W = 320 };
            foreach (var r in l.Next)
            {
                var route = r;
                bool ok = route.Toll * 100 <= G.cents;
                menu.Add(route.Label, () => Choose(route), ok);
            }
            menu.Add("See the map", () => App.Go(new MapScene(() => App.Go(new BranchScene()))));
            if (l.Talk.Length > 0) menu.Add("Talk to people", () => App.Push(new MessageScene(Talk(l), null, true)));
            if (l.HasStore) menu.Add("Buy supplies", () => App.Go(new StoreScene(false)));
        }

        static int talk;
        static string Talk(Landmark l) { var q = l.Talk[talk++ % l.Talk.Length]; return q.who + " tells you:\n\n\"" + q.say + "\""; }

        void Choose(Route r)
        {
            Sim.ChooseRoute(G, r);
            App.SaveGame();
            if (r.Raft) App.Go(new RaftScene());
            else App.Go(new TravelScene(false));
        }

        public override void Draw(Canvas c)
        {
            var l = Trail.Get(G.Here);
            Art.Scene(c, l.Art, 0, 0, Canvas.W, 76, App.Time, 6);
            c.HLine(0, Canvas.W, 76, Canvas.Mid);
            c.TextCenter(82, "The trail divides here. You may:", Canvas.Bright);
            menu.Draw(c);
            int y = menu.Y + menu.Height + 4;
            foreach (var r in l.Next)
                if (!r.Raft) { c.Text(32, y, Trail.Get(r.To).Name + ": " + r.Miles + " miles", Canvas.Dark); y += 10; }
                else { c.Text(32, y, "River to Oregon City: " + r.Miles + " miles", Canvas.Dark); y += 10; }
        }

        public override void Tap(int x, int y) => menu.Tap(x, y);
        public override void Key(char k) => menu.Key(k);
    }

    /// <summary>A grave by the trail: one left by a past journey, or the one you're writing now.</summary>
    public sealed class TombScene : Scene
    {
        readonly Tomb tomb; readonly Action then; readonly bool own;
        public TombScene(Tomb tomb, Action then, bool own = false) { this.tomb = tomb; this.then = then; this.own = own; }

        public override void Enter() { if (!own) S.Death(); }

        public override void Draw(Canvas c)
        {
            Art.Stars(c, 100, 21, App.Time);
            c.FillShade(0, 170, Canvas.W, 70, 1.3f);
            c.HLine(0, Canvas.W, 170, Canvas.Mid);
            Art.Tombstone(c, 112, 40, 160, 130);
            c.TextCenter(70, "Here lies", Canvas.Black);
            c.TextCenter(84, tomb.name, Canvas.Black);
            c.HLine(156, 228, 97, Canvas.Dark);
            var lines = Canvas.Wrap(tomb.epitaph ?? "", 17);
            for (int i = 0; i < lines.Count && i < 5; i++) c.TextCenter(106 + i * 11, lines[i], Canvas.Black);
            c.TextCenter(178, "Mile " + tomb.miles + ", " + tomb.date, Canvas.Bright);
            c.TextCenter(194, own ? "Rest in peace." : "You pass a grave beside the trail.", Canvas.Mid);
            Ui.Prompt(c, Continue, Blink, Canvas.H - 14);
        }

        public override void Tap(int x, int y) { if (T > 0.4f) Done(); }
        public override void Key(char k) => Done();
        void Done() { S.Click(); App.Pop(); then?.Invoke(); }
    }
}
