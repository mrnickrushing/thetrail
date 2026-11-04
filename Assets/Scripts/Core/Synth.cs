using System;
using System.Collections.Generic;

namespace TheTrail.Core
{
    /// <summary>
    /// A one-voice square-wave beeper in the spirit of a PC speaker, plus a noise channel for
    /// gunshots and splashes. The game queues notes; the Unity layer pulls samples from Render()
    /// on the audio thread.
    /// </summary>
    public sealed class Synth
    {
        struct Note { public float Freq, Dur, Vol; public bool Noise; }

        readonly Queue<Note> queue = new Queue<Note>();
        readonly object gate = new object();
        Note cur; bool playing; float t, phase; uint noise = 0x1234567u;
        public bool Muted;
        public float Volume = 0.16f;

        public void Stop() { lock (gate) { queue.Clear(); playing = false; } }
        public bool Busy { get { lock (gate) return playing || queue.Count > 0; } }

        void Add(Note n) { if (!Muted) lock (gate) queue.Enqueue(n); }

        public void Beep(float freq, float seconds, float vol = 1f) => Add(new Note { Freq = freq, Dur = seconds, Vol = vol });
        public void Rest(float seconds) => Add(new Note { Freq = 0, Dur = seconds });
        public void Noise(float seconds, float vol = 1f) => Add(new Note { Noise = true, Dur = seconds, Vol = vol });

        // Sound effects.
        public void Click() { Stop(); Beep(1320, 0.025f, 0.6f); }
        public void Select() { Stop(); Beep(880, 0.04f, 0.7f); Beep(1320, 0.05f, 0.7f); }
        public void Bad() { Stop(); Beep(330, 0.12f); Beep(247, 0.12f); Beep(196, 0.25f); }
        public void Good() { Stop(); Beep(523, 0.08f); Beep(659, 0.08f); Beep(784, 0.16f); }
        public void Shot() { Stop(); Noise(0.09f, 1f); Noise(0.12f, 0.35f); }
        public void Thud() { Stop(); Noise(0.05f, 0.8f); Beep(110, 0.08f, 0.8f); }
        public void Splash() { Stop(); Noise(0.25f, 0.6f); Noise(0.3f, 0.25f); }
        public void Death() { Stop(); Play("E4:3 R:1 E4:2 E4:1 E4:4 G4:3 F4:1 F4:2 E4:1 E4:2 D#4:1 E4:6", 0.13f); }
        public void Arrive() { Stop(); Play("C5:1 E5:1 G5:1 C6:3", 0.09f); }

        /// <summary>Oh! Susanna (Stephen Foster, 1848; public domain).</summary>
        public const string Susanna =
            "C4:1 D4:1 E4:2 G4:2 G4:3 A4:1 G4:2 E4:2 C4:3 D4:1 E4:2 E4:2 D4:2 C4:2 D4:6 " +
            "C4:1 D4:1 E4:2 G4:2 G4:3 A4:1 G4:2 E4:2 C4:3 D4:1 E4:2 E4:2 D4:2 D4:2 C4:6 R:2 " +
            "F4:4 F4:4 A4:2 A4:4 A4:2 G4:2 G4:2 E4:2 C4:2 D4:6 C4:1 D4:1 " +
            "E4:2 G4:2 G4:3 A4:1 G4:2 E4:2 C4:3 D4:1 E4:2 E4:2 D4:2 D4:2 C4:8";

        /// <summary>Plays notes like "C4:2 D#4:1 R:2" where the number is a length in eighth notes.</summary>
        public void Play(string tune, float eighth = 0.15f)
        {
            foreach (var tok in tune.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = tok.Split(':');
                float len = (parts.Length > 1 ? float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : 1) * eighth;
                float f = NoteFreq(parts[0]);
                if (f <= 0) Rest(len);
                else { Beep(f, len * 0.88f, 0.9f); Rest(len * 0.12f); }
            }
        }

        static readonly string[] Names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        public static float NoteFreq(string n)
        {
            if (n == "R") return 0;
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
                    float s = 0;
                    if (!playing && queue.Count > 0) { cur = queue.Dequeue(); playing = true; t = 0; }
                    if (playing)
                    {
                        // Short attack/release so notes don't click.
                        float env = Math.Min(1f, Math.Min(t / 0.004f, (cur.Dur - t) / 0.006f));
                        if (env < 0) env = 0;
                        if (cur.Noise)
                        {
                            noise ^= noise << 13; noise ^= noise >> 17; noise ^= noise << 5;
                            s = ((noise & 0xFFFF) / 32768f - 1f) * cur.Vol * env * (1f - t / cur.Dur);
                        }
                        else if (cur.Freq > 0)
                        {
                            phase += cur.Freq * dt; if (phase >= 1) phase -= (float)Math.Floor(phase);
                            s = (phase < 0.5f ? 1f : -1f) * cur.Vol * env;
                        }
                        t += dt;
                        if (t >= cur.Dur) playing = false;
                    }
                    s *= Muted ? 0 : Volume;
                    for (int c = 0; c < channels; c++) data[i + c] = s;
                }
            }
        }
    }
}
