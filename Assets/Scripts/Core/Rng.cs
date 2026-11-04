using System;

namespace TheTrail.Core
{
    /// <summary>
    /// xoshiro128** random numbers. Plain uint fields so the whole state saves with the game
    /// (Unity's JsonUtility and System.Text.Json both handle it), which keeps a saved journey
    /// deterministic after a reload.
    /// </summary>
    [Serializable]
    public class Rng
    {
        public uint a, b, c, d;

        public Rng() : this(12345) { }

        public Rng(uint seed)
        {
            // SplitMix32 spreads a small seed across the four words.
            uint s = seed;
            a = Mix(ref s); b = Mix(ref s); c = Mix(ref s); d = Mix(ref s);
            if ((a | b | c | d) == 0) a = 1;
        }

        static uint Mix(ref uint s)
        {
            uint z = s += 0x9E3779B9u;
            z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
            z = (z ^ (z >> 13)) * 0xC2B2AE35u;
            return z ^ (z >> 16);
        }

        public uint Next()
        {
            uint r = Rotl(b * 5, 7) * 9;
            uint t = b << 9;
            c ^= a; d ^= b; b ^= c; a ^= d; c ^= t; d = Rotl(d, 11);
            return r;
        }

        static uint Rotl(uint x, int k) => (x << k) | (x >> (32 - k));

        /// <summary>0 &lt;= result &lt; 1.</summary>
        public float Float() => (Next() >> 8) * (1f / 16777216f);

        /// <summary>min &lt;= result &lt; max.</summary>
        public int Range(int min, int max) => max <= min ? min : min + (int)(Next() % (uint)(max - min));

        public float Range(float min, float max) => min + (max - min) * Float();

        public bool Chance(float p) => Float() < p;

        public T Pick<T>(T[] items) => items[Range(0, items.Length)];
    }
}
