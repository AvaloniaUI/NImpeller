using NImpeller.Tests.Headless;
using Xunit;

namespace NImpeller.Tests;

[Collection(ImpellerGLCollection.Name)]
public sealed class RenderTargetTests
{
    private readonly ImpellerGLFixture _gl;

    public RenderTargetTests(ImpellerGLFixture gl) => _gl = gl;

    private static ImpellerDisplayList Record(int width, int height, Action<ImpellerDisplayListBuilder> record)
    {
        using var builder = ImpellerDisplayListBuilder.New(new ImpellerRect(0, 0, width, height))!;
        record(builder);
        return builder.CreateDisplayListNew()!;
    }

    private static void Fill(ImpellerDisplayListBuilder builder, ImpellerRect rect, ImpellerColor color)
    {
        using var paint = ImpellerPaint.New()!;
        paint.SetColor(color);
        builder.DrawRect(rect, paint);
    }

    [Fact]
    public void Renders_into_a_texture_and_reads_it_back_top_down()
    {
        RenderGate.Require(_gl);

        var pixels = _gl.Run(context =>
        {
            using var texture = context.TextureCreateRenderTargetNew(new ImpellerISize(64, 32),
                ImpellerPixelFormat.kImpellerPixelFormatRGBA8888)!;
            using var surface = context.SurfaceCreateWithTextureNew(texture)!;
            using var dl = Record(64, 32, b =>
            {
                Fill(b, new ImpellerRect(0, 0, 64, 16), new ImpellerColor { Red = 1, Alpha = 1 });
                Fill(b, new ImpellerRect(0, 16, 32, 16), new ImpellerColor { Green = 1, Alpha = 0.5f });
            });
            Assert.True(surface.DrawDisplayList(dl));

            var result = new byte[64 * 32 * 4];
            Assert.True(context.TextureReadPixels(texture, new ImpellerIRect { Width = 64, Height = 32 }, result,
                64 * 4));
            return result;
        });

        byte[] Pixel(int x, int y) => pixels.AsSpan((y * 64 + x) * 4, 4).ToArray();
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, Pixel(10, 5));
        Assert.Equal(0, Pixel(10, 25)[0]);
        Assert.InRange(Pixel(10, 25)[1], 127, 129);
        Assert.InRange(Pixel(10, 25)[3], 127, 129);
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, Pixel(50, 25));
    }

    [Fact]
    public void Reads_regions_with_padded_rows()
    {
        RenderGate.Require(_gl);

        var (sub, outsideRead) = _gl.Run(context =>
        {
            using var texture = context.TextureCreateRenderTargetNew(new ImpellerISize(64, 32),
                ImpellerPixelFormat.kImpellerPixelFormatRGBA8888)!;
            using var surface = context.SurfaceCreateWithTextureNew(texture)!;
            using var dl = Record(64, 32,
                b => Fill(b, new ImpellerRect(0, 0, 32, 16), new ImpellerColor { Blue = 1, Alpha = 1 }));
            surface.DrawDisplayList(dl);

            var region = new byte[4 * 32];
            region.AsSpan().Fill(0xAB);
            Assert.True(context.TextureReadPixels(texture,
                new ImpellerIRect { X = 30, Y = 14, Width = 4, Height = 4 }, region, 32));
            var outside = context.TextureReadPixels(texture,
                new ImpellerIRect { X = 60, Y = 0, Width = 8, Height = 8 }, new byte[8 * 8 * 4], 32);
            return (region, outside);
        });

        // (30, 14) is blue, (33, 17) is clear and the row padding is untouched.
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, sub.AsSpan(0, 4).ToArray());
        Assert.Equal(0, sub[3 * 32 + 3 * 4 + 3]);
        Assert.Equal(0xAB, sub[16]);
        Assert.False(outsideRead);
    }

    [Fact]
    public void Render_target_textures_can_be_drawn()
    {
        RenderGate.Require(_gl);

        var pixels = _gl.Run(context =>
        {
            using var source = context.TextureCreateRenderTargetNew(new ImpellerISize(16, 16),
                ImpellerPixelFormat.kImpellerPixelFormatRGBA8888)!;
            using (var surface = context.SurfaceCreateWithTextureNew(source)!)
            using (var dl = Record(16, 16,
                       b => Fill(b, new ImpellerRect(0, 0, 8, 16), new ImpellerColor { Red = 1, Alpha = 1 })))
                surface.DrawDisplayList(dl);

            using var target = context.TextureCreateRenderTargetNew(new ImpellerISize(32, 16),
                ImpellerPixelFormat.kImpellerPixelFormatRGBA8888)!;
            using (var surface = context.SurfaceCreateWithTextureNew(target)!)
            using (var paint = ImpellerPaint.New()!)
            using (var dl = Record(32, 16, b => b.DrawTexture(source, new ImpellerPoint { X = 16, Y = 0 },
                       ImpellerTextureSampling.kImpellerTextureSamplingNearestNeighbor, paint)))
                surface.DrawDisplayList(dl);

            var result = new byte[32 * 16 * 4];
            context.TextureReadPixels(target, new ImpellerIRect { Width = 32, Height = 16 }, result, 32 * 4);
            return result;
        });

        Assert.Equal(0, pixels[(8 * 32 + 4) * 4 + 3]);
        Assert.Equal(255, pixels[(8 * 32 + 20) * 4 + 0]);
        Assert.Equal(0, pixels[(8 * 32 + 28) * 4 + 3]);
    }

    [Fact]
    public void Draws_glyphs_where_the_font_puts_ink()
    {
        RenderGate.Require(_gl);

        var pixels = _gl.Run(context =>
        {
            using var typeface = ImpellerTypeface.CreateWithData(Unit.TypefaceTests.Asset("Ahem.ttf"))!;
            using var font = typeface.FontNew(20)!;
            // Every inked Ahem glyph is an em box from 0.8em above the baseline to 0.2em below it.
            var glyph = Unit.TypefaceTests.FindInkedGlyph(font, 19);
            Assert.NotEqual(0, glyph);

            using var texture = context.TextureCreateRenderTargetNew(new ImpellerISize(64, 64),
                ImpellerPixelFormat.kImpellerPixelFormatRGBA8888)!;
            using var surface = context.SurfaceCreateWithTextureNew(texture)!;
            using var paint = ImpellerPaint.New()!;
            paint.SetColor(new ImpellerColor { Blue = 1, Alpha = 1 });
            using var dl = Record(64, 64, b => b.DrawGlyphs(font, [glyph, glyph],
                [new ImpellerPoint(), new ImpellerPoint { X = 30 }], new ImpellerPoint { X = 10, Y = 40 }, paint));
            surface.DrawDisplayList(dl);

            var result = new byte[64 * 64 * 4];
            context.TextureReadPixels(texture, new ImpellerIRect { Width = 64, Height = 64 }, result, 64 * 4);
            return result;
        });

        byte Alpha(int x, int y) => pixels[(y * 64 + x) * 4 + 3];
        Assert.Equal(255, Alpha(20, 34));
        Assert.Equal(255, pixels[(34 * 64 + 20) * 4 + 2]);
        Assert.Equal(255, Alpha(50, 34));
        Assert.Equal(0, Alpha(35, 34));
        Assert.Equal(0, Alpha(20, 20));
        Assert.Equal(0, Alpha(20, 48));
    }
}
