using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    public enum Good { Animals, Food, Kegs, Kits, Medicine, Powder, Pans, Picks, Rockers }

    /// <summary>The company's trades, the stores for sale, and the strangers on the hiring board.</summary>
    public static class Content
    {
        // ---- Trades ---------------------------------------------------------------------------------

        public static string TradeName(Trade t)
        {
            switch (t)
            {
                case Trade.Blacksmith: return "Blacksmith";
                case Trade.Physician: return "Physician";
                case Trade.Hunter: return "Hunter";
                case Trade.Guide: return "Guide";
                case Trade.Cook: return "Cook";
                case Trade.Teamster: return "Teamster";
                case Trade.Miner: return "Miner";
                default: return "Greenhorn";
            }
        }

        public static string TradeSkill(Trade t)
        {
            switch (t)
            {
                case Trade.Blacksmith: return "The wagon wears slower; mends it fast.";
                case Trade.Physician: return "The sick get well sooner; fewer die.";
                case Trade.Hunter: return "Finds more game; shoots straighter.";
                case Trade.Guide: return "Reads rivers; fewer days lost.";
                case Trade.Cook: return "Food goes further; spirits stay up.";
                case Trade.Teamster: return "The team keeps its strength.";
                case Trade.Miner: return "More gold every week of work.";
                default: return "No trade, but more money for the pot.";
            }
        }

        public static string TemperName(Temper t) => t == Temper.Cheerful ? "cheerful" : t == Temper.Grumbler ? "a grumbler" : "steady";

        public static string IllName(Ill i)
        {
            switch (i)
            {
                case Ill.Cholera: return "cholera";
                case Ill.MountainFever: return "mountain fever";
                case Ill.Scurvy: return "scurvy";
                case Ill.Injury: return "an injury";
                case Ill.Snakebite: return "a rattlesnake bite";
                case Ill.CampFever: return "camp fever";
                default: return "nothing";
            }
        }

        // ---- Stores ---------------------------------------------------------------------------------

        public const int WagonOxen = 2800, WagonMules = 2300;   // the most a wagon can carry, pounds
        public const int MaxAnimals = 12, MinAnimals = 4;
        public const int KegGallons = 10;
        public const int BaseLoad = 350;                        // bedding, tents, tools, clothes

        public static string GoodName(Good g, TeamKind team)
        {
            switch (g)
            {
                case Good.Animals: return team == TeamKind.Mules ? "Mules" : "Oxen";
                case Good.Food: return "Flour and bacon";
                case Good.Kegs: return "Water kegs";
                case Good.Kits: return "Repair kits";
                case Good.Medicine: return "Medicine";
                case Good.Powder: return "Powder and shot";
                case Good.Pans: return "Gold pans";
                case Good.Picks: return "Picks and shovels";
                default: return "Rocker";
            }
        }

        public static string GoodUnit(Good g)
        {
            switch (g)
            {
                case Good.Food: return "lb";
                case Good.Powder: return "shots";
                case Good.Medicine: return "doses";
                default: return "";
            }
        }

        public static string GoodNote(Good g, TeamKind team)
        {
            switch (g)
            {
                case Good.Animals: return team == TeamKind.Mules ? "Quick, but need good grass. 6 pull a wagon well." : "Slow and steady, eat anything. 6 pull a wagon well.";
                case Good.Food: return "About 2 lb a person a day.";
                case Good.Kegs: return "10 gallons each, for the dry stretches. Heavy when full.";
                case Good.Kits: return "Iron and seasoned timber to mend a broken wagon.";
                case Good.Medicine: return "A dose helps someone through an illness.";
                case Good.Powder: return "For hunting. Fresh meat keeps off scurvy.";
                case Good.Pans: return "For washing gold. One per worker.";
                case Good.Picks: return "For digging. One per worker.";
                default: return "A cradle that washes gravel four times faster than a pan.";
            }
        }

        /// <summary>How many you buy at once with one tap of +.</summary>
        public static int Step(Good g) => g == Good.Food ? 50 : g == Good.Powder ? 20 : g == Good.Medicine ? 2 : 1;

        /// <summary>Price in cents of one step at St. Joseph prices.</summary>
        public static int BaseCents(Good g, TeamKind team)
        {
            switch (g)
            {
                case Good.Animals: return team == TeamKind.Mules ? 5000 : 2500;
                case Good.Food: return 400;      // 50 lb
                case Good.Kegs: return 250;
                case Good.Kits: return 1200;
                case Good.Medicine: return 150;  // 2 doses
                case Good.Powder: return 200;    // 20 shots
                case Good.Pans: return 300;
                case Good.Picks: return 500;
                default: return 3000;
            }
        }

        /// <summary>Weight in pounds of one unit (not one step).</summary>
        public static float Weight(Good g)
        {
            switch (g)
            {
                case Good.Food: return 1;
                case Good.Kegs: return 15;
                case Good.Kits: return 60;
                case Good.Medicine: return 0.5f;
                case Good.Powder: return 0.1f;
                case Good.Pans: return 4;
                case Good.Picks: return 12;
                case Good.Rockers: return 70;
                default: return 0;
            }
        }

        public static int MaxOf(Good g)
        {
            switch (g)
            {
                case Good.Animals: return MaxAnimals;
                case Good.Food: return 4000;
                case Good.Kegs: return 20;
                case Good.Kits: return 6;
                case Good.Medicine: return 30;
                case Good.Powder: return 400;
                case Good.Pans: return 8;
                case Good.Picks: return 8;
                default: return 2;
            }
        }

        public static int Have(GameState g, Good k)
        {
            switch (k)
            {
                case Good.Animals: return g.animals;
                case Good.Food: return g.food;
                case Good.Kegs: return g.kegs;
                case Good.Kits: return g.kits;
                case Good.Medicine: return g.medicine;
                case Good.Powder: return g.powder;
                case Good.Pans: return g.pans;
                case Good.Picks: return g.picks;
                default: return g.rockers;
            }
        }

        public static void Add(GameState g, Good k, int n)
        {
            switch (k)
            {
                case Good.Animals: g.animals = Math.Max(0, g.animals + n); break;
                case Good.Food: g.food = Math.Max(0, g.food + n); break;
                case Good.Kegs: g.kegs = Math.Max(0, g.kegs + n); g.water = Math.Min(g.water, g.kegs * KegGallons); break;
                case Good.Kits: g.kits = Math.Max(0, g.kits + n); break;
                case Good.Medicine: g.medicine = Math.Max(0, g.medicine + n); break;
                case Good.Powder: g.powder = Math.Max(0, g.powder + n); break;
                case Good.Pans: g.pans = Math.Max(0, g.pans + n); break;
                case Good.Picks: g.picks = Math.Max(0, g.picks + n); break;
                default: g.rockers = Math.Max(0, g.rockers + n); break;
            }
        }

        // ---- The hiring board -----------------------------------------------------------------------

        static readonly string[] Men =
        {
            "Asa", "Benjamin", "Caleb", "Daniel", "Elias", "Ezra", "Franklin", "Gideon", "Hiram", "Isaac", "Jacob", "Jonas",
            "Lemuel", "Levi", "Moses", "Nathan", "Obadiah", "Orrin", "Peter", "Reuben", "Samuel", "Silas", "Thomas", "Walter",
            "Willard", "Zebulon", "Alonzo", "Horace", "Jasper", "Lyman", "Owen", "Rufus",
        };
        static readonly string[] Women = { "Abigail", "Clarissa", "Delia", "Eliza", "Harriet", "Louisa", "Mary Ann", "Olive", "Phoebe", "Rachel", "Sarah", "Tabitha" };
        static readonly string[] Surnames =
        {
            "Abbott", "Barlow", "Bixby", "Cady", "Colfax", "Dorsey", "Eames", "Fenwick", "Garland", "Hale", "Hollister", "Ingalls",
            "Jessup", "Kimball", "Lathrop", "Mabry", "Nolan", "Osgood", "Pettibone", "Quimby", "Rand", "Satterlee", "Tolliver",
            "Underhill", "Vail", "Wardwell", "Yates", "Bramble", "Crowder", "Dunning", "Fitch", "Goss", "Harlow", "Kellogg",
            "Mercer", "Prentice", "Stoddard", "Truax", "Whitcomb",
        };
        static readonly string[] Homes =
        {
            "Ohio", "Pike County, Missouri", "Vermont", "Kentucky", "New York", "Illinois", "Massachusetts", "Indiana",
            "Tennessee", "Pennsylvania", "Iowa", "Wisconsin", "Maine", "Michigan", "Ireland", "Wales", "the German states", "Arkansas",
        };

        static readonly Trade[] BoardTrades = { Trade.Blacksmith, Trade.Physician, Trade.Hunter, Trade.Guide, Trade.Cook, Trade.Teamster, Trade.Miner };

        /// <summary>Eight strangers looking for a company: always at least five different trades and two greenhorns.</summary>
        public static List<Hand> Board(Rng r, string leaderName)
        {
            var list = new List<Hand>();
            var trades = new List<Trade>(BoardTrades);
            Shuffle(r, trades);
            var picks = new List<Trade> { trades[0], trades[1], trades[2], trades[3], trades[4], trades[5], Trade.Greenhorn, Trade.Greenhorn };
            Shuffle(r, picks);
            var used = new HashSet<string> { leaderName ?? "" };
            var firsts = new HashSet<string>();
            foreach (var t in picks)
            {
                string name;
                do
                {
                    string first = r.Chance(0.2f) ? r.Pick(Women) : r.Pick(Men);
                    name = first + " " + r.Pick(Surnames);
                    if (firsts.Contains(first)) name = leaderName ?? "";
                } while (used.Contains(name));
                used.Add(name); firsts.Add(name.Substring(0, name.LastIndexOf(' ')));
                var temper = r.Chance(0.5f) ? Temper.Steady : r.Chance(0.55f) ? Temper.Cheerful : Temper.Grumbler;
                int stake = t == Trade.Greenhorn ? r.Range(22, 34) * 10 : r.Range(5, 15) * 10;
                if (temper == Temper.Grumbler) stake += 60;   // a grumbler with money is still a grumbler
                list.Add(new Hand { name = name, home = r.Pick(Homes), trade = t, temper = temper, stake = stake });
            }
            return list;
        }

        public static string RandomName(Rng r) => r.Pick(Men) + " " + r.Pick(Surnames);

        static void Shuffle<T>(Rng r, List<T> l)
        {
            for (int i = l.Count - 1; i > 0; i--) { int j = r.Range(0, i + 1); (l[i], l[j]) = (l[j], l[i]); }
        }

        // ---- Departure dates ------------------------------------------------------------------------

        public static readonly (int doy, string label, string note)[] Departures =
        {
            (105, "April 15", "Ahead of the crowd, but the spring grass is still short."),
            (121, "May 1", "The grass is up and most companies leave about now."),
            (135, "May 15", "Good grass, but thousands of wagons ahead of you."),
            (152, "June 1", "Late: the grass will be eaten and the best claims taken."),
        };
    }
}
