using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    /// <summary>
    /// Plays a whole journey through the rules with sensible choices, for the tests and the balance
    /// tool (Tools/TrailSim). skill (0..1) stands in for how well the minigames are played.
    /// </summary>
    public static class Bot
    {
        public sealed class Result
        {
            public GameState Game;
            public bool Reached, Lost;
            public int Days, Travel, Dollars, Survivors, Deaths, Events, Hunts;
            public string Route = "";
        }

        public static Action<GameState, string> Trace;

        public static Result Play(uint seed, int departure = 1, float skill = 0.7f, TeamKind team = TeamKind.Oxen, int partners = 4)
        {
            var r = new Rng(seed * 7919u + 13u);
            var g = Sim.NewGame("Tester", Content.Departures[departure].doy, seed);
            var board = Content.Board(new Rng(g.seed ^ 0xB0A2Du), g.Leader.name);
            for (int i = 0; i < partners && i < board.Count; i++) g.company.Add(board[i]);
            Sim.BeginOutfit(g, team);
            Outfit(g);
            Sim.FillKegs(g);
            Sim.Depart(g);
            var res = new Result { Game = g };

            for (int guard = 0; guard < 3000; guard++)
            {
                if (g.Count == 0) { res.Lost = true; break; }
                switch (g.Phase)
                {
                    case Phase.Road:
                        if (Worn(g)) { Sim.Rest(g); Trace?.Invoke(g, "rest"); continue; }
                        var d = Sim.Day(g, true);
                        Trace?.Invoke(g, "road " + d.Miles);
                        if (g.Count == 0) continue;
                        if (d.Event != null) { res.Events++; Decide(g, d.Event, r, skill, res); }
                        if (d.Arrived && g.Phase == Phase.Road) Sim.Arrive(g);
                        continue;
                    case Phase.Crossing:
                        Cross(g, r, skill);
                        continue;
                    case Phase.Haul:
                        int slips = 0;
                        for (int k = 0; k < 4; k++) if (r.Chance((1 - skill) * 0.5f + Sim.HaulDifficulty(g) * 0.2f)) slips++;
                        Sim.Hauled(g, slips);
                        continue;
                    case Phase.Stop:
                        AtStop(g, r);
                        continue;
                    case Phase.Diggings:
                        Dig(g, skill);
                        res.Reached = true;
                        break;
                }
                break;
            }
            res.Days = g.day; res.Travel = g.arrivedDay > 0 ? g.arrivedDay - g.startDoy : g.day; res.Survivors = g.Count; res.Deaths = g.deaths;
            res.Dollars = g.Count > 0 ? Sim.TakeDollars(g) : 0;
            res.Route = (g.visited.Contains("saltlake") ? "SLC" : "Greenwood") + "/" + (g.visited.Contains("lassenrancho") ? "Lassen" : "Carson");
            return res;
        }

        static bool Worn(GameState g)
        {
            if (Sim.OnDryStretch(g)) return false;
            foreach (var h in g.company) if (h.Here && h.health < 30) return g.rng.Chance(0.5f);
            return g.strength < 25 && g.rng.Chance(0.6f);
        }

        static bool Buy(GameState g, Good k, int n)
        {
            int step = Content.Step(k);
            int cost = Sim.PriceCents(g, k) * n / step;
            if (cost > g.cents || Content.Have(g, k) + n > Content.MaxOf(k)) return false;
            g.cents -= cost; Content.Add(g, k, n);
            return true;
        }

        static void Outfit(GameState g)
        {
            Buy(g, Good.Animals, 6);
            Buy(g, Good.Kegs, 5);
            Buy(g, Good.Kits, 2);
            Buy(g, Good.Medicine, 6);
            Buy(g, Good.Powder, 60);
            Buy(g, Good.Pans, 2); Buy(g, Good.Picks, 2);
            int want = g.Count * 2 * 150;
            while (g.food < want && Sim.Load(g) + 50 <= Sim.Capacity(g) && Buy(g, Good.Food, 50)) { }
            while (Sim.Load(g) > Sim.Capacity(g) && g.animals < Content.MaxAnimals && Buy(g, Good.Animals, 1)) { }
        }

        static void Decide(GameState g, EventDef e, Rng r, float skill, Result res)
        {
            var choices = e.Choices(g);
            var ok = choices.FindAll(c => c.Enabled);
            if (ok.Count == 0) return;
            var ch = ok[r.Range(0, ok.Count)];
            if (ch.Hunt != null)
            {
                res.Hunts++;
                int shots = r.Range(1, 3);
                int meat = r.Chance(0.3f + skill * 0.5f) ? (ch.Hunt == "buffalo" ? 420 : 90) : 0;
                Sim.HuntResult(g, ch.Hunt, meat, Math.Min(shots, g.powder));
            }
            else ch.Do(g);
            g.morale = Sim.Clamp(g.morale, 0, 100); g.strength = Sim.Clamp(g.strength, 0, 100); g.wagon = Sim.Clamp(g.wagon, 0, 100);
            g.food = Math.Max(0, g.food); g.powder = Math.Max(0, g.powder); g.kits = Math.Max(0, g.kits);
            g.medicine = Math.Max(0, g.medicine); g.animals = Math.Max(0, g.animals);
            foreach (var h in g.company) h.health = Sim.Clamp(h.health, 0, 100);
        }

        static void Cross(GameState g, Rng r, float skill)
        {
            var s = g.HereStop;
            var fords = Sim.Fords(g);
            Ford best = fords[0];
            foreach (var f in fords) if (Sim.Fordable(f) && (!Sim.Fordable(best) || f.Depth + f.Current * 4 < best.Depth + best.Current * 4)) best = f;
            bool deep = !Sim.Fordable(best) || best.Depth > 4.5f;
            if (deep && s.Ferry > 0 && g.Dollars >= s.Ferry)
            {
                int days = Sim.FerryDays(g);
                for (int i = 0; i < days && g.Count > 0; i++) Sim.Rest(g);
                if (g.Count > 0) Sim.Ferry(g);
                return;
            }
            if (!Sim.Fordable(best)) { Sim.Rest(g); Sim.ReadRiver(g); return; }
            float off = Sim.Clamp((1 - skill) * Sim.Drift(best) * r.Range(0.5f, 1.5f), 0, 1);
            Sim.Cross(g, best, off);
        }

        static void AtStop(GameState g, Rng r)
        {
            var s = g.HereStop;
            if (s.Prices > 0)
            {
                while ((g.animals < 6 || Sim.Load(g) > Sim.Capacity(g)) && g.animals < Content.MaxAnimals && Buy(g, Good.Animals, 1)) { }
                if (g.kits == 0) Buy(g, Good.Kits, 1);
                int need = g.Count * 2 * 40;
                while (g.food < need && Buy(g, Good.Food, 50)) { }
            }
            if (g.next == null) Sim.ChooseLeg(g, s.Next[r.Range(0, s.Next.Length)]);
            if (g.wagon < 50 && g.kits > 0) Sim.Repair(g);
            if (g.animals == 0) { Sim.Rest(g); if (g.rng.Chance(0.3f)) g.animals = 2; return; }
            Sim.Depart(g);
        }

        static void Dig(GameState g, float skill)
        {
            if (g.offers.Count == 0) Sim.OfferClaims(g);
            int bestI = 0;
            for (int i = 1; i < g.offers.Count; i++) if (g.offers[i] > g.offers[bestI]) bestI = i;
            Sim.TakeClaim(g, bestI);
            for (int guard = 0; guard < 60 && !Sim.SeasonOver(g); guard++)
            {
                if (g.day - g.freshDay > 40 && g.cents > 600 * g.Count * 3) Sim.BuyFresh(g);
                bool rest = false;
                foreach (var h in g.company) if (h.Here && h.health < 35) rest = true;
                Sim.WorkWeek(g, !rest, skill);
            }
        }
    }
}
