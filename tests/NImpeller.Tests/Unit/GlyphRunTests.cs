using System;
using System.IO;
using NImpeller;
using Xunit;

namespace NImpeller.Tests.Unit;

public sealed class GlyphRunTests
{
    // Ahem: every glyph is a box one em wide, 0.8 em above and 0.2 em below the baseline.
    private const ushort X = 58;

    private static byte[] Ahem() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Assets", "Ahem.ttf"));

    private static ImpellerTypeface LoadAhem()
    {
        using var memory = new ImpellerUnmanagedMemory(Ahem());
        return ImpellerTypeface.New(memory, 0) ?? throw new InvalidOperationException("Ahem did not load");
    }

    [Fact]
    public void Invalid_font_data_gives_no_typeface()
    {
        using var memory = new ImpellerUnmanagedMemory(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        Assert.Null(ImpellerTypeface.New(memory, 0));
    }

    [Fact]
    public void Missing_face_gives_no_typeface()
    {
        using var memory = new ImpellerUnmanagedMemory(Ahem());
        Assert.Null(ImpellerTypeface.New(memory, 5));
    }

    [Fact]
    public void Font_data_may_be_freed_after_the_typeface_is_created()
    {
        var memory = new ImpellerUnmanagedMemory(Ahem());
        using var typeface = ImpellerTypeface.New(memory, 0);
        memory.Dispose();

        using var run = typeface!.GlyphRunNew(40, null, new[] { X }, new[] { new ImpellerPoint() });
        Assert.NotNull(run);
    }

    [Fact]
    public void Bounds_cover_the_glyph_boxes()
    {
        using var typeface = LoadAhem();
        using var run = typeface.GlyphRunNew(100, null, new[] { X, X },
            new[] { new ImpellerPoint { X = 10, Y = 0 }, new ImpellerPoint { X = 110, Y = 0 } })!;

        run.GetBounds(out var bounds);
        Assert.True(bounds.X <= 10 && bounds.Y <= -80, $"bounds {bounds}");
        Assert.True(bounds.X + bounds.Width >= 210 && bounds.Y + bounds.Height >= 20, $"bounds {bounds}");
    }

    [Fact]
    public void Options_are_accepted()
    {
        using var typeface = LoadAhem();
        var options = new ImpellerGlyphRunOptions
        {
            Edging = ImpellerFontEdging.kImpellerFontEdgingAlias,
            Hinting = ImpellerFontHinting.kImpellerFontHintingFull,
            Skew_x = -0.3f,
            Embolden = 1,
        };
        using var run = typeface.GlyphRunNew(24, options, new[] { X }, new[] { new ImpellerPoint() });
        Assert.NotNull(run);
    }

    [Fact]
    public void Empty_run_gives_no_glyph_run()
    {
        using var typeface = LoadAhem();
        Assert.Null(typeface.GlyphRunNew(24, null, ReadOnlySpan<ushort>.Empty, ReadOnlySpan<ImpellerPoint>.Empty));
    }

    [Fact]
    public void Glyphs_and_positions_must_have_the_same_length()
    {
        using var typeface = LoadAhem();
        Assert.Throws<ArgumentException>(() =>
            typeface.GlyphRunNew(24, null, new[] { X, X }, new[] { new ImpellerPoint() }));
    }
}
