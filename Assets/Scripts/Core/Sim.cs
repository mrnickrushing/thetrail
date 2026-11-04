using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    /// <summary>Something that happened on the trail. Stop = the wagon halts so the player reads it.</summary>
    public struct Notice
    {
        public string Text; public bool Stop; public bool Death; public bool Good;
        public Notice(string text, bool stop = true, bool death = false, bool good = false) { Text = text; Stop = stop; Death = death; Good = good; }
        public override string ToString() => Text;
    }

    /// <summary>
    /// The rules of the trail: one call to Day() advances the journey by one day, moving the wagon,
    /// eating food, changing the weather, wearing the party down and rolling for trouble.
    /// </summary>
    public static class Sim
    {
        // ---- Setup ------------------------------------------------------------------------------

        public static int StartingCash(Job j) => j == Job.Banker ? 1600 : j == Job.Carpenter ? 800 : 400;
        public static int Multiplier(Job j) => j == Job.Banker ? 1 : j == Job.Carpenter ? 2 : 3;
        public static string JobName(Job j) => j == Job.Banker ? "banker" : j == Job.Carpenter ? "carpenter" : "farmer";

        public static GameState NewGame(Job job, string[] names, int month, uint seed)
        {
            var g = new GameState { job = job, startMonth = month, seed = seed, rng = new Rng(seed) };
            foreach (var n in names) g.party.Add(new Member { name = n });
            g.cents = StartingCash(job) * 100;
            g.visited.Add(Trail.Start);
            g.Phase = Phase.Store;
            SetLeg(g, Trail.Get(Trail.Start).Next[0]);
            RollWeather(g, true);
            return g;
        }

        public static void SetLeg(GameState g, Route r)
        {
            g.next = r.To; g.legMiles = r.Miles; g.legDone = 0; g.legRaft = r.Raft;
        }

        // ---- Labels -----------------------------------------------------------------------------

        public static string HealthLabel(float h) => h < 30 ? "good" : h < 60 ? "fair" : h < 95 ? "poor" : "very poor";
        public static int HealthLevel(float h) => h < 30 ? 0 : h < 60 ? 1 : h < 95 ? 2 : 3;

        public static string PaceName(Pace p) => p == Pace.Steady ? "steady" : p == Pace.Strenuous ? "strenuous" : "grueling";
        public static string RationName(Rations r) => r == Rations.Filling ? "filling" : r == Rations.Meager ? "meager" : "bare bones";

        public static string WeatherLabel(GameState g)
        {
            string t = g.temp >= 90 ? "very hot" : g.temp >= 78 ? "hot" : g.temp >= 58 ? "warm" : g.temp >= 40 ? "cool" : g.temp >= 25 ? "cold" : "very cold";
            switch (g.sky)
            {
                case Sky.Rain: return t + ", rain";
                case Sky.HeavyRain: return t + ", storms";
                case Sky.Snow: return t + ", snow";
                case Sky.HeavySnow: return "blizzard";
                default: return t;
            }
        }

        public static string AilmentName(Ailment a)
        {
            switch (a)
            {
                case Ailment.Exhaustion: return "exhaustion";
                case Ailment.Fever: return "a fever";
                case Ailment.Measles: return "measles";
                case Ailment.Dysentery: return "dysentery";
                case Ailment.Typhoid: return "typhoid";
                case Ailment.Cholera: return "cholera";
                case Ailment.BrokenArm: return "a broken arm";
                case Ailment.BrokenLeg: return "a broken leg";
                case Ailment.Snakebite: return "a snakebite";
                default: return "nothing";
            }
        }

        public static string PartName(Part p) => p == Part.Wheel ? "wagon wheel" : p == Part.Axle ? "wagon axle" : "wagon tongue";

        public static string DateLabel(GameState g)
        {
            var d = g.Date;
            return MonthName(d.Month) + " " + d.Day + ", " + d.Year;
        }

        public static string MonthName(int m) =>
            new[] { "", "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" }[m];

        // ---- Geography --------------------------------------------------------------------------

        public static Landmark From(GameState g) => Trail.Get(g.Here);
        public static Terrain TerrainNow(GameState g) => From(g).Ahead;
        public static bool OnDryCutoff(GameState g) => g.Here == "southpass" && g.next == "greenriver" && g.legDone > 5;

        /// <summary>Temperature offset for altitude and region, from how far along the road you are.</summary>
        static float ClimateOffset(GameState g, int month)
        {
            int m = g.miles;
            if (m < 650) return 0;
            if (m < 900) return -5;
            if (m < 1300) return -13;                                    // South Pass country, 7,000 ft and up
            if (m < 1650) return month >= 6 && month <= 8 ? 5 : -2;      // Snake River plain: hot summers
            return -7;                                                   // Blue Mountains and the Cascades
        }

        static readonly int[] MonthTemp = { 0, 20, 26, 40, 53, 63, 73, 79, 77, 67, 54, 40, 28 };

        static float RainChance(GameState g, int month)
        {
            var t = TerrainNow(g);
            if (t == Terrain.Desert) return 0.05f;
            if (t == Terrain.Forest) return month >= 9 ? 0.32f : 0.18f;
            if (t == Terrain.Mountains) return 0.14f;
            return month >= 4 && month <= 6 ? 0.22f : 0.12f;
        }

        public static void RollWeather(GameState g, bool first = false)
        {
            var r = g.rng;
            int month = g.Date.Month;
            float anomaly = (r.Float() + r.Float() + r.Float() - 1.5f) * 16f;
            g.tempAnomaly = first ? anomaly : g.tempAnomaly * 0.6f + anomaly * 0.4f;
            g.temp = (int)Math.Round(MonthTemp[month] + ClimateOffset(g, month) + g.tempAnomaly);
            g.sky = Sky.Clear;
            if (r.Chance(RainChance(g, month)))
            {
                bool heavy = r.Chance(0.25f);
                if (g.temp < 34) g.sky = heavy ? Sky.HeavySnow : Sky.Snow;
                else g.sky = heavy ? Sky.HeavyRain : Sky.Rain;
            }
            g.wetness = g.wetness * 0.88f + (g.sky == Sky.Rain ? 1f : g.sky == Sky.HeavyRain ? 2.5f : 0f);
        }

        // ---- Movement ---------------------------------------------------------------------------

        public static int BaseMiles(Pace p) => p == Pace.Steady ? 16 : p == Pace.Strenuous ? 20 : 25;

        static float TerrainFactor(Terrain t) =>
            t == Terrain.Plains ? 1f : t == Terrain.Hills ? 0.85f : t == Terrain.Desert ? 0.9f : t == Terrain.Mountains ? 0.68f : 0.75f;

        public static float OxenFactor(GameState g)
        {
            float eff = g.oxen - g.oxenWeak * 0.5f;
            if (eff <= 0) return 0;
            return eff >= 6 ? 1f : 0.4f + 0.6f * eff / 6f;
        }

        public static int MilesToday(GameState g)
        {
            if (g.oxen <= 0 || g.broken != Part.None) return 0;
            float m = BaseMiles(g.pace) * TerrainFactor(TerrainNow(g)) * OxenFactor(g);
            if (g.sky == Sky.HeavySnow) m *= 0.4f; else if (g.sky == Sky.Snow) m *= 0.7f; else if (g.sky == Sky.HeavyRain) m *= 0.8f;
            int sick = 0; foreach (var p in g.party) if (p.alive && p.ailment != Ailment.None) sick++;
            m *= 1f - 0.04f * sick;
            m *= g.rng.Range(0.88f, 1.12f);
            return Math.Max(1, (int)Math.Round(m));
        }

        // ---- The day ----------------------------------------------------------------------------

        /// <summary>
        /// Advances one day. travel=false is a rest day (or a day stuck in camp). Returns what
        /// happened; check g.legDone &gt;= g.legMiles afterwards to see if you arrived.
        /// </summary>
        public static List<Notice> Day(GameState g, bool travel)
        {
            var log = new List<Notice>();
            var r = g.rng;
            g.day++;
            RollWeather(g);

            bool moving = travel && g.lostDays == 0 && g.broken == Part.None && g.oxen > 0;
            if (travel && g.lostDays > 0) g.lostDays--;

            if (moving)
            {
                int m = Math.Min(MilesToday(g), g.legMiles - g.legDone);
                g.legDone += m; g.miles += m;
            }

            Eat(g, log);
            Wear(g, moving);
            Illness(g, log, !moving);

            if (g.Alive == 0) return log;

            if (travel && g.broken != Part.None) TryFixInCamp(g, log);
            else if (travel && g.oxen <= 0) StrandedOxen(g, log);
            else if (moving && g.legDone < g.legMiles) RandomEvent(g, log);
            OxenRecover(g, log, !moving);

            if (g.sky == Sky.HeavySnow && moving && r.Chance(0.35f))
            {
                int lose = r.Range(1, 3);
                g.lostDays += lose;
                log.Add(new Notice("A blizzard. The trail is buried in snow. You lose " + Days(lose) + "."));
            }
            return log;
        }

        static string Days(int n) => n == 1 ? "1 day" : n + " days";

        public static int FoodPerPerson(Rations r) => r == Rations.Filling ? 3 : r == Rations.Meager ? 2 : 1;

        static void Eat(GameState g, List<Notice> log)
        {
            int need = g.Alive * FoodPerPerson(g.rations);
            bool had = g.food > 0;
            g.food = Math.Max(0, g.food - need);
            if (had && g.food == 0) log.Add(new Notice("You have run out of food."));
        }

        /// <summary>The daily hardship: pace, rations, weather and sickness wear the party down; rest and time heal it.</summary>
        static void Wear(GameState g, bool moving)
        {
            float add = 0.4f;
            if (moving) add += g.pace == Pace.Steady ? 0.6f : g.pace == Pace.Strenuous ? 2.0f : 4.6f;
            if (g.food <= 0) add += 7f;
            else add += g.rations == Rations.Filling ? 0f : g.rations == Rations.Meager ? 1.5f : 3.6f;

            float warmth = Math.Min(1f, g.clothing / Math.Max(1f, 2f * g.Alive));
            if (g.temp >= 90) add += 1.6f;
            else if (g.temp >= 78) add += 0.5f;
            else if (g.temp < 25) add += 5f * (1f - 0.7f * warmth);
            else if (g.temp < 40) add += 1.8f * (1f - 0.7f * warmth);

            if (g.sky == Sky.Rain) add += 0.3f;
            else if (g.sky == Sky.HeavyRain) add += 0.8f;
            else if (g.sky == Sky.Snow) add += 0.7f;
            else if (g.sky == Sky.HeavySnow) add += 1.6f;

            foreach (var p in g.party) if (p.alive && p.ailment != Ailment.None) add += 1f;

            g.hardship = g.hardship * (moving ? 0.9f : 0.8f) + add;
        }

        static readonly Ailment[] Sickness = { Ailment.Exhaustion, Ailment.Fever, Ailment.Measles, Ailment.Dysentery, Ailment.Typhoid, Ailment.Cholera };
        static readonly int[] SicknessWeight = { 22, 18, 10, 18, 12, 8 };

        public static float DeathRate(Ailment a)
        {
            switch (a)
            {
                case Ailment.Exhaustion: return 0.003f;
                case Ailment.Fever: return 0.006f;
                case Ailment.Measles: return 0.007f;
                case Ailment.Dysentery: return 0.010f;
                case Ailment.Typhoid: return 0.014f;
                case Ailment.Cholera: return 0.045f;
                case Ailment.BrokenArm: return 0.0005f;
                case Ailment.BrokenLeg: return 0.003f;
                case Ailment.Snakebite: return 0.02f;
                default: return 0;
            }
        }

        static int Duration(Ailment a, Rng r)
        {
            switch (a)
            {
                case Ailment.Exhaustion: return r.Range(4, 8);
                case Ailment.Fever: return r.Range(6, 11);
                case Ailment.Measles: return r.Range(8, 13);
                case Ailment.Dysentery: return r.Range(8, 15);
                case Ailment.Typhoid: return r.Range(10, 17);
                case Ailment.Cholera: return r.Range(4, 8);
                case Ailment.BrokenArm: return r.Range(14, 22);
                case Ailment.BrokenLeg: return r.Range(20, 31);
                case Ailment.Snakebite: return r.Range(5, 9);
                default: return 0;
            }
        }

        public static void Afflict(GameState g, Member m, Ailment a)
        {
            m.ailment = a; m.sickDays = Duration(a, g.rng);
        }

        static void Illness(GameState g, List<Notice> log, bool resting)
        {
            var r = g.rng;
            float h = g.hardship;
            float onset = 0.0025f + h * 0.00022f;
            if (g.pace == Pace.Grueling) onset += 0.002f;
            float mult = (0.35f + (float)Math.Pow(h / 32f, 1.6)) * (resting ? 0.5f : 1f) * (g.food <= 0 ? 1.5f : 1f);

            foreach (var m in g.party)
            {
                if (!m.alive) continue;
                if (m.ailment == Ailment.None)
                {
                    if (r.Chance(onset))
                    {
                        int pick = 0, roll = r.Range(0, 88), acc = 0;
                        for (int i = 0; i < Sickness.Length; i++) { acc += SicknessWeight[i]; if (roll < acc) { pick = i; break; } }
                        var a = Sickness[pick];
                        if (a == Ailment.Exhaustion && g.pace == Pace.Steady && r.Chance(0.5f)) a = Ailment.Fever;
                        Afflict(g, m, a);
                        log.Add(new Notice(m.name + " has " + AilmentName(a) + "."));
                    }
                    else if (h > 130 && r.Chance(0.02f + (h - 130) * 0.0005f))
                        Die(g, m, g.food <= 0 ? "starvation" : "exhaustion", log);
                    continue;
                }

                if (r.Chance(DeathRate(m.ailment) * mult)) { Die(g, m, AilmentName(m.ailment).Replace("a ", ""), log); continue; }
                m.sickDays -= resting ? 2 : 1;
                if (m.sickDays <= 0)
                {
                    m.ailment = Ailment.None; m.sickDays = 0;
                    log.Add(new Notice(m.name + " is well again.", true, false, true));
                }
            }
        }

        public static void Die(GameState g, Member m, string cause, List<Notice> log)
        {
            m.alive = false; m.cause = cause; m.ailment = Ailment.None; g.deaths++;
            log.Add(new Notice(m.name + " died of " + cause + ".", true, true));
        }

        static Member RandomAlive(GameState g)
        {
            var alive = g.party.FindAll(p => p.alive);
            return alive.Count == 0 ? null : alive[g.rng.Range(0, alive.Count)];
        }

        static Member RandomHealthy(GameState g)
        {
            var ok = g.party.FindAll(p => p.alive && p.ailment == Ailment.None);
            return ok.Count == 0 ? null : ok[g.rng.Range(0, ok.Count)];
        }

        static void OxenRecover(GameState g, List<Notice> log, bool resting)
        {
            if (g.oxenWeak <= 0) return;
            if (g.rng.Chance(resting ? 0.25f : 0.06f)) { g.oxenWeak--; log.Add(new Notice("An injured ox is pulling well again.", true, false, true)); }
        }

        static void TryFixInCamp(GameState g, List<Notice> log)
        {
            float p = g.job == Job.Carpenter ? 0.4f : 0.22f;
            if (g.rng.Chance(p))
            {
                log.Add(new Notice("You fashioned a new " + PartName(g.broken) + " from a tree by the trail. You can go on.", true, false, true));
                g.broken = Part.None;
            }
            else log.Add(new Notice("You spend the day trying to make a new " + PartName(g.broken) + ". It isn't right yet.", false));
        }

        static void StrandedOxen(GameState g, List<Notice> log)
        {
            var r = g.rng;
            if (!r.Chance(0.3f)) { log.Add(new Notice("You wait by the trail with no oxen to pull the wagon.", false)); return; }
            if (g.cents >= 2500) { g.cents -= 2500; g.oxen += 1; log.Add(new Notice("A passing family sells you an ox for $25.", true, false, true)); }
            else { g.oxen += 1; log.Add(new Notice("A passing family takes pity and gives you an old ox. It is slow, but it pulls.", true, false, true)); }
        }

        // ---- Random trouble (and luck) ----------------------------------------------------------

        static string People(GameState g)
        {
            int m = g.miles;
            if (m < 400) return g.rng.Chance(0.5f) ? "Pawnee" : "Kansa";
            if (m < 900) return g.rng.Chance(0.5f) ? "Lakota" : "Cheyenne";
            if (m < 1600) return g.rng.Chance(0.7f) ? "Shoshone" : "Bannock";
            return g.rng.Chance(0.5f) ? "Cayuse" : "Nez Perce";
        }

        static void RandomEvent(GameState g, List<Notice> log)
        {
            var r = g.rng;
            var t = TerrainNow(g);
            int month = g.Date.Month;
            var who = RandomAlive(g);
            float roll = r.Float();
            float acc = 0;
            bool Hit(float p) { acc += p; return roll < acc; }

            if (OnDryCutoff(g) && g.legDone < g.legMiles - 5 && r.Chance(0.35f))
            {
                g.hardship += 5;
                log.Add(new Notice("No water on the cutoff today. The oxen are suffering.", false));
                if (r.Chance(0.12f) && g.oxen > 1) { g.oxen--; log.Add(new Notice("One of the oxen dropped from thirst.")); }
                return;
            }

            float breakP = t == Terrain.Mountains || t == Terrain.Forest ? 0.022f : 0.014f;
            if (Hit(breakP)) { BreakPart(g, log); return; }
            if (Hit(g.job == Job.Farmer ? 0.004f : 0.007f) && g.oxen > g.oxenWeak)
            {
                g.oxenWeak++;
                log.Add(new Notice("One of your oxen is injured and limping."));
                return;
            }
            if (Hit((g.job == Job.Farmer ? 0.0025f : 0.005f) * (t == Terrain.Desert ? 2f : 1f)) && g.oxen > 0)
            {
                g.oxen--; g.oxenWeak = Math.Min(g.oxenWeak, g.oxen);
                log.Add(new Notice(t == Terrain.Desert ? "An ox drank alkali water and died." : "One of the oxen wandered off in the night and is gone."));
                return;
            }
            if (Hit(0.006f)) { Thief(g, log); return; }
            if (Hit(0.007f))
            {
                int d = r.Range(1, 5); g.lostDays += d;
                log.Add(new Notice("You lost the trail. It takes " + Days(d) + " to find it again."));
                return;
            }
            if (Hit(t == Terrain.Hills || t == Terrain.Mountains || t == Terrain.Forest ? 0.006f : 0.0f))
            {
                g.lostDays += 1;
                log.Add(new Notice("Heavy fog. You lose a day."));
                return;
            }
            if (Hit(t == Terrain.Plains && month >= 4 && month <= 8 ? 0.004f : 0f))
            {
                int lost = Math.Min(g.food, g.food / 20 + 5); g.food -= lost; g.lostDays += 1;
                log.Add(new Notice("A hailstorm! Stones the size of eggs. You lose a day" + (lost > 0 ? " and " + lost + " pounds of food" : "") + "."));
                return;
            }
            if (Hit(t == Terrain.Desert || t == Terrain.Plains ? 0.009f : 0.004f))
            {
                g.hardship += 8;
                log.Add(new Notice("Bad water. Several of you are feeling poorly."));
                return;
            }
            if (Hit(t == Terrain.Desert ? 0.01f : 0.004f))
            {
                g.hardship += 5;
                log.Add(new Notice("Very little water today."));
                return;
            }
            if (Hit(0.006f))
            {
                if (g.oxen > g.oxenWeak && r.Chance(0.3f)) g.oxenWeak++;
                log.Add(new Notice("Very little grass for the oxen. They grow thin."));
                return;
            }
            if (Hit(month >= 5 && month <= 9 && t != Terrain.Desert ? 0.01f : 0f))
            {
                int lbs = r.Range(15, 46); g.food += lbs;
                log.Add(new Notice("You found wild " + r.Pick(new[] { "berries", "currants", "onions", "plums" }) + ". You gather " + lbs + " pounds of food.", true, false, true));
                return;
            }
            if (Hit(0.005f)) { AbandonedWagon(g, log); return; }
            if (Hit(0.004f) && who != null)
            {
                int d = r.Range(1, 4); g.lostDays += d;
                log.Add(new Notice(who.name + " wandered from camp and was lost. You spend " + Days(d) + " searching. Found, safe."));
                return;
            }
            if (Hit((t == Terrain.Plains || t == Terrain.Desert) && month >= 4 && month <= 9 ? 0.0035f : 0f))
            {
                var m = RandomHealthy(g);
                if (m != null) { Afflict(g, m, Ailment.Snakebite); log.Add(new Notice(m.name + " was bitten by a rattlesnake.")); return; }
            }
            if (Hit(0.004f))
            {
                var m = RandomHealthy(g);
                if (m != null)
                {
                    var a = r.Chance(0.55f) ? Ailment.BrokenArm : Ailment.BrokenLeg;
                    Afflict(g, m, a);
                    log.Add(new Notice(m.name + (a == Ailment.BrokenArm ? " fell from the wagon and broke an arm." : " was kicked by an ox and has a broken leg.")));
                    return;
                }
            }
            if (Hit(0.003f)) { Fire(g, log); return; }
            if (Hit(0.005f))
            {
                string p = People(g);
                if (r.Chance(0.5f))
                {
                    int lbs = r.Range(25, 61); g.food += lbs;
                    log.Add(new Notice("A " + p + " family trades you " + lbs + " pounds of " + (g.miles > 1300 ? "dried salmon" : g.miles > 900 ? "dried meat and roots" : "corn and squash") + " for a few small things.", true, false, true));
                }
                else
                {
                    g.hardship = Math.Max(0, g.hardship - 10);
                    log.Add(new Notice("A " + p + " guide shows you a better campsite with good grass and clean water.", true, false, true));
                }
                return;
            }
            if (Hit(0.004f))
            {
                log.Add(new Notice(r.Pick(new[]
                {
                    "You pass a wagon heading back east. They say they've seen enough of the West.",
                    "You pass a lonely grave beside the trail. Someone has kept the stones in place.",
                    "A herd of buffalo blackens the hills to the north. The ground trembles.",
                    "Another wagon party camps with you tonight. There is fiddle music and dancing.",
                    "A letter for home goes east with a passing rider.",
                }), true, false, true));
            }
        }

        public static void BreakPart(GameState g, List<Notice> log)
        {
            var r = g.rng;
            int roll = r.Range(0, 100);
            var p = roll < 45 ? Part.Wheel : roll < 80 ? Part.Axle : Part.Tongue;
            string name = PartName(p);
            if (r.Chance(g.job == Job.Carpenter ? 0.55f : 0.3f))
            {
                g.lostDays += 1;
                log.Add(new Notice("A " + name + " broke. You were able to repair it, losing a day."));
            }
            else if (g.Spares(p) > 0)
            {
                g.AddSpare(p, -1);
                log.Add(new Notice("A " + name + " broke. You replaced it with a spare."));
            }
            else
            {
                g.broken = p;
                log.Add(new Notice("A " + name + " broke and you have no spare. You can't go on until it's mended or replaced. Try trading for one."));
            }
        }

        static void Thief(GameState g, List<Notice> log)
        {
            var r = g.rng;
            int k = r.Range(0, 4);
            if (k == 0 && g.food > 20) { int n = Math.Max(10, g.food * r.Range(8, 20) / 100); g.food -= n; log.Add(new Notice("A thief came in the night and stole " + n + " pounds of food.")); }
            else if (k == 1 && g.bullets > 20) { int n = Math.Max(10, g.bullets * r.Range(20, 45) / 100); g.bullets -= n; log.Add(new Notice("A thief came in the night and stole " + n + " bullets.")); }
            else if (k == 2 && g.clothing > 0) { int n = Math.Min(g.clothing, r.Range(1, 3)); g.clothing -= n; log.Add(new Notice("A thief came in the night and stole " + (n == 1 ? "a set" : n + " sets") + " of clothing.")); }
            else if (g.oxen > 2 && r.Chance(0.5f)) { g.oxen--; g.oxenWeak = Math.Min(g.oxenWeak, g.oxen); log.Add(new Notice("A thief came in the night and led away one of your oxen.")); }
            else log.Add(new Notice("A thief crept into camp, but the dog barked and he ran off empty-handed.", true, false, true));
        }

        static void Fire(GameState g, List<Notice> log)
        {
            var r = g.rng;
            var lost = new List<string>();
            int f = Math.Min(g.food, r.Range(10, 50)); if (f > 0) { g.food -= f; lost.Add(f + " pounds of food"); }
            int c = Math.Min(g.clothing, r.Range(0, 3)); if (c > 0) { g.clothing -= c; lost.Add(c + (c == 1 ? " set" : " sets") + " of clothing"); }
            int b = Math.Min(g.bullets, r.Range(0, 30)); if (b > 0) { g.bullets -= b; lost.Add(b + " bullets"); }
            log.Add(new Notice(lost.Count == 0 ? "A fire broke out in the wagon, but you put it out quickly."
                : "A fire broke out in the wagon. You lost " + Join(lost) + "."));
        }

        static void AbandonedWagon(GameState g, List<Notice> log)
        {
            var r = g.rng;
            int k = r.Range(0, 5);
            string found;
            if (k == 0) { int n = r.Range(20, 80); g.food += n; found = n + " pounds of food"; }
            else if (k == 1) { int n = r.Range(1, 3); g.clothing += n; found = n == 1 ? "a set of clothing" : n + " sets of clothing"; }
            else if (k == 2) { int n = r.Range(10, 40); g.bullets += n; found = n + " bullets"; }
            else if (k == 3) { var p = (Part)r.Range(1, 4); g.AddSpare(p, 1); found = "a spare " + PartName(p); }
            else { log.Add(new Notice("You find an abandoned wagon. It has been picked clean.", true)); return; }
            log.Add(new Notice("You find an abandoned wagon. Inside: " + found + ".", true, false, true));
        }

        public static string Join(List<string> items)
        {
            if (items.Count == 1) return items[0];
            if (items.Count == 2) return items[0] + " and " + items[1];
            return string.Join(", ", items.GetRange(0, items.Count - 1)) + " and " + items[items.Count - 1];
        }

        // ---- Arriving ---------------------------------------------------------------------------

        public static bool AtNext(GameState g) => g.legDone >= g.legMiles;

        /// <summary>Marks the next landmark reached and sets up the following leg (when there's only one road on).</summary>
        public static Landmark Arrive(GameState g)
        {
            var l = Trail.Get(g.next);
            g.visited.Add(l.Id);
            if (l.Kind == Kind.End) { g.Phase = Phase.Won; return l; }
            if (l.Kind == Kind.River) g.riverDepthTenths = RiverDepth(g, l);
            g.Phase = l.Kind == Kind.River ? Phase.River : l.Next.Length > 1 ? Phase.Branch : Phase.Arrived;
            if (l.Next.Length == 1) SetLeg(g, l.Next[0]);
            return l;
        }

        public static void ChooseRoute(GameState g, Route r)
        {
            if (r.Toll > 0) g.cents = Math.Max(0, g.cents - r.Toll * 100);
            SetLeg(g, r);
            g.Phase = r.Raft ? Phase.Raft : Phase.Traveling;
        }

        // ---- Rivers -----------------------------------------------------------------------------

        /// <summary>River depth in tenths of a foot: high in spring, low by late summer, up after rain.</summary>
        public static int RiverDepth(GameState g, Landmark l)
        {
            int month = g.Date.Month;
            float season = month <= 5 ? 0.85f : month == 6 ? 0.7f : month == 7 ? 0.45f : 0.2f;
            float d = l.DepthLo + (l.DepthHi - l.DepthLo) * season + g.wetness * 0.18f + g.rng.Range(-0.5f, 0.5f);
            return Math.Max(10, (int)Math.Round(Math.Max(l.DepthLo * 0.7f, d) * 10));
        }

        public static int RiverWidth(GameState g, Landmark l) => (int)(l.RiverWidth * (0.85f + g.riverDepthTenths / 400f)) / 5 * 5;

        public enum Crossing { Ford, Float, Ferry }

        public struct CrossResult { public List<Notice> Log; public bool Swamped; public bool Capsized; public int DaysLost; }

        /// <summary>Waits a day at the river. Depth drifts with the weather.</summary>
        public static List<Notice> WaitAtRiver(GameState g)
        {
            var log = Day(g, false);
            var l = Trail.Get(g.Here);
            int target = RiverDepth(g, l);
            g.riverDepthTenths = (g.riverDepthTenths + target) / 2 - g.rng.Range(0, 3);
            g.riverDepthTenths = Math.Max(10, g.riverDepthTenths);
            return log;
        }

        public static CrossResult Cross(GameState g, Crossing how)
        {
            var r = g.rng;
            var l = Trail.Get(g.Here);
            float depth = g.riverDepthTenths / 10f;
            var res = new CrossResult { Log = new List<Notice>() };
            float risk;
            if (how == Crossing.Ferry)
            {
                g.cents = Math.Max(0, g.cents - l.Ferry * 100);
                int wait = r.Range(0, 3);
                for (int i = 0; i < wait; i++) res.Log.AddRange(Day(g, false));
                res.DaysLost = wait;
                risk = 0.02f;
                if (wait > 0) res.Log.Insert(0, new Notice("You waited " + Days(wait) + " for the ferry.", true));
            }
            else if (how == Crossing.Ford)
            {
                risk = depth < 2.5f ? 0.03f : depth < 3.2f ? 0.35f : depth < 4.5f ? 0.75f : 0.95f;
                risk += l.Swift * 0.1f;
            }
            else
            {
                risk = 0.05f + l.Swift * 0.15f + (l.RiverWidth > 600 ? 0.04f : 0);
                if (depth < 1.5f)
                {
                    g.day++; res.DaysLost = 1;
                    res.Log.Add(new Notice("The river is too shallow to float. The wagon dragged and stuck on the bottom. You lose a day, but get across."));
                    return Finish(g, res);
                }
            }

            if (!r.Chance(risk))
            {
                res.Log.Add(new Notice(how == Crossing.Ferry ? "The ferry carries you safely across." : "You made it safely across the river.", true, false, true));
                return Finish(g, res);
            }

            // Something went wrong. Fords in middling water swamp the wagon; deep water or a bad float tips it.
            bool tip = how == Crossing.Float || depth >= 3.2f;
            if (!tip || how == Crossing.Ferry)
            {
                res.Swamped = true;
                int f = Math.Min(g.food, g.food / 10 + r.Range(5, 20));
                g.food -= f;
                g.hardship += 6;
                res.Log.Add(new Notice("The wagon was swamped. Everything is soaked." + (f > 0 ? " You lost " + f + " pounds of food" : "") + (f > 0 ? "." : "") + " You lose a day drying out."));
                g.day++; res.DaysLost++;
                return Finish(g, res);
            }

            res.Capsized = true;
            var lost = new List<string>();
            int fl = Math.Min(g.food, g.food * r.Range(25, 60) / 100); if (fl > 0) { g.food -= fl; lost.Add(fl + " pounds of food"); }
            int cl = Math.Min(g.clothing, g.clothing * r.Range(20, 60) / 100); if (cl > 0) { g.clothing -= cl; lost.Add(cl + (cl == 1 ? " set" : " sets") + " of clothing"); }
            int bl = Math.Min(g.bullets, g.bullets * r.Range(20, 70) / 100); if (bl > 0) { g.bullets -= bl; lost.Add(bl + " bullets"); }
            if (g.oxen > 1 && r.Chance(0.4f)) { int o = r.Range(1, Math.Min(3, g.oxen)); g.oxen -= o; g.oxenWeak = Math.Min(g.oxenWeak, g.oxen); lost.Add(o == 1 ? "an ox" : o + " oxen"); }
            foreach (Part p in new[] { Part.Wheel, Part.Axle, Part.Tongue })
                if (g.Spares(p) > 0 && r.Chance(0.4f)) { g.AddSpare(p, -1); lost.Add("a spare " + PartName(p)); }
            res.Log.Add(new Notice("The wagon tipped over in the current!" + (lost.Count > 0 ? " You lost " + Join(lost) + "." : " Somehow nothing was lost.")));
            foreach (var m in g.party)
                if (m.alive && r.Chance(0.06f + l.Swift * 0.08f)) Die(g, m, "drowning", res.Log);
            g.day++; res.DaysLost++;
            return Finish(g, res);
        }

        static CrossResult Finish(GameState g, CrossResult res)
        {
            g.Phase = g.Alive == 0 ? Phase.Lost : Phase.Traveling;
            return res;
        }

        // ---- Resting, trading -------------------------------------------------------------------

        public static List<Notice> Rest(GameState g, int days)
        {
            var log = new List<Notice>();
            for (int i = 0; i < days && g.Alive > 0; i++) log.AddRange(Day(g, false));
            return log;
        }

        public enum Good { Oxen, Food, Clothing, Bullets, Wheel, Axle, Tongue }

        public static string GoodName(Good k, int n)
        {
            switch (k)
            {
                case Good.Oxen: return n == 1 ? "1 ox" : n + " oxen";
                case Good.Food: return n + " pounds of food";
                case Good.Clothing: return n == 1 ? "1 set of clothing" : n + " sets of clothing";
                case Good.Bullets: return n + " bullets";
                case Good.Wheel: return n == 1 ? "a wagon wheel" : n + " wagon wheels";
                case Good.Axle: return n == 1 ? "a wagon axle" : n + " wagon axles";
                default: return n == 1 ? "a wagon tongue" : n + " wagon tongues";
            }
        }

        public static int Have(GameState g, Good k)
        {
            switch (k)
            {
                case Good.Oxen: return g.oxen;
                case Good.Food: return g.food;
                case Good.Clothing: return g.clothing;
                case Good.Bullets: return g.bullets;
                case Good.Wheel: return g.wheels;
                case Good.Axle: return g.axles;
                default: return g.tongues;
            }
        }

        public static void Give(GameState g, Good k, int n)
        {
            switch (k)
            {
                case Good.Oxen: g.oxen += n; g.oxenWeak = Math.Min(g.oxenWeak, g.oxen); break;
                case Good.Food: g.food += n; break;
                case Good.Clothing: g.clothing += n; break;
                case Good.Bullets: g.bullets += n; break;
                case Good.Wheel: g.wheels += n; break;
                case Good.Axle: g.axles += n; break;
                default: g.tongues += n; break;
            }
            if (g.broken != Part.None && g.Spares(g.broken) > 0) { g.AddSpare(g.broken, -1); g.broken = Part.None; }
        }

        /// <summary>Rough worth in cents, for building fair-ish trades.</summary>
        static int Worth(Good k) => k == Good.Oxen ? 2000 : k == Good.Food ? 20 : k == Good.Clothing ? 1000 : k == Good.Bullets ? 10 : 1000;

        [Serializable]
        public class Offer { public Good want; public int wantN; public Good give; public int giveN; }

        /// <summary>Someone offers a trade, or nobody does (null). Takes a day.</summary>
        public static Offer MakeOffer(GameState g)
        {
            var r = g.rng;
            if (!r.Chance(0.75f)) return null;
            var goods = (Good[])Enum.GetValues(typeof(Good));
            // What they give: what you lack is likelier, especially a broken part.
            Good give;
            if (g.broken != Part.None && r.Chance(0.6f)) give = g.broken == Part.Wheel ? Good.Wheel : g.broken == Part.Axle ? Good.Axle : Good.Tongue;
            else if (g.oxen < 3 && r.Chance(0.5f)) give = Good.Oxen;
            else give = goods[r.Range(0, goods.Length)];
            int giveN = give == Good.Food ? r.Range(5, 21) * 10 : give == Good.Bullets ? r.Range(2, 6) * 10 : give == Good.Oxen || give == Good.Clothing ? r.Range(1, 3) : 1;

            for (int tries = 0; tries < 12; tries++)
            {
                var want = goods[r.Range(0, goods.Length)];
                if (want == give) continue;
                int wantN = (int)Math.Ceiling(Worth(give) * giveN * r.Range(1.1f, 1.6f) / Worth(want));
                if (want == Good.Food) wantN = (wantN + 9) / 10 * 10;
                if (want == Good.Bullets) wantN = (wantN + 9) / 10 * 10;
                if (Have(g, want) >= wantN && (want != Good.Oxen || g.oxen - wantN >= 2)) return new Offer { want = want, wantN = wantN, give = give, giveN = giveN };
            }
            return null;
        }

        public static void Accept(GameState g, Offer o)
        {
            Give(g, o.want, -o.wantN);
            Give(g, o.give, o.giveN);
        }

        // ---- Store ------------------------------------------------------------------------------

        public static readonly int[] PriceCents = { 4000, 20, 1000, 200, 1000, 1000, 1000 }; // per yoke, lb, set, box of 20, part
        public static int Price(GameState g, Good k) => (int)Math.Round(PriceCents[(int)k] * Trail.Get(g.Here).Prices / 5f) * 5;

        // ---- Score ------------------------------------------------------------------------------

        public struct ScoreLine { public string What; public int Count; public int Points; }

        public static List<ScoreLine> ScoreLines(GameState g)
        {
            var lines = new List<ScoreLine>();
            int[] byHealth = { 600, 450, 300, 150 };
            int alive = g.Alive;
            lines.Add(new ScoreLine { What = "People in " + HealthLabel(g.hardship) + " health", Count = alive, Points = alive * byHealth[HealthLevel(g.hardship)] });
            lines.Add(new ScoreLine { What = "Wagon", Count = 1, Points = 100 });
            lines.Add(new ScoreLine { What = "Oxen", Count = g.oxen, Points = g.oxen * 5 });
            int parts = g.wheels + g.axles + g.tongues;
            lines.Add(new ScoreLine { What = "Spare wagon parts", Count = parts, Points = parts * 3 });
            lines.Add(new ScoreLine { What = "Sets of clothing", Count = g.clothing, Points = g.clothing * 3 });
            lines.Add(new ScoreLine { What = "Bullets", Count = g.bullets, Points = g.bullets / 40 });
            lines.Add(new ScoreLine { What = "Pounds of food", Count = g.food, Points = g.food / 20 });
            lines.Add(new ScoreLine { What = "Cash", Count = g.Dollars, Points = g.Dollars / 4 });
            return lines;
        }

        public static int Score(GameState g)
        {
            int s = 0; foreach (var l in ScoreLines(g)) s += l.Points;
            return s * Multiplier(g.job);
        }

        public static string Rating(int score) => score < 2500 ? "Tenderfoot" : score < 6000 ? "Trailhand" : "Wagon Master";
    }
}
