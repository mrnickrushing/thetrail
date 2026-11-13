using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    public enum Tone { Info, Good, Bad, Death }

    /// <summary>Something that happened. Stop = show it to the player before going on.</summary>
    public struct Notice
    {
        public string Text; public Tone Tone; public bool Stop;
        public Notice(string text, Tone tone = Tone.Info, bool stop = true) { Text = text; Tone = tone; Stop = stop; }
        public override string ToString() => Text;
    }

    /// <summary>
    /// The rules of the road. Day() advances one day: weather, miles, water, food, the team, health,
    /// the wagon and the company's spirits. Everything random comes from the save's own Rng, so a
    /// journey replays the same from the same seed and choices.
    /// </summary>
    public static partial class Sim
    {
        public const int LeaderStake = 400;
        public const int MaxPartners = 4;
        public const int DollarsPerOunce = 16;
        public const int SurvivorBonus = 500;

        // ---- Setup ------------------------------------------------------------------------------

        public static GameState NewGame(string leaderName, int startDoy, uint seed)
        {
            var g = new GameState { seed = seed, rng = new Rng(seed), startDoy = startDoy };
            g.company.Add(new Hand { name = string.IsNullOrWhiteSpace(leaderName) ? "Captain" : leaderName.Trim(), home = "St. Joseph", trade = Trade.Greenhorn, temper = Temper.Steady, stake = LeaderStake });
            g.visited.Add(Route.Start);
            g.Phase = Phase.Hire;
            g.freshDay = 0;
            RollWeather(g);
            return g;
        }

        public static int Pot(GameState g) { int s = 0; foreach (var h in g.company) s += h.stake; return s; }
        public static int Partners(GameState g) => g.company.Count - 1;

        /// <summary>Hiring is done: the pot becomes the company's cash and outfitting begins.</summary>
        public static void BeginOutfit(GameState g, TeamKind team)
        {
            g.cents = Pot(g) * 100;
            g.Team = team;
            g.Phase = Phase.Outfit;
            SetLeg(g, Route.Get(Route.Start).Next[0]);
            Log(g, "The company of " + g.Leader.name + " formed at St. Joseph with $" + Pot(g) + " in the pot.");
        }

        public static void SetLeg(GameState g, Leg l)
        {
            g.next = l.To; g.legMiles = l.Miles; g.legDone = 0;
        }

        public static Leg CurrentLeg(GameState g)
        {
            foreach (var l in g.HereStop.Next) if (l.To == g.next) return l;
            var n = g.HereStop.Next;
            return n.Length > 0 ? n[0] : new Leg(g.Here, 0, Ground.Plains, 0.5f);
        }

        /// <summary>Ready to roll: at least the minimum team and some food.</summary>
        public static string CannotLeave(GameState g)
        {
            if (g.animals < Content.MinAnimals) return "You need at least " + Content.MinAnimals + " " + (g.Team == TeamKind.Mules ? "mules" : "oxen") + " to pull the wagon.";
            if (g.food < 100) return "Buy some food first.";
            if (Load(g) > Capacity(g) * 1.35f) return "The wagon is far too heavy for your team. Buy more animals or less weight.";
            return null;
        }

        // ---- Load -------------------------------------------------------------------------------

        public static int Load(GameState g)
        {
            float w = Content.BaseLoad + g.food + g.water * 8;
            foreach (Good k in Enum.GetValues(typeof(Good)))
                if (k != Good.Food && k != Good.Animals) w += Content.Have(g, k) * Content.Weight(k);
            return (int)w;
        }

        public static int Capacity(GameState g)
        {
            int per = g.Team == TeamKind.Mules ? 380 : 470;
            int max = g.Team == TeamKind.Mules ? Content.WagonMules : Content.WagonOxen;
            return Math.Min(max, g.animals * per);
        }

        // ---- Calendar and weather ---------------------------------------------------------------

        static readonly string[] Months = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
        public static string MonthName(int m) => Months[Math.Max(1, Math.Min(12, m)) - 1];
        public static string DateLabel(GameState g) => DateLabel(g.Doy);
        public static string DateLabel(int doy) { var d = new DateTime(1849, 1, 1).AddDays(doy - 1); return MonthName(d.Month) + " " + d.Day; }
        public static string ShortDate(int doy) { var d = new DateTime(1849, 1, 1).AddDays(doy - 1); return MonthName(d.Month).Substring(0, 3) + " " + d.Day; }

        public static string SkyName(Sky s)
        {
            switch (s)
            {
                case Sky.Hot: return "hot";
                case Sky.Rain: return "rain";
                case Sky.Storm: return "thunderstorm";
                case Sky.Cold: return "cold";
                case Sky.Snow: return "snow";
                default: return "fair";
            }
        }

        public static Ground GroundNow(GameState g) => CurrentLeg(g).Ground;

        public static void RollWeather(GameState g)
        {
            var r = g.rng;
            int doy = g.Doy;
            var ground = g.next == null ? Ground.Prairie : GroundNow(g);
            bool high = ground == Ground.Mountains || ground == Ground.Forest;
            // Mean temperature through the year, colder up high.
            float mean = 50 + 32 * (float)Math.Sin((doy - 105) / 365f * 2 * Math.PI);
            if (high) mean -= 14;
            if (ground == Ground.Desert) mean += 8;
            g.temp = (int)(mean + r.Range(-10f, 10f));
            float wet = ground == Ground.Prairie || ground == Ground.Plains ? (doy < 190 ? 0.28f : 0.15f) : ground == Ground.Desert || ground == Ground.Sage ? 0.06f : 0.18f;
            if (g.temp <= 32 && r.Chance(0.5f)) g.Sky = Sky.Snow;
            else if (r.Chance(wet)) g.Sky = r.Chance(0.35f) && g.temp > 60 ? Sky.Storm : Sky.Rain;
            else if (g.temp >= 90) g.Sky = Sky.Hot;
            else if (g.temp <= 40) g.Sky = Sky.Cold;
            else g.Sky = Sky.Fair;
        }

        // ---- Grass and water --------------------------------------------------------------------

        /// <summary>
        /// Feed for the team today, 0..1: the land's grass, the season, and how many thousand animals
        /// have already grazed it (1849's companies left in a crowd about May 6 and made ~14 miles a day).
        /// </summary>
        public static float Grass(GameState g)
        {
            if (g.next == null) return 0.7f;
            var leg = CurrentLeg(g);
            if (g.legDone < leg.Dry) return 0f;
            float season = g.Doy < 110 ? 0.45f : g.Doy < 150 ? 0.45f + (g.Doy - 110) / 40f * 0.55f : g.Doy < 225 ? 1f : Math.Max(0.6f, 1f - (g.Doy - 225) / 100f);
            int herd = 126 + g.miles / 14;
            float traffic = Clamp((g.Doy - herd) / 45f, 0, 0.5f);
            return Clamp(leg.Grass * season * (1 - traffic), 0, 1);
        }

        public static string GrassLabel(float grass) => grass >= 0.65f ? "good" : grass >= 0.4f ? "thin" : grass > 0.12f ? "poor" : "none";

        public static bool OnDryStretch(GameState g)
        {
            if (g.next == null || g.Phase != Phase.Road) return false;
            return g.legDone < CurrentLeg(g).Dry;
        }

        public static int WaterPerDay(GameState g) => g.Count + (int)Math.Ceiling(g.animals * (g.Team == TeamKind.Mules ? 1f : 1.5f));

        /// <summary>
        /// Fills the kegs: all the way before a dry stretch, otherwise only as far as the team can pull
        /// without strain (water is 8 lb a gallon).
        /// </summary>
        public static void FillKegs(GameState g)
        {
            int full = g.kegs * Content.KegGallons;
            bool dryAhead = g.next != null && CurrentLeg(g).Dry > 0;
            if (dryAhead) { g.water = full; return; }
            g.water = 0;
            int room = Capacity(g) - Load(g);
            g.water = Math.Max(0, Math.Min(full, room / 8));
        }

        // ---- Movement ---------------------------------------------------------------------------

        public static float GroundSpeed(Ground gr)
        {
            switch (gr)
            {
                case Ground.Hills: return 0.85f;
                case Ground.Mountains: return 0.65f;
                case Ground.Forest: return 0.75f;
                case Ground.Sage: return 0.92f;
                case Ground.Desert: return 0.9f;
                default: return 1f;
            }
        }

        public static int MilesToday(GameState g)
        {
            if (g.animals <= 0 || g.wagon <= 0) return 0;
            float mph = g.Team == TeamKind.Mules ? 2.15f : 1.9f;
            float m = mph * g.hours * GroundSpeed(GroundNow(g));
            m *= 0.6f + 0.4f * g.strength / 100f;
            m *= 0.5f + 0.5f * Math.Min(1f, g.animals / 6f);
            int load = Load(g), cap = Capacity(g);
            if (load > cap) m *= Math.Max(0.35f, cap / (float)load);
            if (g.Sky == Sky.Storm) m *= 0.6f; else if (g.Sky == Sky.Snow) m *= 0.5f; else if (g.Sky == Sky.Rain) m *= 0.85f;
            return Math.Max(1, (int)Math.Round(m));
        }

        // ---- The day ----------------------------------------------------------------------------

        public sealed class DayResult
        {
            public readonly List<Notice> Notices = new List<Notice>();
            public int Miles;
            public bool Arrived;
            public EventDef Event;
        }

        /// <summary>One day. travel = false means the company rests in camp.</summary>
        public static DayResult Day(GameState g, bool travel)
        {
            var res = new DayResult();
            var log = res.Notices;
            var r = g.rng;
            g.day++;
            RollWeather(g);
            bool moving = travel && g.stuckDays == 0;
            if (g.stuckDays > 0) g.stuckDays--;

            bool dry = OnDryStretch(g);
            float grass = Grass(g);

            if (moving)
            {
                int m = Math.Min(MilesToday(g), g.legMiles - g.legDone);
                g.legDone += m; g.miles += m; res.Miles = m;
            }

            // Water on the dry stretches.
            float thirst = 0;
            if (dry)
            {
                int need = WaterPerDay(g);
                if (g.water >= need) g.water -= need;
                else
                {
                    thirst = need <= 0 ? 0 : 1f - g.water / (float)need;
                    g.water = 0;
                    if (thirst > 0.3f) log.Add(new Notice("No water left. The animals bellow all night and the company's tongues are swollen.", Tone.Bad));
                }
            }

            // Food.
            float hunger = 0;
            int eat = (int)Math.Ceiling(g.portion * g.Count * (g.Has(Trade.Cook) ? 0.85f : 1f));
            if (g.food >= eat) g.food -= eat;
            else
            {
                hunger = eat <= 0 ? 0 : 1f - g.food / (float)eat;
                g.food = 0;
                if (!g.Flag("warned_food")) { g.SetFlag("warned_food"); log.Add(new Notice("The food has run out. Hunt, trade, or buy at the next post.", Tone.Bad)); }
            }
            if (g.food > 0) g.flags.Remove("warned_food");

            Team(g, travel, grass, thirst, log);
            if (g.animals == 0) Stranded(g, log);
            Health(g, travel, hunger, thirst, log);
            if (moving) Wagon(g, log);
            Spirits(g, travel, hunger, thirst, log);

            if (g.Count == 0) return res;

            // A decision to make, now and then.
            if (travel && g.day - g.lastEventDay >= 3 && r.Chance(0.24f))
            {
                var e = Events.Pick(g);
                if (e != null) { res.Event = e; g.lastEventDay = g.day; }
            }

            if (g.legDone >= g.legMiles && g.Phase == Phase.Road) res.Arrived = true;
            foreach (var n in log) if (n.Tone != Tone.Info || n.Stop) Log(g, n.Text);
            return res;
        }

        static void Team(GameState g, bool travel, float grass, float thirst, List<Notice> log)
        {
            var r = g.rng;
            float d;
            if (travel)
            {
                d = (grass - 0.3f) * 6f;
                if (d < 0) d *= g.Team == TeamKind.Mules ? 1.4f : 0.7f;   // oxen eat anything; mules fail on poor feed
                d -= g.hours >= 14 ? 3.2f : g.hours >= 12 ? 1.6f : g.hours >= 10 ? 0.4f : -0.6f;
                int load = Load(g), cap = Capacity(g);
                if (load > cap) d -= (load - cap) / (float)cap * 12f;
                if (GroundNow(g) == Ground.Mountains) d -= 0.8f;
            }
            else d = 2f + grass * 5f;
            if (g.Has(Trade.Teamster)) d += 1f;
            d -= thirst * 18f;
            g.strength = Clamp(g.strength + d, 0, 100);

            // Weak or thirsty animals give out.
            if (g.animals > 0 && ((travel && g.strength < 25) || thirst > 0.4f))
            {
                float p = (travel ? Math.Max(0, 25 - g.strength) / 100f : 0) + thirst * 0.3f;
                if (r.Chance(Clamp(p, 0, 0.35f)))
                {
                    g.animals--;
                    string kind = g.Team == TeamKind.Mules ? "mule" : "ox";
                    log.Add(new Notice(thirst > 0.4f ? "One of the " + (g.Team == TeamKind.Mules ? "mules" : "oxen") + " went down from thirst and did not get up." : "An exhausted " + kind + " lay down in the traces and died.", Tone.Bad));
                }
            }
        }

        /// <summary>No team: the wagon can't move until someone on the road sells or lends animals.</summary>
        static void Stranded(GameState g, List<Notice> log)
        {
            var r = g.rng;
            if (!g.Flag("stranded")) { g.SetFlag("stranded"); log.Add(new Notice("The last of the team is gone. The wagon can't move until you get more animals. Companies pass every day; one may sell you some.", Tone.Bad)); return; }
            if (!r.Chance(0.3f)) return;
            int price = (int)(Content.BaseCents(Good.Animals, g.Team) * 2.5f) * 2;
            string kind = g.Team == TeamKind.Mules ? "mules" : "oxen";
            if (g.cents >= price)
            {
                g.cents -= price; g.animals = 2; g.strength = Math.Max(g.strength, 45);
                log.Add(new Notice("A passing company sells you two worn " + kind + " for " + Ui.Dollars(price) + ". Robbery, but the wagon rolls again.", Tone.Info));
            }
            else if (r.Chance(0.4f))
            {
                g.animals = 2; g.strength = Math.Max(g.strength, 40);
                log.Add(new Notice("A Mormon train heading for the valley leaves you two lame " + kind + " they can't use. Enough to move, slowly.", Tone.Good));
            }
            else return;
            g.flags.Remove("stranded");
        }

        static void Health(GameState g, bool travel, float hunger, float thirst, List<Notice> log)
        {
            var r = g.rng;
            bool doctor = g.Has(Trade.Physician);
            foreach (var h in g.company)
            {
                if (!h.Here) continue;
                float d = 0.6f;
                d += g.portion >= 3 ? 0.5f : g.portion == 2 ? 0f : -2.2f;
                if (travel) d -= g.hours >= 14 ? 1.8f : g.hours >= 12 ? 0.7f : g.hours <= 8 ? -0.4f : 0f;
                else d += 2.5f;
                if (g.Sky == Sky.Hot || g.Sky == Sky.Cold) d -= 0.8f; else if (g.Sky == Sky.Snow || g.Sky == Sky.Storm) d -= 1.2f;
                d -= hunger * 7f + thirst * 14f;
                if (g.morale < 30) d -= 0.5f;

                if (h.ill != Ill.None)
                {
                    d -= IllDamage(h.ill) * (doctor ? 0.6f : 1f);
                    if (h.ill == Ill.Scurvy) h.illDays = g.day - g.freshDay < 5 ? 0 : 999;
                    else h.illDays -= doctor ? 2 : 1;
                    if (h.illDays <= 0)
                    {
                        log.Add(new Notice(h.name + " has recovered from " + Content.IllName(h.ill) + ".", Tone.Good, false));
                        h.ill = Ill.None;
                    }
                }
                else Catch(g, h, log);

                h.health = Clamp(h.health + d, 0, 100);
                if (h.health <= 0) Die(g, h, h.ill != Ill.None ? Content.IllName(h.ill) : thirst > 0.3f ? "thirst" : hunger > 0.3f ? "starvation" : "exhaustion", log);
            }
        }

        public static float IllDamage(Ill i)
        {
            switch (i)
            {
                case Ill.Cholera: return 16f;
                case Ill.MountainFever: return 5f;
                case Ill.Scurvy: return 2.2f;
                case Ill.Injury: return 2.5f;
                case Ill.Snakebite: return 7f;
                case Ill.CampFever: return 4f;
                default: return 0;
            }
        }

        /// <summary>Rolls for falling ill: cholera on the crowded Platte in early summer, mountain fever in the Rockies, scurvy without fresh food.</summary>
        static void Catch(GameState g, Hand h, List<Notice> log)
        {
            var r = g.rng;
            float weak = h.health < 50 ? 1.6f : 1f;
            bool platte = g.miles > 60 && g.miles < 750 && g.Doy < 205;
            var ground = g.next == null ? Ground.Prairie : GroundNow(g);
            Ill got = Ill.None;
            if (platte && r.Chance(0.0045f * weak)) got = Ill.Cholera;
            else if ((ground == Ground.Mountains || ground == Ground.Hills) && r.Chance(0.004f * weak)) got = Ill.MountainFever;
            else if (g.day - g.freshDay > 70 && r.Chance((g.day - g.freshDay - 70) / 3000f)) got = Ill.Scurvy;
            else if (r.Chance(0.0018f)) got = Ill.Injury;
            else if ((ground == Ground.Sage || ground == Ground.Desert || ground == Ground.Hills) && g.Sky == Sky.Hot && r.Chance(0.002f)) got = Ill.Snakebite;
            if (got != Ill.None) Afflict(g, h, got, log);
        }

        public static void Afflict(GameState g, Hand h, Ill ill, List<Notice> log)
        {
            if (!h.Here || h.ill != Ill.None) return;
            var r = g.rng;
            h.ill = ill;
            h.illDays = ill == Ill.Cholera ? r.Range(2, 5) : ill == Ill.MountainFever ? r.Range(6, 12) : ill == Ill.Scurvy ? 999 : ill == Ill.Injury ? r.Range(5, 10) : r.Range(3, 6);
            string what = ill == Ill.Injury ? r.Pick(new[] { "was kicked by " + (g.Team == TeamKind.Mules ? "a mule" : "an ox"), "was hurt when a wheel rolled over a foot", "fell from the wagon and hurt a shoulder" })
                : ill == Ill.Snakebite ? "was bitten by a rattlesnake" : "has " + Content.IllName(ill);
            string text = h.name + " " + what + ".";
            if (g.medicine > 0 && ill != Ill.Scurvy)
            {
                g.medicine--;
                h.illDays = Math.Max(1, h.illDays * 2 / 3);
                if (ill == Ill.Cholera) h.health = Math.Min(100, h.health + 15);
                text += " A dose of medicine was given.";
            }
            if (ill == Ill.Injury) h.health -= 12;
            log.Add(new Notice(text, Tone.Bad));
        }

        public static void Die(GameState g, Hand h, string cause, List<Notice> log)
        {
            if (!h.alive) return;
            h.alive = false; h.health = 0; h.fate = "Died of " + cause + ", " + DateLabel(g) + ", " + g.miles + " miles out.";
            g.deaths++;
            g.morale = Clamp(g.morale - 14, 0, 100);
            log.Add(new Notice(h.name + " has died of " + cause + ". The company buries " + (g.company.IndexOf(h) == 0 ? "its captain" : "a partner") + " beside the trail.", Tone.Death));
        }

        static void Wagon(GameState g, List<Notice> log)
        {
            var r = g.rng;
            var ground = GroundNow(g);
            float rough = ground == Ground.Mountains ? 1.6f : ground == Ground.Forest || ground == Ground.Hills ? 1.0f : ground == Ground.Desert ? 0.9f : 0.45f;
            float wear = rough * g.hours / 10f * (g.Has(Trade.Blacksmith) ? 0.6f : 1f);
            g.wagon = Clamp(g.wagon - wear, 0, 100);
            if (r.Chance((100 - g.wagon) / 100f * 0.05f * rough)) Breakdown(g, log);
        }

        public static void Breakdown(GameState g, List<Notice> log)
        {
            var r = g.rng;
            string part = r.Pick(new[] { "a wheel", "the axle", "the tongue", "a hound", "an iron tire" });
            if (g.kits > 0)
            {
                g.kits--;
                g.wagon = Math.Min(100, g.wagon + 45);
                bool smith = g.Has(Trade.Blacksmith);
                if (!smith) g.stuckDays = Math.Max(g.stuckDays, 1);
                log.Add(new Notice("The wagon broke " + part + ". " + (smith ? g.With(Trade.Blacksmith).name + " mended it with a repair kit before noon." : "A repair kit mends it, but the job costs a day."), Tone.Bad));
            }
            else
            {
                int days = r.Range(2, 5);
                g.stuckDays = Math.Max(g.stuckDays, days);
                g.wagon = Math.Max(g.wagon, 30);
                log.Add(new Notice("The wagon broke " + part + ", and there's no repair kit. Mending it from scraps will take " + days + " days.", Tone.Bad));
            }
        }

        /// <summary>Uses a repair kit in camp (half a day; free with a blacksmith).</summary>
        public static string Repair(GameState g)
        {
            if (g.kits <= 0) return "You have no repair kits.";
            g.kits--;
            g.wagon = Math.Min(100, g.wagon + 45);
            return g.Has(Trade.Blacksmith) ? g.With(Trade.Blacksmith).name + " goes over the wagon: new spokes, a reset tire, tight bolts." : "The company spends the evening mending the wagon.";
        }

        static void Spirits(GameState g, bool travel, float hunger, float thirst, List<Notice> log)
        {
            var r = g.rng;
            float d = 0;
            d += g.portion >= 3 ? 0.8f : g.portion == 2 ? 0.1f : -1.6f;
            if (travel) d -= g.hours >= 14 ? 1.6f : g.hours >= 12 ? 0.6f : 0f; else d += 2.2f;
            if (g.Sky == Sky.Storm || g.Sky == Sky.Snow) d -= 1f;
            d -= hunger * 4 + thirst * 5;
            foreach (var h in g.company)
            {
                if (!h.Here) continue;
                if (h.ill != Ill.None) d -= 0.4f;
                if (h.temper == Temper.Cheerful) d += 0.35f; else if (h.temper == Temper.Grumbler) d -= 0.35f;
            }
            if (g.Has(Trade.Cook)) d += 0.4f;
            d += (65 - g.morale) * 0.02f;   // spirits drift back toward ordinary
            g.morale = Clamp(g.morale + d, 0, 100);

            // A company in despair breaks up: a partner takes a share and leaves.
            if (g.morale < 28 && g.Count > 1 && r.Chance((28 - g.morale) / 160f)) Desert(g, log);
        }

        public static void Desert(GameState g, List<Notice> log)
        {
            Hand who = null;
            for (int i = g.company.Count - 1; i >= 1; i--)
            {
                var h = g.company[i];
                if (!h.Here) continue;
                if (who == null || (h.temper == Temper.Grumbler && who.temper != Temper.Grumbler)) who = h;
            }
            if (who == null) return;
            int share = g.Count;
            who.gone = true; who.fate = "Left the company, " + DateLabel(g) + ", " + g.miles + " miles out.";
            g.departures++;
            int food = g.food / share, cash = g.cents / share / 2;
            g.food -= food; g.cents -= cash;
            bool animal = g.animals > Content.MinAnimals + 1;
            if (animal) g.animals--;
            g.morale = Clamp(g.morale + 8, 0, 100);   // the complainer is gone
            log.Add(new Notice(who.name + " has had enough and left to join another company, taking " + food + " lb of food" + (cash > 0 ? ", " + Ui.Dollars(cash) : "") + (animal ? " and one of the animals" : "") + " as a share.", Tone.Bad));
        }

        // ---- Resting, hunting -------------------------------------------------------------------

        public static DayResult Rest(GameState g) => Day(g, false);

        /// <summary>The stalk is over: meat brought back to camp.</summary>
        public static string HuntResult(GameState g, string animal, int pounds, int shots)
        {
            g.powder = Math.Max(0, g.powder - shots);
            if (pounds <= 0) return shots > 0 ? "No meat today. " + shots + " shots spent." : "You came back with nothing.";
            int room = Math.Max(0, Capacity(g) - Load(g) + 200);
            int kept = Math.Min(pounds, room);
            g.food += kept;
            g.freshDay = g.day;
            g.morale = Clamp(g.morale + 5, 0, 100);
            foreach (var h in g.company) if (h.Here && h.ill == Ill.Scurvy) h.illDays = 0;
            Log(g, "Hunted " + animal + ": " + kept + " lb of fresh meat.");
            return "You bring back " + kept + " lb of fresh " + animal + " meat." + (kept < pounds ? " There was no room in the wagon for the rest." : "") + " The company eats well tonight.";
        }

        // ---- Arriving ---------------------------------------------------------------------------

        public static Stop Arrive(GameState g)
        {
            var s = Route.Get(g.next);
            g.visited.Add(s.Id);
            g.legDone = g.legMiles;
            Log(g, "Reached " + s.Name + ".");
            if (s.Water) FillKegs(g);
            if (s.Fresh) { g.freshDay = g.day; foreach (var h in g.company) if (h.Here && h.ill == Ill.Scurvy) h.illDays = 0; }
            if (s.End) { g.Phase = Phase.Diggings; g.arrivedDay = g.Doy; }
            else if (s.Kind == StopKind.River) g.Phase = Phase.Crossing;
            else if (s.Haul > 0) g.Phase = Phase.Haul;
            else g.Phase = Phase.Stop;
            if (s.Next.Length == 1) SetLeg(g, s.Next[0]);
            else if (s.Next.Length > 1) g.next = null;
            return s;
        }

        public static void ChooseLeg(GameState g, Leg l)
        {
            SetLeg(g, l);
            Log(g, "Chose: " + (l.Label ?? Route.Get(l.To).Name) + ".");
        }

        /// <summary>Leave the stop and take to the road.</summary>
        public static void Depart(GameState g)
        {
            if (g.next == null) return;
            g.Phase = Phase.Road;
            if (g.HereStop.Water) FillKegs(g);
        }

        // ---- Stores -----------------------------------------------------------------------------

        public static int PriceCents(GameState g, Good k)
        {
            float f = g.HereStop.Prices <= 0 ? 1f : g.HereStop.Prices;
            return (int)Math.Round(Content.BaseCents(k, g.Team) * f / 5f) * 5;
        }

        /// <summary>Selling back at a store gets a third of its price.</summary>
        public static int SellCents(GameState g, Good k) => PriceCents(g, k) / 3 / 5 * 5;

        // ---- Score ------------------------------------------------------------------------------

        public struct Line { public string What; public int Cents; }

        public static List<Line> Payout(GameState g)
        {
            var l = new List<Line>();
            l.Add(new Line { What = (g.gold).ToString("0.00") + " oz of gold dust at $" + DollarsPerOunce, Cents = (int)(g.gold * DollarsPerOunce * 100) });
            l.Add(new Line { What = "Cash left", Cents = g.cents });
            l.Add(new Line { What = g.Count + " of " + g.company.Count + " brought through, $" + SurvivorBonus + " each", Cents = g.Count * SurvivorBonus * 100 });
            return l;
        }

        public static int TakeDollars(GameState g) { int c = 0; foreach (var l in Payout(g)) c += l.Cents; return c / 100; }

        public static string Rank(int dollars) =>
            dollars < 2000 ? "Seen the Elephant" : dollars < 4500 ? "Wages and No More" : dollars < 8000 ? "A Fair Strike" : dollars < 13000 ? "Paying Dirt" : "Bonanza";

        // ---- Helpers ----------------------------------------------------------------------------

        public static void Log(GameState g, string text)
        {
            g.journal.Add(ShortDate(g.Doy) + ". " + text);
            if (g.journal.Count > 120) g.journal.RemoveAt(0);
        }

        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        public static string HealthLabel(float h) => h >= 75 ? "well" : h >= 50 ? "worn" : h >= 25 ? "weak" : "failing";
        public static string MoraleLabel(float m) => m >= 75 ? "high" : m >= 50 ? "steady" : m >= 30 ? "low" : "breaking";
        public static string StrengthLabel(float s) => s >= 75 ? "strong" : s >= 50 ? "tiring" : s >= 25 ? "worn" : "failing";
    }
}
