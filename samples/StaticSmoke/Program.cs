using System.Runtime.InteropServices;
using NImpeller;
using StaticSmoke;

// Proves that a NativeAOT app can link Impeller statically next to SkiaSharp and HarfBuzzSharp,
// and that neither interposes on the other's internal Skia/HarfBuzz/ICU. No GPU needed.
var ok = true;
void Check(bool condition, string message)
{
    if (!condition)
    {
        Console.WriteLine("FAIL: " + message);
        ok = false;
    }
}

using var fontStream = typeof(SkiaChecks).Assembly.GetManifestResourceStream("NotoSans-Regular.ttf")!;
var font = new byte[fontStream.Length];
fontStream.ReadExactly(font);

var version = Native.ImpellerGetVersion();
Console.WriteLine($"Impeller version 0x{version:x8}");
Check(version != 0, "ImpellerGetVersion returned 0");

using (var pathBuilder = ImpellerPathBuilder.New()!)
using (var displayListBuilder = ImpellerDisplayListBuilder.New(new ImpellerRect(0, 0, 100, 100))!)
using (var paint = ImpellerPaint.New()!)
{
    pathBuilder.MoveTo(new ImpellerPoint { X = 10, Y = 10 });
    pathBuilder.LineTo(new ImpellerPoint { X = 90, Y = 10 });
    pathBuilder.LineTo(new ImpellerPoint { X = 50, Y = 90 });
    using var path = pathBuilder.TakePathNew(ImpellerFillType.kImpellerFillTypeNonZero);
    Check(path != null, "path is null");
    displayListBuilder.DrawPath(path!, paint);
    using var displayList = displayListBuilder.CreateDisplayListNew();
    Check(displayList != null, "display list is null");
}

// Exercises Impeller's internal Skia (fonts), HarfBuzz (shaping) and ICU (line breaking).
using (var typography = ImpellerTypographyContext.New()!)
using (var memory = new ImpellerUnmanagedMemory(font))
{
    Check(typography.RegisterFont(memory, "Smoke"), "RegisterFont failed");
    using var builder = typography.ParagraphBuilderNew()!;
    using var style = ImpellerParagraphStyle.New()!;
    using var paint = ImpellerPaint.New()!;
    paint.SetColor(ImpellerColor.FromRgb(0, 0, 0));
    style.SetForeground(paint);
    style.SetFontFamily("Smoke");
    style.SetFontSize(20);
    builder.PushStyle(style);
    builder.AddText("The quick brown fox jumps over the lazy dog, twice over.");
    using var paragraph = builder.BuildParagraphNew(200)!;
    var lines = paragraph.GetLineCount();
    var longest = paragraph.GetLongestLineWidth();
    Console.WriteLine($"Impeller paragraph: {lines} lines, longest {longest:F1}, height {paragraph.GetHeight():F1}");
    Check(lines >= 3, "paragraph didn't wrap");
    Check(longest > 50 && longest <= 200, "paragraph width is off");
}

Check(SkiaChecks.Run(font, Console.WriteLine), "SkiaSharp/HarfBuzzSharp checks failed");

Console.WriteLine(ok ? "OK" : "FAILED");
return ok ? 0 : 1;

static partial class Native
{
    [LibraryImport("impeller")]
    public static partial uint ImpellerGetVersion();
}
