using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    /// <summary>A river crossing: read the water, choose how to cross, watch it happen.</summary>
    public sealed class RiverScene : Scene
    {
        Menu menu;
        Sim.Crossing? crossing;
        float anim;
        Sim.CrossResult result;

        Landmark L => Trail.Get(G.Here);
        float Depth => G.riverDepthTenths / 10f;

        public override void Enter() => Build();

        void Build()
        {
            menu = new Menu(App) { X = 32, Y = 156, Pitch = 15, W = 320 };
            menu.Add("Attempt to ford the river", () => Start(Sim.Crossing.Ford));
            menu.Add("Caulk the wagon and float it", () => Start(Sim.Crossing.Float));
            if (L.Ferry > 0) menu.Add("Take the ferry ($" + L.Ferry + ")", () => Start(Sim.Crossing.Ferry), G.Dollars >= L.Ferry);
            menu.Add("Wait a day for better water", Wait);
            menu.Add("Get more information", Info);
        }

        void Wait()
        {
            var log = Sim.WaitAtRiver(G);
            App.SaveGame();
            log.Insert(0, new Notice("You camp beside the river for a day. The river is now " + Depth.ToString("0.0") + " feet deep.", true, false, true));
            Flow.Report(App, log, () => { App.Go(new RiverScene()); });
        }

        void Info() => App.SayAll(new[]
        {
            "To ford a river, you drive the wagon straight across. In water under two and a half feet it is usually safe. Deeper, and the wagon may be swamped or swept away.",
            "To float, you seal the wagon box with tar and drive the oxen to swim. It works in deep water, but a swift current can tip you over.",
            L.Ferry > 0 ? "The ferry is safest of all, but costs money, and you may wait a day or two for your turn." : "There is no ferry at this crossing.",
        }, null);

        void Start(Sim.Crossing how)
        {
            crossing = how; anim = 0;
            result = Sim.Cross(G, how);
            if (how == Sim.Crossing.Ferry) S.Select(); else S.Splash();
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (crossing == null) return;
            anim += dt;
            if (anim > 3.4f)
            {
                crossing = null;
                App.SaveGame();
                Flow.Report(App, result.Log, () => App.Go(new TravelScene(false)), true);
            }
        }

        public override void Draw(Canvas c)
        {
            Art.Scene(c, "river", 0, 0, Canvas.W, 112, App.Time, L.Name.Length);
            if (crossing != null) DrawCrossing(c);
            c.HLine(0, Canvas.W, 112, Canvas.Mid);
            c.TextCenter(118, L.Name, Canvas.Hot);
            c.TextCenter(130, "Weather: " + Sim.WeatherLabel(G) + "   " + Sim.DateLabel(G), Canvas.Mid);
            if (crossing == null)
            {
                c.TextCenter(142, "River width: " + Sim.RiverWidth(G, L) + " feet.  Depth: " + Depth.ToString("0.0") + " feet.", Canvas.Bright);
                menu.Draw(c);
            }
            else c.TextCenter(160, crossing == Sim.Crossing.Ferry ? "The ferry carries you over..." : "Crossing...", Canvas.Bright);
        }

        void DrawCrossing(Canvas c)
        {
            // The wagon works its way from the near bank to the far one, rocking in the current.
            float p = Math.Min(1f, anim / 3f);
            int x = (int)(Canvas.W - 60 - p * (Canvas.W - 120));
            int y = (int)(92 - p * 30 + Math.Sin(anim * 5) * 1.5f);
            bool trouble = result.Capsized || result.Swamped;
            if (crossing == Sim.Crossing.Ferry)
                for (int i = 0; i < 6; i++) c.Fill(x - 8, y + 34 + i * 2, 64, 1, i % 2 == 0 ? Canvas.Mid : Canvas.Bright);
            if (result.Capsized && p > 0.5f)
            {
                // Tipped: the wagon on its side, things floating off.
                c.Clip(0, 0, Canvas.W, 112);
                Art.Wagon(c, x, y + 10, anim);
                for (int i = 0; i < 5; i++) c.Fill(x + 50 + i * 9 + (int)(anim * 8), y + 32 + (i % 2) * 3, 3, 2, Canvas.Bright);
                c.NoClip();
            }
            else Art.Wagon(c, x, y, -anim * 2);
            // Water line over the wheels.
            int water = y + (crossing == Sim.Crossing.Ford ? 26 : 22) + (trouble && p > 0.5f ? -6 : 0);
            for (int i = -6; i < 56; i++)
            {
                float wv = (float)Math.Sin((x + i) * 0.4f + anim * 6);
                c.Set(x + i, water + (wv > 0 ? 0 : 1), Canvas.Hot);
                c.FillShade(x + i, water + 1, 1, 10, 0.8f);
            }
        }

        public override void Tap(int x, int y) { if (crossing == null) menu.Tap(x, y); }
        public override void Key(char k) { if (crossing == null) menu.Key(k); }
    }

    /// <summary>The party is gone. Write an epitaph for the leader's grave.</summary>
    public sealed class GameOverScene : Scene
    {
        Tomb tomb;

        public override void Enter()
        {
            var leader = G.party[0];
            tomb = new Tomb { miles = G.miles, name = leader.name, epitaph = "", date = Sim.DateLabel(G) };
            S.Death();
        }

        public override void Draw(Canvas c)
        {
            {
                Art.Stars(c, 120, 30, App.Time);
                c.FillShade(0, 150, Canvas.W, 90, 1.2f);
                Art.Tombstone(c, 150, 70, 84, 80);
                c.TextCenter(24, "Everyone in your party has died.", Canvas.Hot);
                c.TextCenter(40, G.miles + " miles from Independence. " + Sim.DateLabel(G), Canvas.Mid);
                c.TextCenter(176, "Would you like to write an epitaph", Canvas.Bright);
                c.TextCenter(188, "for " + tomb.name + "? Future travelers", Canvas.Bright);
                c.TextCenter(200, "will pass this grave.", Canvas.Bright);
                Ui.Prompt(c, Continue, Blink, Canvas.H - 14);
            }
        }

        public override void Tap(int x, int y) { if (T > 0.6f) Next(); }
        public override void Key(char k) { if (k == ' ' || k == Keys.Enter) Next(); }

        void Next()
        {
            S.Click();
            App.Push(new TextScene("Epitaph for " + tomb.name + ":", "", 40, text =>
            {
                tomb.epitaph = text;
                App.Records.tombs.Add(tomb);
                if (App.Records.tombs.Count > 25) App.Records.tombs.RemoveAt(0);
                App.SaveRecords();
                App.ClearSave();
                App.Go(new TombScene(tomb, () => App.Go(new TitleScene()), true));
            }));
        }
    }

    /// <summary>Oregon City. The score, and maybe the Top Ten.</summary>
    public sealed class WinScene : Scene
    {
        int stage;
        int score;

        public override void Enter()
        {
            score = Sim.Score(G);
            App.ClearSave();
            S.Stop();
            S.Play(Synth.Susanna, 0.12f);
        }

        public override void Draw(Canvas c)
        {
            if (stage == 0)
            {
                Art.Scene(c, "valley", 0, 0, Canvas.W, 150, App.Time, 3);
                c.HLine(0, Canvas.W, 150, Canvas.Mid);
                c.TextCenter(158, "You have reached Oregon City!", Canvas.Hot);
                c.TextCenter(170, Sim.DateLabel(G) + ", " + G.day + " days on the trail", Canvas.Mid);
                c.Paragraph(24, 186, 42, G.Alive == G.party.Count
                    ? "Your whole party made it. Rich land waits in the Willamette Valley."
                    : G.Alive + " of " + G.party.Count + " in your party made it. You will remember those you left beside the trail.", Canvas.Bright);
                Ui.Prompt(c, Continue, Blink, Canvas.H - 10);
                return;
            }
            Ui.Title(c, "POINTS FOR ARRIVING");
            int y = 28;
            foreach (var l in Sim.ScoreLines(G))
            {
                c.TextRight(56, y, l.Count.ToString(), Canvas.Mid);
                c.Text(68, y, l.What, Canvas.Bright);
                c.TextRight(344, y, l.Points.ToString(), Canvas.Bright);
                y += 13;
            }
            c.HLine(240, 352, y, Canvas.Mid);
            int mult = Sim.Multiplier(G.job);
            c.Text(68, y + 6, "Bonus for a " + Sim.JobName(G.job), Canvas.Mid);
            c.TextRight(344, y + 6, "x" + mult, Canvas.Mid);
            c.Text(68, y + 22, "Total", Canvas.Hot);
            c.TextRight(344, y + 22, score.ToString(), Canvas.Hot);
            c.TextCenter(y + 42, "Rating: " + Sim.Rating(score), Canvas.Bright);
            Ui.Prompt(c, Continue, Blink, Canvas.H - 12);
        }

        public override void Tap(int x, int y) { if (T > 0.6f) Next(); }
        public override void Key(char k) { if (k == ' ' || k == Keys.Enter) Next(); }

        void Next()
        {
            S.Click(); T = 0;
            if (stage == 0) { stage = 1; return; }
            if (App.Records.MakesTopTen(score))
            {
                string name = G.party[0].name;
                App.Records.AddScore(name, score);
                App.SaveRecords();
                int idx = App.Records.top.FindIndex(e => e.name == name && e.score == score);
                App.Go(new TopTenScene(idx));
            }
            else App.Go(new TopTenScene());
        }
    }
}
