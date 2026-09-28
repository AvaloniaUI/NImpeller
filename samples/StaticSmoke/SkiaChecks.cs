using HarfBuzzSharp;
using SkiaSharp;

namespace StaticSmoke;

// SkiaSharp raster draw + readback and HarfBuzzSharp shaping. Shared with Sandbox.Web (NImpellerSkiaCheck).
public static class SkiaChecks
{
    public static bool Run(byte[] font, Action<string> log)
    {
        var ok = true;

        using (var surface = SKSurface.Create(new SKImageInfo(64, 64, SKColorType.Rgba8888, SKAlphaType.Premul)))
        using (var typeface = SKTypeface.FromData(SKData.CreateCopy(font)))
        using (var skFont = new SKFont(typeface, 40))
        using (var paint = new SKPaint { Color = SKColors.Red })
        using (var textPaint = new SKPaint { Color = SKColors.Black, IsAntialias = false })
        {
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);
            canvas.DrawRect(0, 0, 8, 8, paint);
            canvas.DrawText("W", 12, 50, SKTextAlign.Left, skFont, textPaint);

            using var bitmap = new SKBitmap(64, 64, SKColorType.Rgba8888, SKAlphaType.Premul);
            surface.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0);
            var corner = bitmap.GetPixel(4, 4);
            var inked = 0;
            for (var y = 10; y < 64; y++)
            for (var x = 10; x < 64; x++)
                if (bitmap.GetPixel(x, y) != SKColors.White)
                    inked++;

            log($"SkiaSharp {SkiaSharpVersion.Native}: corner={corner} text pixels={inked}");
            ok &= Check(corner == SKColors.Red, "SkiaSharp readback returned the wrong color", log);
            ok &= Check(inked > 50, "SkiaSharp drew no text", log);
        }

        using (var blob = Blob.FromStream(new MemoryStream(font)))
        using (var face = new Face(blob, 0))
        using (var hbFont = new HarfBuzzSharp.Font(face))
        using (var buffer = new HarfBuzzSharp.Buffer())
        {
            buffer.AddUtf8("Hello, HarfBuzz");
            buffer.GuessSegmentProperties();
            hbFont.Shape(buffer);
            var glyphs = buffer.GlyphInfos;
            var notdef = glyphs.Count(g => g.Codepoint == 0);
            log($"HarfBuzzSharp: {glyphs.Length} glyphs, {notdef} missing");
            ok &= Check(glyphs.Length == 15 && notdef == 0, "HarfBuzzSharp shaping returned the wrong glyphs", log);
        }

        return ok;
    }

    static bool Check(bool condition, string message, Action<string> log)
    {
        if (!condition)
            log("FAIL: " + message);
        return condition;
    }
}
