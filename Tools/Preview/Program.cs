using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TheTrail.Core;

sealed class FakePlatform : IPlatform
{
    readonly Dictionary<string, string> store = new Dictionary<string, string>();
    static readonly JsonSerializerOptions Opt = new JsonSerializerOptions { IncludeFields = true };
    public string Load(string key) => store.TryGetValue(key, out var v) ? v : null;
    public void Save(string key, string value) => store[key] = value;
    public string ToJson(object o) => JsonSerializer.Serialize(o, o.GetType(), Opt);
    public T FromJson<T>(string json) => JsonSerializer.Deserialize<T>(json, Opt);
    public bool Touch => true;
}

// Renders every screen of the game to PNG without Unity: dotnet run -- <folder> [only-prefix]
static class Program
{
    static App app; static string dir; static int n;

    static void Run(float seconds) { for (float t = 0; t < seconds; t += 1 / 30f) app.Tick(1 / 30f); }
    static void Shot(string name, float settle = 0.3f)
    {
        Run(settle); app.Draw();
        Png.Save(app.C, Path.Combine(dir, $"{n++:00}_{name}.png"), 2);
        Console.WriteLine($"{n - 1:00} {name}: {app.Top?.GetType().Name}");
    }
    static void K(char k) { app.Key(k); Run(0.05f); }

    static GameState Ready(uint seed)
    {
        var g = Sim.NewGame("Josiah Hale", 121, seed);
        g.company.AddRange(Content.Board(new Rng(g.seed ^ 0xB0A2Du), "Josiah Hale").GetRange(0, 4));
        Sim.BeginOutfit(g, TeamKind.Oxen);
        g.animals = 6; g.food = 1200; g.kegs = 3; g.powder = 60; g.kits = 2; g.medicine = 6; g.pans = 2; g.picks = 2;
        g.cents = 18000;
        return g;
    }

    static void At(GameState g, string id, Phase phase, string next = null)
    {
        g.visited.Add(id);
        g.Phase = phase;
        var s = Route.Get(id);
        if (next != null) foreach (var l in s.Next) if (l.To == next) Sim.SetLeg(g, l);
        if (next == null && s.Next.Length > 0) Sim.SetLeg(g, s.Next[0]);
        app.Game = g;
    }

    static void Main(string[] args)
    {
        dir = args.Length > 0 ? args[0] : "preview";
        Directory.CreateDirectory(dir);
        foreach (var f in Directory.GetFiles(dir, "*.png")) File.Delete(f);
        app = new App(new FakePlatform());
        Shot("title", 2);
        app.Push(new HelpScene()); Shot("help"); for (int hp = 2; hp <= 8; hp++) { K(Keys.Right); Shot("help" + hp); } app.Go(new TitleScene());
        app.Push(new SettingsScene()); Shot("settings"); app.Go(new TitleScene());
        app.Push(new CreditsScene()); Shot("credits"); app.Go(new TitleScene());
        app.Push(new LedgerScene(-1)); Shot("ledger"); app.Go(new TitleScene());
        app.Go(new IntroScene()); Shot("intro"); K(' '); Shot("intro2"); K(' '); K(' '); Shot("name");
        foreach (var ch in "josiah hale") K(ch); K(Keys.Enter); Shot("departure");
        K(Keys.Down); K(Keys.Enter); Run(0.2f);
        Shot("hire");
        app.Tap(30, 40); app.Tap(150, 40); app.Tap(270, 150); Run(0.1f); Shot("hire_chosen");
        K(Keys.Enter); Shot("team_ask"); K('1'); Run(0.1f);
        Shot("store");
        for (int i = 0; i < 6; i++) app.Key(Keys.Right);
        app.Key(Keys.Down); for (int i = 0; i < 20; i++) app.Key(Keys.Right);
        app.Key(Keys.Down); for (int i = 0; i < 3; i++) app.Key(Keys.Right);
        Shot("store_filled");

        var g = Ready(3);
        app.Game = g; Sim.Depart(g); app.Go(new RoadScene()); Shot("road", 0.6f);
        app.Push(new CampMenuAtStop()); Shot("camp");
        app.Go(new OrdersScene()); Shot("orders");
        app.Go(new StatusScene(0)); Shot("company");
        app.Go(new StatusScene(1)); Shot("supplies");
        g.visited.AddRange(new[] { "bigblue", "kearny", "ashhollow", "courthouse", "laramie" });
        app.Go(new MapScene()); Shot("map");
        for (int i = 0; i < 12; i++) Sim.Log(g, "Something happened on day " + i + ".");
        app.Go(new JournalScene()); Shot("journal");

        // An event card.
        var e = Events.Get("buffalo");
        app.Go(new RoadScene()); Flow.Decide(app, e, () => { }); Shot("event");

        // Every stop.
        foreach (var id in Route.Order)
        {
            var gg = Ready(5);
            var s = Route.Get(id);
            At(gg, id, s.Kind == StopKind.River ? Phase.Crossing : s.Haul > 0 ? Phase.Haul : s.End ? Phase.Diggings : Phase.Stop);
            if (s.End) continue;
            if (s.Next.Length > 1) gg.next = null;
            app.Go(new StopScene(true)); Shot("stop_" + id, 0.4f);
        }
        // Road views by ground.
        foreach (var (id, nx) in new[] { ("courthouse", "laramie"), ("devilsgate", "southpass"), ("hall", "cityrocks"), ("humboldtwells", "gravellyford"), ("sink", "ragtown"), ("ragtown", "summit"), ("blackrock", "highrock") })
        {
            var gg = Ready(6); At(gg, id, Phase.Road, nx); gg.legDone = gg.legMiles / 2;
            app.Go(new RoadScene()); Shot("road_" + id, 0.5f);
        }

        // River crossing and the ford.
        var gr = Ready(7); At(gr, "platteferry", Phase.Crossing); Sim.ReadRiver(gr);
        app.Go(new CrossingScene()); Shot("crossing");
        app.Go(new FordScene(Sim.Fords(gr)[1])); Shot("ford", 2.5f); app.HeldSpace = true; Run(1.5f); app.HeldSpace = false; Shot("ford2", 1.5f);

        // Hunting.
        var gh = Ready(8); At(gh, "kearny", Phase.Road); app.Game = gh;
        app.Go(new RoadScene()); app.Push(new StalkScene("buffalo", null)); Shot("stalk", 1);
        app.HeldSpace = true; Run(2); app.HeldSpace = false; Shot("stalk_near", 0.1f);
        var gd = Ready(9); At(gd, "ragtown", Phase.Road, "summit"); app.Go(new RoadScene()); app.Push(new StalkScene("deer", null)); Shot("stalk_deer", 1);

        // Ropes.
        var ga = Ready(10); At(ga, "ashhollow", Phase.Haul); app.Go(new HaulScene()); Shot("haul_down", 3);
        var gs = Ready(11); At(gs, "summit", Phase.Haul); app.Go(new HaulScene()); app.HeldSpace = true; Shot("haul_up", 3); app.HeldSpace = false;

        // The diggings.
        var gdg = Ready(12); At(gdg, "hangtown", Phase.Diggings); gdg.day = 120; gdg.miles = 2050;
        app.Go(new DiggingsScene(true)); Shot("diggings_arrive", 0.5f); K(' '); Shot("claims");
        K('1'); Shot("diggings");
        app.Push(new PanScene(q => { })); Shot("pan", 1.2f); K(' '); Run(0.4f); K(' '); Shot("pan2", 0.2f);
        gdg.gold = 42.5f; gdg.week = 9;
        app.Go(new PayoutScene()); Shot("payout", 1.2f);
        app.Settings.rateAsked = false; app.Go(new LedgerScene(0)); app.AskForRating(); Shot("rate_ask", 0.4f); app.Settings.rateAsked = false;
        var gl = Ready(13); At(gl, "sink", Phase.Road); foreach (var h in gl.company) { h.alive = false; h.fate = "Died of thirst, August 20"; }
        app.Go(new LostScene()); Shot("lost", 1.6f);
        app.Say("The river is up and the bar is under water. No work this week.", null, Tone.Bad); Shot("message");
        var ic = new Canvas(); ic.Clear(); Art.Icon(ic); Png.Save(ic, Path.Combine(dir, "icon_full.png"), 4);
    }
}
