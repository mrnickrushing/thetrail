using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    /// <summary>The hunt: game crosses the field, you tap to shoot. You can only carry so much back.</summary>
    public sealed class HuntScene : Scene
    {
        public const float Length = 30f;
        /// <summary>Most meat you can haul back to the wagon from one hunt.</summary>
        public const int CarryLimit = 400;
        const int HunterX = 192, HunterY = 226, Top = 16;

        public sealed class Animal
        {
            public string Kind; public float X, Y, Vx, Vy; public bool Dead, Fleeing; public int Lbs; public float Age;
            public int W => Core.Animals.Size(Kind).w;
            public int H => Core.Animals.Size(Kind).h;
            public bool Contains(float px, float py) => px >= X && px < X + W && py >= Y - H && py < Y;
        }

        sealed class Shot { public float X, Y, Tx, Ty; public bool Done; public float Puff = -1; }
        sealed class Cover { public int X, Y, Kind, Size; public int W => Kind == 2 ? Size * 2 + 2 : Size * 2 + 2; }

        readonly List<Animal> animals = new List<Animal>();
        readonly List<Shot> shots = new List<Shot>();
        readonly List<Cover> cover = new List<Cover>();
        Rng r;
        float time, spawn = 0.6f, aimX = HunterX, aimY = 100;
        int meat, fired;
        bool over;

        public int Meat => meat;
        public IReadOnlyList<Animal> Animals => animals;

        static int Lbs(string k) => k == "buffalo" ? 350 : k == "elk" ? 220 : k == "bear" ? 160 : k == "deer" ? 90 : k == "antelope" ? 50 : k == "rabbit" ? 3 : 1;
        static float Speed(string k) => k == "buffalo" ? 26 : k == "elk" ? 40 : k == "bear" ? 30 : k == "deer" ? 48 : k == "antelope" ? 78 : k == "rabbit" ? 60 : 44;

        string[] Kinds()
        {
            switch (Sim.TerrainNow(G))
            {
                case Terrain.Plains: return new[] { "buffalo", "buffalo", "buffalo", "antelope", "antelope", "rabbit", "rabbit", "rabbit", "deer" };
                case Terrain.Hills: return new[] { "deer", "deer", "deer", "antelope", "antelope", "rabbit", "rabbit", "buffalo" };
                case Terrain.Mountains: return new[] { "elk", "elk", "deer", "deer", "deer", "bear", "squirrel", "squirrel", "rabbit" };
                case Terrain.Desert: return new[] { "rabbit", "rabbit", "rabbit", "rabbit", "antelope", "antelope", "antelope" };
                default: return new[] { "deer", "deer", "deer", "elk", "elk", "bear", "squirrel", "squirrel", "squirrel" };
            }
        }

        public override void Enter()
        {
            r = new Rng(G.rng.Next());
            int n = r.Range(6, 10);
            for (int i = 0; i < n; i++)
            {
                int x, y;
                do { x = r.Range(10, Canvas.W - 30); y = r.Range(60, 200); } while (Math.Abs(x - HunterX) < 50 && y > 170);
                cover.Add(new Cover { X = x, Y = y, Kind = r.Range(0, 3), Size = r.Range(5, 10) });
            }
            cover.Sort((a, b) => a.Y.CompareTo(b.Y));
        }

        void Spawn()
        {
            var kinds = Kinds();
            string k = kinds[r.Range(0, kinds.Length)];
            bool left = r.Chance(0.5f);
            var a = new Animal { Kind = k, Lbs = Lbs(k) };
            a.Y = r.Range(54, 200);
            a.X = left ? -a.W : Canvas.W;
            a.Vx = (left ? 1 : -1) * Speed(k) * r.Range(0.8f, 1.2f);
            a.Vy = r.Range(-6f, 6f);
            animals.Add(a);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (over) return;
            time += dt;

            spawn -= dt;
            int alive = 0; foreach (var a in animals) if (!a.Dead && a.X > -80 && a.X < Canvas.W + 80) alive++;
            if (spawn <= 0 && alive < 4)
            {
                Spawn();
                spawn = r.Range(1.0f, 2.4f) * (1 + Math.Min(3f, G.huntPressure) * 0.5f);
            }

            foreach (var a in animals)
            {
                if (a.Dead) continue;
                a.Age += dt;
                a.X += a.Vx * dt * (a.Fleeing ? 2.2f : 1f);
                a.Y += a.Vy * dt;
                if (a.Y < 54 || a.Y > 204) a.Vy = -a.Vy;
            }
            animals.RemoveAll(a => !a.Dead && (a.X < -120 || a.X > Canvas.W + 120));

            foreach (var s in shots)
            {
                if (s.Done) { s.Puff += dt; continue; }
                float dx = s.Tx - s.X, dy = s.Ty - s.Y, d = (float)Math.Sqrt(dx * dx + dy * dy);
                float step = 700 * dt;
                int sub = Math.Max(1, (int)(Math.Min(step, d) / 3));
                for (int i = 0; i < sub && !s.Done; i++)
                {
                    float m = Math.Min(step, d) / sub;
                    if (d < 0.5f) { s.Done = true; s.Puff = 0; break; }
                    s.X += dx / d * m; s.Y += dy / d * m;
                    if (Hit(s)) break;
                    if (Math.Abs(s.X - s.Tx) < 1.5f && Math.Abs(s.Y - s.Ty) < 1.5f) { s.Done = true; s.Puff = 0; }
                }
            }
            shots.RemoveAll(s => s.Done && s.Puff > 0.25f);

            if (time >= Length || (G.bullets <= 0 && shots.TrueForAll(s => s.Done))) Finish();
        }

        bool Hit(Shot s)
        {
            // Trees and rocks nearer than the target stop the bullet.
            foreach (var cv in cover)
                if (cv.Y > s.Ty && s.X >= cv.X - cv.W / 2 && s.X <= cv.X + cv.W / 2 && s.Y <= cv.Y && s.Y >= cv.Y - CoverHeight(cv))
                { s.Done = true; s.Puff = 0; return true; }
            foreach (var a in animals)
                if (!a.Dead && a.Contains(s.X, s.Y))
                {
                    a.Dead = true; meat += a.Lbs; s.Done = true; s.Puff = 0;
                    S.Good();
                    foreach (var b in animals) if (!b.Dead && Math.Abs(b.X - a.X) < 120) { b.Fleeing = true; b.Vx = Math.Sign(b.X - a.X + 0.01f) * Math.Abs(b.Vx); }
                    return true;
                }
            return false;
        }

        static int CoverHeight(Cover cv) => cv.Kind == 0 ? cv.Size * 4 : cv.Kind == 1 ? cv.Size * 3 : cv.Size;

        public void Fire(int tx, int ty)
        {
            if (over) return;
            if (G.bullets <= 0) { S.Beep(150, 0.08f); return; }
            if (ty < Top + 2) return;
            G.bullets--; fired++;
            aimX = tx; aimY = ty;
            var (mx, my) = Muzzle();
            shots.Add(new Shot { X = mx, Y = my, Tx = tx, Ty = ty });
            S.Shot();
        }

        (float, float) Muzzle()
        {
            float dx = aimX - HunterX, dy = aimY - (HunterY - 22), d = (float)Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
            return (HunterX + dx / d * 16, HunterY - 22 + dy / d * 16);
        }

        void Finish()
        {
            if (over) return;
            over = true;
            int got = Math.Min(meat, CarryLimit);
            got = Math.Min(got, Math.Max(0, 2000 - G.food));
            G.food += got;
            G.huntPressure += 1f;
            var texts = new List<string>();
            if (meat == 0) texts.Add(fired == 0 ? "You didn't fire a shot. You return to the wagon empty-handed." : "You used " + fired + " bullets and didn't bring anything down.");
            else
            {
                string t = "You shot " + meat + " pounds of meat.";
                if (meat > got) t += " You could only carry " + got + " pounds back to the wagon.";
                else t += " You carry it all back to the wagon.";
                texts.Add(t);
            }
            var log = Sim.Day(G, false);
            App.SaveGame();
            App.SayAll(texts, () => Flow.Report(App, log, () => App.Go(new TrailMenuScene())));
        }

        public override void Draw(Canvas c)
        {
            // Field: a far tree line, grass getting brighter toward you.
            c.Gradient(0, Top, Canvas.W, 26, 0.1f, 0.5f);
            for (int i = 0; i < 30; i++) Art.RoundTree(c, i * 14 + (i * 7) % 9, Top + 30, 4 + i % 3, 1.2f);
            c.Gradient(0, Top + 30, Canvas.W, Canvas.H - Top - 30, 0.55f, 1.25f);
            for (int i = 0; i < 70; i++)
            {
                int gx = (i * 71) % Canvas.W, gy = Top + 36 + (i * 37) % (Canvas.H - Top - 40);
                c.Set(gx, gy, Canvas.Mid); c.Set(gx - 1, gy + 1, Canvas.Dark); c.Set(gx + 1, gy + 1, Canvas.Dark);
            }

            // Painter's order: things farther up the screen first.
            var items = new List<(float y, Action draw)>();
            foreach (var cv in cover) { var v = cv; items.Add((v.Y, () => DrawCover(c, v))); }
            foreach (var a in animals) { var v = a; items.Add((v.Y, () => DrawAnimal(c, v))); }
            items.Sort((a, b) => a.y.CompareTo(b.y));
            foreach (var it in items) it.draw();

            foreach (var s in shots)
            {
                if (!s.Done) { c.Set((int)s.X, (int)s.Y, Canvas.Hot); c.Set((int)s.X + 1, (int)s.Y, Canvas.Hot); }
                else if (s.Puff >= 0) c.Ring((int)s.Tx, (int)s.Ty, 1 + (int)(s.Puff * 12), Canvas.Bright);
            }
            DrawHunter(c);

            // Heads-up line.
            c.Fill(0, 0, Canvas.W, Top, Canvas.Black);
            c.HLine(0, Canvas.W, Top - 1, Canvas.Mid);
            c.Text(6, 4, "Bullets " + G.bullets, Canvas.Bright);
            c.Text(130, 4, "Meat " + meat + " lb", Canvas.Hot);
            int left = Math.Max(0, (int)Math.Ceiling(Length - time));
            c.TextRight(Canvas.W - 6, 4, "Time 0:" + left.ToString("00"), left <= 5 && Blink ? Canvas.Hot : Canvas.Bright);
            if (T < 2.5f) c.TextOutlined((Canvas.W - Canvas.TextWidth(App.Touch ? "TAP TO SHOOT" : "CLICK TO SHOOT")) / 2, 120, App.Touch ? "TAP TO SHOOT" : "CLICK TO SHOOT", Canvas.Hot);
        }

        void DrawCover(Canvas c, Cover cv)
        {
            if (cv.Kind == 0) Art.Pine(c, cv.X, cv.Y, cv.Size * 4, Canvas.Mid, Canvas.Dark);
            else if (cv.Kind == 1) Art.RoundTree(c, cv.X, cv.Y, cv.Size + 2, 2.4f);
            else
            {
                for (int j = 0; j < cv.Size; j++)
                    for (int i = -cv.Size; i <= cv.Size; i++)
                        if (i * i / (float)(cv.Size * cv.Size) + j * j / (float)(cv.Size * cv.Size) <= 1)
                            c.SetShade(cv.X + i, cv.Y - j, 2.2f + (i < 0 ? 0.8f : -0.4f) + j * 0.05f);
            }
        }

        void DrawAnimal(Canvas c, Animal a)
        {
            bool right = a.Vx > 0;
            float phase = a.Dead ? 0.6f : a.Age * (a.Fleeing ? 15f : 9f);
            var sp = Core.Animals.Draw(a.Kind, phase, a.Fleeing && !a.Dead);
            sp.Blit(c, (int)a.X, (int)a.Y - a.H, right, a.Dead, a.Dead ? -0.9f : 0);
            if (a.Dead) c.TextOutlined((int)a.X + a.W / 2 - Canvas.TextWidth(a.Lbs.ToString()) / 2, (int)a.Y - a.H - 10, a.Lbs.ToString(), Canvas.Bright);
        }

        void DrawHunter(Canvas c)
        {
            int x = HunterX, y = HunterY;
            // Seen from behind: hat, shoulders, coat, legs planted.
            c.Fill(x - 6, y - 30, 12, 2, Canvas.Mid);
            c.Fill(x - 3, y - 34, 6, 4, Canvas.Mid);
            c.Fill(x - 3, y - 28, 6, 3, Canvas.Bright);
            c.Fill(x - 7, y - 25, 14, 14, Canvas.Mid);
            c.VLine(x, y - 25, y - 12, Canvas.Dark);
            c.Fill(x - 5, y - 11, 4, 11, Canvas.Dark);
            c.Fill(x + 1, y - 11, 4, 11, Canvas.Dark);
            var (mx, my) = Muzzle();
            c.Line(x + 4, y - 20, (int)mx, (int)my, Canvas.Hot);
            c.Line(x + 5, y - 20, (int)mx + 1, (int)my, Canvas.Bright);
        }

        public override void Tap(int x, int y) => Fire(x, y);
        public override void Key(char k) { if (k == Keys.Escape) time = Length; }
    }
}
