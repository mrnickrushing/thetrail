using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    public enum Job { Banker, Carpenter, Farmer }
    public enum Pace { Steady = 1, Strenuous = 2, Grueling = 3 }
    public enum Rations { Filling = 1, Meager = 2, BareBones = 3 }
    public enum Ailment { None, Exhaustion, Fever, Measles, Dysentery, Typhoid, Cholera, BrokenArm, BrokenLeg, Snakebite }
    public enum Sky { Clear, Rain, HeavyRain, Snow, HeavySnow }
    public enum Part { None, Wheel, Axle, Tongue }
    public enum Phase { Store, Traveling, Arrived, River, Branch, Raft, Won, Lost }

    [Serializable]
    public class Member
    {
        public string name;
        public bool alive = true;
        public Ailment ailment;
        public int sickDays;
        public string cause;
    }

    /// <summary>Everything about one journey. Plain fields only, so it saves as JSON.</summary>
    [Serializable]
    public class GameState
    {
        public int version = 1;
        public Job job;
        public int startMonth = 4;          // 3 (March) .. 7 (July)
        public int day;                     // days since leaving Independence
        public List<Member> party = new List<Member>();

        public int cents, oxen, food, clothing, bullets, wheels, axles, tongues;
        public Pace pace = Pace.Steady;
        public Rations rations = Rations.Filling;

        public float hardship;              // accumulated wear on the party; see Sim.HealthLabel
        public int miles;                   // total miles traveled
        public List<string> visited = new List<string>();
        public string next;                 // the landmark ahead
        public int legMiles, legDone;
        public bool legRaft;

        public int temp = 60;
        public float tempAnomaly;
        public Sky sky;
        public float wetness;               // recent rain; raises rivers

        public Part broken;                 // a part that broke with no spare
        public int oxenWeak;                // injured oxen pull half
        public int lostDays;                // days still to lose (lost trail, searching, storms)
        public float huntPressure;          // game gets scarce if you hunt one area hard
        public int riverDepthTenths;        // current river reading while at a crossing
        public int phase;                   // (int)Phase
        public int deaths;
        public uint seed;
        public Rng rng = new Rng();

        public string Here => visited.Count > 0 ? visited[visited.Count - 1] : Trail.Start;
        public Phase Phase { get => (Phase)phase; set => phase = (int)value; }
        public int Alive { get { int n = 0; foreach (var m in party) if (m.alive) n++; return n; } }
        public Member Leader { get { foreach (var m in party) if (m.alive) return m; return null; } }
        public int Dollars => cents / 100;
        public int Spares(Part p) => p == Part.Wheel ? wheels : p == Part.Axle ? axles : p == Part.Tongue ? tongues : 0;

        public void AddSpare(Part p, int n)
        {
            if (p == Part.Wheel) wheels = Math.Max(0, wheels + n);
            else if (p == Part.Axle) axles = Math.Max(0, axles + n);
            else if (p == Part.Tongue) tongues = Math.Max(0, tongues + n);
        }

        public DateTime Date => new DateTime(Trail.Year, startMonth, 1).AddDays(day);
    }
}
