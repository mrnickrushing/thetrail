using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    /// <summary>One answer to an event. Do applies it and returns what happened.</summary>
    public sealed class Choice
    {
        public string Label;
        public bool Enabled = true;
        public Func<GameState, string> Do;
        public string Hunt;   // when set, the answer leads into the stalking game for this animal
        public Choice(string label, Func<GameState, string> act, bool enabled = true) { Label = label; Do = act; Enabled = enabled; }
    }

    /// <summary>A decision on the road.</summary>
    public sealed class EventDef
    {
        public string Id, Title, Art;
        public bool Once;
        public float Weight = 1;
        public Func<GameState, bool> When = g => true;
        public Func<GameState, string> Text;
        public Func<GameState, List<Choice>> Choices;
    }

    public static class Events
    {
        public static readonly List<EventDef> All = new List<EventDef>();

        public static EventDef Get(string id) { foreach (var e in All) if (e.Id == id) return e; return null; }

        public static EventDef Pick(GameState g)
        {
            float total = 0;
            var ok = new List<EventDef>();
            foreach (var e in All)
            {
                if (e.Once && g.Flag("ev_" + e.Id)) continue;
                if (g.Flag("last_" + e.Id) ) continue;
                if (!e.When(g)) continue;
                ok.Add(e); total += e.Weight;
            }
            if (ok.Count == 0) return null;
            float roll = g.rng.Float() * total;
            foreach (var e in ok)
            {
                roll -= e.Weight;
                if (roll <= 0) { Mark(g, e); return e; }
            }
            Mark(g, ok[ok.Count - 1]);
            return ok[ok.Count - 1];
        }

        static void Mark(GameState g, EventDef e)
        {
            g.SetFlag("ev_" + e.Id);
            // The same event never comes twice in a row.
            g.flags.RemoveAll(f => f.StartsWith("last_"));
            g.SetFlag("last_" + e.Id);
        }

        static int Miles(GameState g) => g.miles;
        static bool Plains(GameState g) => g.miles < 700;
        static string Animal(GameState g) => g.Team == TeamKind.Mules ? "mule" : "ox";
        static string Animals(GameState g) => g.Team == TeamKind.Mules ? "mules" : "oxen";
        static int Room(GameState g) => Math.Max(0, Sim.Capacity(g) - Sim.Load(g));
        static string Partner(GameState g)
        {
            for (int i = g.company.Count - 1; i >= 1; i--) if (g.company[i].Here) return g.company[i].name;
            return null;
        }

        static void Add(EventDef e) => All.Add(e);

        static Events()
        {
            Add(new EventDef
            {
                Id = "shortcut", Title = "A Shortcut for Sale", Art = "stranger", Weight = 1.2f,
                When = g => g.miles > 150 && g.legMiles - g.legDone > 60,
                Text = g => "A man in a good coat stops your wagon. He has a map of a cutoff \"known only to old hands\" that will save you three days. He wants $20 for it.",
                Choices = g => new List<Choice>
                {
                    new Choice("Buy the map ($20)", s =>
                    {
                        s.cents -= 2000;
                        if (s.Has(Trade.Guide)) { s.cents += 2000; return s.With(Trade.Guide).name + " looks the map over and laughs. \"This creek runs the wrong way.\" The man pockets his map and rides off."; }
                        if (s.rng.Chance(0.35f)) { int m = s.rng.Range(25, 45); s.legDone = Math.Min(s.legMiles - 1, s.legDone + m); s.miles += m; return "The cutoff is real! You rejoin the road " + m + " miles ahead of where you left it."; }
                        int d = s.rng.Range(2, 5); s.stuckDays += d; s.wagon = Math.Max(0, s.wagon - 8); s.morale -= 8;
                        return "The \"cutoff\" ends in a dry ravine. It takes " + d + " days of backtracking to find the road again.";
                    }, g.Dollars >= 20),
                    new Choice("Send him on his way", s => "He tips his hat and goes to try the next wagon."),
                },
            });
            Add(new EventDef
            {
                Id = "castoff", Title = "Goods by the Road", Art = "castoff", Weight = 1.3f,
                When = g => g.miles > 250,
                Text = g => "The road is littered with what the companies ahead threw away to lighten their wagons: a cookstove, an anvil, trunks of clothes, barrels, a crate of books.",
                Choices = g => new List<Choice>
                {
                    new Choice("Pick through it (half a day)", s =>
                    {
                        s.legDone = Math.Max(0, s.legDone - 6); s.miles = Math.Max(0, s.miles - 6);
                        float roll = s.rng.Float();
                        if (roll < 0.3f) { s.kits++; return "Under a broken wagon you find a good spoke wheel, an iron tire and bolts: a repair kit."; }
                        if (roll < 0.55f) { int p = 40; s.powder += p; return "A keg of powder and a bag of shot, still dry. " + p + " shots."; }
                        if (roll < 0.75f) { s.medicine += 2; return "A doctor's chest with two doses of medicine left in it."; }
                        s.morale += 3; return "Nothing you can use, but someone finds a fiddle with three strings. There's music that night.";
                    }),
                    new Choice("Keep moving", s => "You leave it to the next company."),
                },
            });
            Add(new EventDef
            {
                Id = "overload", Title = "Too Heavy", Art = "wagon", Weight = 3f,
                When = g => Sim.Load(g) > Sim.Capacity(g) * 1.05f,
                Text = g => "The " + Animals(g) + " are straining and the wagon cuts deep ruts. It carries " + Sim.Load(g) + " lb; your team can pull " + Sim.Capacity(g) + " lb. Something has to go.",
                Choices = g => new List<Choice>
                {
                    new Choice("Leave 300 lb of food", s => { s.food = Math.Max(0, s.food - 300); return "You stack the flour sacks by the road for whoever comes next."; }, g.food >= 300),
                    new Choice("Leave the rocker", s => { s.rockers--; return "The rocker goes on the pile of castoffs. You'll pan by hand."; }, g.rockers > 0),
                    new Choice("Leave half the kegs (empty)", s => { int k = s.kegs / 2; s.kegs -= k; s.water = Math.Min(s.water, s.kegs * Content.KegGallons); return k + " kegs left by the road."; }, g.kegs >= 2),
                    new Choice("Push on as you are", s => { s.strength -= 5; return "The team leans into the yokes. They won't last long like this."; }),
                },
            });
            Add(new EventDef
            {
                Id = "cholera", Title = "A Stricken Camp", Art = "camp", Weight = 1.2f, Once = true,
                When = g => g.miles > 100 && g.miles < 700 && g.Doy < 200,
                Text = g => "A company has stopped in the road with cholera. Two graves are fresh. A woman asks if you have any medicine to spare.",
                Choices = g => new List<Choice>
                {
                    new Choice("Give them 2 doses of medicine", s => { s.medicine -= 2; s.morale += 6; s.SetFlag("kind"); return "She takes them with both hands. \"God keep you.\" The company is quiet, and proud, as you go on."; }, g.medicine >= 2),
                    new Choice("Go round by the river (half a day)", s => { s.legDone = Math.Max(0, s.legDone - 7); s.miles = Math.Max(0, s.miles - 7); return "You give them a wide berth and camp upwind."; }),
                    new Choice("Drive straight past", s =>
                    {
                        if (s.rng.Chance(0.3f)) { var h = Sim.RandomHand(s); var l = new List<Notice>(); if (h != null) Sim.Afflict(s, h, Ill.Cholera, l); return "You hurry past. Two days later " + (h != null ? h.name : "someone") + " is doubled over with cramps. Cholera."; }
                        return "You hurry past with your faces covered.";
                    }),
                },
            });
            Add(new EventDef
            {
                Id = "stampede", Title = "Stampede", Art = "night",
                When = g => g.animals > 3 && (g.Sky == Sky.Storm || g.rng.Chance(0.4f)),
                Text = g => "Thunder in the night, and the " + Animals(g) + " broke out of the corral and scattered across the country. In the morning you have " + Math.Max(1, g.animals / 2) + " left in camp.",
                Choices = g => new List<Choice>
                {
                    new Choice("Search for them", s =>
                    {
                        int d = s.Has(Trade.Guide) || s.Has(Trade.Teamster) ? 1 : s.rng.Range(2, 4);
                        s.stuckDays += d;
                        int lost = s.rng.Chance(0.6f) ? 0 : 1;
                        s.animals -= lost;
                        return "It takes " + d + (d == 1 ? " day" : " days") + " of riding to round them up" + (lost > 0 ? ". One " + Animal(s) + " is never found." : ", every one.");
                    }),
                    new Choice("Go on with what you have", s => { int lost = Math.Max(1, s.animals / 4); s.animals -= lost; return "You leave " + lost + " " + (lost == 1 ? Animal(s) : Animals(s)) + " to the prairie."; }),
                },
            });
            Add(new EventDef
            {
                Id = "quarrel", Title = "A Quarrel", Art = "camp",
                When = g => g.Count >= 3 && g.morale < 70,
                Text = g => { var a = g.company[g.company.Count - 1]; return a.name + " accuses the others of shirking: always first to the fire, last to the yoke. Voices are raised and hands are on knives."; },
                Choices = g => new List<Choice>
                {
                    new Choice("Draw up a duty roster", s => { s.morale += 6; s.legDone = Math.Max(0, s.legDone - 5); return "You sit everyone down and write out who stands guard, who cooks, who drives. It costs the morning, but it holds."; }),
                    new Choice("Tell them to settle it themselves", s =>
                    {
                        if (s.rng.Chance(0.5f)) { s.morale += 3; return "They shout it out, then share a pipe. By supper it's forgotten."; }
                        var h = Sim.RandomHand(s); var l = new List<Notice>(); if (h != null) Sim.Afflict(s, h, Ill.Injury, l); s.morale -= 6;
                        return "It comes to blows. " + (h != null ? h.name : "Someone") + " is hurt.";
                    }),
                },
            });
            Add(new EventDef
            {
                Id = "gambler", Title = "Three-Card Monte", Art = "stranger",
                When = g => g.Dollars >= 10,
                Text = g => "A fellow on a barrelhead is throwing three cards. \"Find the queen, double your money.\" Two men have just won. They look very pleased.",
                Choices = g => new List<Choice>
                {
                    new Choice("Bet $10", s => Bet(s, 10), g.Dollars >= 10),
                    new Choice("Bet $50", s => Bet(s, 50), g.Dollars >= 50),
                    new Choice("Walk away", s => "You walk on. The two winners walk on too, back to the start of the line."),
                },
            });
            Add(new EventDef
            {
                Id = "buffalo", Title = "Buffalo", Art = "buffalo", Weight = 1.4f,
                When = g => g.miles > 200 && g.miles < 800 && g.Doy < 215,
                Text = g => "A herd of buffalo is crossing the road ahead, thousands of them, raising a cloud of dust that hides the hills.",
                Choices = g => new List<Choice>
                {
                    new Choice("Go hunting", s => "", g.powder >= 5) { Hunt = "buffalo" },
                    new Choice("Wait for them to pass", s => { s.legDone = Math.Max(0, s.legDone - 6); s.miles = Math.Max(0, s.miles - 6); return "It takes half the day for the herd to pass."; }),
                    new Choice("Drive through the gap", s =>
                    {
                        if (s.rng.Chance(0.35f)) { s.wagon -= 12; s.animals = Math.Max(1, s.animals - 1); return "The herd swings into you. An " + Animal(s) + " is gored and the wagon is battered."; }
                        return "You find a gap in the herd and slip through.";
                    }),
                },
            });
            Add(new EventDef
            {
                Id = "trade", Title = "Traders", Art = "traders", Weight = 1.3f,
                When = g => g.miles > 120,
                Text = g => Locals(g) + " ride up to the wagon. They have " + (g.miles > 1100 ? "dried fish and pine nuts" : g.miles > 600 ? "dried meat and fresh fish" : "dried buffalo meat") + " to trade, and want powder, cloth or coffee.",
                Choices = g => new List<Choice>
                {
                    new Choice("Trade 30 shots of powder", s => { s.powder -= 30; s.food += 120; s.freshDay = s.day; return "A fair trade: 120 lb of food, some of it fresh. Everyone eats well."; }, g.powder >= 30),
                    new Choice("Trade $10 of goods", s => { s.cents -= 1000; s.food += 80; s.freshDay = s.day; return "Calico, a mirror and some coffee for 80 lb of food."; }, g.Dollars >= 10),
                    new Choice("Thank them and go on", s => "They nod and ride on to the next wagon."),
                },
            });
            Add(new EventDef
            {
                Id = "tollbridge", Title = "A Toll Bridge", Art = "river", Once = true,
                When = g => g.miles > 140 && g.miles < 400,
                Text = g => "Pawnee men have built a bridge of logs over a muddy creek and ask $2 a wagon to cross. The ford beside it is steep and soft.",
                Choices = g => new List<Choice>
                {
                    new Choice("Pay the toll ($2)", s => { s.cents -= 200; return "A sound bridge, well built. You're over in minutes."; }, g.Dollars >= 2),
                    new Choice("Use the ford", s =>
                    {
                        if (s.rng.Chance(0.4f)) { s.wagon -= 10; s.stuckDays += 1; return "The wagon sinks to the hubs in the mud. It takes all day to dig it out."; }
                        return "You make it through the mud with the whole company pushing.";
                    }),
                },
            });
            Add(new EventDef
            {
                Id = "straggler", Title = "Left Behind", Art = "stranger", Once = true,
                When = g => g.miles > 300 && g.Count < 6,
                Text = g => { var n = Content.RandomName(new Rng(g.seed ^ 77)); return "A young man is sitting by the road with a carpet bag. His company left him when he fell sick, and he asks to join yours. He says his name is " + n + "."; },
                Choices = g => new List<Choice>
                {
                    new Choice("Take him in", s =>
                    {
                        var n = Content.RandomName(new Rng(s.seed ^ 77));
                        s.company.Add(new Hand { name = n, home = "Ohio", trade = Trade.Greenhorn, temper = Temper.Cheerful, stake = 15, health = 60 });
                        s.cents += 1500; s.morale += 4;
                        return n + " puts $15 in the pot, all he has, and takes his turn at the oxen without being asked.";
                    }),
                    new Choice("Give him food (50 lb)", s => { s.food -= 50; return "He thanks you and waits for the next company."; }, g.food >= 50),
                    new Choice("Leave him", s => { s.morale -= 4; return "Nobody looks back. Nobody talks much that evening either."; }),
                },
            });
            Add(new EventDef
            {
                Id = "letters", Title = "Mail Going East", Art = "stranger",
                When = g => g.miles > 200 && g.miles < 1300,
                Text = g => "A rider heading back to the States will carry letters home for 50 cents apiece.",
                Choices = g => new List<Choice>
                {
                    new Choice("Send letters home ($" + Math.Max(1, g.Count / 2) + ")", s => { s.cents -= Math.Max(1, s.Count / 2) * 100; s.morale += 7; return "Everyone writes by firelight. It's good to think of home."; }, g.Dollars >= Math.Max(1, g.Count / 2)),
                    new Choice("Save the money", s => "The rider goes on without your letters."),
                },
            });
            Add(new EventDef
            {
                Id = "alkali", Title = "Alkali Water", Art = "sink",
                When = g => g.miles > 650 && (Sim.GroundNow(g) == Ground.Sage || Sim.GroundNow(g) == Ground.Desert),
                Text = g => "Pools of water crusted white with alkali lie beside the road. The thirsty " + Animals(g) + " pull hard toward them.",
                Choices = g => new List<Choice>
                {
                    new Choice("Let them drink", s =>
                    {
                        if (s.rng.Chance(0.45f)) { s.animals = Math.Max(0, s.animals - 1); s.strength -= 5; return "By evening one " + Animal(s) + " is bloated and dying. Alkali."; }
                        s.strength += 4; return "They drink their fill and nothing comes of it. This time.";
                    }),
                    new Choice("Drive them past", s => { s.strength -= 5; return "It takes all hands to keep them on the road."; }),
                },
            });
            Add(new EventDef
            {
                Id = "burned", Title = "Grazed to the Ground", Art = "plains",
                When = g => Sim.Grass(g) < 0.45f && g.Phase == Phase.Road && !Sim.OnDryStretch(g),
                Text = g => "The companies ahead have grazed the valley bare. There is better grass in the hills, miles off the road.",
                Choices = g => new List<Choice>
                {
                    new Choice("Drive the stock to the hills (a day)", s => { s.stuckDays += 1; s.strength += 12; return "A day's grazing in the hills fills them out."; }),
                    new Choice("Push on", s => { s.strength -= 6; return "The " + Animals(s) + " nose the dust for what's left."; }),
                },
            });
            Add(new EventDef
            {
                Id = "animalsale", Title = "A Company Breaks Up", Art = "camp",
                When = g => g.miles > 300 && g.animals < Content.MaxAnimals,
                Text = g => "A company has split up in the road and is selling off what it can't divide, including " + Animals(g) + " at $" + Price(g) + " each.",
                Choices = g => new List<Choice>
                {
                    new Choice("Buy one ($" + Price(g) + ")", s => { s.cents -= Price(s) * 100; s.animals++; return "A sound " + Animal(s) + " joins your team."; }, g.Dollars >= Price(g)),
                    new Choice("Buy two ($" + Price(g) * 2 + ")", s => { s.cents -= Price(s) * 200; s.animals += 2; return "Two more for the team."; }, g.Dollars >= Price(g) * 2 && g.animals <= Content.MaxAnimals - 2),
                    new Choice("No thanks", s => "You leave them to their argument."),
                },
            });
            Add(new EventDef
            {
                Id = "hail", Title = "Hailstorm", Art = "storm",
                When = g => g.miles < 900 && (g.Sky == Sky.Storm || g.Sky == Sky.Rain),
                Text = g => "The sky turns green-black. Hail the size of hen's eggs begins to fall.",
                Choices = g => new List<Choice>
                {
                    new Choice("Shelter the animals behind the wagon", s => { s.legDone = Math.Max(0, s.legDone - 5); s.miles = Math.Max(0, s.miles - 5); return "You hold the animals' heads while the hail drums on the wagon cover. Nobody hurt."; }),
                    new Choice("Keep driving", s =>
                    {
                        if (s.rng.Chance(0.5f)) { var h = Sim.RandomHand(s); var l = new List<Notice>(); if (h != null) Sim.Afflict(s, h, Ill.Injury, l); s.wagon -= 6; return "The " + Animals(s) + " bolt and the wagon jolts into a gully. " + (h != null ? h.name + " is hurt." : ""); }
                        return "You drive through it, cover torn but no worse.";
                    }),
                },
            });
            Add(new EventDef
            {
                Id = "berries", Title = "Wild Food", Art = "plains",
                When = g => g.day - g.freshDay > 20 && Sim.GroundNow(g) != Ground.Desert,
                Text = g => "Someone spots " + (g.miles < 700 ? "wild onions and currant bushes" : g.miles < 1200 ? "serviceberries and wild onions" : "a patch of wild greens by a spring") + " a little way off the road.",
                Choices = g => new List<Choice>
                {
                    new Choice("Stop and gather (half a day)", s => { s.legDone = Math.Max(0, s.legDone - 6); s.miles = Math.Max(0, s.miles - 6); s.freshDay = s.day; foreach (var h in s.company) if (h.Here && h.ill == Ill.Scurvy) h.illDays = 0; s.morale += 3; return "Two buckets full. Fresh food keeps off scurvy."; }),
                    new Choice("Keep moving", s => "There'll be more, maybe."),
                },
            });
            Add(new EventDef
            {
                Id = "doctor", Title = "A Call for a Doctor", Art = "camp",
                When = g => g.Has(Trade.Physician),
                Text = g => "Word has gone down the road that your company has a physician. A man from another company begs " + g.With(Trade.Physician).name + " to see his sick wife.",
                Choices = g => new List<Choice>
                {
                    new Choice("Go (half a day)", s => { s.legDone = Math.Max(0, s.legDone - 6); s.miles = Math.Max(0, s.miles - 6); int fee = s.rng.Range(10, 30); s.cents += fee * 100; s.morale += 4; return "She has a fever that breaks by evening. The grateful husband insists on paying $" + fee + "."; }),
                    new Choice("Decline", s => "\"We can't stop for every wagon on the road.\""),
                },
            });
            Add(new EventDef
            {
                Id = "brokenwagon", Title = "A Broken Wagon", Art = "wagon",
                When = g => g.kits > 0,
                Text = g => "A family sits beside a wagon with a shattered wheel. They have no spare and ask if you can sell them a repair kit.",
                Choices = g => new List<Choice>
                {
                    new Choice("Sell a kit for $25", s => { s.kits--; s.cents += 2500; return "They pay gladly. You hear later they made it."; }),
                    new Choice("Give it to them", s => { s.kits--; s.morale += 7; s.SetFlag("kind"); return "The father shakes every hand in the company."; }),
                    new Choice("You can't spare it", s => "You go on. The children watch you out of sight."),
                },
            });
            Add(new EventDef
            {
                Id = "rattler", Title = "Rattlesnake", Art = "plains",
                When = g => (Sim.GroundNow(g) == Ground.Sage || Sim.GroundNow(g) == Ground.Hills || Sim.GroundNow(g) == Ground.Plains) && g.temp > 70,
                Text = g => "A rattlesnake is coiled under the wagon, buzzing, where the children were sleeping.",
                Choices = g => new List<Choice>
                {
                    new Choice("Kill it with a shovel", s =>
                    {
                        if (!s.Has(Trade.Hunter) && s.rng.Chance(0.25f)) { var h = Sim.RandomHand(s); var l = new List<Notice>(); if (h != null) Sim.Afflict(s, h, Ill.Snakebite, l); return (h != null ? h.name : "Someone") + " is bitten on the hand before it's dead."; }
                        return "One blow. It's six feet long with eleven rattles.";
                    }),
                    new Choice("Move the wagon and leave it be", s => "You move camp a few yards. The snake doesn't follow."),
                },
            });
            Add(new EventDef
            {
                Id = "thief", Title = "Thieves in the Night", Art = "night",
                When = g => g.food > 200,
                Text = g => "In the morning a sack of flour and a side of bacon are gone from the wagon. Tracks lead off toward another camp.",
                Choices = g => new List<Choice>
                {
                    new Choice("Follow the tracks (half a day)", s =>
                    {
                        s.legDone = Math.Max(0, s.legDone - 6); s.miles = Math.Max(0, s.miles - 6);
                        if (s.rng.Chance(0.5f)) return "You find your sack in a camp of sheepish young men from Indiana. They give it back.";
                        s.food -= 80; return "The tracks vanish on hard ground. 80 lb of food gone.";
                    }),
                    new Choice("Let it go", s => { s.food -= 80; return "You set a stronger guard. 80 lb of food gone."; }),
                },
            });
            Add(new EventDef
            {
                Id = "lame", Title = "A Lame " + "Animal", Art = "wagon",
                When = g => g.animals > 2 && Sim.GroundNow(g) != Ground.Prairie,
                Text = g => "One of the " + Animals(g) + " is limping badly. The sharp stones have split a hoof.",
                Choices = g => new List<Choice>
                {
                    new Choice(g.Has(Trade.Blacksmith) ? "Shoe it (" + g.With(Trade.Blacksmith).name + ")" : "Shoe it (uses a repair kit)", s =>
                    {
                        if (!s.Has(Trade.Blacksmith)) s.kits--;
                        return "Shoed with iron from the kit. It walks sound again.";
                    }, g.Has(Trade.Blacksmith) || g.kits > 0),
                    new Choice("Rest a day", s => { s.stuckDays += 1; return s.rng.Chance(0.6f) ? "A day's rest and it walks on it again." : "The rest doesn't help. You have to leave it behind tomorrow."; }),
                    new Choice("Leave it behind", s => { s.animals--; return "You turn it loose by a creek. Someone may find it."; }),
                },
            });
            Add(new EventDef
            {
                Id = "deserted", Title = "Abandoned Wagons", Art = "desert",
                When = g => Sim.OnDryStretch(g),
                Text = g => "Dead animals and abandoned wagons line the road for miles. One wagon still has barrels lashed to its side.",
                Choices = g => new List<Choice>
                {
                    new Choice("Search the wagons", s =>
                    {
                        if (s.rng.Chance(0.5f) && s.kegs > 0) { int w = Math.Min(s.kegs * Content.KegGallons - s.water, s.rng.Range(8, 25)); s.water += Math.Max(0, w); return "One barrel still holds water, warm and stale, but water: " + Math.Max(0, w) + " gallons."; }
                        if (s.rng.Chance(0.4f)) { s.kits++; return "No water, but a sound wheel and tools: a repair kit."; }
                        return "Empty. Whoever left them took everything they could carry.";
                    }),
                    new Choice("Keep moving", s => "You don't stop. Stopping is what killed these animals."),
                },
            });
            Add(new EventDef
            {
                Id = "snowwarning", Title = "Snow in the Mountains", Art = "summit", Once = true,
                When = g => g.Doy > 275 && (g.next == "summit" || g.next == "gooselake" || g.next == "lassenrancho"),
                Text = g => "A rider coming down from the mountains says snow has already fallen on the high passes. \"Get over now, or you'll winter where you stand.\"",
                Choices = g => new List<Choice>
                {
                    new Choice("Drive 14 hours a day", s => { s.hours = 14; return "Long days from now on. You'll rest on the other side."; }),
                    new Choice("Keep your pace", s => "You keep on as you were, watching the sky."),
                },
            });
            Add(new EventDef
            {
                Id = "lightload", Title = "Lighten the Load", Art = "castoff",
                When = g => g.Phase == Phase.Road && Sim.Load(g) > Sim.Capacity(g) * 0.9f && g.strength < 55,
                Text = g => "The team is tiring. You could leave the heaviest things you don't need by the road, as hundreds have before you.",
                Choices = g => new List<Choice>
                {
                    new Choice("Leave 150 lb of food", s => { s.food = Math.Max(0, s.food - 150); s.strength += 3; return "The wagon rides higher."; }, g.food > 400),
                    new Choice("Leave a repair kit", s => { s.kits--; s.strength += 2; return "60 lb of iron and timber left for someone else."; }, g.kits > 1),
                    new Choice("Keep everything", s => "You'll need all of it in California."),
                },
            });
            Add(new EventDef
            {
                Id = "fiddler", Title = "A Dance", Art = "camp",
                When = g => g.morale < 60 && g.Count >= 2,
                Text = g => "Three companies have camped together and someone has a fiddle. There's to be a dance tonight, if anyone has the legs for it.",
                Choices = g => new List<Choice>
                {
                    new Choice("Join in", s => { s.morale += 10; foreach (var h in s.company) if (h.Here) h.health -= 2; return "You dance till midnight on the prairie. Tired feet, light hearts."; }),
                    new Choice("Turn in early", s => { foreach (var h in s.company) if (h.Here) h.health += 2; return "You sleep to the sound of the fiddle across the camp."; }),
                },
            });
        }

        static int Price(GameState g) => g.Team == TeamKind.Mules ? 40 : 20;

        static string Bet(GameState g, int stake)
        {
            if (g.rng.Chance(0.2f)) { g.cents += stake * 100; return "You find the queen! He pays $" + stake + " with a sour smile and folds up his table."; }
            g.cents -= stake * 100; g.morale -= 3;
            return "The queen was never under that card. $" + stake + " gone.";
        }

        static string Locals(GameState g)
        {
            if (g.miles < 450) return "Pawnee men";
            if (g.miles < 860) return "Lakota hunters";
            if (g.miles < 1400) return "Shoshone riders";
            return "Paiute men";
        }
    }
}
