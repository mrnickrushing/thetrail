using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    public enum Bottom { Gravel, Sand, Mud, Quicksand }

    /// <summary>A place to cross a river, as scouted.</summary>
    public struct Ford
    {
        public string Name;
        public float Depth;    // feet
        public float Current;  // 0..1
        public Bottom Bottom;
    }

    public static partial class Sim
    {
        // ---- Rivers -----------------------------------------------------------------------------

        /// <summary>Sets how high the river runs: spring snowmelt peaks in June, and rain raises it.</summary>
        public static void ReadRiver(GameState g)
        {
            int doy = g.Doy;
            float season = doy < 130 ? 0.55f : doy < 160 ? 0.55f + (doy - 130) / 30f * 0.45f : doy < 190 ? 1f - (doy - 160) / 30f * 0.6f : 0.35f;
            float rain = g.Sky == Sky.Storm ? 0.25f : g.Sky == Sky.Rain ? 0.12f : 0f;
            g.river = Clamp(season * 0.85f + rain + g.rng.Range(-0.08f, 0.08f), 0.05f, 1f);
        }

        public static float RiverDepth(GameState g, Stop s) => s.DepthLo + (s.DepthHi - s.DepthLo) * g.river;

        /// <summary>The three crossing places scouted at this river, today.</summary>
        public static Ford[] Fords(GameState g)
        {
            var s = g.HereStop;
            var r = new Rng(g.seed ^ (uint)(g.day * 7919) ^ Hash(s.Id));
            float d = RiverDepth(g, s);
            string[] names = { "The upstream riffle", "At the road", "Below the bend" };
            float[] depth = { 0.75f, 1f, 1.3f }, cur = { 1.3f, 1f, 0.65f };
            var fords = new Ford[3];
            for (int i = 0; i < 3; i++)
            {
                var b = (Bottom)r.Range(0, 3);
                if (s.Id == "platteferry" && r.Chance(0.4f)) b = Bottom.Quicksand;
                fords[i] = new Ford
                {
                    Name = names[i],
                    Depth = Math.Max(0.8f, d * depth[i] * r.Range(0.85f, 1.15f)),
                    Current = Clamp(s.Swift * cur[i] * r.Range(0.8f, 1.2f) * (0.7f + g.river * 0.5f), 0.05f, 1f),
                    Bottom = b,
                };
            }
            return fords;
        }

        public static string BottomName(Bottom b) => b == Bottom.Gravel ? "firm gravel" : b == Bottom.Sand ? "sand" : b == Bottom.Mud ? "soft mud" : "quicksand";
        public static string CurrentName(float c) => c < 0.25f ? "gentle" : c < 0.45f ? "steady" : c < 0.65f ? "strong" : "fierce";

        /// <summary>What the company can tell about a ford. A guide reads it exactly; others guess.</summary>
        public static string DepthReading(GameState g, Ford f)
        {
            if (g.Has(Trade.Guide)) return f.Depth.ToString("0.0") + " ft";
            int lo = Math.Max(1, (int)Math.Floor(f.Depth - 0.8f)), hi = (int)Math.Ceiling(f.Depth + 0.8f);
            return lo + " to " + hi + " ft";
        }

        /// <summary>How hard the crossing game will be: the drift speed of the wagon off the bar, 0..1.</summary>
        public static float Drift(Ford f) => Clamp(f.Current * (0.6f + f.Depth / 8f), 0.05f, 1f);

        public static bool Fordable(Ford f) => f.Depth <= 7.5f;

        /// <summary>
        /// The crossing is done. off = the share of the crossing spent off the shallow bar (0..1).
        /// Deeper water and a soft bottom punish it more; a guide helps.
        /// </summary>
        public static List<Notice> Cross(GameState g, Ford f, float off)
        {
            var log = new List<Notice>();
            var r = g.rng;
            float risk = off * 1.1f + Math.Max(0, f.Depth - 2.5f) * 0.09f + f.Current * 0.25f;
            if (f.Bottom == Bottom.Mud) risk += 0.08f; else if (f.Bottom == Bottom.Quicksand) risk += 0.18f;
            if (g.Has(Trade.Guide)) risk -= 0.08f;
            risk = Clamp(risk, 0, 1);
            float roll = r.Float();
            if (roll < risk * 0.3f)
            {
                int food = (int)(g.food * r.Range(0.2f, 0.4f));
                g.food -= food;
                int pw = g.powder / 2; g.powder -= pw;
                var lost = new List<string> { food + " lb of food", "half the powder" };
                if (g.animals > 1 && r.Chance(0.7f)) { g.animals--; lost.Add(g.Team == TeamKind.Mules ? "a mule" : "an ox"); }
                if (g.kits > 0 && r.Chance(0.4f)) { g.kits--; lost.Add("a repair kit"); }
                g.wagon = Math.Max(0, g.wagon - 15);
                log.Add(new Notice("The current caught the wagon and rolled it on its side! You lost " + Join(lost) + ".", Tone.Bad));
                if (f.Depth > 4.5f && r.Chance(0.35f))
                {
                    var h = RandomHand(g);
                    if (h != null) { h.alive = false; h.health = 0; h.fate = "Drowned crossing " + g.HereStop.Name + ", " + DateLabel(g) + "."; g.deaths++; g.morale = Clamp(g.morale - 14, 0, 100); log.Add(new Notice(h.name + " was swept away and drowned.", Tone.Death)); }
                }
                g.morale = Clamp(g.morale - 8, 0, 100);
            }
            else if (roll < risk * 0.75f)
            {
                int food = (int)(g.food * r.Range(0.05f, 0.12f));
                g.food -= food;
                int pw = g.powder / 4; g.powder -= pw;
                log.Add(new Notice("Water came over the wagon bed. " + food + " lb of food was spoiled" + (pw > 0 ? " and some powder got wet" : "") + ".", Tone.Bad));
            }
            else log.Add(new Notice("The wagon came up the far bank streaming water, everything dry inside. Well driven.", Tone.Good));
            Log(g, "Crossed " + g.HereStop.Name + ".");
            g.Phase = Phase.Stop;
            return log;
        }

        /// <summary>Takes the ferry: pays and waits in line (the days pass in camp), then a safe crossing.</summary>
        public static int FerryDays(GameState g) => g.rng.Range(0, g.HereStop.FerryWait + 1);

        public static List<Notice> Ferry(GameState g)
        {
            g.cents -= g.HereStop.Ferry * 100;
            Log(g, "Ferried over " + g.HereStop.Name + " for $" + g.HereStop.Ferry + ".");
            g.Phase = Phase.Stop;
            return new List<Notice> { new Notice("The ferrymen pole the wagon across on their raft while the animals swim behind. Safe on the far bank.", Tone.Good) };
        }

        // ---- Ropes ------------------------------------------------------------------------------

        /// <summary>How hard the haul is: steeper at the Sierra, worse in snow.</summary>
        public static float HaulDifficulty(GameState g)
        {
            float d = g.HereStop.Haul == 2 ? 0.6f : 0.4f;
            if (g.HereStop.Haul == 2 && g.Doy > 290) d += Math.Min(0.3f, (g.Doy - 290) / 60f);
            if (Load(g) > Capacity(g)) d += 0.1f;
            return Clamp(d, 0, 1);
        }

        /// <summary>The haul is done. slips = times the rope slipped or the chain snapped.</summary>
        public static List<Notice> Hauled(GameState g, int slips)
        {
            var log = new List<Notice>();
            var r = g.rng;
            bool up = g.HereStop.Haul == 2;
            if (slips == 0) log.Add(new Notice(up ? "Foot by foot the wagon comes up the granite, and over the top. The whole company cheers." : "The wagon goes down the hill as gently as a cradle.", Tone.Good));
            else
            {
                float dmg = slips * r.Range(10f, 18f);
                g.wagon = Math.Max(5, g.wagon - dmg);
                var lost = new List<string>();
                if (slips >= 2) { int f = (int)(g.food * 0.08f * slips); g.food -= f; lost.Add(f + " lb of food"); }
                if (slips >= 3 && g.kits > 0) { g.kits--; lost.Add("a repair kit"); }
                log.Add(new Notice((up ? "The chain " + (slips == 1 ? "slipped once" : "slipped " + slips + " times") + " and the wagon slid back down the rock." : "The rope ran out of control " + (slips == 1 ? "once" : slips + " times") + " and the wagon slammed down the slope.")
                    + " The wagon is badly shaken" + (lost.Count > 0 ? ", and you lost " + Join(lost) : "") + ".", Tone.Bad));
            }
            g.strength = Clamp(g.strength - (up ? 6 : 2), 0, 100);
            Log(g, (up ? "Hauled the wagon over " : "Let the wagon down at ") + g.HereStop.Name + ".");
            g.Phase = Phase.Stop;
            return log;
        }

        // ---- The diggings -----------------------------------------------------------------------

        public const int SeasonEnd = 354;   // December 20: the winter rains close the river bars

        public static string ClaimName(Claim c) => c == Claim.RiverBar ? "River bar" : c == Claim.DryGulch ? "Dry gulch" : "Hillside";
        public static string ClaimNote(Claim c) => c == Claim.RiverBar ? "Richest gravel, but floods when the winter rains come."
            : c == Claim.DryGulch ? "No water to wash with until the rains; better late in the season."
            : "Poorer but steady. Digging into the hill is hard and dangerous work.";

        static float ClaimBase(Claim c) => c == Claim.RiverBar ? 1.25f : c == Claim.DryGulch ? 1.0f : 0.8f;

        /// <summary>How good the claims still are: every week after mid-August the best are gone.</summary>
        public static float Lateness(GameState g) => Clamp(1.35f - (g.Doy - 228) / 70f, 0.35f, 1.35f);

        public static void OfferClaims(GameState g)
        {
            g.offers.Clear();
            float late = Lateness(g);
            if (g.Here == "lassenrancho") late *= 0.9f;
            for (int i = 0; i < 3; i++) g.offers.Add(ClaimBase((Claim)i) * late * g.rng.Range(0.6f, 1.4f));
        }

        public static string ProspectLabel(GameState g, float rich)
        {
            if (g.Has(Trade.Miner)) return "about " + (rich * 3f).ToString("0.0") + " oz a week a man";
            float seen = rich * new Rng((uint)(rich * 10000)).Range(0.8f, 1.2f);
            return seen >= 1.3f ? "looks rich" : seen >= 0.95f ? "looks good" : seen >= 0.65f ? "looks fair" : "looks poor";
        }

        public static void TakeClaim(GameState g, int i)
        {
            g.claim = i; g.claimRich = g.offers[i];
            Log(g, "Staked a claim: " + ClaimName((Claim)i).ToLowerInvariant() + ".");
        }

        public static int Workers(GameState g) { int n = 0; foreach (var h in g.company) if (h.Here && h.ill == Ill.None && h.health > 25) n++; return n; }

        public static int Tools(GameState g) => g.pans + g.picks;

        /// <summary>Can the claim be worked this week? River bars flood in the rains.</summary>
        public static bool Flooded(GameState g) => (Claim)g.claim == Claim.RiverBar && g.Doy > 305 && new Rng(g.seed ^ (uint)g.week * 31u).Chance(Math.Min(0.85f, (g.Doy - 305) / 40f));

        public static float WeekFactor(GameState g)
        {
            if ((Claim)g.claim == Claim.DryGulch) return g.Doy < 290 ? 0.7f : g.Doy > 305 ? 1.3f : 1f;
            return 1f;
        }

        public sealed class Week
        {
            public float Ounces;
            public int FoodCents;
            public readonly List<Notice> Notices = new List<Notice>();
        }

        /// <summary>A week at the claim. work = false rests. quality is the panning game's result (0..1).</summary>
        public static Week WorkWeek(GameState g, bool work, float quality)
        {
            var w = new Week();
            var r = g.rng;
            g.week++;
            if (work && !Flooded(g))
            {
                int men = Workers(g);
                int tooled = Math.Min(men, Tools(g));
                float rate = 3f * g.claimRich * WeekFactor(g) * (0.55f + 0.6f * quality) * (g.Has(Trade.Miner) ? 1.25f : 1f);
                float rockerBoost = Math.Min(g.rockers * 2, tooled) * 0.8f;   // each rocker doubles the work of two hands
                w.Ounces = Math.Max(0, (tooled + rockerBoost + (men - tooled) * 0.25f) * rate * r.Range(0.75f, 1.25f));
                g.gold += w.Ounces;
                g.claimRich *= 0.93f;
                g.lastPan = quality;
            }
            else if (work) w.Notices.Add(new Notice("The river is up and the bar is under water. No work this week.", Tone.Bad));

            // Living: a week of food, bought at camp prices when the wagon runs out.
            int eat = g.portion * 7 * g.Count;
            if (g.food >= eat) g.food -= eat;
            else
            {
                int need = eat - g.food; g.food = 0;
                int cents = (int)(need * PriceCents(g, Good.Food) / 50f);
                int canDust = (int)(g.gold * DollarsPerOunce * 100);
                if (g.cents >= cents) g.cents -= cents;
                else
                {
                    int rest = cents - g.cents; g.cents = 0;
                    float oz = Math.Min(g.gold, rest / (DollarsPerOunce * 100f));
                    g.gold -= oz;
                    if (rest > canDust) foreach (var h in g.company) if (h.Here) h.health -= 12;
                }
                w.FoodCents = cents;
            }

            foreach (var h in g.company)
            {
                if (!h.Here) continue;
                h.health += work ? -2f : 9f;
                h.health += g.portion >= 3 ? 3 : g.portion == 2 ? 1 : -6;
                if (h.ill != Ill.None)
                {
                    h.health -= IllDamage(h.ill) * 3f * (g.Has(Trade.Physician) ? 0.6f : 1f);
                    h.illDays -= 7;
                    if (h.ill == Ill.Scurvy && g.freshDay >= g.day - 7) h.illDays = 0;
                    if (h.illDays <= 0) { w.Notices.Add(new Notice(h.name + " is well again.", Tone.Good, false)); h.ill = Ill.None; }
                }
                else if (r.Chance(0.06f)) Afflict(g, h, Ill.CampFever, w.Notices);
                else if (work && (Claim)g.claim == Claim.Hillside && r.Chance(0.03f)) { Afflict(g, h, Ill.Injury, w.Notices); }
                h.health = Clamp(h.health, 0, 100);
                if (h.health <= 0) Die(g, h, h.ill != Ill.None ? Content.IllName(h.ill) : "exhaustion", w.Notices);
            }
            g.day += 7;
            g.morale = Clamp(g.morale + (w.Ounces > g.Count * 2 ? 6 : w.Ounces > 0 ? 1 : -4), 0, 100);
            if (w.Ounces > 0) Log(g, "Week " + g.week + ": " + w.Ounces.ToString("0.0") + " oz of gold.");
            return w;
        }

        /// <summary>A week spent looking for a better claim: new offers at today's lateness.</summary>
        public static Week Prospect(GameState g)
        {
            var w = WorkWeek(g, false, 0);
            OfferClaims(g);
            g.claim = -1;
            return w;
        }

        public static bool SeasonOver(GameState g) => g.Doy >= SeasonEnd || g.Count == 0;

        /// <summary>Buys fresh food in the camp store (keeps off scurvy).</summary>
        public static string BuyFresh(GameState g)
        {
            int cents = 600 * g.Count;
            if (g.cents < cents) return "Fresh vegetables cost " + Ui.Dollars(cents) + " for the company. You can't afford them.";
            g.cents -= cents; g.freshDay = g.day;
            foreach (var h in g.company) if (h.Here && h.ill == Ill.Scurvy) h.illDays = 0;
            g.morale = Clamp(g.morale + 4, 0, 100);
            return "Potatoes, onions and dried apples: " + Ui.Dollars(cents) + ". Nobody will get scurvy this month.";
        }

        // ---- Helpers ----------------------------------------------------------------------------

        /// <summary>A string hash that is the same on every run and platform (string.GetHashCode is not).</summary>
        public static uint Hash(string s)
        {
            uint h = 2166136261u;
            foreach (char c in s ?? "") { h ^= c; h *= 16777619u; }
            return h;
        }

        public static Hand RandomHand(GameState g)
        {
            var here = new List<Hand>();
            foreach (var h in g.company) if (h.Here) here.Add(h);
            return here.Count == 0 ? null : here[g.rng.Range(0, here.Count)];
        }

        public static string Join(List<string> items)
        {
            if (items.Count == 0) return "nothing";
            if (items.Count == 1) return items[0];
            return string.Join(", ", items.GetRange(0, items.Count - 1)) + " and " + items[items.Count - 1];
        }
    }
}
