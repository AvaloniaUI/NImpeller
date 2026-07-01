using System;
using NImpeller;
using Xunit;

namespace NImpeller.Tests.Unit;

public sealed class FontRegistrationTests
{
    // Not a real font, so registration fails — but the copy + release-callback machinery still runs
    // (Impeller frees the mapping, and thus invokes our callback, even on failure). A null callback
    // would crash here; the test passing means it doesn't.
    private static readonly byte[] NotAFont = { 1, 2, 3, 4, 5, 6, 7, 8 };

    [Fact]
    public void Registering_invalid_font_returns_false_without_crashing()
    {
        using var typography = ImpellerTypographyContext.New();
        Assert.NotNull(typography);

        using var mem = new ImpellerUnmanagedMemory(NotAFont);
        bool registered = typography!.RegisterFont(mem, "MyAlias");

        Assert.False(registered); // junk bytes are not a valid font
    }

    [Fact]
    public void Registering_with_null_alias_is_allowed()
    {
        using var typography = ImpellerTypographyContext.New();
        using var mem = new ImpellerUnmanagedMemory(NotAFont);

        // family_name_alias is IMPELLER_NULLABLE; null must marshal cleanly (not throw).
        bool registered = typography!.RegisterFont(mem, familyNameAlias: null);

        Assert.False(registered);
    }

    [Fact]
    public void Contents_may_be_disposed_immediately_after_registration()
    {
        using var typography = ImpellerTypographyContext.New();

        var mem = new ImpellerUnmanagedMemory(NotAFont);
        typography!.RegisterFont(mem);
        // The wrapper copied the bytes, so disposing the source right away must be safe: Impeller
        // holds its own copy, freed by the release callback and not this buffer.
        mem.Dispose();
    }

    [Fact]
    public void Null_contents_throws()
    {
        using var typography = ImpellerTypographyContext.New();
        Assert.Throws<ArgumentNullException>(() => typography!.RegisterFont(null!));
    }
}
