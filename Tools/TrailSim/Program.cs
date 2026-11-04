using System;
using System.Collections.Generic;
using System.Linq;
using TheTrail.Core;

// A bot that plays the trail rules (no minigames: hunting and rafting use average yields) with a few
// strategies, and prints survival, deaths, days and score. Rerun it after touching Sim's numbers.
static class Program
{
    record Strategy(string Name, Job Job, int Month, Pace Pace, Rations Rations, bool Careful);

    static void Main(string[] args)
    {
        int games = args.Length > 0 ? int.Parse(args[0]) : 2000;
        var strategies = new[]
        {
            new Strategy("careful farmer, April", Job.Farmer, 4, Pace.Steady, Rations.Filling, true),
            new Strategy("careful banker, April", Job.Banker, 4, Pace.Steady, Rations.Filling, true),
            new Strategy("careful carpenter, May", Job.Carpenter, 5, Pace.Steady, Rations.Filling, true),
            new Strategy("hurried banker, April", Job.Banker, 4, Pace.Strenuous, Rations.Meager, false),
            new Strategy("reckless farmer, April", Job.Farmer, 4, Pace.Grueling, Rations.BareBones, false),
            new Strategy("careful farmer, July", Job.Farmer, 7, Pace.Steady, Rations.Filling, true),
        };
        Console.WriteLine($"{"strategy",-26} {"arrive",7} {"all5",6} {"deaths",7} {"days",5} {"score",7}  causes");
        foreach (var s in strategies)
        {
            rests = 0; stuck = 0;
            int arrived = 0, allFive = 0, deaths = 0, days = 0, score = 0;
            var causes = new Dictionary<string, int>();
            for (int i = 0; i < games; i++)
            {
                var g = Play(s, (uint)(i * 7919 + 17));
                deaths += g.deaths;
                foreach (var m in g.party) if (!m.alive) causes[m.cause] = causes.GetValueOrDefault(m.cause) + 1;
                if (g.Phase == Phase.Won) { arrived++; days += g.day; score += Sim.Score(g); if (g.Alive == 5) allFive++; }
            }
            string top = string.Join(", ", causes.OrderByDescending(kv => kv.Value).Take(5).Select(kv => $"{kv.Key} {100.0 * kv.Value / games:0}%"));
            Console.WriteLine($"{s.Name,-26} {100.0 * arrived / games,6:0}% {100.0 * allFive / games,5:0}% {(double)deaths / games,7:0.00} {(arrived > 0 ? days / arrived : 0),5} {(arrived > 0 ? score / arrived : 0),7}  {top}  rest/g {rests / games} stuck/g {stuck / games}");
        }
    }

    static void Buy(GameState g, Sim.Good k, int units)
    {
        int cost = Sim.Price(g, k) * units;
        if (units <= 0 || cost > g.cents) return;
        g.cents -= cost;
        int n = k == Sim.Good.Oxen ? units * 2 : k == Sim.Good.Bullets ? units * 20 : units;
        Sim.Give(g, k, n);
    }

    public static int rests, stuck;
    static GameState Play(Strategy s, uint seed)
    {
        var g = Sim.NewGame(s.Job, new[] { "A", "B", "C", "D", "E" }, s.Month, seed);
        // Outfit: scaled to the purse.
        int budget = g.cents;
        Buy(g, Sim.Good.Oxen, budget >= 80000 ? 4 : 3);
        Buy(g, Sim.Good.Clothing, budget >= 80000 ? 10 : 6);
        Buy(g, Sim.Good.Bullets, budget >= 80000 ? 10 : 5);
        Buy(g, Sim.Good.Wheel, 1); Buy(g, Sim.Good.Axle, 1); Buy(g, Sim.Good.Tongue, 1);
        int lbs = Math.Min(2000 - g.food, g.cents / Sim.Price(g, Sim.Good.Food) - 50);
        Buy(g, Sim.Good.Food, Math.Max(0, Math.Min(lbs, budget >= 80000 ? 1200 : 1000)));
        g.pace = s.Pace; g.rations = s.Rations; g.Phase = Phase.Traveling;

        for (int guard = 0; guard < 600 && g.Alive > 0; guard++)
        {
            if (g.Phase == Phase.Won) break;
            if (g.Phase == Phase.Raft)
            {
                // The Columbia: a typical run bumps a rock or two.
                int hits = g.rng.Range(0, 3);
                for (int h = 0; h < hits; h++) foreach (var m in g.party) if (m.alive && g.rng.Chance(0.06f)) Sim.Die(g, m, "drowning", new List<Notice>());
                g.day += 2; g.miles += g.legMiles; g.legDone = g.legMiles;
                if (g.Alive > 0) Sim.Arrive(g);
                break;
            }
            if (g.Phase == Phase.River)
            {
                var l = Trail.Get(g.Here);
                float d = g.riverDepthTenths / 10f;
                int waits = 0;
                while (s.Careful && d >= 2.5f && l.Ferry == 0 && waits < 4) { Sim.WaitAtRiver(g); d = g.riverDepthTenths / 10f; waits++; }
                var how = d < 2.5f ? Sim.Crossing.Ford : l.Ferry > 0 && g.Dollars >= l.Ferry ? Sim.Crossing.Ferry : Sim.Crossing.Float;
                Sim.Cross(g, how);
                continue;
            }
            if (g.Phase == Phase.Branch)
            {
                var l = Trail.Get(g.Here);
                var r = l.Next[s.Careful || l.Id == "dalles" ? (l.Id == "dalles" && g.Dollars < 5 ? 0 : l.Next.Length - 1) : 1];
                if (l.Id == "southpass") r = s.Careful ? l.Next[0] : l.Next[1];
                Sim.ChooseRoute(g, r);
                continue;
            }
            if (g.Phase == Phase.Arrived)
            {
                var l = Trail.Get(g.Here);
                if (l.HasStore)
                {
                    if (g.food < 400) Buy(g, Sim.Good.Food, Math.Min(400, (g.cents - 2000) / Math.Max(1, Sim.Price(g, Sim.Good.Food))));
                    if (g.oxen < 4) Buy(g, Sim.Good.Oxen, 1);
                    if (g.wheels == 0) Buy(g, Sim.Good.Wheel, 1);
                    if (g.axles == 0) Buy(g, Sim.Good.Axle, 1);
                    if (g.clothing < 2 * g.Alive) Buy(g, Sim.Good.Clothing, 2 * g.Alive - g.clothing);
                }
                g.Phase = Phase.Traveling;
            }

            // Careful players rest when health slips; everyone hunts when food runs low.
            if (g.food < 25 * g.Alive && g.bullets >= 10)
            {
                int meat = Math.Min(400, g.rng.Range(40, 420));
                g.bullets -= g.rng.Range(8, 20); g.bullets = Math.Max(0, g.bullets);
                g.food += meat; Sim.Day(g, false);
                continue;
            }
            if (s.Careful && g.hardship >= 60 && g.food > 0) { Sim.Rest(g, 2); rests += 2; continue; }
            if (g.broken != Part.None && g.rng.Chance(0.5f))
            {
                var o = Sim.MakeOffer(g); g.day++;
                if (o != null && (o.give == Sim.Good.Wheel || o.give == Sim.Good.Axle || o.give == Sim.Good.Tongue)) Sim.Accept(g, o);
            }
            if (g.broken != Part.None || g.oxen == 0) stuck++;
            Sim.Day(g, true);
            if (g.Alive > 0 && Sim.AtNext(g) && g.Phase == Phase.Traveling) Sim.Arrive(g);
        }
        if (g.Alive == 0) g.Phase = Phase.Lost;
        return g;
    }
}
