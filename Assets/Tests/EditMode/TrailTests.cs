using System;
using System.Collections.Generic;
using NUnit.Framework;
using TheTrail.Core;

namespace TheTrail.Tests
{
    /// <summary>In-memory storage; JSON through Unity's JsonUtility in the Editor, System.Text.Json under dotnet.</summary>
    sealed class MemoryPlatform : IPlatform
    {
        public readonly Dictionary<string, string> Data = new Dictionary<string, string>();
        public string Load(string key) => Data.TryGetValue(key, out var v) ? v : null;
        public void Save(string key, string value) => Data[key] = value;
#if UNITY_5_3_OR_NEWER
        public string ToJson(object o) => UnityEngine.JsonUtility.ToJson(o);
        public T FromJson<T>(string json) => UnityEngine.JsonUtility.FromJson<T>(json);
#else
        static readonly System.Text.Json.JsonSerializerOptions Opt = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
        public string ToJson(object o) => System.Text.Json.JsonSerializer.Serialize(o, o.GetType(), Opt);
        public T FromJson<T>(string json) => System.Text.Json.JsonSerializer.Deserialize<T>(json, Opt);
#endif
        public bool Touch { get; set; } = true;
    }

    public class TrailTests
    {
        [Test]
        public void RngIsDeterministicAndInRange()
        {
            var a = new Rng(42); var b = new Rng(42);
            for (int i = 0; i < 1000; i++)
            {
                Assert.AreEqual(a.Next(), b.Next());
                int r = a.Range(3, 9); b.Range(3, 9);
                Assert.That(r, Is.InRange(3, 8));
                float f = a.Float(); b.Float();
                Assert.That(f, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }
        }

        [Test]
        public void HashIsStable()
        {
            Assert.AreEqual(2166136261u, Sim.Hash(""));
            Assert.AreEqual(Sim.Hash("stjoe"), Sim.Hash("stjoe"));
            Assert.AreNotEqual(Sim.Hash("stjoe"), Sim.Hash("kearny"));
        }

        [Test]
        public void EveryRoadLeadsToTheDiggings()
        {
            Assert.IsTrue(Route.Exists(Route.Start));
            foreach (var s in Route.All.Values)
            {
                foreach (var l in s.Next) Assert.IsTrue(Route.Exists(l.To), s.Id + " -> " + l.To);
                if (!s.End) Assert.Greater(s.Next.Length, 0, s.Id + " is a dead end");
                if (s.Kind == StopKind.River) Assert.Greater(s.Width, 0, s.Id);
                Assert.IsFalse(string.IsNullOrEmpty(s.Blurb), s.Id);
            }
            // Walk every branch: all of them end at a diggings, with no loops.
            var paths = new List<string>();
            void Walk(string id, string path, int depth)
            {
                Assert.Less(depth, 40, "loop at " + path);
                var s = Route.Get(id);
                if (s.End) { paths.Add(path + id); return; }
                foreach (var l in s.Next) Walk(l.To, path + id + ">", depth + 1);
            }
            Walk(Route.Start, "", 0);
            Assert.GreaterOrEqual(paths.Count, 4);
            foreach (var p in paths)
            {
                int miles = 0; var ids = p.Split('>');
                for (int i = 0; i + 1 < ids.Length; i++) foreach (var l in Route.Get(ids[i]).Next) if (l.To == ids[i + 1]) miles += l.Miles;
                Assert.That(miles, Is.InRange(1700, 2300), p);
            }
        }

        [Test]
        public void TheHiringBoardHasVariety()
        {
            for (uint s = 1; s < 30; s++)
            {
                var b = Content.Board(new Rng(s), "Tester");
                Assert.AreEqual(8, b.Count);
                var trades = new HashSet<Trade>(); var names = new HashSet<string>();
                foreach (var h in b) { trades.Add(h.trade); names.Add(h.name); Assert.Greater(h.stake, 0); }
                Assert.GreaterOrEqual(trades.Count, 5);
                Assert.AreEqual(8, names.Count);
            }
        }

        [Test]
        public void OutfittingPoolsTheStakes()
        {
            var g = Sim.NewGame("Tester", 121, 1);
            Assert.AreEqual(Phase.Hire, g.Phase);
            var b = Content.Board(new Rng(1), "Tester");
            g.company.Add(b[0]); g.company.Add(b[1]);
            Sim.BeginOutfit(g, TeamKind.Oxen);
            Assert.AreEqual((Sim.LeaderStake + b[0].stake + b[1].stake) * 100, g.cents);
            Assert.AreEqual(Phase.Outfit, g.Phase);
            Assert.IsNotNull(Sim.CannotLeave(g));
            g.animals = 6; g.food = 600;
            Assert.IsNull(Sim.CannotLeave(g));
        }

        [Test]
        public void LoadLimitsWhatTheTeamPulls()
        {
            var g = Sim.NewGame("Tester", 121, 1);
            Sim.BeginOutfit(g, TeamKind.Mules);
            g.animals = 4;
            int cap = Sim.Capacity(g);
            g.animals = 8;
            Assert.Greater(Sim.Capacity(g), cap);
            int load = Sim.Load(g);
            g.food += 100;
            Assert.AreEqual(load + 100, Sim.Load(g));
        }

        [Test]
        public void JourneysFinishAndMostCompaniesArrive()
        {
            int reached = 0, n = 40;
            var dollars = new List<int>();
            for (uint s = 1; s <= n; s++)
            {
                var r = Bot.Play(s, 1, 0.75f);
                Assert.IsTrue(r.Reached || r.Lost, "seed " + s + " never finished (" + r.Game.Phase + " at " + r.Game.Here + ")");
                if (r.Reached) { reached++; dollars.Add(r.Dollars); }
            }
            Assert.GreaterOrEqual(reached, n * 6 / 10, "too many companies lost");
            dollars.Sort();
            Assert.Greater(dollars[dollars.Count / 2], 1500, "the median take is too poor");
        }

        [Test]
        public void LeavingLateIsHarder()
        {
            int early = 0, late = 0;
            for (uint s = 1; s <= 30; s++)
            {
                if (Bot.Play(s, 0, 0.7f).Reached) early++;
                if (Bot.Play(s, 3, 0.7f).Reached) late++;
            }
            Assert.GreaterOrEqual(early, late);
        }

        [Test]
        public void SameSeedSameJourney()
        {
            var a = Bot.Play(9, 1); var b = Bot.Play(9, 1);
            Assert.AreEqual(a.Days, b.Days);
            Assert.AreEqual(a.Dollars, b.Dollars);
            CollectionAssert.AreEqual(a.Game.journal, b.Game.journal);
        }

        [Test]
        public void ClaimsGetPoorerLate()
        {
            var g = Sim.NewGame("Tester", 200, 3);
            float early = Sim.Lateness(g);
            g.day = 90;
            Assert.Less(Sim.Lateness(g), early);
        }

        [Test]
        public void SaveRoundTripsMidJourney()
        {
            var p = new MemoryPlatform();
            var app = new App(p);
            var g = Sim.NewGame("Tester", 121, 5);
            g.company.AddRange(Content.Board(new Rng(5), "Tester").GetRange(0, 3));
            Sim.BeginOutfit(g, TeamKind.Oxen);
            g.animals = 6; g.food = 900; g.kegs = 2;
            Sim.Depart(g);
            for (int i = 0; i < 15; i++) { var d = Sim.Day(g, true); if (d.Arrived) { Sim.Arrive(g); Sim.Depart(g); } }
            app.Game = g; app.SaveGame();
            var back = app.LoadGame();
            Assert.IsNotNull(back);
            Assert.AreEqual(g.miles, back.miles);
            Assert.AreEqual(g.food, back.food);
            Assert.AreEqual(g.company.Count, back.company.Count);
            Assert.AreEqual(g.Here, back.Here);
            Assert.AreEqual(g.Phase, back.Phase);
            Assert.AreEqual(g.rng.Next(), back.rng.Next());
        }

        [Test]
        public void ABadSaveIsIgnored()
        {
            var p = new MemoryPlatform();
            p.Data[App.SaveKey] = "{not json";
            var app = new App(p);
            Assert.IsNull(app.LoadGame());
            p.Data[App.SaveKey] = "{\"version\":1}";
            Assert.IsNull(app.LoadGame());
        }

        static GameState Ready(uint s, TeamKind team = TeamKind.Oxen)
        {
            var g = Sim.NewGame("Tester", 121, s);
            g.company.AddRange(Content.Board(new Rng(s), "Tester").GetRange(0, 4));
            Sim.BeginOutfit(g, team);
            g.animals = 6; g.food = 800; g.powder = 40; g.kits = 1; g.medicine = 4; g.kegs = 2; g.water = 20;
            return g;
        }

        [Test]
        public void EveryEventOffersAChoice()
        {
            for (uint s = 1; s <= 10; s++)
            {
                var g = Ready(s, s % 2 == 0 ? TeamKind.Mules : TeamKind.Oxen);
                foreach (var e in Events.All)
                {
                    if (!e.When(g)) continue;
                    Assert.IsFalse(string.IsNullOrEmpty(e.Text(g)), e.Id);
                    var ch = e.Choices(g);
                    Assert.Greater(ch.Count, 0, e.Id);
                    Assert.IsTrue(ch.Exists(c => c.Enabled), e.Id + " has no answer that can be chosen");
                }
            }
        }

        [Test]
        public void EventsLeaveTheStateSane()
        {
            for (uint s = 1; s <= 6; s++)
                foreach (var e in Events.All)
                {
                    var g = Ready(s);
                    if (!e.When(g)) continue;
                    foreach (var c in e.Choices(g))
                    {
                        if (!c.Enabled || c.Hunt != null) continue;
                        var copy = Clone(g);
                        string said = c.Do(copy);
                        Assert.IsNotNull(said, e.Id + ": " + c.Label);
                        Assert.GreaterOrEqual(copy.animals, 0, e.Id);
                        Assert.GreaterOrEqual(copy.cents, 0, e.Id + ": " + c.Label + " spent money it did not have");
                    }
                }
        }

        static GameState Clone(GameState g)
        {
            var p = new MemoryPlatform();
            return p.FromJson<GameState>(p.ToJson(g));
        }

        [Test]
        public void ThePayoutAddsUp()
        {
            var g = Sim.NewGame("Tester", 121, 1);
            g.company.AddRange(Content.Board(new Rng(1), "Tester").GetRange(0, 2));
            g.gold = 10; g.cents = 5000;
            Assert.AreEqual(10 * Sim.DollarsPerOunce + 50 + 3 * Sim.SurvivorBonus, Sim.TakeDollars(g));
        }

        [Test]
        public void TheLedgerKeepsTheBestTen()
        {
            var l = Ledger.Default();
            Assert.AreEqual(10, l.top.Count);
            int low = l.top[l.top.Count - 1].dollars;
            Assert.IsFalse(l.Makes(low - 1));
            int at = l.Add("Best", 999999, "");
            Assert.AreEqual(0, at);
            Assert.AreEqual(10, l.top.Count);
        }

        /// <summary>Taps and keys at random from several starting points: nothing may throw or get stuck.</summary>
        [Test]
        public void MonkeyPlaysWithoutCrashing()
        {
            var seen = new HashSet<string>();
            for (uint seed = 1; seed <= 8; seed++)
            {
                var p = new MemoryPlatform { Touch = seed % 2 == 0 };
                var app = new App(p);
                var g = Ready(seed, seed % 3 == 0 ? TeamKind.Mules : TeamKind.Oxen);
                g.Phase = Phase.Road; Sim.Depart(g);
                switch (seed % 4)
                {
                    case 1: app.Game = g; app.Go(new RoadScene()); break;
                    case 2: g.visited.Add("bigblue"); g.Phase = Phase.Crossing; app.Game = g; app.Go(new StopScene(true)); break;
                    case 3: g.visited.Add("hangtown"); g.Phase = Phase.Diggings; app.Game = g; app.Go(new DiggingsScene(true)); break;
                }
                var r = new Rng(seed * 101u);
                char[] keys = { Keys.Enter, Keys.Space, Keys.Space, Keys.Escape, Keys.Up, Keys.Down, Keys.Left, Keys.Right, '1', '2', '3', 'f', 'w', '+', '-', 'a' };
                var here = new HashSet<string>();
                for (int frame = 0; frame < 12000; frame++)
                {
                    app.HeldSpace = r.Chance(0.3f);
                    app.PointerDown = r.Chance(0.2f);
                    app.PointerX = r.Range(0, Canvas.W); app.PointerY = r.Range(0, Canvas.H);
                    app.Tick(1 / 20f);
                    if (frame % 10 == 0) app.Draw();
                    if (r.Chance(0.12f)) app.Tap(r.Range(0, Canvas.W), r.Range(0, Canvas.H));
                    if (r.Chance(0.06f)) app.Key(keys[r.Range(0, keys.Length)]);
                    if (app.Top != null) { seen.Add(app.Top.GetType().Name); here.Add(app.Top.GetType().Name); }
                }
                Assert.GreaterOrEqual(here.Count, 4, "seed " + seed + ": " + string.Join(",", here));
            }
            Assert.GreaterOrEqual(seen.Count, 14, string.Join(",", seen));
        }

        /// <summary>Every screen draws for a game at each phase and stop.</summary>
        [Test]
        public void EveryScreenDraws()
        {
            var p = new MemoryPlatform();
            var app = new App(p);
            var g = Ready(2);
            app.Game = g;
            var scenes = new List<Func<Scene>>
            {
                () => new TitleScene(), () => new HireScene(), () => new StoreScene(true), () => new RoadScene(), () => new StopScene(true),
                () => new CampMenuAtStop(), () => new OrdersScene(), () => new StatusScene(0), () => new StatusScene(1), () => new MapScene(),
                () => new JournalScene(), () => new StalkScene("buffalo", null), () => new StalkScene(null, null), () => new HaulScene(),
                () => new PanScene(q => { }), () => new LedgerScene(2), () => new SettingsScene(), () => new CreditsScene(), () => new HelpScene(),
            };
            foreach (var make in scenes)
            {
                app.Game = Clone(g);
                app.Go(make());
                for (int i = 0; i < 30; i++) { app.Tick(0.05f); app.Draw(); }
            }
            foreach (var id in Route.Order)
            {
                var gg = Clone(g);
                gg.visited.Add(id);
                var s = Route.Get(id);
                if (s.Kind == StopKind.River) { gg.Phase = Phase.Crossing; app.Game = gg; app.Go(new CrossingScene()); app.Draw(); app.Go(new FordScene(Sim.Fords(gg)[0])); }
                else if (s.End) { gg.Phase = Phase.Diggings; app.Game = gg; app.Go(new DiggingsScene(true)); }
                else { app.Game = gg; app.Go(new StopScene(true)); }
                for (int i = 0; i < 20; i++) { app.Tick(0.05f); app.Draw(); }
                Art.Scene(app.C, s.Art, 0, 0, Canvas.W, 150, 1f, 1, 0, Sky.Fair);
            }
            var end = Clone(g); end.gold = 30; app.Game = end; app.Go(new PayoutScene()); app.Draw();
            var lost = Clone(g); app.Game = lost; app.Go(new LostScene()); app.Draw();
        }
    }
}
