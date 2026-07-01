using NImpeller;
using Xunit;

namespace NImpeller.Tests.Unit;

public sealed class PathBuilderTests
{
    [Fact]
    public void New_returns_a_path_builder()
    {
        using var builder = ImpellerPathBuilder.New();
        Assert.NotNull(builder);
    }

    [Fact]
    public void CopyPathNew_yields_a_path_from_recorded_segments()
    {
        using var builder = ImpellerPathBuilder.New()!;
        builder.MoveTo(new ImpellerPoint { X = 100, Y = 100 });
        builder.LineTo(new ImpellerPoint { X = 200, Y = 200 });
        builder.Close();

        using var path = builder.CopyPathNew(ImpellerFillType.kImpellerFillTypeNonZero);
        Assert.NotNull(path);
    }

    [Fact]
    public void Builder_supports_curves_rects_and_ovals()
    {
        using var builder = ImpellerPathBuilder.New()!;
        builder.MoveTo(new ImpellerPoint { X = 0, Y = 0 });
        builder.QuadraticCurveTo(new ImpellerPoint { X = 10, Y = 10 }, new ImpellerPoint { X = 20, Y = 0 });
        builder.CubicCurveTo(
            new ImpellerPoint { X = 30, Y = 10 },
            new ImpellerPoint { X = 40, Y = -10 },
            new ImpellerPoint { X = 50, Y = 0 });
        builder.AddRect(new ImpellerRect(0, 0, 10, 10));
        builder.AddOval(new ImpellerRect(0, 0, 10, 10));

        using var path = builder.TakePathNew(ImpellerFillType.kImpellerFillTypeOdd);
        Assert.NotNull(path);
    }

    [Fact]
    public void GetBounds_reports_the_extent_of_a_line()
    {
        using var builder = ImpellerPathBuilder.New()!;
        builder.MoveTo(new ImpellerPoint { X = 100, Y = 100 });
        builder.LineTo(new ImpellerPoint { X = 200, Y = 200 });
        using var path = builder.CopyPathNew(ImpellerFillType.kImpellerFillTypeNonZero)!;

        path.GetBounds(out var bounds);

        Assert.Equal(new ImpellerRect(100, 100, 100, 100), bounds);
    }

    [Fact]
    public void GetBounds_reports_the_rect_of_an_added_rect()
    {
        using var builder = ImpellerPathBuilder.New()!;
        builder.AddRect(new ImpellerRect(15, 25, 60, 40));
        using var path = builder.CopyPathNew(ImpellerFillType.kImpellerFillTypeNonZero)!;

        path.GetBounds(out var bounds);

        Assert.Equal(new ImpellerRect(15, 25, 60, 40), bounds);
    }
}
