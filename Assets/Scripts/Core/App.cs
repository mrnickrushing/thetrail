using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    /// <summary>What the host (Unity, or a test) provides: storage and JSON.</summary>
    public interface IPlatform
    {
        string Load(string key);
        void Save(string key, string value);
        string ToJson(object o);
        T FromJson<T>(string json);
        bool Touch { get; }
    }

    /// <summary>Special key codes passed to App.Key alongside printable characters.</summary>
    public static class Keys
    {
        public const char Enter = '\n', Back = '\b', Escape = (char)27, Space = ' ';
        public const char Up = (char)0x11, Down = (char)0x12, Left = (char)0x13, Right = (char)0x14;
    }

    [Serializable]
    public class Settings
    {
        public bool sound = true;
        public int monitor;          // 0 green, 1 amber, 2 white
        public bool crt = true;
        public bool fast;            // quicker travel
    }

    [Serializable] public class ScoreEntry { public string name; public int score; public string rating; }
    [Serializable] public class Tomb { public int miles; public string name; public string epitaph; public string date; }

    [Serializable]
    public class Records
    {
        public List<ScoreEntry> top = new List<ScoreEntry>();
        public List<Tomb> tombs = new List<Tomb>();

        public static Records Default()
        {
            var r = new Records();
            (string, int)[] d =
            {
                ("Abigail Thorne", 7420), ("Jedediah Cole", 6310), ("Martha Quill", 5280), ("Silas Brennan", 4460),
                ("Hester Lowell", 3750), ("Ezekiel Marsh", 3120), ("Cora Whitlow", 2690), ("Amos Kettering", 2150),
                ("Lydia Vance", 1620), ("Otis Pruett", 1080),
            };
            foreach (var (n, s) in d) r.top.Add(new ScoreEntry { name = n, score = s, rating = Sim.Rating(s) });
            return r;
        }

        public bool MakesTopTen(int score) => top.Count < 10 || score > top[top.Count - 1].score;

        public void AddScore(string name, int score)
        {
            top.Add(new ScoreEntry { name = name, score = score, rating = Sim.Rating(score) });
            top.Sort((a, b) => b.score.CompareTo(a.score));
            if (top.Count > 10) top.RemoveRange(10, top.Count - 10);
        }
    }

    public abstract class Scene
    {
        public App App;
        /// <summary>Overlays draw on top of the scene beneath them.</summary>
        public virtual bool Overlay => false;
        protected float T;
        protected GameState G => App.Game;
        protected Synth S => App.Synth;
        public virtual void Enter() { }
        public virtual void Tick(float dt) { T += dt; }
        public abstract void Draw(Canvas c);
        public virtual void Tap(int x, int y) { }
        public virtual void Key(char k) { }
        protected bool Blink => (App.Time * 2.2f) % 2f < 1.35f;
        protected string Continue => App.Touch ? "TAP TO CONTINUE" : "PRESS SPACE TO CONTINUE";
    }

    public sealed class App
    {
        public readonly Canvas C = new Canvas();
        public readonly Synth Synth = new Synth();
        public readonly IPlatform P;
        public Settings Settings = new Settings();
        public Records Records = Records.Default();
        public GameState Game;
        public float Time;
        /// <summary>Held input, set by the host every frame (for steering the raft).</summary>
        public bool PointerDown, HeldLeft, HeldRight;
        public int PointerX, PointerY;
        readonly List<Scene> stack = new List<Scene>();
        public bool Touch => P.Touch;
        public Scene Top => stack.Count > 0 ? stack[stack.Count - 1] : null;
        public int Depth => stack.Count;

        public const string SaveKey = "thetrail.journey", RecordsKey = "thetrail.records", SettingsKey = "thetrail.settings";

        public App(IPlatform p, Func<App, Scene> first = null)
        {
            P = p;
            try { var s = p.Load(SettingsKey); if (!string.IsNullOrEmpty(s)) Settings = p.FromJson<Settings>(s) ?? new Settings(); } catch { Settings = new Settings(); }
            try { var r = p.Load(RecordsKey); if (!string.IsNullOrEmpty(r)) Records = p.FromJson<Records>(r) ?? Records.Default(); } catch { Records = Records.Default(); }
            if (Records.top == null || Records.top.Count == 0) Records.top = Records.Default().top;
            if (Records.tombs == null) Records.tombs = new List<Tomb>();
            Synth.Muted = !Settings.sound;
            Go(first != null ? first(this) : new TitleScene());
        }

        public void Go(Scene s) { stack.Clear(); Push(s); }
        public void Push(Scene s) { s.App = this; stack.Add(s); s.Enter(); }
        public void Pop() { if (stack.Count > 0) stack.RemoveAt(stack.Count - 1); }
        public void Replace(Scene s) { Pop(); Push(s); }

        public void Tick(float dt)
        {
            dt = Math.Min(dt, 0.1f);
            Time += dt;
            Top?.Tick(dt);
        }

        public void Draw()
        {
            int start = stack.Count - 1;
            while (start > 0 && stack[start].Overlay) start--;
            C.NoClip();
            C.Clear();
            for (int i = Math.Max(0, start); i < stack.Count; i++) { C.NoClip(); stack[i].Draw(C); }
            C.NoClip();
        }

        public void Tap(int x, int y) => Top?.Tap(x, y);
        public void Key(char k) => Top?.Key(k);

        // ---- Helpers for common dialogs ---------------------------------------------------------

        public void Say(string text, Action then = null, bool overlay = true) => Push(new MessageScene(text, then, overlay));
        public void SayAll(IList<string> texts, Action then)
        {
            if (texts.Count == 0) { then?.Invoke(); return; }
            var rest = new List<string>(texts); var first = rest[0]; rest.RemoveAt(0);
            Say(first, () => SayAll(rest, then));
        }

        // ---- Persistence ------------------------------------------------------------------------

        public void SaveGame()
        {
            try
            {
                if (Game == null || Game.Phase == Phase.Won || Game.Phase == Phase.Lost) P.Save(SaveKey, "");
                else P.Save(SaveKey, P.ToJson(Game));
            }
            catch { }
        }

        public GameState LoadGame()
        {
            try
            {
                var s = P.Load(SaveKey);
                if (string.IsNullOrEmpty(s)) return null;
                var g = P.FromJson<GameState>(s);
                if (g == null || g.party == null || g.party.Count == 0 || g.visited == null || g.rng == null) return null;
                if (!Trail.All.ContainsKey(g.next ?? "") || !Trail.All.ContainsKey(g.Here)) return null;
                return g;
            }
            catch { return null; }
        }

        public void ClearSave() { try { P.Save(SaveKey, ""); } catch { } }
        public void SaveRecords() { try { P.Save(RecordsKey, P.ToJson(Records)); } catch { } }
        public void SaveSettings() { try { P.Save(SettingsKey, P.ToJson(Settings)); } catch { } Synth.Muted = !Settings.sound; }
    }
}
