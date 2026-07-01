using System.IO.Compression;

namespace NImpeller.Tests.Headless;

/// <summary>
/// Clankered Image format for storing RGBA pixel data in a compact, lossless way. Used for baseline images in headless tests.
/// Also outputs to PNG.
/// </summary>
public sealed class RawImage
{
    private const uint RawMagic = 0x474D4952; // "RIMG"

    public int Width { get; }
    public int Height { get; }

    /// <summary>Row-major RGBA, 4 bytes/pixel, top-left origin.</summary>
    public byte[] Pixels { get; }

    public RawImage(int width, int height, byte[] pixels)
    {
        if (pixels.Length != width * height * 4)
        {
            throw new ArgumentException($"Expected {width * height * 4} bytes, got {pixels.Length}.");
        }
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    /// <summary>True if any pixel is not fully transparent black — i.e. something was drawn.</summary>
    public bool HasContent()
    {
        foreach (var b in Pixels)
        {
            if (b != 0) return true;
        }
        return false;
    }

    public ImageDiff Compare(RawImage other, int perChannelTolerance)
    {
        if (Width != other.Width || Height != other.Height)
        {
            return new ImageDiff(false, long.MaxValue, 255, (long)Width * Height);
        }

        long differing = 0;
        int maxChannelDiff = 0;
        for (int i = 0; i < Pixels.Length; i += 4)
        {
            bool pixelDiffers = false;
            for (int c = 0; c < 4; c++)
            {
                int d = Math.Abs(Pixels[i + c] - other.Pixels[i + c]);
                if (d > maxChannelDiff) maxChannelDiff = d;
                if (d > perChannelTolerance) pixelDiffers = true;
            }
            if (pixelDiffers) differing++;
        }

        return new ImageDiff(true, differing, maxChannelDiff, (long)Width * Height);
    }

    public void SaveRaw(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var fs = File.Create(path);
        using var w = new BinaryWriter(fs);
        w.Write(RawMagic);
        w.Write(Width);
        w.Write(Height);
        // Pixels are deflate-compressed so committed baselines stay small.
        using var deflate = new DeflateStream(fs, CompressionLevel.Optimal);
        deflate.Write(Pixels, 0, Pixels.Length);
    }

    public static RawImage LoadRaw(string path)
    {
        using var fs = File.OpenRead(path);
        using var r = new BinaryReader(fs);
        if (r.ReadUInt32() != RawMagic)
        {
            throw new InvalidDataException($"{path} is not a RIMG file.");
        }
        int w = r.ReadInt32();
        int h = r.ReadInt32();
        var px = new byte[w * h * 4];
        using var deflate = new DeflateStream(fs, CompressionMode.Decompress);
        int read = 0;
        while (read < px.Length)
        {
            int n = deflate.Read(px, read, px.Length - read);
            if (n == 0) break;
            read += n;
        }
        if (read != px.Length)
        {
            throw new InvalidDataException($"{path}: expected {px.Length} pixel bytes, got {read}.");
        }
        return new RawImage(w, h, px);
    }

    /// <summary>Write a standard 8-bit RGBA PNG. Used only for eyeballing results; never read back.</summary>
    public void SavePng(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var fs = File.Create(path);
        Span<byte> sig = stackalloc byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        fs.Write(sig);

        // IHDR
        var ihdr = new byte[13];
        WriteBigEndian(ihdr, 0, (uint)Width);
        WriteBigEndian(ihdr, 4, (uint)Height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 6;  // color type: RGBA
        WriteChunk(fs, "IHDR", ihdr);

        // IDAT: zlib-wrapped deflate of filtered scanlines (filter byte 0 per row).
        int stride = Width * 4;
        var raw = new byte[(stride + 1) * Height];
        for (int y = 0; y < Height; y++)
        {
            raw[y * (stride + 1)] = 0; // no filter
            Array.Copy(Pixels, y * stride, raw, y * (stride + 1) + 1, stride);
        }
        WriteChunk(fs, "IDAT", ZlibCompress(raw));

        WriteChunk(fs, "IEND", Array.Empty<byte>());
    }

    private static byte[] ZlibCompress(byte[] data)
    {
        using var ms = new MemoryStream();
        ms.WriteByte(0x78);
        ms.WriteByte(0x01); // zlib header: deflate, 32K window, no dict
        using (var deflate = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(data, 0, data.Length);
        }
        var adler = Adler32(data);
        ms.WriteByte((byte)(adler >> 24));
        ms.WriteByte((byte)(adler >> 16));
        ms.WriteByte((byte)(adler >> 8));
        ms.WriteByte((byte)adler);
        return ms.ToArray();
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        Span<byte> len = stackalloc byte[4];
        WriteBigEndian(len, 0, (uint)data.Length);
        s.Write(len);

        var typeBytes = new byte[4];
        for (int i = 0; i < 4; i++) typeBytes[i] = (byte)type[i];
        s.Write(typeBytes);
        s.Write(data);

        uint crc = Crc32(typeBytes, data);
        Span<byte> crcBytes = stackalloc byte[4];
        WriteBigEndian(crcBytes, 0, crc);
        s.Write(crcBytes);
    }

    private static void WriteBigEndian(Span<byte> dst, int offset, uint value)
    {
        dst[offset + 0] = (byte)(value >> 24);
        dst[offset + 1] = (byte)(value >> 16);
        dst[offset + 2] = (byte)(value >> 8);
        dst[offset + 3] = (byte)value;
    }

    private static uint Adler32(byte[] data)
    {
        const uint mod = 65521;
        uint a = 1, b = 0;
        foreach (var x in data)
        {
            a = (a + x) % mod;
            b = (b + a) % mod;
        }
        return (b << 16) | a;
    }

    private static uint Crc32(byte[] type, byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        crc = Crc32Update(crc, type);
        crc = Crc32Update(crc, data);
        return crc ^ 0xFFFFFFFF;
    }

    private static uint Crc32Update(uint crc, byte[] data)
    {
        foreach (var b in data)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }
        }
        return crc;
    }
}

/// <param name="SizeMatches">False when dimensions differ; other fields are meaningless then.</param>
/// <param name="DiffPixels">Pixels whose per-channel delta exceeded the tolerance.</param>
/// <param name="MaxChannelDiff">Largest single-channel delta seen (0-255).</param>
/// <param name="TotalPixels">Total pixel count.</param>
public readonly record struct ImageDiff(bool SizeMatches, long DiffPixels, int MaxChannelDiff, long TotalPixels)
{
    public double DiffFraction => TotalPixels == 0 ? 0 : (double)DiffPixels / TotalPixels;
}
