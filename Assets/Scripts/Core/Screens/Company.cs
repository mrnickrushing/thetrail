using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    /// <summary>The hiring board at St. Joseph: eight strangers, choose up to four partners.</summary>
    public sealed class HireScene : Scene
    {
        List<Hand> board;
        Btn go;
        const int CW = 114, CH = 104, X0 = 8, Y0 = 24, Gap = 4;

        public override void Enter()
        {
            board = Content.Board(new Rng(G.seed ^ 0xB0A2Du), G.Leader.name);
            go = new Btn(Canvas.W - 156, Canvas.H - 26, 148, 20, "FORM THE COMPANY");
            if (!App.Synth.MusicPlaying) App.Synth.Music(Synth.Susanna, 0.2f, true);
        }

        bool Hired(Hand h) { foreach (var m in G.company) if (m.name == h.name) return true; return false; }

        (int x, int y) Pos(int i) => (X0 + (i % 4) * (CW + Gap), Y0 + (i / 4) * (CH + Gap));

        public override void Draw(Canvas c)
        {
            c.Fill(0, 0, Canvas.W, Canvas.H, Pal.Ink);
            Ui.Title(c, "LOOKING FOR A COMPANY: CHOOSE UP TO FOUR PARTNERS");
            for (int i = 0; i < board.Count; i++)
            {
                var h = board[i];
                var (x, y) = Pos(i);
                bool on = Hired(h);
                c.RoundRect(x, y, CW, CH, on ? Pal.Leather : Pal.Walnut, on ? Pal.Gold : Pal.Leather);
                if (on) c.Rect(x + 1, y + 1, CW - 2, CH - 2, Pal.Gold);
                Portrait(c, x + 6, y + 6, h, i);
                c.Text(x + 30, y + 6, First(h.name), Pal.Cream);
                c.Text(x + 30, y + 16, Last(h.name), Pal.Cream);
                c.Text(x + 6, y + 32, Content.TradeName(h.trade), h.trade == Trade.Greenhorn ? Pal.Tan : Pal.Gold);
                c.Paragraph(x + 6, y + 43, CW - 12, Content.TradeSkill(h.trade), Pal.Tan, 9);
                c.Text(x + 6, y + CH - 24, Cap(Content.TemperName(h.temper)), h.temper == Temper.Grumbler ? Pal.Dusk : h.temper == Temper.Cheerful ? Pal.Green : Pal.Cream);
                c.Text(x + 6, y + CH - 13, "Puts in $" + h.stake, Pal.GoldLight);
                if (on) { c.Fill(x + CW - 40, y + 31, 36, 11, Pal.Gold); c.TextCenterIn(x + CW - 40, 36, y + 33, "HIRED", Pal.Ink); }
            }
            int partners = G.company.Count - 1;
            c.Text(10, Canvas.H - 22, "Partners " + partners + " of " + Sim.MaxPartners + ".  The pot: $" + Sim.Pot(G) + " (yours $" + Sim.LeaderStake + ").", Pal.Cream);
            go.Draw(c, App.PointerDown && go.Hit(App.PointerX, App.PointerY));
        }

        static string First(string n) { int s = n.LastIndexOf(' '); return s > 0 ? n.Substring(0, s) : n; }
        static string Last(string n) { int s = n.LastIndexOf(' '); return s > 0 ? n.Substring(s + 1) : ""; }
        static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        /// <summary>A small face: skin, hair and hat vary by the person.</summary>
        static void Portrait(Canvas c, int x, int y, Hand h, int i)
        {
            uint k = Sim.Hash(h.name);
            c.Fill(x, y, 20, 20, Pal.Ink);
            byte skin = (k & 3) == 0 ? Pal.SkinDark : Pal.Skin;
            byte hair = new[] { Pal.WoodDark, Pal.Black, Pal.GoldDark, Pal.RockLight, Pal.RedRockDark }[(k >> 2) % 5];
            byte shirt = new[] { Pal.Blue, Pal.Red, Pal.Earth, Pal.Cloth, Pal.Sage }[(k >> 5) % 5];
            c.Fill(x + 3, y + 15, 14, 5, shirt);
            c.Ellipse(x + 10, y + 10, 5, 6, skin);
            c.Set(x + 8, y + 9, Pal.Black); c.Set(x + 12, y + 9, Pal.Black);
            bool woman = h.name.StartsWith("Abigail") || h.name.StartsWith("Clarissa") || h.name.StartsWith("Delia") || h.name.StartsWith("Eliza") || h.name.StartsWith("Harriet")
                || h.name.StartsWith("Louisa") || h.name.StartsWith("Mary") || h.name.StartsWith("Olive") || h.name.StartsWith("Phoebe") || h.name.StartsWith("Rachel") || h.name.StartsWith("Sarah") || h.name.StartsWith("Tabitha");
            if (woman) { c.Fill(x + 4, y + 2, 12, 5, Pal.Cloth); c.Fill(x + 3, y + 5, 3, 8, Pal.Cloth); c.Fill(x + 14, y + 5, 3, 8, Pal.Cloth); }
            else
            {
                c.Fill(x + 5, y + 4, 10, 2, hair);
                if (((k >> 8) & 1) == 0) { c.HLine(x + 2, x + 17, y + 4, Pal.WoodDark); c.Fill(x + 5, y + 1, 10, 3, Pal.WoodDark); }
                if (((k >> 9) & 3) == 0) c.Fill(x + 7, y + 13, 7, 3, hair);
            }
        }

        public override void Tap(int x, int y)
        {
            if (go.Hit(x, y)) { Form(); return; }
            for (int i = 0; i < board.Count; i++)
            {
                var (cx, cy) = Pos(i);
                if (x >= cx && x < cx + CW && y >= cy && y < cy + CH) { Toggle(board[i]); return; }
            }
        }

        public override void Key(char k)
        {
            if (k >= '1' && k <= '8') Toggle(board[k - '1']);
            else if (k == Keys.Enter) Form();
        }

        void Toggle(Hand h)
        {
            var have = G.company.Find(m => m.name == h.name);
            if (have != null) { G.company.Remove(have); S.Click(); }
            else if (G.company.Count - 1 >= Sim.MaxPartners) { S.Bad(); return; }
            else
            {
                G.company.Add(new Hand { name = h.name, home = h.home, trade = h.trade, temper = h.temper, stake = h.stake });
                S.Coin();
            }
            App.SaveGame();
        }

        void Form()
        {
            if (G.company.Count == 1)
            {
                App.Ask("ALONE?", "A man alone can't stand guard, drive and cook. Take at least one partner.", ("Choose partners", null));
                return;
            }
            App.Ask("OXEN OR MULES?", "What will pull your wagon? Oxen cost $" + Content.BaseCents(Good.Animals, TeamKind.Oxen) / 100 + " each, are slow and eat anything. Mules cost $" + Content.BaseCents(Good.Animals, TeamKind.Mules) / 100 + " each and are faster, but fail on poor grass.",
                ("Oxen", () => Begin(TeamKind.Oxen)), ("Mules", () => Begin(TeamKind.Mules)), ("Not yet", null));
        }

        void Begin(TeamKind team)
        {
            Sim.BeginOutfit(G, team);
            App.SaveGame();
            App.Go(new StoreScene(true));
        }
    }
}
