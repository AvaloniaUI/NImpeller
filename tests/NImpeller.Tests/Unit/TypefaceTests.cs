using NImpeller;
using Xunit;

namespace NImpeller.Tests.Unit;

public sealed class TypefaceTests
{
    internal static byte[] Asset(string name) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Assets", name));

    internal static uint Tag(string tag) =>
        (uint)(tag[0] << 24 | tag[1] << 16 | tag[2] << 8 | tag[3]);

    // The first glyph whose ink is at least the given size.
    internal static ushort FindInkedGlyph(ImpellerFont font, float minSize)
    {
        Span<ImpellerRect> bounds = stackalloc ImpellerRect[1];
        for (ushort glyph = 1; glyph < 500; glyph++)
        {
            font.GetGlyphBounds([glyph], bounds);
            if (bounds[0].Width >= minSize && bounds[0].Height >= minSize)
                return glyph;
        }
        return 0;
    }

    [Fact]
    public void Typefaces_expose_metrics_tables_and_data()
    {
        var bytes = Asset("Roboto-Regular.ttf");
        using var typeface = ImpellerTypeface.CreateWithData(bytes)!;
        Assert.NotNull(typeface);
        Assert.Equal(2048u, typeface.GetUnitsPerEm());
        Assert.Equal(54, typeface.GetTableData(Tag("head"))!.Length);
        Assert.Null(typeface.GetTableData(Tag("zzzz")));
        Assert.Equal(bytes, typeface.GetData(out var faceIndex));
        Assert.Equal(0u, faceIndex);
        Assert.Equal("Roboto", typeface.GetFamilyName());
        typeface.GetStyle(out var style);
        Assert.Equal(ImpellerTypefaceStyle.Normal, style);
    }

    [Fact]
    public void CreateWithData_returns_null_for_garbage()
    {
        Assert.Null(ImpellerTypeface.CreateWithData([1, 2, 3, 4]));
    }

    [Fact]
    public void Glyph_paths_match_glyph_bounds()
    {
        using var typeface = ImpellerTypeface.CreateWithData(Asset("Roboto-Regular.ttf"))!;
        using var font = typeface.FontNew(100)!;
        var glyph = FindInkedGlyph(font, 40);
        Assert.NotEqual(0, glyph);

        using var path = font.CreateGlyphPathNew(glyph)!;
        path.GetTightBounds(out var pathBounds);
        Span<ImpellerRect> bounds = stackalloc ImpellerRect[1];
        font.GetGlyphBounds([glyph], bounds);

        // Y-down with the origin on the baseline; glyph bounds are pixel aligned.
        Assert.True(bounds[0].Y < -30);
        Assert.Equal(bounds[0].X, pathBounds.X, 2f);
        Assert.Equal(bounds[0].Y, pathBounds.Y, 2f);
        Assert.Equal(bounds[0].Width, pathBounds.Width, 2f);
        Assert.Equal(bounds[0].Height, pathBounds.Height, 2f);

        font.SetEmbolden(true);
        using var bold = font.CreateGlyphPathNew(glyph)!;
        bold.GetTightBounds(out var boldBounds);
        Assert.True(boldBounds.Width > pathBounds.Width);

        font.SetEmbolden(false);
        font.SetSkewX(-0.25f);
        using var skewed = font.CreateGlyphPathNew(glyph)!;
        skewed.GetTightBounds(out var skewedBounds);
        Assert.True(skewedBounds.Width > pathBounds.Width);
    }

    [Fact]
    public void Variations_change_glyph_outlines()
    {
        using var typeface = ImpellerTypeface.CreateWithData(Asset("RobotoSlab-VariableFont_wght.ttf"))!;
        using var thin = typeface.CreateWithVariations([new ImpellerFontVariation { Axis_tag = Tag("wght"), Value = 100 }])!;
        using var black = typeface.CreateWithVariations([new ImpellerFontVariation { Axis_tag = Tag("wght"), Value = 900 }])!;
        using var thinFont = thin.FontNew(100)!;
        using var blackFont = black.FontNew(100)!;
        var glyph = FindInkedGlyph(thinFont, 40);

        using var thinPath = thinFont.CreateGlyphPathNew(glyph)!;
        using var blackPath = blackFont.CreateGlyphPathNew(glyph)!;
        thinPath.GetTightBounds(out var thinBounds);
        blackPath.GetTightBounds(out var blackBounds);
        Assert.True(blackBounds.Width > thinBounds.Width + 1);
    }
}
