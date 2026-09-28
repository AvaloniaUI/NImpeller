using NImpeller;
using Xunit;

namespace NImpeller.Tests.Unit;

public sealed class FontMatchingTests
{
    private static ImpellerTypographyContext Context(params (string File, string Alias)[] fonts)
    {
        var context = ImpellerTypographyContext.New()!;
        foreach (var (file, alias) in fonts)
        {
            using var memory = new ImpellerUnmanagedMemory(TypefaceTests.Asset(file));
            Assert.True(context.RegisterFont(memory, alias));
        }
        return context;
    }

    [Fact]
    public void Matches_registered_families_by_style()
    {
        using var context = Context(("Roboto-Regular.ttf", "Faces"), ("Roboto-Medium.ttf", "Faces"),
            ("Ahem.ttf", "MyAhem"));

        using var ahem = context.MatchTypefaceNew("MyAhem", ImpellerTypefaceStyle.Normal)!;
        Assert.Equal(1000u, ahem.GetUnitsPerEm());

        using var regular = context.MatchTypefaceNew("Faces", ImpellerTypefaceStyle.Normal)!;
        regular.GetStyle(out var style);
        Assert.Equal(400u, style.Weight);
        Assert.Equal(5u, style.Width);
        Assert.Equal(ImpellerFontSlant.kImpellerFontSlantUpright, style.Slant);

        using var medium = context.MatchTypefaceNew("Faces", new ImpellerTypefaceStyle(600, 5, ImpellerFontSlant.kImpellerFontSlantUpright))!;
        medium.GetStyle(out style);
        Assert.Equal(500u, style.Weight);

        Assert.Null(context.MatchTypefaceNew("No Such Family", ImpellerTypefaceStyle.Normal));

        var names = context.GetFamilyNames();
        Assert.Contains("Faces", names);
        Assert.Contains("MyAhem", names);
    }

    [Fact]
    public void Lists_the_styles_of_a_family()
    {
        using var context = Context(("Roboto-Regular.ttf", "Faces"), ("Roboto-Medium.ttf", "Faces"));

        var styles = context.GetFamilyStyles("Faces");
        Assert.Equal([400u, 500u], styles.Select(s => s.Weight).Order());
        Assert.All(styles, s => Assert.Equal(5u, s.Width));
        Assert.Empty(context.GetFamilyStyles("No Such Family"));
    }

    [Fact]
    public void Falls_back_to_a_registered_family_with_the_glyph()
    {
        using var context = Context(("Ahem.ttf", "Box"), ("Roboto-Regular.ttf", "Text"));

        using var latin = context.MatchCharacterNew("Box", ImpellerTypefaceStyle.Normal, "en-US", 'X')!;
        Assert.Equal(1000u, latin.GetUnitsPerEm());

        // Ahem has no Cyrillic; Roboto does.
        using var cyrillic = context.MatchCharacterNew("Box", ImpellerTypefaceStyle.Normal, null, 0x0444)!;
        Assert.Equal("Roboto", cyrillic.GetFamilyName());
    }

    [Fact]
    public void Resolves_a_system_family_on_linux()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "fontconfig matching is Linux only.");
        using var context = ImpellerTypographyContext.New()!;
        using var typeface = context.MatchTypefaceNew(null, ImpellerTypefaceStyle.Normal);
        Assert.NotNull(typeface);
        Assert.NotEmpty(typeface.GetFamilyName());
        Assert.NotEmpty(context.GetFamilyNames());
    }
}
