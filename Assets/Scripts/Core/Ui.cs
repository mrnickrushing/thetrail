using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    /// <summary>A numbered list of choices: tap a row or press its number (or arrows and Enter).</summary>
    public sealed class Menu
    {
        public sealed class Item { public string Label; public Action Do; public bool Enabled = true; public string Note; }
        public readonly List<Item> Items = new List<Item>();
        public int X = 40, Y = 60, Pitch = 14, W = 312;
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

        public int Height => Items.Count * Pitch;

        public void Draw(Canvas c)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                var it = Items[i];
                int y = Y + i * Pitch;
                bool hi = i == Sel || (i == flashed && app.Time < flash);
                if (hi) c.Fill(X - 6, y - (Pitch - 8) / 2 - 1, W + 12, Math.Max(10, Pitch - 2), Canvas.Dark);
                byte col = !it.Enabled ? Canvas.Dark : hi ? Canvas.Hot : Canvas.Bright;
                c.Text(X, y, KeyFor(i) + ". " + it.Label, col);
                if (it.Note != null) c.TextRight(X + W, y, it.Note, it.Enabled ? Canvas.Mid : Canvas.Dark);
            }
        }

        public bool Tap(int x, int y)
        {
            if (x < X - 24 || x > X + W + 24) return false;
            int top = Y - (Pitch - 8) / 2 - 1;
            if (y < top) return false;
            int i = (y - top) / Pitch;
            if (i < 0 || i >= Items.Count) return false;
            Activate(i);
            return true;
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
            if (!it.Enabled) { app.Synth.Beep(150, 0.08f); return; }
            flashed = i; flash = app.Time + 0.15f;
            app.Synth.Select();
            it.Do?.Invoke();
        }
    }

    /// <summary>A rectangular on-screen button.</summary>
    public struct Btn
    {
        public int X, Y, W, H; public string Label;
        public Btn(int x, int y, int w, int h, string label) { X = x; Y = y; W = w; H = h; Label = label; }
        public bool Hit(int x, int y) => x >= X - 2 && x < X + W + 2 && y >= Y - 2 && y < Y + H + 2;
        public void Draw(Canvas c, bool hot = false, bool enabled = true)
        {
            if (hot) c.Fill(X, Y, W, H, Canvas.Bright); else c.Fill(X, Y, W, H, Canvas.Black);
            c.Rect(X, Y, W, H, enabled ? Canvas.Mid : Canvas.Dim);
            if (!hot) { c.HLine(X + 1, X + W - 2, Y + H - 2, Canvas.Dim); }
            byte col = hot ? Canvas.Black : enabled ? Canvas.Bright : Canvas.Dark;
            c.Text(X + (W - Canvas.TextWidth(Label)) / 2, Y + (H - 8) / 2, Label, col);
        }
    }

    public static class Ui
    {
        public const int Margin = 16;

        /// <summary>A screen-wide title bar: name on a bright band.</summary>
        public static void Title(Canvas c, string text, int y = 6)
        {
            c.Fill(0, y - 3, Canvas.W, 14, Canvas.Dark);
            c.TextCenter(y, text, Canvas.Hot);
        }

        public static void Box(Canvas c, int x, int y, int w, int h)
        {
            c.Fill(x, y, w, h, Canvas.Black);
            c.Frame(x, y, w, h, Canvas.Mid);
        }

        public static void Prompt(Canvas c, string text, bool blink, int y = Canvas.H - 14)
        {
            if (blink) c.TextCenter(y, text, Canvas.Mid);
        }

        public static string Money(int cents) => "$" + (cents / 100) + "." + (cents % 100).ToString("00");
        public static string Dollars(int cents) => "$" + (cents / 100) + (cents % 100 != 0 ? "." + (cents % 100).ToString("00") : "");
    }

    /// <summary>A boxed message: tap or press a key to continue.</summary>
    public sealed class MessageScene : Scene
    {
        readonly string text; readonly Action then; readonly bool overlay;
        public override bool Overlay => overlay;
        public MessageScene(string text, Action then, bool overlay) { this.text = text; this.then = then; this.overlay = overlay; }

        public override void Draw(Canvas c)
        {
            var lines = Canvas.Wrap(text, 38);
            int h = lines.Count * 11 + 40, w = 336;
            int x = (Canvas.W - w) / 2, y = Math.Max(8, (Canvas.H - h) / 2);
            Ui.Box(c, x, y, w, h);
            for (int i = 0; i < lines.Count; i++) c.Text(x + 16, y + 14 + i * 11, lines[i], Canvas.Bright);
            if (Blink) c.TextCenter(y + h - 16, App.Touch ? "TAP TO CONTINUE" : "PRESS SPACE", Canvas.Mid);
        }

        public override void Tap(int x, int y) { if (T > 0.25f) Done(); }
        public override void Key(char k) { if (k == Keys.Space || k == Keys.Enter || k == Keys.Escape) Done(); }
        void Done() { App.Synth.Click(); App.Pop(); then?.Invoke(); }
    }

    /// <summary>A question with a few answers, drawn as a box over the current screen.</summary>
    public sealed class AskScene : Scene
    {
        readonly string text; readonly Menu menu;
        public override bool Overlay => true;
        public AskScene(App app, string text, params (string label, Action act)[] answers)
        {
            this.text = text;
            menu = new Menu(app) { Pitch = 16 };
            foreach (var a in answers) { var act = a.act; menu.Add(a.label, () => { app.Pop(); act?.Invoke(); }); }
        }

        public override void Draw(Canvas c)
        {
            var lines = Canvas.Wrap(text, 38);
            int h = lines.Count * 11 + menu.Height + 34, w = 336;
            int x = (Canvas.W - w) / 2, y = Math.Max(8, (Canvas.H - h) / 2);
            Ui.Box(c, x, y, w, h);
            for (int i = 0; i < lines.Count; i++) c.Text(x + 16, y + 14 + i * 11, lines[i], Canvas.Bright);
            menu.X = x + 24; menu.Y = y + 22 + lines.Count * 11; menu.W = w - 48;
            menu.Draw(c);
        }

        public override void Tap(int x, int y) { if (T > 0.2f) menu.Tap(x, y); }
        public override void Key(char k) => menu.Key(k);
    }

    /// <summary>On-screen number pad for amounts (also takes typed digits).</summary>
    public sealed class NumberScene : Scene
    {
        readonly string title; readonly int max; readonly Action<int> done; string value = "";
        bool fresh;   // a pre-filled amount is replaced by the first digit typed
        readonly List<Btn> keys = new List<Btn>();
        const int BW = 64, BH = 26, X0 = 92, Y0 = 92;
        public override bool Overlay => true;

        public NumberScene(string title, int max, Action<int> done, int start = -1)
        {
            this.title = title; this.max = max; this.done = done;
            if (start > 0) { value = start.ToString(); fresh = true; }
            string[] labels = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "DEL", "0", "OK" };
            for (int i = 0; i < 12; i++) keys.Add(new Btn(X0 + (i % 3) * (BW + 4), Y0 + (i / 3) * (BH + 4), BW, BH, labels[i]));
        }

        public override void Draw(Canvas c)
        {
            Ui.Box(c, 64, 30, 256, 196);
            var lines = Canvas.Wrap(title, 28);
            for (int i = 0; i < lines.Count && i < 2; i++) c.TextCenter(42 + i * 10, lines[i], Canvas.Bright);
            c.Fill(140, 66, 104, 16, Canvas.Dim);
            string v = value.Length == 0 ? "0" : value;
            if (fresh) c.Fill(235 - Canvas.TextWidth(v), 68, Canvas.TextWidth(v) + 2, 12, Canvas.Mid);
            c.Text(236 - Canvas.TextWidth(v), 70, v, fresh ? Canvas.Black : Canvas.Hot);
            if (Blink) c.Fill(238, 70, 3, 8, Canvas.Hot);
            foreach (var b in keys) b.Draw(c);
            c.TextCenter(212, "max " + max, Canvas.Dark);
        }

        public override void Tap(int x, int y)
        {
            if (x < 64 || x > 320 || y < 30 || y > 226) { Cancel(); return; }
            foreach (var b in keys) if (b.Hit(x, y)) { Press(b.Label); return; }
        }

        public override void Key(char k)
        {
            if (k >= '0' && k <= '9') Press(k.ToString());
            else if (k == Keys.Back) Press("DEL");
            else if (k == Keys.Enter) Press("OK");
            else if (k == Keys.Escape) Cancel();
        }

        void Press(string l)
        {
            if (l == "OK")
            {
                int n = value.Length == 0 ? 0 : int.Parse(value);
                App.Synth.Select(); App.Pop(); done(Math.Min(n, max));
                return;
            }
            App.Synth.Click();
            if (fresh) { fresh = false; value = ""; if (l == "DEL") return; }
            if (l == "DEL") { if (value.Length > 0) value = value.Substring(0, value.Length - 1); }
            else if (value.Length < 5)
            {
                string nv = value == "0" ? l : value + l;
                long n = long.Parse(nv);
                value = n > max ? max.ToString() : nv;
            }
        }

        void Cancel() { App.Synth.Click(); App.Pop(); }
    }

    /// <summary>On-screen keyboard for names and epitaphs (also takes typed letters).</summary>
    public sealed class TextScene : Scene
    {
        readonly string title; readonly int maxLen; readonly Action<string> done; string value;
        bool fresh;   // the starting text is replaced by the first letter typed
        readonly List<Btn> keys = new List<Btn>();
        public override bool Overlay => true;
        const int Top = 84;

        public TextScene(string title, string start, int maxLen, Action<string> done)
        {
            this.title = title; this.maxLen = maxLen; this.done = done; value = start ?? ""; fresh = value.Length > 0;
            string[] rows = { "QWERTYUIOP", "ASDFGHJKL'", "ZXCVBNM,.-" };
            for (int r = 0; r < 3; r++)
                for (int i = 0; i < rows[r].Length; i++)
                    keys.Add(new Btn(14 + i * 36, Top + 36 + r * 30, 32, 26, rows[r][i].ToString()));
            keys.Add(new Btn(14, Top + 126, 104, 26, "CLEAR"));
            keys.Add(new Btn(122, Top + 126, 104, 26, "SPACE"));
            keys.Add(new Btn(230, Top + 126, 60, 26, "DEL"));
            keys.Add(new Btn(294, Top + 126, 76, 26, "DONE"));
        }

        public override void Draw(Canvas c)
        {
            Ui.Box(c, 4, Top - 10, Canvas.W - 8, Canvas.H - Top + 6);
            c.TextCenter(Top, title, Canvas.Bright);
            c.Fill(14, Top + 14, Canvas.W - 28, 16, Canvas.Dim);
            if (fresh && value.Length > 0) c.Fill(19, Top + 16, value.Length * 8 + 2, 12, Canvas.Mid);
            c.Text(20, Top + 18, value, fresh ? Canvas.Black : Canvas.Hot);
            if (Blink && value.Length < maxLen) c.Fill(20 + value.Length * 8, Top + 18, 7, 8, Canvas.Hot);
            foreach (var b in keys) b.Draw(c);
        }

        public override void Tap(int x, int y)
        {
            if (y < Top - 10 && T > 0.3f) { Finish(); return; }
            foreach (var b in keys) if (b.Hit(x, y)) { Press(b.Label); return; }
        }

        public override void Key(char k)
        {
            if (k == Keys.Enter) Finish();
            else if (k == Keys.Back) Press("DEL");
            else if (k == Keys.Escape) { App.Pop(); }
            else if (k == ' ') Press("SPACE");
            else if (k >= 32 && k < 127 && (char.IsLetterOrDigit(k) || ",.-'!?".IndexOf(k) >= 0)) Type(k);
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
