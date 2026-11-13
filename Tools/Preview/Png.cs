using System;
using System.IO;
using System.IO.Compression;
using TheTrail.Core;

// Writes the 480x270 palette screen as a PNG, scaled up with square pixels, as the phone shows it.
static class Png
{
    public static void Save(Canvas c, string path, int scale = 3)
    {
        int w = Canvas.W * scale, h = Canvas.H * scale;
        var raw = new byte[h * (w * 3 + 1)];
        for (int y = 0; y < h; y++)
        {
            int row = y * (w * 3 + 1);
            raw[row] = 0;
                        for (int x = 0; x < w; x++)
            {
                int p = c.Px[(y / scale) * Canvas.W + x / scale] * 3;
                for (int k = 0; k < 3; k++) raw[row + 1 + x * 3 + k] = Pal.Rgb[p + k];
            }
        }
        using var fs = File.Create(path);
        fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var ihdr = new byte[13];
        BE(ihdr, 0, w); BE(ihdr, 4, h); ihdr[8] = 8; ihdr[9] = 2;
        Chunk(fs, "IHDR", ihdr);
        using (var ms = new MemoryStream())
        {
            using (var z = new ZLibStream(ms, CompressionLevel.Fastest, true)) z.Write(raw);
            Chunk(fs, "IDAT", ms.ToArray());
        }
        Chunk(fs, "IEND", Array.Empty<byte>());
    }

    static void BE(byte[] b, int o, int v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }

    static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4]; BE(len, 0, data.Length); s.Write(len);
        var td = new byte[4 + data.Length];
        for (int i = 0; i < 4; i++) td[i] = (byte)type[i];
        Array.Copy(data, 0, td, 4, data.Length);
        s.Write(td);
        var crc = new byte[4]; BE(crc, 0, (int)Crc(td)); s.Write(crc);
    }

    static uint[] table;
    static uint Crc(byte[] d)
    {
        if (table == null)
        {
            table = new uint[256];
            for (uint n = 0; n < 256; n++) { uint c = n; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; table[n] = c; }
        }
        uint r = 0xFFFFFFFFu;
        foreach (var b in d) r = table[(r ^ b) & 0xFF] ^ (r >> 8);
        return r ^ 0xFFFFFFFFu;
    }
}
