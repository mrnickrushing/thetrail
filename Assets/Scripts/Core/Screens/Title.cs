using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    public sealed class TitleScene : Scene
    {
        Menu menu;
        GameState saved;

        public override void Enter()
        {
            saved = App.LoadGame();
            menu = new Menu(App) { X = 112, Y = 132, Pitch = 15, W = 180 };
            menu.Add("Travel the trail", () => App.Go(new JobScene()));
            if (saved != null) menu.Add("Continue journey", () => { App.Game = saved; Flow.Resume(App); });
            menu.Add("Learn about the trail", () => App.Go(new LearnScene()));
            menu.Add("See the Top Ten", () => App.Go(new TopTenScene()));
            menu.Add("Options", () => App.Go(new OptionsScene()));
            S.Stop();
            S.Play(Synth.Susanna, 0.14f);
        }

        public override void Draw(Canvas c)
        {
            Art.Stars(c, 120, 3, App.Time);
            // Title lettering: big block letters with a dithered drop shadow.
            const string t = "THE TRAIL";
            int tx = (Canvas.W - Canvas.TextWidth(t, 4)) / 2;
            for (int j = 0; j < 32; j++)
                for (int i = 0; i < Canvas.TextWidth(t, 4); i++)
                {
                    int gx = i / 4, gy = j / 4;
                    char ch = t[gx / 8];
                    if (Font8x8.Pixel(ch, gx % 8, gy))
                    {
                        c.Set(tx + i + 3, 20 + j + 3, Canvas.Dark);
                        c.Set(tx + i, 20 + j, j < 4 ? Canvas.Hot : Canvas.Dither(i, j, 4.6f - j * 0.05f));
                    }
                }
            c.TextCenter(60, "1848  INDEPENDENCE TO OREGON CITY", Canvas.Mid);
            // The wagon crossing the prairie, endlessly.
            c.FillShade(0, 104, Canvas.W, 22, 1.2f);
            c.HLine(0, Canvas.W, 104, Canvas.Mid);
            int x = Canvas.W - (int)((App.Time * 18 + 230) % (Canvas.W + 160));
            Art.Outfit(c, x, 120, 4, App.Time, true);
            menu.Draw(c);
            c.TextCenter(Canvas.H - 12, App.Touch ? "Tap a choice to begin" : "Press a number to choose", Canvas.Dark);
        }

        public override void Tap(int x, int y) => menu.Tap(x, y);
        public override void Key(char k) => menu.Key(k);
    }

    public sealed class LearnScene : Scene
    {
        int page;
        static readonly (string title, string art, string text)[] Pages =
        {
            ("THE TRAIL WEST", "town",
             "In 1848 thousands of families gathered at Independence, Missouri, to travel 2,000 miles by ox-drawn wagon to the Oregon Country. The trip took five to six months. You lead one such party."),
            ("YOUR OUTFIT", "fort",
             "Oxen pull your wagon: six is a good team. Each person eats up to three pounds of food a day. Carry clothing for the cold mountains, bullets for hunting, and spare wheels, axles and tongues. Forts along the way sell supplies, but prices rise the farther west you go."),
            ("PACE AND RATIONS", "pass",
             "A faster pace covers more ground but wears your party down. Smaller rations stretch your food but weaken everyone. Watch your party's health. Rest when it slips, and they will recover."),
            ("RIVERS", "river",
             "You must cross several rivers. Fording is safe in shallow water, under about two and a half feet. Caulking the wagon and floating it is risky in a swift current. A ferry, where there is one, costs money but is safest. Sometimes it pays to wait for the water to drop."),
            ("DANGERS", "mountains",
             "Cholera, typhoid, dysentery and accidents took many emigrants. Leave in spring: too early and there's no grass for the oxen, too late and snow will catch you in the Blue Mountains."),
            ("SCORING", "valley",
             "When you reach Oregon City you earn points for every survivor, their health, and what remains in your wagon. A farmer's points count triple and a carpenter's double, since they start with less money."),
        };

        public override void Draw(Canvas c)
        {
            var p = Pages[page];
            Art.Scene(c, p.art, 0, 0, Canvas.W, 104, App.Time, page + 2);
            Ui.Title(c, p.title, 112);
            c.Paragraph(20, 128, 43, p.text, Canvas.Bright);
            c.TextRight(Canvas.W - 12, Canvas.H - 12, (page + 1) + "/" + Pages.Length, Canvas.Dark);
            Ui.Prompt(c, Continue, Blink);
        }

        void Next()
        {
            S.Click();
            if (++page >= Pages.Length) App.Go(new TitleScene());
        }

        public override void Tap(int x, int y) { if (T > 0.2f) Next(); }
        public override void Key(char k) { if (k == Keys.Escape) App.Go(new TitleScene()); else if (k == ' ' || k == Keys.Enter) Next(); }
    }

    public sealed class TopTenScene : Scene
    {
        readonly int highlight;
        public TopTenScene(int highlight = -1) { this.highlight = highlight; }

        public override void Draw(Canvas c)
        {
            Ui.Title(c, "THE TOP TEN");
            c.Text(40, 26, "Name", Canvas.Mid); c.Text(200, 26, "Points", Canvas.Mid); c.Text(272, 26, "Rating", Canvas.Mid);
            c.HLine(36, 348, 36, Canvas.Dark);
            var top = App.Records.top;
            for (int i = 0; i < top.Count; i++)
            {
                byte col = i == highlight ? (Blink ? Canvas.Hot : Canvas.Bright) : Canvas.Bright;
                int y = 44 + i * 15;
                string n = top[i].name.Length > 18 ? top[i].name.Substring(0, 18) : top[i].name;
                c.Text(24, y, (i + 1).ToString().PadLeft(2), Canvas.Dark);
                c.Text(48, y, n, col);
                c.TextRight(248, y, top[i].score.ToString(), col);
                c.Text(264, y, top[i].rating, col);
            }
            Ui.Prompt(c, Continue, Blink, Canvas.H - 12);
        }

        public override void Tap(int x, int y) { if (T > 0.2f) { S.Click(); App.Go(new TitleScene()); } }
        public override void Key(char k) { S.Click(); App.Go(new TitleScene()); }
    }

    public sealed class OptionsScene : Scene
    {
        Menu menu;
        static readonly string[] Monitors = { "green", "amber", "white" };

        public override void Enter() => Build();

        void Build()
        {
            var s = App.Settings;
            int sel = menu?.Sel ?? -1;
            menu = new Menu(App) { X = 48, Y = 52, Pitch = 18, W = 288, Sel = sel };
            menu.Add("Sound", () => { s.sound = !s.sound; App.SaveSettings(); Build(); }, true, s.sound ? "on" : "off");
            menu.Add("Monitor", () => { s.monitor = (s.monitor + 1) % 3; App.SaveSettings(); Build(); }, true, Monitors[s.monitor]);
            menu.Add("Screen glow", () => { s.crt = !s.crt; App.SaveSettings(); Build(); }, true, s.crt ? "on" : "off");
            menu.Add("Travel speed", () => { s.fast = !s.fast; App.SaveSettings(); Build(); }, true, s.fast ? "fast" : "normal");
            menu.Add("Erase top ten and graves", () => App.Push(new AskScene(App, "Erase the Top Ten list and every grave left on the trail?",
                ("Yes, erase them", () => { App.Records = Records.Default(); App.SaveRecords(); App.Say("Erased."); }),
                ("No", null))));
            menu.Add("Done", () => App.Go(new TitleScene()));
        }

        public override void Draw(Canvas c)
        {
            Ui.Title(c, "OPTIONS");
            menu.Draw(c);
            c.Paragraph(32, 176, 40, "The monitor choice changes the phosphor colour. Screen glow adds scanlines and a soft CRT bloom.", Canvas.Mid);
        }

        public override void Tap(int x, int y) => menu.Tap(x, y);
        public override void Key(char k) { if (k == Keys.Escape) App.Go(new TitleScene()); else menu.Key(k); }
    }
}
