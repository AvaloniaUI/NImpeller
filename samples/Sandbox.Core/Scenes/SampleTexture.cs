using NImpeller;
using static Sandbox.Scenes.Draw;

namespace Sandbox.Scenes;

/// <summary>A CPU-generated RGBA test image, uploaded once per context.</summary>
internal sealed class SampleTexture : IDisposable
{
    public const int Size = 256;

    private ImpellerContext? _context;
    private ImpellerTexture? _texture;

    public ImpellerTexture Get(ImpellerContext context)
    {
        if (_texture is not null && ReferenceEquals(_context, context)) return _texture;

        _texture?.Dispose();
        using var mem = new ImpellerUnmanagedMemory(Pixels());
        var descriptor = new ImpellerTextureDescriptor
        {
            Pixel_format = ImpellerPixelFormat.kImpellerPixelFormatRGBA8888,
            Size = new ImpellerISize(Size, Size),
            Mip_count = 1,
        };
        _texture = context.TextureCreateWithContentsNew(descriptor, mem)
            ?? throw new InvalidOperationException("Could not create the texture.");
        _context = context;
        return _texture;
    }

    private static byte[] Pixels()
    {
        var pixels = new byte[Size * Size * 4];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float u = x / (float)Size, v = y / (float)Size;
                var c = Hsv(u * 0.8f + v * 0.2f, 0.55f, 0.35f + 0.5f * v);
                float dx = u - 0.5f, dy = v - 0.5f, r = MathF.Sqrt(dx * dx + dy * dy);
                if (r is > 0.18f and < 0.24f) c = Hsv(0.12f, 0.9f, 1f);
                if (x % 32 == 0 || y % 32 == 0) c = new ImpellerColor { Red = 1, Green = 1, Blue = 1, Alpha = 1 };
                if (((x / 8) + (y / 8)) % 2 == 0 && x > 176 && y > 176) c = new ImpellerColor { Alpha = 1 };

                int i = (y * Size + x) * 4;
                pixels[i] = (byte)(c.Red * 255f);
                pixels[i + 1] = (byte)(c.Green * 255f);
                pixels[i + 2] = (byte)(c.Blue * 255f);
                pixels[i + 3] = 255;
            }
        }
        return pixels;
    }

    public void Dispose() => _texture?.Dispose();
}
