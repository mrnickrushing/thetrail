using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    /// <summary>The title: a wagon train against the evening, and the main menu.</summary>
    public sealed class TitleScene : Scene
    {
        Menu menu;
        GameState saved;

        public override void Enter()
        {
            saved = App.LoadGame();
            menu = new Menu(App) { X = Canvas.W / 2 - 100, Y = 132, W = 200, Pitch = 21, H = 18 };
            if (saved != null) menu.Add("Continue the journey", () => { App.Game = saved; Flow.Resume(App); });
            menu.Add("Raise a new company", () =>
            {
                if (saved != null) App.Ask("START OVER?", "Your company on the road will be lost.", ("Start a new one", () => App.Go(new IntroScene())), ("Keep it", null));
                else App.Go(new IntroScene());
            });
            menu.Add("How to play", () => App.Push(new HelpScene()));
            menu.Add("The Assay Ledger", () => App.Push(new LedgerScene(-1)));
            menu.Add("Settings", () => App.Push(new SettingsScene()));
            menu.Add("Credits", () => App.Push(new CreditsScene()));
            if (!App.Synth.MusicPlaying) App.Synth.Music(Synth.Susanna, 0.15f, true);
        }

        public override void Draw(Canvas c)
        {
            Art.Scene(c, "title", 0, 0, Canvas.W, Canvas.H, App.Time, 3, 1);
            c.Shade(0, 120, Canvas.W, Canvas.H - 120, 0.3f);
            c.TextShadowCenter(22, "ARGONAUTS", Pal.Gold, 4);
            c.TextShadowCenter(60, "'49", Pal.GoldLight, 3);
            c.TextShadowCenter(92, "To the California gold fields, 1849", Pal.Cream);
            menu.Draw(c);
            c.Text(6, Canvas.H - 11, "Rushing Technologies", Pal.Tan);
        }

        public override void Tap(int x, int y) { if (T > 0.3f) menu.Tap(x, y); }
        public override void Key(char k) => menu.Key(k);
    }

    /// <summary>The news that starts it all, then the captain's name and the departure date.</summary>
    public sealed class IntroScene : Scene
    {
        int page;
        string name = "";
        Menu dates;

        static readonly string[] Pages =
        {
            "December 1848. The President tells Congress that the stories from California are true: gold lies in the streams of the Sierra Nevada, free for the taking.",
            "By spring the whole country has the fever. Farmers, clerks, doctors and blacksmiths sell what they own and head west. They call themselves argonauts, after the heroes who sailed for the Golden Fleece.",
            "You will raise a company, outfit a wagon at St. Joseph, Missouri, and cross two thousand miles of prairie, mountain and desert to the diggings. Then you will have until the winter rains to make your fortune.",
        };

        public override void Draw(Canvas c)
        {
            c.Fill(0, 0, Canvas.W, Canvas.H, Pal.SandLight);
            for (int j = 0; j < Canvas.H; j += 2) for (int i = 0; i < Canvas.W; i += 3) if (Art.Noise(i * 0.7f + j * 13.1f, 5) > 0.86f) c.Set(i, j, Pal.Sand);
            if (page < Pages.Length)
            {
                c.TextCenter(14, "THE WEEKLY ARGUS", Pal.WoodDark, 2);
                c.HLine(20, Canvas.W - 20, 34, Pal.WoodDark); c.HLine(20, Canvas.W - 20, 36, Pal.WoodDark);
                c.TextCenter(46, page == 0 ? "GOLD!" : page == 1 ? "THE CALIFORNIA FEVER" : "WILL YOU GO?", Pal.Black, 3);
                c.Paragraph(50, 86, Canvas.W - 100, Pages[page], Pal.WoodDark, 12);
                if (Blink) c.TextCenter(Canvas.H - 20, App.Touch ? "tap to read on" : "press space", Pal.Earth);
                return;
            }
            c.Fill(0, 0, Canvas.W, Canvas.H, Pal.Ink);
            if (dates == null) return;
            Ui.Title(c, "WHEN WILL YOU LEAVE ST. JOSEPH?");
            c.TextCenter(28, "Captain: " + name, Pal.Gold);
            dates.Draw(c);
            c.Paragraph(30, Canvas.H - 36, Canvas.W - 60, "Leave early and the spring grass is short; leave late and thousands of wagons have grazed it bare, and the best claims are gone.", Pal.Tan);
        }

        public override void Tap(int x, int y) { if (page < Pages.Length) Next(); else if (dates == null) AskName(); else dates.Tap(x, y); }
        public override void Key(char k)
        {
            if (page < Pages.Length) { if (k == ' ' || k == Keys.Enter) Next(); else if (k == Keys.Escape) App.Go(new TitleScene()); }
            else if (dates == null) AskName();
            else if (k == Keys.Escape) { dates = null; AskName(); }
            else dates.Key(k);
        }

        void Next()
        {
            S.Click();
            page++;
            if (page == Pages.Length) AskName();
        }

        void AskName()
        {
            App.Push(new TextScene("What is your name, captain?", name, 18, n =>
            {
                name = string.IsNullOrWhiteSpace(n) ? "Captain Hale" : n;
                dates = new Menu(App) { X = 24, Y = 50, W = Canvas.W - 48, Pitch = 34, H = 26 };
                foreach (var d in Content.Departures)
                {
                    var dd = d;
                    dates.Add(dd.label, () => Begin(dd.doy), true, dd.note);
                }
            }));
        }

        void Begin(int doy)
        {
            uint seed = (uint)(DateTime.Now.Ticks & 0x7FFFFFFF) | 1u;
            App.Game = Sim.NewGame(name, doy, seed);
            App.SaveGame();
            App.Go(new HireScene());
        }

        public override void Enter() { }
    }

    /// <summary>How the game works, a page at a time.</summary>
    public sealed class HelpScene : Scene
    {
        int page;
        public override bool Overlay => false;
        static readonly (string title, string text)[] Pages =
        {
            ("YOUR COMPANY", "Choose four partners from the strangers on the hiring board. Each has a trade that helps on the road or in the diggings, a temper that lifts or sours the company's spirits, and money for the pot. Experts bring skills; greenhorns bring cash."),
            ("OUTFITTING", "Spend the pot at St. Joseph. Oxen are slow but eat anything; mules are faster but need good grass. Watch the load: a wagon heavier than the team can pull wears it out. Water kegs carry you over the dry stretches; mining tools cost far more in California."),
            ("ON THE ROAD", "The wagon rolls a day at a time. Tap MAKE CAMP to change orders (hours on the road, pounds of food a day), check the company and supplies, see the map, hunt, rest or mend the wagon. Now and then you will have a decision to make."),
            ("GRASS, WATER, SPIRITS", "Thousands of companies are on the road in 1849, and their animals eat the grass ahead of you. Rest the team where the grass is good. On a dry stretch everyone drinks from the kegs. If spirits sink too low, partners leave and take their share."),
            ("RIVERS AND ROPES", "At a river, scout three fords or pay for a ferry. In a ford the current pushes the wagon off the shallow bar: hold to drive the team back onto it. On the steepest hills the wagon goes on ropes: hold to take up rope, let go to pay it out, keep the strain green."),
            ("HUNTING", "Hold to creep toward the animal while its head is down. When it lifts its head, keep still. The closer you get, the surer the shot: tap FIRE. Fresh meat keeps off scurvy."),
            ("THE DIGGINGS", "Choose a claim and work it a week at a time. Wash the week's best pan: tap as the swinging marker crosses the gold, five swirls in all. River bars are rich but flood when the rains come in November. The season ends on December 20."),
            ("THE TAKE", "Your result is the gold dust at $16 an ounce, the cash left, and $500 for every member of the company you bring through alive. The best ten companies go in the Assay Ledger."),
        };

        static readonly string[] Pictures = { "town", "courthouse", "valley", "humboldt", "ferry", "prairie", "diggings", "title" };

        public override void Draw(Canvas c)
        {
            c.Fill(0, 0, Canvas.W, Canvas.H, Pal.Ink);
            Ui.Title(c, "HOW TO PLAY  (" + (page + 1) + " OF " + Pages.Length + ")");
            c.TextCenter(40, Pages[page].title, Pal.Gold, 2);
            c.Paragraph(50, 66, Canvas.W - 100, Pages[page].text, Pal.Cream, 13);
            // A picture of the page's part of the journey along the foot of the page.
            int py = 150, ph = Canvas.H - py - 24;
            c.Fill(48, py - 2, Canvas.W - 96, ph + 4, Pal.Tan);
            Art.Scene(c, Pictures[page], 50, py, Canvas.W - 100, ph, App.Time, 3 + page, page == Pages.Length - 1 ? 1 : 0);
            int gy = py + (int)(ph * 0.8f);
            if (Pictures[page] == "valley") Art.Wagon(c, 300, gy, App.Time * 3, 0, false);
            if (Pictures[page] == "prairie")
            {
                Art.Buffalo(c, 250, gy - 6, App.Time * 0.5f, false, false, 1);
                Art.Buffalo(c, 290, gy - 4, App.Time * 0.5f + 1, true, true, 1);
                Art.Buffalo(c, 330, gy - 7, App.Time * 0.5f + 2, false, false, 1);
                Art.Antelope(c, 140, gy, 0, false, true, 1);
            }
            bool last = page == Pages.Length - 1;
            Ui.Prompt(c, App.Touch ? (last ? "tap to close" : "tap to turn the page") : (last ? "space or escape to close" : "space for the next page, escape to close"), Blink);
        }

        public override void Tap(int x, int y) { if (T > 0.2f) Next(x < 80 ? -1 : 1); }
        public override void Key(char k) { if (k == Keys.Escape) App.Pop(); else if (k == Keys.Left) Next(-1); else Next(1); }
        void Next(int d) { S.Click(); page += d; if (page < 0) page = 0; if (page >= Pages.Length) App.Pop(); }
    }

    /// <summary>The Assay Ledger: the best ten companies.</summary>
    public sealed class LedgerScene : Scene
    {
        readonly int mark;
        public LedgerScene(int mark) { this.mark = mark; }

        public override void Draw(Canvas c)
        {
            c.Fill(0, 0, Canvas.W, Canvas.H, Pal.SandLight);
            c.TextCenter(10, "THE ASSAY LEDGER", Pal.WoodDark, 2);
            c.HLine(30, Canvas.W - 30, 30, Pal.WoodDark);
            int y = 40;
            for (int i = 0; i < App.Ledger.top.Count; i++)
            {
                var e = App.Ledger.top[i];
                bool me = i == mark;
                if (me && Blink) c.Fill(26, y - 3, Canvas.W - 52, 20, Pal.GoldLight);
                c.TextRight(52, y, (i + 1) + ".", Pal.Earth);
                c.Text(60, y, e.name, Pal.Black);
                c.TextRight(Canvas.W - 36, y, "$" + e.dollars.ToString("N0"), Pal.Black);
                c.Text(60, y + 9, e.rank + (string.IsNullOrEmpty(e.note) ? "" : ". " + e.note), Pal.Earth);
                y += 21;
            }
            if (Blink) c.TextCenter(Canvas.H - 12, App.Touch ? "tap to close" : "press space", Pal.Earth);
        }

        public override void Tap(int x, int y) { if (T > 0.4f) Close(); }
        public override void Key(char k) => Close();
        void Close() { S.Click(); if (App.Depth > 1) App.Pop(); else App.Go(new TitleScene()); }
    }

    public sealed class SettingsScene : Scene
    {
        Menu menu;
        public override void Enter() => Build();

        void Build()
        {
            menu = new Menu(App) { X = Canvas.W / 2 - 130, Y = 50, W = 260, Pitch = 24, H = 20 };
            menu.Add("Sound", () => { App.Settings.sound = !App.Settings.sound; App.SaveSettings(); Build(); }, true, App.Settings.sound ? "ON" : "OFF");
            menu.Add("Music", () => { App.Settings.music = !App.Settings.music; App.SaveSettings(); if (App.Settings.music) App.Synth.Music(Synth.Susanna, 0.15f, true); else App.Synth.StopMusic(); Build(); }, true, App.Settings.music ? "ON" : "OFF");
            menu.Add("Travel speed", () => { App.Settings.fast = !App.Settings.fast; App.SaveSettings(); Build(); }, true, App.Settings.fast ? "FAST" : "NORMAL");
            menu.Add("Clear the Assay Ledger", () => App.Ask("CLEAR THE LEDGER?", "Your companies will be removed from the ledger.", ("Clear it", () => { App.Ledger = Ledger.Default(); App.SaveLedger(); }), ("Keep it", null)));
            menu.Add("Back", () => App.Pop());
        }

        public override void Draw(Canvas c)
        {
            c.Fill(0, 0, Canvas.W, Canvas.H, Pal.Ink);
            Ui.Title(c, "SETTINGS");
            menu.Draw(c);
            c.TextCenter(Canvas.H - 40, "Argonauts '49 saves on this device only.", Pal.Tan);
            c.TextCenter(Canvas.H - 28, "No accounts, no ads, nothing is collected or sent anywhere.", Pal.Tan);
        }

        public override void Tap(int x, int y) => menu.Tap(x, y);
        public override void Key(char k) { if (k == Keys.Escape) App.Pop(); else menu.Key(k); }
    }

    public sealed class CreditsScene : Scene
    {
        static readonly string[] Lines =
        {
            "ARGONAUTS '49",
            "",
            "Design, writing, pictures and code by Rushing Technologies.",
            "",
            "Lettering: font8x8 by Daniel Hepper, after Marcel Sondaar's",
            "IBM VGA fonts (public domain).",
            "Music: \"Oh! Susanna\", Stephen Foster, 1848 (public domain).",
            "",
            "The places, dates and peoples along the road are real history.",
            "The members of your company and everyone you meet are fictional.",
            "",
            "For everyone who went to see the elephant.",
        };

        public override void Draw(Canvas c)
        {
            Art.Scene(c, "title", 0, 0, Canvas.W, Canvas.H, App.Time, 3, 1);
            Ui.Panel(c, 16, 16, Canvas.W - 32, Canvas.H - 50);
            for (int i = 0; i < Lines.Length; i++) c.TextCenter(26 + i * 15, Lines[i], i == 0 ? Pal.Gold : Pal.Cream, i == 0 ? 2 : 1);
            Ui.Prompt(c, App.Touch ? "tap to close" : "press space", Blink);
        }

        public override void Tap(int x, int y) { if (T > 0.3f) App.Pop(); }
        public override void Key(char k) => App.Pop();
    }
}
