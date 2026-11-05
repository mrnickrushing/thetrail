using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    /// <summary>Choosing who you are: the purse you start with and the score multiplier.</summary>
    public sealed class JobScene : Scene
    {
        Menu menu;

        public override void Enter()
        {
            menu = new Menu(App) { X = 56, Y = 64, Pitch = 18, W = 272 };
            menu.Add("Be a banker from Boston", () => Pick(Job.Banker));
            menu.Add("Be a carpenter from Ohio", () => Pick(Job.Carpenter));
            menu.Add("Be a farmer from Illinois", () => Pick(Job.Farmer));
            menu.Add("Find out the differences", () => App.Say(
                "A banker starts with $1,600, a carpenter with $1,300 and a farmer with $1,000.\n\n" +
                "A carpenter is handy at mending a broken wagon. A farmer knows oxen and loses fewer of them.\n\n" +
                "At the end of the trail, a carpenter's points are doubled and a farmer's are tripled."));
        }

        void Pick(Job j) => App.Go(new NamesScene(j));

        public override void Draw(Canvas c)
        {
            Ui.Title(c, "CHOOSE YOUR WAY OF LIFE");
            c.Paragraph(32, 30, 42, "Many kinds of people made the trip west. Which are you?", Canvas.Bright);
            menu.Draw(c);
            c.Paragraph(32, 150, 42, "Your choice decides how much money you start with, and how your score is counted.", Canvas.Mid);
        }

        public override void Tap(int x, int y) => menu.Tap(x, y);
        public override void Key(char k) { if (k == Keys.Escape) App.Go(new TitleScene()); else menu.Key(k); }
    }

    public sealed class NamesScene : Scene
    {
        readonly Job job;
        readonly string[] names = new string[5];
        Menu menu;
        static readonly string[] First = { "Mary", "John", "Sarah", "William", "Eliza", "Thomas", "Hannah", "James", "Ruth", "Samuel", "Clara", "Henry", "Abigail", "Jacob", "Emma", "Caleb", "Lucy", "Amos", "Martha", "Levi" };

        public NamesScene(Job job)
        {
            this.job = job;
            var r = new Rng((uint)DateTime.Now.Ticks);
            var used = new HashSet<string>();
            for (int i = 0; i < 5; i++)
            {
                string n; do n = r.Pick(First); while (!used.Add(n));
                names[i] = n;
            }
        }

        public override void Enter() => Build();

        void Build()
        {
            menu = new Menu(App) { X = 56, Y = 54, Pitch = 18, W = 272 };
            for (int i = 0; i < 5; i++)
            {
                int k = i;
                menu.Add((i == 0 ? "Leader: " : "Member: ") + names[i], () =>
                    App.Push(new TextScene(k == 0 ? "What is the name of the wagon leader?" : "Name of party member " + (k + 1) + "?", names[k], 14,
                        v => { if (v.Length > 0) names[k] = v; Build(); })));
            }
            menu.Add("These names are correct", () => App.Go(new MonthScene(job, names)));
        }

        public override void Draw(Canvas c)
        {
            Ui.Title(c, "YOUR PARTY");
            c.Paragraph(32, 28, 42, "Five people will make the journey. " + (App.Touch ? "Tap" : "Choose") + " a name to change it.", Canvas.Bright);
            menu.Draw(c);
        }

        public override void Tap(int x, int y) => menu.Tap(x, y);
        public override void Key(char k) { if (k == Keys.Escape) App.Go(new JobScene()); else menu.Key(k); }
    }

    public sealed class MonthScene : Scene
    {
        readonly Job job; readonly string[] names;
        Menu menu;
        public MonthScene(Job job, string[] names) { this.job = job; this.names = names; }

        public override void Enter()
        {
            menu = new Menu(App) { X = 112, Y = 72, Pitch = 17, W = 160 };
            for (int m = 3; m <= 7; m++) { int mm = m; menu.Add(Sim.MonthName(m), () => Start(mm)); }
            menu.Add("Ask for advice", () => App.Say(
                "Leave in March and the prairie grass hasn't grown: your oxen go hungry, and the rivers run high.\n\n" +
                "Leave in June or July and you may meet snow in the Blue Mountains before you reach Oregon.\n\n" +
                "Most parties leave in April or May."));
        }

        void Start(int month)
        {
            App.Game = Sim.NewGame(job, names, month, (uint)DateTime.Now.Ticks ^ 0x5EEDu);
            App.Go(new StoreScene(true));
        }

        public override void Draw(Canvas c)
        {
            Ui.Title(c, "WHEN TO LEAVE");
            c.Paragraph(32, 28, 42, "It is 1848. Your jumping-off place is Independence, Missouri. Which month will you set out?", Canvas.Bright);
            menu.Draw(c);
        }

        public override void Tap(int x, int y) => menu.Tap(x, y);
        public override void Key(char k) { if (k == Keys.Escape) App.Go(new TitleScene()); else menu.Key(k); }
    }

    /// <summary>The general store. At Independence it's the outfitting; at forts, a chance to restock.</summary>
    public sealed class StoreScene : Scene
    {
        readonly bool outfitting;
        readonly int[] cart = new int[7];
        Menu menu;
        bool greeted;
        static readonly string[] Names = { "Oxen", "Food", "Clothing", "Bullets", "Wagon wheels", "Wagon axles", "Wagon tongues" };
        static readonly string[] Units = { "yoke", "lb", "set", "box", "each", "each", "each" };

        public StoreScene(bool outfitting) { this.outfitting = outfitting; }

        public override void Enter()
        {
            Build();
            if (outfitting && !greeted)
            {
                greeted = true;
                App.SayAll(new[]
                {
                    "Before leaving Independence you'll need supplies. You have " + Ui.Dollars(G.cents) + " in cash, but you don't have to spend it all now.",
                    "Hiram Pruett, the outfitter, looks you over. \"Bound for Oregon, are you? I'll see you right.\"",
                    "\"Get at least three yoke of oxen, six head. Two hundred pounds of food a person to start. Two sets of clothes each for the mountains. A few boxes of bullets, and a spare of every wagon part. Wheels, axles and tongues break.\"",
                }, null);
            }
        }

        int Price(int i) => Sim.Price(G, (Sim.Good)i);

        int Bill() { int b = 0; for (int i = 0; i < 7; i++) b += cart[i] * Price(i); return b; }

        int MaxFor(int i)
        {
            switch (i)
            {
                case 0: return Math.Max(0, (20 - G.oxen) / 2);
                case 1: return Math.Max(0, 2000 - G.food);
                case 2: return Math.Max(0, 50 - G.clothing);
                case 3: return Math.Max(0, 50 - G.bullets / 20);
                case 4: return Math.Max(0, Sim.MaxSpares - G.wheels);
                case 5: return Math.Max(0, Sim.MaxSpares - G.axles);
                default: return Math.Max(0, Sim.MaxSpares - G.tongues);
            }
        }

        void Build()
        {
            menu = new Menu(App) { X = 20, Y = 50, Pitch = 16, W = 344 };
            for (int i = 0; i < 7; i++)
            {
                int k = i;
                menu.Add(Names[i], () => Ask(k));
            }
            menu.Add(outfitting ? "Done shopping, hit the trail" : "Leave the store", Leave);
        }

        void Ask(int i)
        {
            string q = i == 0 ? "How many yoke of oxen? (2 oxen to a yoke)" : i == 1 ? "How many pounds of food?" : i == 2 ? "How many sets of clothing?" :
                i == 3 ? "How many boxes of 20 bullets?" : "How many " + Names[i].ToLower() + "?";
            int max = MaxFor(i);
            if (max == 0) { App.Say(i >= 4 ? "Your wagon can only carry " + Sim.MaxSpares + " of each spare part." : "Your wagon can't hold any more of that."); return; }
            App.Push(new NumberScene(q, max, n => cart[i] = n, cart[i]));
        }

        void Leave()
        {
            int bill = Bill();
            if (bill > G.cents) { App.Say("Okay, that comes to " + Ui.Money(bill) + ". You only have " + Ui.Money(G.cents) + ". You'll need to put something back."); return; }
            if (outfitting && G.oxen + cart[0] * 2 == 0) { App.Say("\"Don't forget, you'll need oxen to pull your wagon.\""); return; }
            G.cents -= bill;
            for (int i = 0; i < 7; i++)
            {
                if (cart[i] == 0) continue;
                int n = i == 0 ? cart[i] * 2 : i == 3 ? cart[i] * 20 : cart[i];
                Sim.Give(G, (Sim.Good)i, n);
            }
            if (bill > 0) S.Good();
            if (outfitting)
            {
                G.Phase = Phase.Traveling;
                App.SaveGame();
                App.Go(new LandmarkScene(Trail.Get(Trail.Start), () => App.Go(new TravelScene(false))));
            }
            else
            {
                App.SaveGame();
                if (G.Phase == Phase.Branch) App.Go(new BranchScene());
                else App.Go(new TrailMenuScene());
            }
        }

        public override void Draw(Canvas c)
        {
            var l = Trail.Get(G.Here);
            Ui.Title(c, outfitting ? "PRUETT'S OUTFITTING" : (l.Name.ToUpper() + " STORE"));
            c.Text(20, 22, outfitting ? "Independence, Missouri" : "Prices are high out here", Canvas.Mid);
            c.TextRight(364, 22, Sim.DateLabel(G), Canvas.Mid);
            c.Text(212, 36, "price", Canvas.Dark); c.TextRight(320, 36, "buy", Canvas.Dark); c.TextRight(364, 36, "have", Canvas.Dark);
            menu.Draw(c);
            for (int i = 0; i < 7; i++)
            {
                int y = menu.Y + i * menu.Pitch;
                c.Text(196, y, Ui.Dollars(Price(i)).PadLeft(6), Canvas.Mid);
                if (cart[i] > 0) c.TextRight(320, y, cart[i].ToString(), Canvas.Hot);
                c.TextRight(364, y, Have(i).ToString(), Canvas.Dark);
            }
            int bill = Bill();
            c.HLine(16, 368, 184, Canvas.Dark);
            c.Text(20, 192, "Total bill:", Canvas.Bright);
            c.TextRight(180, 192, Ui.Money(bill), bill > G.cents ? (Blink ? Canvas.Hot : Canvas.Dark) : Canvas.Hot);
            c.Text(200, 192, "Cash:", Canvas.Bright);
            c.TextRight(364, 192, Ui.Money(G.cents), Canvas.Bright);
            c.Text(20, 206, "Amount left after:", Canvas.Mid);
            c.TextRight(364, 206, bill > G.cents ? "not enough" : Ui.Money(G.cents - bill), Canvas.Mid);
            c.TextCenter(224, "Oxen per yoke, bullets per box of 20", Canvas.Dark);
        }

        int Have(int i)
        {
            switch (i)
            {
                case 0: return G.oxen;
                case 1: return G.food;
                case 2: return G.clothing;
                case 3: return G.bullets;
                case 4: return G.wheels;
                case 5: return G.axles;
                default: return G.tongues;
            }
        }

        public override void Tap(int x, int y) => menu.Tap(x, y);
        public override void Key(char k) { if (k == Keys.Escape && !outfitting) Leave(); else menu.Key(k); }
    }
}
