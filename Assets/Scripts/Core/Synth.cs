using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    /// <summary>
    /// Two voices of plucked strings (Karplus-Strong, like a banjo or guitar) plus noise, one for
    /// music and one for sound effects. The game queues notes; the host pulls samples from Render()
    /// on the audio thread.
    /// </summary>
    public sealed class Synth
    {
        enum Kind { Rest, Pluck, Tone, Noise }
        struct Note { public Kind Kind; public float Freq, Dur, Vol; }

        sealed class Voice
        {
            public readonly Queue<Note> Queue = new Queue<Note>();
            public Note Cur; public bool Playing; public float T, Phase;
            public readonly float[] Line = new float[4096]; public int Len, Pos;
            public uint Noise = 0x1234567u;
            public bool Loop; public readonly List<Note> Tune = new List<Note>();
        }

        readonly Voice sfx = new Voice(), music = new Voice();
        readonly object gate = new object();
        public bool Muted;
        public bool MusicOn = true;
        public float Volume = 0.22f;

        public void Stop() { lock (gate) { sfx.Queue.Clear(); sfx.Playing = false; } }
        public void StopMusic() { lock (gate) { music.Queue.Clear(); music.Playing = false; music.Loop = false; music.Tune.Clear(); } }
        public bool Busy { get { lock (gate) return sfx.Playing || sfx.Queue.Count > 0; } }
        public bool MusicPlaying { get { lock (gate) return music.Playing || music.Queue.Count > 0; } }

        void Add(Voice v, Note n) { lock (gate) v.Queue.Enqueue(n); }

        public void Pluck(float freq, float seconds, float vol = 1f) => Add(sfx, new Note { Kind = Kind.Pluck, Freq = freq, Dur = seconds, Vol = vol });
        public void Beep(float freq, float seconds, float vol = 1f) => Add(sfx, new Note { Kind = Kind.Tone, Freq = freq, Dur = seconds, Vol = vol });
        public void Rest(float seconds) => Add(sfx, new Note { Kind = Kind.Rest, Dur = seconds });
        public void Noise(float seconds, float vol = 1f) => Add(sfx, new Note { Kind = Kind.Noise, Dur = seconds, Vol = vol });

        // Sound effects.
        public void Click() { Stop(); Pluck(1046, 0.05f, 0.35f); }
        public void Select() { Stop(); Pluck(784, 0.06f, 0.5f); Pluck(1175, 0.12f, 0.5f); }
        public void Bad() { Stop(); Pluck(294, 0.16f, 0.8f); Pluck(262, 0.16f, 0.8f); Pluck(196, 0.4f, 0.8f); }
        public void Good() { Stop(); Pluck(523, 0.1f); Pluck(659, 0.1f); Pluck(784, 0.1f); Pluck(1046, 0.3f); }
        public void Coin() { Stop(); Pluck(1568, 0.06f, 0.6f); Pluck(2093, 0.18f, 0.6f); }
        public void Shot() { Stop(); Noise(0.08f, 1f); Noise(0.2f, 0.3f); }
        public void Thud() { Stop(); Noise(0.05f, 0.8f); Pluck(98, 0.2f, 0.9f); }
        public void Splash() { Stop(); Noise(0.3f, 0.5f); Noise(0.4f, 0.2f); }
        public void Creak() { Stop(); Beep(140, 0.08f, 0.25f); Beep(120, 0.1f, 0.2f); }
        public void Snap() { Stop(); Noise(0.04f, 1f); Pluck(80, 0.3f, 1f); Noise(0.25f, 0.4f); }
        public void Death() { Stop(); PlayOn(sfx, "A3:3 R:1 A3:2 A3:1 A3:4 C4:3 B3:1 B3:2 A3:1 A3:2 G#3:1 A3:6", 0.14f); }
        public void Arrive() { Stop(); PlayOn(sfx, "G4:1 B4:1 D5:1 G5:3", 0.09f); }
        public void Fanfare() { Stop(); PlayOn(sfx, "C5:1 C5:1 C5:1 G5:4 E5:2 C5:2 G5:6", 0.1f); }

        /// <summary>Oh! Susanna (Stephen Foster, 1848; public domain), the forty-niners' marching song.</summary>
        public const string Susanna =
            "C4:1 D4:1 E4:2 G4:2 G4:3 A4:1 G4:2 E4:2 C4:3 D4:1 E4:2 E4:2 D4:2 C4:2 D4:6 " +
            "C4:1 D4:1 E4:2 G4:2 G4:3 A4:1 G4:2 E4:2 C4:3 D4:1 E4:2 E4:2 D4:2 D4:2 C4:6 R:2 " +
            "F4:4 F4:4 A4:2 A4:4 A4:2 G4:2 G4:2 E4:2 C4:2 D4:6 C4:1 D4:1 " +
            "E4:2 G4:2 G4:3 A4:1 G4:2 E4:2 C4:3 D4:1 E4:2 E4:2 D4:2 D4:2 C4:8 R:8";

        /// <summary>Plays a tune on the music voice, optionally looping. Notes like "C4:2 D#4:1 R:2" (eighths).</summary>
        public void Music(string tune, float eighth = 0.15f, bool loop = false)
        {
            StopMusic();
            if (!MusicOn) return;
            var notes = Parse(tune, eighth, 0.7f);
            lock (gate)
            {
                foreach (var n in notes) music.Queue.Enqueue(n);
                music.Loop = loop;
                if (loop) music.Tune.AddRange(notes);
            }
        }

        void PlayOn(Voice v, string tune, float eighth) { foreach (var n in Parse(tune, eighth, 0.9f)) Add(v, n); }

        static List<Note> Parse(string tune, float eighth, float vol)
        {
            var l = new List<Note>();
            foreach (var tok in tune.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = tok.Split(':');
                float len = (parts.Length > 1 ? float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : 1) * eighth;
                float f = NoteFreq(parts[0]);
                l.Add(f <= 0 ? new Note { Kind = Kind.Rest, Dur = len } : new Note { Kind = Kind.Pluck, Freq = f, Dur = len, Vol = vol });
            }
            return l;
        }

        static readonly string[] Names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        public static float NoteFreq(string n)
        {
            if (n == "R" || n.Length < 2) return 0;
            int octave = n[n.Length - 1] - '0';
            string name = n.Substring(0, n.Length - 1);
            int idx = Array.IndexOf(Names, name);
            if (idx < 0) return 0;
            int midi = (octave + 1) * 12 + idx;
            return 440f * (float)Math.Pow(2, (midi - 69) / 12.0);
        }

        /// <summary>Fills an interleaved buffer. Called from the audio thread.</summary>
        public void Render(float[] data, int channels, int rate)
        {
            float dt = 1f / rate;
            lock (gate)
            {
                for (int i = 0; i < data.Length; i += channels)
                {
                    float s = Sample(sfx, dt, rate) + Sample(music, dt, rate) * (MusicOn ? 0.55f : 0f);
                    s = Muted ? 0 : Math.Max(-1f, Math.Min(1f, s * Volume));
                    for (int c = 0; c < channels; c++) data[i + c] = s;
                }
            }
        }

        static float Sample(Voice v, float dt, int rate)
        {
            if (!v.Playing)
            {
                if (v.Queue.Count == 0 && v.Loop && v.Tune.Count > 0) foreach (var n in v.Tune) v.Queue.Enqueue(n);
                if (v.Queue.Count == 0) return 0;
                v.Cur = v.Queue.Dequeue(); v.Playing = true; v.T = 0;
                if (v.Cur.Kind == Kind.Pluck)
                {
                    // Karplus-Strong: a burst of noise in a delay line one period long, softened each pass.
                    v.Len = Math.Max(2, Math.Min(v.Line.Length, (int)(rate / v.Cur.Freq)));
                    for (int k = 0; k < v.Len; k++)
                    {
                        v.Noise ^= v.Noise << 13; v.Noise ^= v.Noise >> 17; v.Noise ^= v.Noise << 5;
                        v.Line[k] = (v.Noise & 0xFFFF) / 32768f - 1f;
                    }
                    v.Pos = 0;
                }
            }
            float s = 0;
            var cur = v.Cur;
            switch (cur.Kind)
            {
                case Kind.Pluck:
                {
                    int a = v.Pos, b = (v.Pos + 1) % v.Len;
                    s = v.Line[a];
                    v.Line[a] = (v.Line[a] + v.Line[b]) * 0.4985f;
                    v.Pos = b;
                    float tail = Math.Min(1f, (cur.Dur + 0.05f - v.T) / 0.05f);
                    s *= cur.Vol * Math.Max(0, tail);
                    break;
                }
                case Kind.Tone:
                {
                    v.Phase += cur.Freq * dt; if (v.Phase >= 1) v.Phase -= (float)Math.Floor(v.Phase);
                    float env = Math.Min(1f, Math.Min(v.T / 0.004f, (cur.Dur - v.T) / 0.01f));
                    s = (v.Phase < 0.5f ? 4 * v.Phase - 1 : 3 - 4 * v.Phase) * cur.Vol * Math.Max(0, env);
                    break;
                }
                case Kind.Noise:
                {
                    v.Noise ^= v.Noise << 13; v.Noise ^= v.Noise >> 17; v.Noise ^= v.Noise << 5;
                    s = ((v.Noise & 0xFFFF) / 32768f - 1f) * cur.Vol * (1f - v.T / cur.Dur);
                    break;
                }
            }
            v.T += dt;
            // A plucked note rings a little past its length; the next note cuts it off.
            if (v.T >= cur.Dur + (cur.Kind == Kind.Pluck ? 0.05f : 0)) v.Playing = false;
            return s;
        }
    }
}
