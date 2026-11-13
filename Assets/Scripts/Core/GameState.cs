using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    public enum Trade { Greenhorn, Blacksmith, Physician, Hunter, Guide, Cook, Teamster, Miner }
    public enum Temper { Steady, Cheerful, Grumbler }
    public enum Ill { None, Cholera, MountainFever, Scurvy, Injury, Snakebite, CampFever }
    public enum Sky { Fair, Hot, Rain, Storm, Cold, Snow }
    public enum TeamKind { Oxen, Mules }
    public enum Phase { Hire, Outfit, Road, Stop, Crossing, Haul, Diggings, Done, Lost }
    public enum Claim { RiverBar, DryGulch, Hillside }

    /// <summary>One member of the company. The leader is the first one still here.</summary>
    [Serializable]
    public class Hand
    {
        public string name;
        public string home;
        public Trade trade;
        public Temper temper;
        public int stake;            // dollars put into the pot
        public float health = 100;   // 0..100
        public Ill ill;
        public int illDays;
        public bool alive = true;
        public bool gone;            // left the company
        public string fate;          // how they died or why they left
        public bool Here => alive && !gone;
    }

    /// <summary>Everything about one company's journey. Plain fields only, so it saves as JSON.</summary>
    [Serializable]
    public class GameState
    {
        public int version = 2;
        public uint seed;
        public Rng rng = new Rng();
        public int phase;
        public Phase Phase { get => (Phase)phase; set => phase = (int)value; }

        // Calendar: day 0 is the departure date.
        public int startDoy = 121;   // day of the year 1849 the company leaves (121 = May 1)
        public int day;

        public List<Hand> company = new List<Hand>();
        public int cents;

        // Stores.
        public int team;
        public TeamKind Team { get => (TeamKind)team; set => team = (int)value; }
        public int animals;
        public float strength = 90;  // the team, 0..100
        public int food;             // pounds
        public int kegs;             // 10-gallon water kegs
        public int water;            // gallons in the kegs
        public int kits;             // repair kits (iron and seasoned timber)
        public int medicine;         // doses
        public int powder;           // shots
        public int pans, picks, rockers;
        public float wagon = 100;    // condition, 0..100

        // Orders.
        public int hours = 10;       // on the road each day: 8, 10, 12 or 14
        public int portion = 2;      // pounds of food per person per day: 1, 2 or 3

        public float morale = 70;
        public int freshDay;         // the last day the company ate fresh meat, fruit or greens

        // Where.
        public List<string> visited = new List<string>();
        public string next;
        public int legMiles, legDone;
        public int miles;
        public int stuckDays;        // days lost to come (searching for strays, repairs)
        public int lastEventDay = -9;
        public List<string> flags = new List<string>();

        // Weather.
        public int sky;
        public Sky Sky { get => (Sky)sky; set => sky = (int)value; }
        public int temp = 60;

        public List<string> journal = new List<string>();

        // The diggings.
        public int claim = -1;
        public float claimRich;
        public float gold;           // ounces
        public int week;
        public float lastPan = -1;   // quality of the last panning, 0..1
        public int deaths, departures;
        public int arrivedDay = -1;
        public List<float> offers = new List<float>();   // richness of the three claims on offer
        public float river = 0.5f;   // how high the river at this crossing is running, 0..1

        public string Here => visited.Count > 0 ? visited[visited.Count - 1] : Route.Start;
        public Stop HereStop => Route.Get(Here);
        public int Dollars => cents / 100;

        public int Count { get { int n = 0; foreach (var h in company) if (h.Here) n++; return n; } }
        public Hand Leader { get { foreach (var h in company) if (h.Here) return h; return null; } }
        public bool Has(Trade t) { foreach (var h in company) if (h.Here && h.trade == t) return true; return false; }
        public Hand With(Trade t) { foreach (var h in company) if (h.Here && h.trade == t) return h; return null; }
        public bool Flag(string f) => flags.Contains(f);
        public void SetFlag(string f) { if (!flags.Contains(f)) flags.Add(f); }

        /// <summary>Day of the year in 1849 (1 = January 1).</summary>
        public int Doy => startDoy + day;
        public DateTime Date => new DateTime(1849, 1, 1).AddDays(Doy - 1);
    }
}
