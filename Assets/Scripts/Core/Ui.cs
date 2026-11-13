using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    /// <summary>A rectangular button: leather with a tan edge, gold when pressed or selected.</summary>
    public struct Btn
    {
        public int X, Y, W, H; public string Label; public bool Enabled;
        public Btn(int x, int y, int w, int h, string label, bool enabled = true) { X = x; Y = y; W = w; H = h; Label = label; Enabled = enabled; }
        public bool Hit(int x, int y) => x >= X - 3 && x < X + W + 3 && y >= Y - 3 && y < Y + H + 3;
        public void Draw(Canvas c, bool hot = false)
        {
            byte fill = !Enabled ? Pal.Walnut : hot ? Pal.Gold : Pal.Leather;
            byte edge = !Enabled ? Pal.Leather : hot ? Pal.GoldLight : Pal.Tan;
            c.RoundRect(X, Y, W, H, fill, edge);
            if (Enabled && !hot) c.HLine(X + 2, X + W - 3, Y + 1, Pal.Tan);
            byte text = !Enabled ? Pal.Tan : hot ? Pal.Ink : Pal.Cream;
            c.TextCenterIn(X, W, Y + (H - 8) / 2, Label, text);
        }
    }

    /// <summary>A vertical list of choices: tap a row, press its number, or use the arrows and Enter.</summary>
    public sealed class Menu
    {
        public sealed class Item { public string Label; public Action Do; public bool Enabled = true; public string Note; }
        public readonly List<Item> Items = new List<Item>();
        public int X = 40, Y = 60, Pitch = 22, W = 300, H = 18;
        public int Sel = -1;
        float flash; int flashed = -1;
        readonly App app;

        public Menu(App app) { this.app = app; }

        public Menu Add(string label, Action a, bool enabled = true, string note = null)
        {
            Items.Add(new Item { Label = label, Do = a, Enabled = enabled, Note = note });
            return this;
        }

        static char KeyFor(int i) => i < 9 ? (char)('1' + i) : i == 9 ? '0' : (char)('a' + i - 10);

        public int Height => Items.Count == 0 ? 0 : (Items.Count - 1) * Pitch + H;

        public void Draw(Canvas c)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                var it = Items[i];
                int y = Y + i * Pitch;
                bool hot = i == Sel || (i == flashed && app.Time < flash);
                byte fill = !it.Enabled ? Pal.Walnut : hot ? Pal.Gold : Pal.Leather;
                c.RoundRect(X, y, W, H, fill, !it.Enabled ? Pal.Leather : hot ? Pal.GoldLight : Pal.Tan);
                if (it.Enabled && !hot) c.HLine(X + 2, X + W - 3, y + 1, Pal.Tan);
                byte col = !it.Enabled ? Pal.Tan : hot ? Pal.Ink : Pal.Cream;
                int ty = y + (H - 8) / 2;
                c.Text(X + 8, ty, it.Label, col);
                if (it.Note != null) c.TextRight(X + W - 8, ty, it.Note, !it.Enabled ? Pal.Leather : hot ? Pal.Walnut : Pal.Tan);
            }
        }

        public bool Tap(int x, int y)
        {
            if (x < X - 6 || x > X + W + 6) return false;
            for (int i = 0; i < Items.Count; i++)
            {
                int top = Y + i * Pitch;
                if (y >= top - 2 && y < top + H + 2) { Activate(i); return true; }
            }
            return false;
        }

        public bool Key(char k)
        {
            if (k == Keys.Up) { Move(-1); return true; }
            if (k == Keys.Down) { Move(1); return true; }
            if ((k == Keys.Enter || k == Keys.Space) && Sel >= 0) { Activate(Sel); return true; }
            for (int i = 0; i < Items.Count; i++)
                if (char.ToLowerInvariant(k) == KeyFor(i)) { Activate(i); return true; }
            return false;
        }

        void Move(int d)
        {
            if (Items.Count == 0) return;
            int s = Sel < 0 ? (d > 0 ? -1 : Items.Count) : Sel;
            for (int n = 0; n < Items.Count; n++)
            {
                s = (s + d + Items.Count) % Items.Count;
                if (Items[s].Enabled) { Sel = s; app.Synth.Click(); return; }
            }
        }

        public void Activate(int i)
        {
            var it = Items[i];
            if (!it.Enabled) { app.Synth.Bad(); return; }
            flashed = i; flash = app.Time + 0.15f;
            app.Synth.Select();
            it.Do?.Invoke();
        }
    }

    public static class Ui
    {
        /// <summary>A walnut panel with a tan rule, a dark shadow and optionally a title band.</summary>
        public static void Panel(Canvas c, int x, int y, int w, int h, string title = null, byte band = Pal.Leather)
        {
            c.Fill(x + 3, y + 3, w, h, Pal.Black);
            c.RoundRect(x, y, w, h, Pal.Walnut, Pal.Tan);
            c.Rect(x + 2, y + 2, w - 4, h - 4, Pal.Leather);
            if (title != null)
            {
                c.Fill(x + 3, y + 3, w - 6, 15, band);
                c.HLine(x + 3, x + w - 4, y + 18, Pal.Tan);
                c.TextCenterIn(x, w, y + 7, title, band == Pal.Leather ? Pal.Gold : Pal.Ink);
            }
        }

        /// <summary>The screen's title across the top, on a leather band.</summary>
        public static void Title(Canvas c, string text, int y = 0)
        {
            c.Fill(0, y, Canvas.W, 18, Pal.Leather);
            c.HLine(0, Canvas.W, y + 18, Pal.Tan);
            c.TextCenter(y + 5, text, Pal.Gold);
        }

        /// <summary>A bar from 0..1 with a label inside.</summary>
        public static void Bar(Canvas c, int x, int y, int w, int h, float f, byte col, string label = null)
        {
            c.Fill(x, y, w, h, Pal.Ink);
            c.Rect(x, y, w, h, Pal.Leather);
            int fw = (int)Math.Round((w - 2) * Math.Max(0, Math.Min(1, f)));
            if (fw > 0) c.Fill(x + 1, y + 1, fw, h - 2, col);
            if (label != null) c.TextShadow(x + 4, y + (h - 8) / 2, label, Pal.Cream);
        }

        public static byte LevelColour(float f) => f >= 0.66f ? Pal.Green : f >= 0.4f ? Pal.GoldMid : f >= 0.2f ? Pal.Dusk : Pal.RedBright;

        public static void Prompt(Canvas c, string text, bool blink, int y = Canvas.H - 14)
        {
            if (blink) c.TextCenter(y, text, Pal.Tan);
        }

        public static string Money(int cents) => "$" + (cents / 100) + "." + (Math.Abs(cents) % 100).ToString("00");
        public static string Dollars(int cents) => "$" + (cents / 100) + (cents % 100 != 0 ? "." + (Math.Abs(cents) % 100).ToString("00") : "");

        public static byte ToneColour(Tone t) => t == Tone.Good ? Pal.Green : t == Tone.Bad ? Pal.Dusk : t == Tone.Death ? Pal.RedBright : Pal.Gold;
        public static string ToneTitle(Tone t) => t == Tone.Good ? "GOOD FORTUNE" : t == Tone.Bad ? "TROUBLE" : t == Tone.Death ? "A DEATH IN THE COMPANY" : "ON THE ROAD";
    }

    /// <summary>A boxed message: tap or press a key to go on.</summary>
    public sealed class MessageScene : Scene
    {
        readonly string text, title; readonly Action then; readonly Tone tone;
        public override bool Overlay => true;
        public MessageScene(string text, Action then, Tone tone, string title) { this.text = text; this.then = then; this.tone = tone; this.title = title; }

        public override void Enter()
        {
            if (tone == Tone.Death) S.Death(); else if (tone == Tone.Bad) S.Bad(); else if (tone == Tone.Good) S.Good();
        }

        public override void Draw(Canvas c)
        {
            c.Shade(0, 0, Canvas.W, Canvas.H, 0.45f);
            int w = 340;
            var lines = Canvas.Wrap(text, w - 32);
            int h = lines.Count * 11 + 52;
            int x = (Canvas.W - w) / 2, y = Math.Max(6, (Canvas.H - h) / 2);
            Ui.Panel(c, x, y, w, h);
            c.Fill(x + 3, y + 3, w - 6, 13, Pal.Leather);
            c.TextCenterIn(x, w, y + 6, title ?? Ui.ToneTitle(tone), Ui.ToneColour(tone));
            for (int i = 0; i < lines.Count; i++) c.Text(x + 16, y + 24 + i * 11, lines[i], Pal.Cream);
            if (Blink) c.TextCenterIn(x, w, y + h - 14, App.Touch ? "tap to continue" : "press space", Pal.Tan);
        }

        public override void Tap(int x, int y) { if (T > 0.3f) Done(); }
        public override void Key(char k) { if (k == Keys.Space || k == Keys.Enter || k == Keys.Escape) Done(); }
        void Done() { App.Synth.Click(); App.Pop(); then?.Invoke(); }
    }

    /// <summary>A question with a few answers as buttons, over the current screen.</summary>
    public sealed class AskScene : Scene
    {
        readonly string text, title; readonly Menu menu;
        public override bool Overlay => true;

        public AskScene(App app, string title, string text, params (string label, Action act)[] answers) : this(app, title, text, ToItems(app, answers)) { }

        static List<(string, Action, bool)> ToItems(App app, (string label, Action act)[] answers)
        {
            var l = new List<(string, Action, bool)>();
            foreach (var a in answers) l.Add((a.label, a.act, true));
            return l;
        }

        public AskScene(App app, string title, string text, List<(string label, Action act, bool enabled)> answers)
        {
            this.title = title; this.text = text;
            menu = new Menu(app) { Pitch = 21, H = 18 };
            foreach (var a in answers) { var act = a.act; menu.Add(a.label, () => { app.Pop(); act?.Invoke(); }, a.enabled); }
        }

        public override void Draw(Canvas c)
        {
            c.Shade(0, 0, Canvas.W, Canvas.H, 0.45f);
            int w = 380;
            var lines = Canvas.Wrap(text, w - 32);
            int h = lines.Count * 11 + menu.Height + 44 + (title != null ? 4 : 0);
            int x = (Canvas.W - w) / 2, y = Math.Max(4, (Canvas.H - h) / 2);
            Ui.Panel(c, x, y, w, h, title);
            int ty = y + (title != null ? 26 : 14);
            for (int i = 0; i < lines.Count; i++) c.Text(x + 16, ty + i * 11, lines[i], Pal.Cream);
            menu.X = x + 20; menu.Y = ty + lines.Count * 11 + 8; menu.W = w - 40;
            menu.Draw(c);
        }

        public override void Tap(int x, int y) { if (T > 0.25f) menu.Tap(x, y); }
        public override void Key(char k) => menu.Key(k);
    }

    /// <summary>On-screen keyboard for names (also takes typed letters).</summary>
    public sealed class TextScene : Scene
    {
        readonly string title; readonly int maxLen; readonly Action<string> done; string value;
        bool fresh;
        readonly List<Btn> keys = new List<Btn>();
        public override bool Overlay => true;
        const int Top = 70;

        public TextScene(string title, string start, int maxLen, Action<string> done)
        {
            this.title = title; this.maxLen = maxLen; this.done = done; value = start ?? ""; fresh = value.Length > 0;
            string[] rows = { "QWERTYUIOP", "ASDFGHJKL'", "ZXCVBNM.-&" };
            int kw = 40, kh = 26, gap = 4, x0 = (Canvas.W - (10 * kw + 9 * gap)) / 2;
            for (int r = 0; r < 3; r++)
                for (int i = 0; i < rows[r].Length; i++)
                    keys.Add(new Btn(x0 + i * (kw + gap), Top + 40 + r * (kh + gap), kw, kh, rows[r][i].ToString()));
            int y = Top + 40 + 3 * (kh + gap);
            keys.Add(new Btn(x0, y, 92, kh, "CLEAR"));
            keys.Add(new Btn(x0 + 96, y, 172, kh, "SPACE"));
            keys.Add(new Btn(x0 + 272, y, 60, kh, "DEL"));
            keys.Add(new Btn(x0 + 336, y, 100, kh, "DONE"));
        }

        public override void Draw(Canvas c)
        {
            c.Shade(0, 0, Canvas.W, Canvas.H, 0.5f);
            Ui.Panel(c, 8, Top - 14, Canvas.W - 16, Canvas.H - Top + 8, title);
            c.Fill(30, Top + 14, Canvas.W - 60, 18, Pal.Ink);
            c.Rect(30, Top + 14, Canvas.W - 60, 18, Pal.Leather);
            int tw = Canvas.TextWidth(value);
            if (fresh && value.Length > 0) c.Fill(35, Top + 18, tw + 2, 10, Pal.Leather);
            c.Text(36, Top + 19, value, Pal.Gold);
            if (Blink && value.Length < maxLen) c.Fill(37 + tw, Top + 19, 2, 8, Pal.Gold);
            foreach (var b in keys) b.Draw(c);
        }

        public override void Tap(int x, int y)
        {
            if (y < Top - 14 && T > 0.3f) { Finish(); return; }
            foreach (var b in keys) if (b.Hit(x, y)) { Press(b.Label); return; }
        }

        public override void Key(char k)
        {
            if (k == Keys.Enter) Finish();
            else if (k == Keys.Back) Press("DEL");
            else if (k == Keys.Escape) { App.Pop(); }
            else if (k == ' ') Press("SPACE");
            else if (k >= 32 && k < 127 && (char.IsLetterOrDigit(k) || ".-'&".IndexOf(k) >= 0)) Type(k);
        }

        void Press(string l)
        {
            App.Synth.Click();
            if (l != "DONE" && fresh) { fresh = false; if (l == "DEL" || l.Length == 1) value = ""; }
            if (l == "DONE") Finish();
            else if (l == "DEL") { if (value.Length > 0) value = value.Substring(0, value.Length - 1); }
            else if (l == "CLEAR") value = "";
            else if (l == "SPACE") { if (value.Length > 0 && value.Length < maxLen && !value.EndsWith(" ")) value += " "; }
            else Type(l[0]);
        }

        void Type(char ch)
        {
            if (fresh) { fresh = false; value = ""; }
            if (value.Length >= maxLen) return;
            bool startOfWord = value.Length == 0 || value.EndsWith(" ") || value.EndsWith("-");
            value += startOfWord ? char.ToUpperInvariant(ch) : char.ToLowerInvariant(ch);
        }

        void Finish()
        {
            App.Synth.Select(); App.Pop();
            done(value.Trim());
        }
    }
}
