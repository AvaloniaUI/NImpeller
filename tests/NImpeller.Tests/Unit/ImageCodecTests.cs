using NImpeller;
using Xunit;

namespace NImpeller.Tests.Unit;

public sealed class ImageCodecTests
{
    // 4x2: opaque red, green, blue, white; half transparent red (premultiplied); three clear.
    private static readonly byte[] Pixels =
    [
        255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 255, 255,
        128, 0, 0, 128, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    ];

    private static readonly ImpellerISize Size = new(4, 2);

    private static byte[] Encode(ImpellerImageFormat format, uint quality)
    {
        using var stream = new MemoryStream();
        Assert.True(ImpellerImageEncoder.Encode(Pixels, Size, 16, format, quality, stream));
        return stream.ToArray();
    }

    [Fact]
    public void Png_quality_is_the_zlib_level()
    {
        var pixels = Enumerable.Repeat((byte)255, 64 * 64 * 4).ToArray();
        byte[] EncodeAt(uint level)
        {
            using var stream = new MemoryStream();
            Assert.True(ImpellerImageEncoder.Encode(pixels, new ImpellerISize(64, 64), 256,
                ImpellerImageFormat.kImpellerImageFormatPNG, level, stream));
            return stream.ToArray();
        }

        var stored = EncodeAt(0);
        Assert.True(stored.Length > pixels.Length);
        Assert.True(EncodeAt(9).Length < stored.Length / 10);
    }

    [Fact]
    public void Png_round_trips()
    {
        var png = Encode(ImpellerImageFormat.kImpellerImageFormatPNG, 9);
        Assert.Equal((byte)'P', png[1]);

        using var decoder = ImpellerImageDecoder.Create(png)!;
        Assert.Equal(Size, decoder.Size);
        var decoded = new byte[32];
        Assert.True(decoder.Decode(decoded, 16));
        for (var i = 0; i < Pixels.Length; i++)
            Assert.InRange(decoded[i], Pixels[i] - 1, Pixels[i] + 1);
    }

    [Fact]
    public void Jpeg_decodes_and_scales()
    {
        // A 64x32 gradient, so the JPEG has something to compress.
        var pixels = new byte[64 * 32 * 4];
        for (var y = 0; y < 32; y++)
        for (var x = 0; x < 64; x++)
        {
            var i = (y * 64 + x) * 4;
            pixels[i] = (byte)(x * 4);
            pixels[i + 1] = (byte)(y * 8);
            pixels[i + 3] = 255;
        }
        using var stream = new MemoryStream();
        Assert.True(ImpellerImageEncoder.Encode(pixels, new ImpellerISize(64, 32), 256,
            ImpellerImageFormat.kImpellerImageFormatJPEG, 90, stream));
        var jpeg = stream.ToArray();
        Assert.Equal(0xFF, jpeg[0]);
        Assert.Equal(0xD8, jpeg[1]);

        using var decoder = ImpellerImageDecoder.Create(jpeg)!;
        Assert.Equal(new ImpellerISize(64, 32), decoder.Size);
        var small = new byte[16 * 8 * 4];
        Assert.True(decoder.Decode(small, 16 * 4, new ImpellerISize(16, 8)));
        Assert.Equal(255, small[3]);
        // The right edge is red, the left edge isn't.
        Assert.True(small[(4 * 16 + 15) * 4] > 200);
        Assert.True(small[(4 * 16 + 0) * 4] < 60);
    }

    [Fact]
    public void Lossless_webp_round_trips()
    {
        var webp = Encode(ImpellerImageFormat.kImpellerImageFormatWebP, 100);
        using var decoder = ImpellerImageDecoder.Create(webp)!;
        var decoded = new byte[32];
        Assert.True(decoder.Decode(decoded, 16));
        Assert.Equal(Pixels[..16], decoded[..16]);
    }

    [Fact]
    public void Create_returns_null_for_garbage()
    {
        Assert.Null(ImpellerImageDecoder.Create([1, 2, 3, 4]));
    }
}
