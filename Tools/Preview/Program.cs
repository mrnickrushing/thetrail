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

// Plays through the game by pressing keys and tapping, saving a screenshot of each screen.
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
    static void Dismiss() { for (int i = 0; i < 20 && app.Top is MessageScene; i++) K(' '); }
    /// <summary>Activates the current screen's menu item whose label starts with the given text.</summary>
    static void Choose(string label)
    {
        Dismiss();
        var f = app.Top.GetType().GetField("menu", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var menu = (Menu)f.GetValue(app.Top);
        int i = menu.Items.FindIndex(it => it.Label.StartsWith(label));
        if (i < 0) throw new Exception("no menu item " + label + " on " + app.Top.GetType().Name);
        menu.Activate(i); Run(0.05f);
    }

    static void Main(string[] args)
    {
        dir = args.Length > 0 ? args[0] : "preview";
        Directory.CreateDirectory(dir);
        foreach (var f in Directory.GetFiles(dir, "*.png")) File.Delete(f);
        app = new App(new FakePlatform());
        Shot("title", 2);
        K('4'); Shot("options"); K((char)27);
        K('2'); Shot("learn"); app.Tap(10, 10); Run(0.3f); Shot("learn2"); K((char)27);
        K('1'); Shot("job");
        K('4'); Shot("job_info"); K(' ');
        K('3'); Shot("names");
        K('1'); Shot("keyboard"); foreach (var ch in "ezekiel") K(ch); Shot("keyboard_typed"); K('\n');
        K('6'); Shot("month");
        K('2'); Shot("store_greeting"); K(' '); K(' '); K(' ');
        Shot("store");
        K('1'); Shot("numpad"); K('3'); K('\n');
        K('2'); foreach (var ch in "600") K(ch); K('\n');
        K('3'); K('1'); K('0'); K('\n');
        K('4'); K('5'); K('\n');
        K('5'); K('1'); K('\n'); K('6'); K('1'); K('\n'); K('7'); K('1'); K('\n');
        K('5'); K('5'); K('2'); K('5'); Shot("numpad_too_many", 0.1f); K('\n');
        Shot("store_filled");
        K('8'); Shot("independence", 1);
        K(' '); Shot("travel_day0", 0.2f);
        Run(3); Shot("travel");
        var g = app.Game;
        g.rng = new Rng(7);
        Dismiss(); app.Tap(100, 100); Run(0.1f); Shot("trail_menu");
        Choose("Check"); Shot("supplies"); K(' ');
        Choose("Look"); Shot("map"); K(' ');
        Choose("Change pace"); Shot("pace"); K('1');
        Choose("Change food"); Shot("rations"); K('1');
        // Jump to the Kansas River.
        g.legDone = g.legMiles - 1; g.miles = 101;
        Choose("Continue"); Run(2); Dismiss();
        Shot("arrive_kansas", 0.5f);
        K(' '); Shot("river");
        Choose("Get more"); Shot("river_info"); Dismiss();
        Choose("Caulk"); Shot("crossing", 1.6f); Run(2.2f); Shot("crossing_result");
        Dismiss();
        Shot("travel_after_river", 0.5f);
        // Hunting.
        Dismiss(); if (app.Top is TravelScene) app.Tap(100, 100); Run(0.1f);
        Choose("Hunt"); Shot("hunt_ask"); K('1');
        Run(4); app.Tap(200, 100); Run(0.02f); Shot("hunt");
        var hunt = (HuntScene)app.Top;
        for (int i = 0; i < 40 && hunt.Animals.Count == 0; i++) Run(0.5f);
        foreach (var a in hunt.Animals) { app.Tap((int)(a.X + a.W / 2 + a.Vx * 0.05f), (int)(a.Y - a.H / 2)); break; }
        Run(0.4f); Shot("hunt_hit");
        app.Tap(260, 7); Run(0.1f); Shot("hunt_result"); Dismiss();
        Shot("menu_after_hunt");
        // A fort: talk and store.
        g.next = "kearny"; g.visited.Add("bigblue"); g.legMiles = 119; g.legDone = 118; g.miles = 303; g.Phase = Phase.Traveling;
        app.Go(new TravelScene(false)); Run(1); Dismiss();
        Shot("fort", 0.5f); K(' ');
        Shot("fort_menu");
        Choose("Talk"); Shot("talk"); K(' ');
        Choose("Buy"); Shot("fort_store"); K((char)27);
        // Trade.
        Choose("Attempt"); Run(0.2f); Dismiss(); Shot("trade");
        while (app.Top is MessageScene || app.Top is AskScene) K('2');
        // Landmarks.
        foreach (var id in new[] { "ashhollow", "chimney", "scotts", "laramie", "indrock", "southpass", "soda", "bluemts", "dalles", "barlow" })
        {
            var l = Trail.Get(id);
            app.Go(new LandmarkScene(l, () => { }));
            Shot("lm_" + id, 0.4f);
        }
        // Travel in each terrain.
        foreach (var (id, nx) in new[] { ("laramie", "indrock"), ("southpass", "bridger"), ("hall", "snake"), ("bluemts", "dalles") })
        {
            g.visited.Add(id); g.next = nx; g.legMiles = 100; g.legDone = 60;
            g.sky = id == "southpass" ? Sky.Snow : id == "laramie" ? Sky.Rain : Sky.Clear;
            app.Go(new TravelScene(false)); Shot("travel_" + id, 0.25f);
        }
        g.sky = Sky.Clear;
        // Branch, raft.
        g.visited.Add("dalles"); g.Phase = Phase.Branch; app.Go(new BranchScene()); Shot("branch");
        Choose("Float"); Shot("raft_intro"); K(' '); K(' ');
        Run(6); app.PointerDown = true; app.PointerX = 120; Run(1); Shot("raft"); app.PointerDown = false;
        // Grave from a past journey, game over, win.
        app.Records.tombs.Add(new Tomb { miles = 400, name = "Old Pete", epitaph = "He never found the shortcut", date = "June 2, 1848" });
        app.Go(new TombScene(app.Records.tombs[0], null)); Shot("tomb");
        app.Go(new GameOverScene()); Shot("gameover");
        K(' '); foreach (var ch in "gone west") K(ch); Shot("epitaph");
        g.visited.Add("oregoncity"); g.Phase = Phase.Won;
        app.Go(new WinScene()); Shot("win", 0.5f); K(' '); Shot("score"); K(' '); Shot("topten");
        // Every hunt animal in four steps of its gait, plus a few lying where they fell.
        var c = app.C; c.NoClip(); c.Clear();
        c.Gradient(0, 0, Canvas.W, Canvas.H, 0.55f, 1.25f);
        int y = 4;
        foreach (var kind in Animals.Kinds)
        {
            var (w, h) = Animals.Size(kind);
            if (y + h > Canvas.H) break;
            for (int f = 0; f < 4; f++) Animals.Draw(kind, f * 1.57f, f == 3).Blit(c, 4 + f * (w + 6), y, f % 2 == 1, false);
            y += h + 3;
        }
        Animals.Draw("buffalo", 0.6f, false).Blit(c, 250, 4, false, true, -0.9f);
        Animals.Draw("deer", 0.6f, false).Blit(c, 250, 40, true, true, -0.9f);
        Png.Save(c, Path.Combine(dir, $"{n++:00}_animals.png"), 3);
    }
}
