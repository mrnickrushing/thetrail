using System;
using System.Collections.Generic;
using System.Linq;
using TheTrail.Core;

// Plays the rules many times through Core/Bot (minigames stood in for by a skill level) and prints
// how companies fare. Rerun it after touching Sim's numbers: dotnet run -c Release -- [games] [verbose seed]
static class Program
{
    static void Main(string[] args)
    {
        int games = args.Length > 0 ? int.Parse(args[0]) : 300;
        if (args.Length > 1)
        {
            if (args.Length > 2) Bot.Trace = (g, what) => { float mh = 100; foreach (var h in g.company) if (h.Here) mh = Math.Min(mh, h.health); Console.WriteLine($"{Sim.ShortDate(g.Doy),-7} {what,-8} {g.Here,-12} str {g.strength,3:0} grass {Sim.Grass(g):0.00} an {g.animals} minH {mh,3:0} food {g.food} water {g.water} stuck {g.stuckDays} wagon {g.wagon:0}"); };
            var one = Bot.Play(uint.Parse(args[1]), 1, 0.7f);
            foreach (var j in one.Game.journal) Console.WriteLine(j);
            Console.WriteLine($"reached={one.Reached} lost={one.Lost} phase={one.Game.Phase} at={one.Game.Here} day={one.Days} $={one.Dollars} alive={one.Survivors}");
            foreach (var h in one.Game.company) Console.WriteLine($"  {h.name}: {(h.Here ? "here " + (int)h.health : h.fate)}");
            return;
        }
        Console.WriteLine($"{"strategy",-30} {"arrive",7} {"alive",6} {"days",5} {"median$",8} {"p90$",7}  routes / where lost");
        var runs = new (string name, int dep, float skill, TeamKind team, int partners)[]
        {
            ("Apr 15, oxen, good play", 0, 0.8f, TeamKind.Oxen, 4),
            ("May 1, oxen, fair play", 1, 0.6f, TeamKind.Oxen, 4),
            ("May 1, mules, fair play", 1, 0.6f, TeamKind.Mules, 4),
            ("May 1, oxen, poor play", 1, 0.3f, TeamKind.Oxen, 4),
            ("May 15, oxen, fair play", 2, 0.6f, TeamKind.Oxen, 4),
            ("Jun 1, oxen, fair play", 3, 0.6f, TeamKind.Oxen, 4),
            ("May 1, oxen, 1 partner", 1, 0.6f, TeamKind.Oxen, 1),
        };
        foreach (var run in runs)
        {
            var res = new List<Bot.Result>();
            for (int i = 0; i < games; i++) res.Add(Bot.Play((uint)(i + 1), run.dep, run.skill, run.team, run.partners));
            var ok = res.Where(r => r.Reached).ToList();
            var dollars = ok.Select(r => r.Dollars).OrderBy(d => d).ToList();
            int Med(List<int> l, float q) => l.Count == 0 ? 0 : l[Math.Min(l.Count - 1, (int)(l.Count * q))];
            var where = res.Where(r => !r.Reached).GroupBy(r => r.Game.Here).OrderByDescending(g => g.Count()).Take(4).Select(g => g.Key + ":" + g.Count());
            var routes = ok.GroupBy(r => r.Route).Select(g => g.Key + ":" + g.Count());
            Console.WriteLine($"{run.name,-30} {100f * ok.Count / games,6:0}% {(ok.Count == 0 ? 0 : ok.Average(r => r.Survivors)),6:0.0} {(ok.Count == 0 ? 0 : ok.Average(r => r.Travel)),5:0} {Med(dollars, 0.5f),8} {Med(dollars, 0.9f),7}  {string.Join(" ", routes)} | {string.Join(" ", where)}");
        }
    }
}
