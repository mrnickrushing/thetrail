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
        public bool Touch => true;
    }

    public class TrailTests
    {
        static GameState Outfitted(Job job = Job.Banker, uint seed = 1, int month = 4)
        {
            var g = Sim.NewGame(job, new[] { "Ann", "Ben", "Cal", "Dot", "Eve" }, month, seed);
            g.oxen = 8; g.food = 1200; g.clothing = 10; g.bullets = 200; g.wheels = g.axles = g.tongues = 1;
            g.Phase = Phase.Traveling;
            return g;
        }

        /// <summary>Plays the rules to the end with sensible choices: rest when worn, ford shallow, ferry or float deep.</summary>
        static GameState PlayOut(GameState g)
        {
            for (int guard = 0; guard < 800 && g.Alive > 0 && g.Phase != Phase.Won; guard++)
            {
                switch (g.Phase)
                {
                    case Phase.River:
                        var l = Trail.Get(g.Here);
                        Sim.Cross(g, g.riverDepthTenths < 25 ? Sim.Crossing.Ford : l.Ferry > 0 ? Sim.Crossing.Ferry : Sim.Crossing.Float);
                        continue;
                    case Phase.Branch:
                        Sim.ChooseRoute(g, Trail.Get(g.Here).Next[g.Here == "dalles" ? 1 : 0]);
                        continue;
                    case Phase.Arrived:
                        g.Phase = Phase.Traveling;
                        continue;
                }
                if (g.food < 60) g.food += 300;  // stands in for hunting
                if (g.broken != Part.None) { g.AddSpare(g.broken, 1); g.AddSpare(g.broken, -1); g.broken = Part.None; }
                if (g.oxen < 2) g.oxen = 4;
                Sim.Day(g, true);
                if (g.Alive > 0 && Sim.AtNext(g)) Sim.Arrive(g);
            }
            return g;
        }

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
        public void NewGameStartsAtIndependenceWithTheRightPurse()
        {
            Assert.AreEqual(160000, Sim.NewGame(Job.Banker, new[] { "A" }, 4, 1).cents);
            Assert.AreEqual(80000, Sim.NewGame(Job.Carpenter, new[] { "A" }, 4, 1).cents);
            var g = Sim.NewGame(Job.Farmer, new[] { "A", "B", "C", "D", "E" }, 5, 1);
            Assert.AreEqual(40000, g.cents);
            Assert.AreEqual(5, g.Alive);
            Assert.AreEqual(Trail.Start, g.Here);
            Assert.AreEqual("kansas", g.next);
            Assert.AreEqual(Phase.Store, g.Phase);
            Assert.AreEqual(new DateTime(1848, 5, 1), g.Date);
        }

        [Test]
        public void EachDayEatsFoodByRations()
        {
            var g = Outfitted();
            int before = g.food;
            Sim.Day(g, false);
            Assert.That(before - g.food, Is.LessThanOrEqualTo(15));   // 5 people x 3 lb
            g.rations = Rations.BareBones;
            before = g.food;
            Sim.Day(g, false);
            Assert.That(before - g.food, Is.LessThanOrEqualTo(g.Alive));
        }

        [Test]
        public void TheTrailIsConnectedAndEndsInOregonCity()
        {
            foreach (var l in Trail.All.Values)
            {
                foreach (var r in l.Next) Assert.IsTrue(Trail.All.ContainsKey(r.To), l.Id + " -> " + r.To);
                if (l.Kind != Kind.End) Assert.IsNotEmpty(l.Next, l.Id);
                Assert.IsNotEmpty(l.Blurb, l.Id);
                Assert.That(Canvas.Wrap(l.Blurb, 42).Count, Is.LessThanOrEqualTo(4), "blurb too long for the landmark screen: " + l.Id);
                foreach (var t in l.Talk) Assert.That(Canvas.Wrap("\"" + t.say + "\"", 40).Count, Is.LessThanOrEqualTo(8), l.Id);
            }
            Assert.AreEqual(Kind.End, Trail.Get(Trail.Finish).Kind);
        }

        [Test]
        public void AFullJourneyReachesOregonOnEveryRoute()
        {
            int won = 0;
            for (uint seed = 1; seed <= 40; seed++)
            {
                var g = PlayOut(Outfitted(Job.Banker, seed));
                if (g.Phase == Phase.Won)
                {
                    won++;
                    Assert.AreEqual(Trail.Finish, g.Here);
                    Assert.That(g.miles, Is.InRange(1850, 2000));
                    Assert.That(g.day, Is.InRange(100, 320));
                    Assert.That(Sim.Score(g), Is.GreaterThan(0));
                }
            }
            Assert.That(won, Is.GreaterThanOrEqualTo(30), "a careful party should usually make it");
        }

        [Test]
        public void RiversAreDeepInSpringAndShallowInLateSummer()
        {
            var l = Trail.Get("kansas");
            var spring = Outfitted(month: 4); var fall = Outfitted(month: 8);
            spring.wetness = fall.wetness = 0;
            Assert.That(Sim.RiverDepth(spring, l), Is.GreaterThan(Sim.RiverDepth(fall, l)));
            for (uint s = 0; s < 50; s++)
            {
                var g = Outfitted(seed: s);
                int d = Sim.RiverDepth(g, l);
                Assert.That(d, Is.InRange(10, 120));
                g.riverDepthTenths = d;
                Assert.That(Sim.RiverWidth(g, l), Is.InRange(400, 900));
            }
        }

        [Test]
        public void FerryIsSaferThanFordingDeepWater()
        {
            int fordLosses = 0, ferryLosses = 0;
            for (uint s = 0; s < 300; s++)
            {
                var a = Outfitted(seed: s); a.visited.Add("kansas"); a.riverDepthTenths = 50;
                var r = Sim.Cross(a, Sim.Crossing.Ford); if (r.Capsized || r.Swamped) fordLosses++;
                var b = Outfitted(seed: s); b.visited.Add("kansas"); b.riverDepthTenths = 50;
                r = Sim.Cross(b, Sim.Crossing.Ferry); if (r.Capsized || r.Swamped) ferryLosses++;
                Assert.AreEqual(b.cents, 160000 - 500);
            }
            Assert.That(fordLosses, Is.GreaterThan(200));
            Assert.That(ferryLosses, Is.LessThan(30));
        }

        [Test]
        public void PricesRiseFartherWest()
        {
            var g = Outfitted();
            int home = Sim.Price(g, Sim.Good.Food);
            g.visited.Add("hall");
            Assert.That(Sim.Price(g, Sim.Good.Food), Is.GreaterThan(home));
            Assert.AreEqual(4000, Sim.Price(Outfitted(), Sim.Good.Oxen));
        }

        [Test]
        public void HealthLabelsFollowHardship()
        {
            Assert.AreEqual("good", Sim.HealthLabel(0));
            Assert.AreEqual("fair", Sim.HealthLabel(45));
            Assert.AreEqual("poor", Sim.HealthLabel(80));
            Assert.AreEqual("very poor", Sim.HealthLabel(150));
            var easy = Outfitted(); var hard = Outfitted();
            hard.pace = Pace.Grueling; hard.rations = Rations.BareBones;
            for (int i = 0; i < 30; i++) { Sim.Day(easy, true); Sim.Day(hard, true); }
            Assert.That(hard.hardship, Is.GreaterThan(easy.hardship + 30));
        }

        [Test]
        public void FarmersScoreTriple()
        {
            var a = Outfitted(Job.Banker); var b = Outfitted(Job.Farmer);
            b.cents = a.cents;
            Assert.AreEqual(Sim.Score(a) * 3, Sim.Score(b));
        }

        [Test]
        public void TradesNeverAskForMoreThanYouHave()
        {
            for (uint s = 0; s < 300; s++)
            {
                var g = Outfitted(seed: s);
                var o = Sim.MakeOffer(g);
                if (o == null) continue;
                Assert.That(Sim.Have(g, o.want), Is.GreaterThanOrEqualTo(o.wantN));
                Assert.AreNotEqual(o.want, o.give);
                Sim.Accept(g, o);
                Assert.That(g.oxen, Is.GreaterThanOrEqualTo(2));
            }
        }

        [Test]
        public void TopTenStaysSortedAndShort()
        {
            var r = Records.Default();
            r.AddScore("Zed", 9999); r.AddScore("Low", 10);
            Assert.AreEqual(10, r.top.Count);
            Assert.AreEqual("Zed", r.top[0].name);
            for (int i = 1; i < r.top.Count; i++) Assert.That(r.top[i - 1].score, Is.GreaterThanOrEqualTo(r.top[i].score));
            Assert.IsFalse(r.MakesTopTen(5));
        }

        [Test]
        public void WrapNeverOverflows()
        {
            var lines = Canvas.Wrap("A fairly long sentence with words, plus averyveryverylongwordthatmustbreak, and more.", 12);
            foreach (var l in lines) Assert.That(l.Length, Is.LessThanOrEqualTo(12));
        }

        [Test]
        public void SavedJourneyLoadsBack()
        {
            var p = new MemoryPlatform();
            var app = new App(p);
            app.Game = Outfitted(seed: 9);
            for (int i = 0; i < 20; i++) Sim.Day(app.Game, true);
            app.SaveGame();
            var g = new App(p).LoadGame();
            Assert.IsNotNull(g);
            Assert.AreEqual(app.Game.day, g.day);
            Assert.AreEqual(app.Game.miles, g.miles);
            Assert.AreEqual(app.Game.food, g.food);
            Assert.AreEqual(app.Game.party[2].name, g.party[2].name);
            Assert.AreEqual(app.Game.next, g.next);
            Assert.AreEqual(app.Game.rng.Next(), g.rng.Next(), "the random stream continues where it left off");
        }

        [Test]
        public void SettingsAndRecordsPersist()
        {
            var p = new MemoryPlatform();
            var app = new App(p);
            app.Settings.monitor = 1; app.SaveSettings();
            app.Records.AddScore("Saved", 99999); app.SaveRecords();
            var again = new App(p);
            Assert.AreEqual(1, again.Settings.monitor);
            Assert.AreEqual("Saved", again.Records.top[0].name);
        }

        /// <summary>Random taps and keys for a long time on many seeds: the game must never throw or get stuck without a screen.</summary>
        [Test]
        public void MonkeyCannotBreakTheGame()
        {
            char[] keys = { '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', ' ', Keys.Enter, Keys.Escape, Keys.Back, 'a', 'q', Keys.Up, Keys.Down };
            var screens = new HashSet<string>();
            for (uint seed = 1; seed <= 6; seed++)
            {
                var r = new Rng(seed);
                var app = new App(new MemoryPlatform());
                for (int step = 0; step < 12000; step++)
                {
                    int k = r.Range(0, 10);
                    if (k < 5) app.Tap(r.Range(0, Canvas.W), r.Range(0, Canvas.H));
                    else if (k < 8) app.Key(keys[r.Range(0, keys.Length)]);
                    app.PointerDown = r.Chance(0.3f); app.PointerX = r.Range(0, Canvas.W);
                    app.Tick(r.Range(0.01f, 0.25f));
                    if (step % 3 == 0) app.Draw();
                    Assert.IsNotNull(app.Top, "no screen after step " + step);
                    screens.Add(app.Top.GetType().Name);
                    // Keep the journey moving: on a game, sometimes jump ahead toward the next stop.
                    if (app.Game != null && app.Game.Phase == Phase.Traveling && r.Chance(0.002f)) app.Game.legDone = Math.Max(app.Game.legDone, app.Game.legMiles - 2);
                }
            }
            foreach (var s in new[] { "TitleScene", "JobScene", "StoreScene", "TravelScene", "TrailMenuScene" })
                Assert.IsTrue(screens.Contains(s), "monkey never reached " + s);
        }
    }
}
