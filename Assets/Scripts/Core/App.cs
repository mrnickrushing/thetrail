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
        public bool music = true;
        public bool fast;            // quicker travel
        public bool rateAsked;       // asked for an App Store rating (or rated from Settings): never again
    }

    [Serializable] public class LedgerEntry { public string name; public int dollars; public string rank; public string note; }

    /// <summary>The Assay Ledger: the best ten companies.</summary>
    [Serializable]
    public class Ledger
    {
        public List<LedgerEntry> top = new List<LedgerEntry>();

        public static Ledger Default()
        {
            var l = new Ledger();
            (string, int, string)[] d =
            {
                ("Hollister & Co.", 14200, "River bar, Hangtown"), ("The Vermont Boys", 11350, "Dry gulch, Hangtown"),
                ("Mabry's Company", 8900, "River bar, Hangtown"), ("Pike County Mess", 7120, "Hillside, Hangtown"),
                ("The Kellogg Party", 5600, "River bar, Lassen's"), ("Quimby & Sons", 4300, "Dry gulch, Hangtown"),
                ("Ohio Mutual", 3150, "Hillside, Hangtown"), ("The Stoddard Mess", 2400, "River bar, Lassen's"),
                ("Fenwick's Company", 1700, "Hillside, Lassen's"), ("The Truax Party", 900, "Dry gulch, Lassen's"),
            };
            foreach (var (n, s, note) in d) l.top.Add(new LedgerEntry { name = n, dollars = s, rank = Sim.Rank(s), note = note });
            return l;
        }

        public bool Makes(int dollars) => top.Count < 10 || dollars > top[top.Count - 1].dollars;

        public int Add(string name, int dollars, string note)
        {
            var e = new LedgerEntry { name = name, dollars = dollars, rank = Sim.Rank(dollars), note = note };
            top.Add(e);
            top.Sort((a, b) => b.dollars.CompareTo(a.dollars));
            if (top.Count > 10) top.RemoveRange(10, top.Count - 10);
            return top.IndexOf(e);
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
        protected bool Blink => (App.Time * 2f) % 2f < 1.4f;
        protected string Continue => App.Touch ? "Tap to continue" : "Press space to continue";
    }

    public sealed class App
    {
        public readonly Canvas C = new Canvas();
        public readonly Synth Synth = new Synth();
        public readonly IPlatform P;
        public Settings Settings = new Settings();
        public Ledger Ledger = Ledger.Default();
        public GameState Game;
        public float Time;
        /// <summary>Held input, set by the host every frame (for the minigames).</summary>
        public bool PointerDown, HeldLeft, HeldRight, HeldUp, HeldDown, HeldSpace;
        public int PointerX, PointerY;
        readonly List<Scene> stack = new List<Scene>();
        public bool Touch => P.Touch;
        public Scene Top => stack.Count > 0 ? stack[stack.Count - 1] : null;
        public int Depth => stack.Count;
        public bool Held => PointerDown || HeldSpace || HeldUp;

        public const string SaveKey = "argonauts.journey", LedgerKey = "argonauts.ledger", SettingsKey = "argonauts.settings";

        public App(IPlatform p, Func<App, Scene> first = null)
        {
            P = p;
            try { var s = p.Load(SettingsKey); if (!string.IsNullOrEmpty(s)) Settings = p.FromJson<Settings>(s) ?? new Settings(); } catch { Settings = new Settings(); }
            try { var r = p.Load(LedgerKey); if (!string.IsNullOrEmpty(r)) Ledger = p.FromJson<Ledger>(r) ?? Ledger.Default(); } catch { Ledger = Ledger.Default(); }
            if (Ledger.top == null || Ledger.top.Count == 0) Ledger = Ledger.Default();
            Synth.Muted = !Settings.sound;
            Synth.MusicOn = Settings.music;
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

        public void Say(string text, Action then = null, Tone tone = Tone.Info, string title = null) => Push(new MessageScene(text, then, tone, title));

        public void SayAll(IList<Notice> notes, Action then)
        {
            if (notes.Count == 0) { then?.Invoke(); return; }
            var rest = new List<Notice>(notes); var first = rest[0]; rest.RemoveAt(0);
            Say(first.Text, () => SayAll(rest, then), first.Tone);
        }

        public void Ask(string title, string text, params (string label, Action act)[] answers) => Push(new AskScene(this, title, text, answers));

        // ---- Rating -----------------------------------------------------------------------------

        public const string ReviewUrl = "https://apps.apple.com/app/id6830442340?action=write-review";
        /// <summary>Set by the Unity runner: Apple's in-app rating sheet, and opening a web address.</summary>
        public static Action RequestReview;
        public static Action<string> OpenUrl;

        /// <summary>
        /// Once, after a company sells up in California: "Enjoying Argonauts '49?" RATE IT brings up Apple's rating
        /// sheet; either answer means it never asks again.
        /// </summary>
        public void AskForRating()
        {
            if (Settings.rateAsked) return;
            Settings.rateAsked = true;
            SaveSettings();
            Ask("ENJOYING ARGONAUTS '49?", "If the road to California was worth the trip, a quick rating on the App Store helps other travellers find the game.",
                ("Rate it", () => { if (RequestReview != null) RequestReview(); else OpenUrl?.Invoke(ReviewUrl); }), ("No thanks", null));
        }

        /// <summary>The Settings row: straight to the App Store's review page.</summary>
        public void OpenStoreReview()
        {
            Settings.rateAsked = true;
            SaveSettings();
            OpenUrl?.Invoke(ReviewUrl);
        }

        // ---- Persistence ------------------------------------------------------------------------

        public void SaveGame()
        {
            try
            {
                if (Game == null || Game.Phase == Phase.Done || Game.Phase == Phase.Lost) P.Save(SaveKey, "");
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
                if (g == null || g.version != 2 || g.company == null || g.company.Count == 0 || g.visited == null || g.rng == null) return null;
                if (g.flags == null) g.flags = new List<string>();
                if (g.journal == null) g.journal = new List<string>();
                if (g.offers == null) g.offers = new List<float>();
                if (!Route.Exists(g.Here)) return null;
                if (g.next != null && !Route.Exists(g.next)) return null;
                if (g.Phase == Phase.Done || g.Phase == Phase.Lost || g.Count == 0) return null;
                return g;
            }
            catch { return null; }
        }

        public void ClearSave() { try { P.Save(SaveKey, ""); } catch { } }
        public void SaveLedger() { try { P.Save(LedgerKey, P.ToJson(Ledger)); } catch { } }
        public void SaveSettings()
        {
            try { P.Save(SettingsKey, P.ToJson(Settings)); } catch { }
            Synth.Muted = !Settings.sound;
            Synth.MusicOn = Settings.music;
        }
    }
}
