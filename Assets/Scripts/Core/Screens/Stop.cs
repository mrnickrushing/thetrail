using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    /// <summary>
    /// A stop on the road: the picture, what it is, and what can be done here: talk, buy, rest, then
    /// cross the river, work the ropes, or choose the road on.
    /// </summary>
    public sealed class StopScene : Scene
    {
        readonly bool fresh;
        Menu menu;
        int talk;
        const int ViewH = 150;

        public StopScene(bool fresh) { this.fresh = fresh; }

        Stop Here => G.HereStop;

        public override void Enter()
        {
            if (G.Phase == Phase.Road) G.Phase = Phase.Stop;
            if (G.Phase == Phase.Crossing && G.HereStop.Kind == StopKind.River && fresh) { Sim.ReadRiver(G); App.SaveGame(); }
            Build();
        }

        void Build()
        {
            menu = new Menu(App) { X = 300, Y = ViewH + 12, W = 170, Pitch = 20, H = 17 };
            if (G.Phase == Phase.Crossing) menu.Add("Cross the river", () => App.Go(new CrossingScene()));
            else if (G.Phase == Phase.Haul) menu.Add(Here.Haul == 2 ? "Haul the wagon up" : "Let the wagon down", () => App.Go(new HaulScene()));
            else if (G.next == null) menu.Add("Choose the road on", ChooseRoad);
            else menu.Add("Back on the road", Leave);
            menu.Add("Talk to people", Talk);
            if (Here.Store) menu.Add("Buy supplies", () => App.Push(new StoreScene(false)));
            menu.Add("Rest a day", Rest);
            menu.Add("Camp", () => App.Push(new CampMenuAtStop()), true, "orders, map");
        }

        void Leave()
        {
            string why = Sim.CannotLeave(G);
            if (G.animals == 0) { App.Say("You have no animals to pull the wagon. Buy some here, or wait for a company to sell you some.", null, Tone.Bad); return; }
            if (why != null && G.animals < Content.MinAnimals) { App.Ask("SHORT-HANDED", why + " Go on anyway, slowly?", ("Go on anyway", Go), ("Stay", null)); return; }
            Go();
        }

        void Go()
        {
            Sim.Depart(G);
            App.SaveGame();
            App.Go(new RoadScene());
        }

        void ChooseRoad()
        {
            var answers = new List<(string, Action, bool)>();
            string text = "";
            foreach (var l in Here.Next)
            {
                var leg = l;
                text += (leg.Label ?? Route.Get(leg.To).Name) + ": " + leg.Note + "\n";
                answers.Add((leg.Label ?? Route.Get(leg.To).Name, () => { Sim.ChooseLeg(G, leg); App.SaveGame(); Build(); }, true));
            }
            answers.Add(("Not yet", null, true));
            App.Push(new AskScene(App, "THE ROAD DIVIDES", text.TrimEnd(), answers));
        }

        void Talk()
        {
            if (Here.Talk.Length == 0) return;
            var (who, say) = Here.Talk[talk % Here.Talk.Length];
            talk++;
            App.Say("\"" + say + "\"", null, Tone.Info, who.ToUpperInvariant());
        }

        void Rest()
        {
            var res = Sim.Rest(G);
            if (Here.Water) Sim.FillKegs(G);
            if (G.Phase == Phase.Crossing) Sim.ReadRiver(G);
            App.SaveGame();
            var log = new List<Notice> { new Notice("A day of rest at " + Here.Name.Split(',')[0] + ".", Tone.Good) };
            log.AddRange(res.Notices);
            Flow.Report(App, log, Build);
        }

        public override void Draw(Canvas c)
        {
            Art.Scene(c, Here.Art, 0, 0, Canvas.W, ViewH, App.Time, (int)(Sim.Hash(Here.Id) & 0xFF), 0, G.Sky);
            c.Fill(0, ViewH, Canvas.W, Canvas.H - ViewH, Pal.Walnut);
            c.HLine(0, Canvas.W, ViewH, Pal.Tan);
            c.TextShadow(10, 8, Here.Name, Pal.Gold, 2);
            c.TextShadow(12, 28, Sim.DateLabel(G) + ", 1849.  " + G.miles + " miles from St. Joseph.", Pal.Cream);
            c.Paragraph(10, ViewH + 10, 280, Here.Blurb, Pal.Cream, 11);
            int y = ViewH + 12 + Canvas.Wrap(Here.Blurb, 280).Count * 11 + 6;
            if (G.Phase == Phase.Crossing)
                c.Paragraph(10, y, 280, "River " + Here.Width + " ft wide, about " + Sim.RiverDepth(G, Here).ToString("0") + " ft deep, " + Sim.CurrentName(Here.Swift * (0.7f + G.river * 0.5f)) + " current.", Pal.Dusk);
            else if (G.next == null) c.Text(10, y, "The road divides here.", Pal.Dusk);
            else if (G.next != null)
            {
                var leg = Sim.CurrentLeg(G);
                c.Paragraph(10, y, 280, "Next: " + Route.Get(leg.To).Name + ", " + leg.Miles + " miles." + (leg.Dry > 0 ? " The first " + leg.Dry + " are dry." : ""), Pal.Tan);
            }
            c.Text(10, Canvas.H - 14, "Food " + G.food + " lb   Cash " + Ui.Dollars(G.cents) + "   " + (G.Team == TeamKind.Mules ? "Mules " : "Oxen ") + G.animals, Pal.Tan);
            menu.Draw(c);
        }

        public override void Tap(int x, int y) { if (T > 0.3f) menu.Tap(x, y); }
        public override void Key(char k) => menu.Key(k);
    }

    /// <summary>The camp menu while stopped: the same panels as on the road, without moving on.</summary>
    public sealed class CampMenuAtStop : Scene
    {
        Menu menu;
        public override bool Overlay => true;

        public override void Enter()
        {
            menu = new Menu(App) { X = Canvas.W - 214, Y = 34, W = 200, Pitch = 20, H = 17 };
            menu.Add("Close", () => App.Pop());
            menu.Add("Orders", () => App.Push(new OrdersScene()), true, G.hours + " hrs, " + G.portion + " lb");
            menu.Add("The company", () => App.Push(new StatusScene(0)));
            menu.Add("Supplies", () => App.Push(new StatusScene(1)));
            menu.Add("Map", () => App.Push(new MapScene()));
            menu.Add("Journal", () => App.Push(new JournalScene()));
            menu.Add("Go hunting", () => App.Push(new StalkScene(null, null)), G.powder >= 5, G.powder + " shots");
            menu.Add("Mend the wagon", () => App.Say(Sim.Repair(G), () => { App.SaveGame(); App.Pop(); }, Tone.Good, "REPAIRS"), G.kits > 0 && G.wagon < 90, G.kits + " kits");
            menu.Add("Save and quit", () => { App.SaveGame(); App.Synth.StopMusic(); App.Go(new TitleScene()); });
        }

        public override void Draw(Canvas c)
        {
            c.Shade(0, 0, Canvas.W, Canvas.H, 0.4f);
            Ui.Panel(c, Canvas.W - 226, 8, 220, menu.Height + 36, "CAMP");
            menu.Draw(c);
        }

        public override void Tap(int x, int y) { if (T > 0.2f && !menu.Tap(x, y) && x < Canvas.W - 230) App.Pop(); }
        public override void Key(char k) { if (k == Keys.Escape) App.Pop(); else menu.Key(k); }
    }
}
