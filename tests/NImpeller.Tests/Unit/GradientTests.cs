using System;
using NImpeller;
using Xunit;

namespace NImpeller.Tests.Unit;

public sealed class GradientTests
{
    private static readonly ImpellerColor[] ThreeColors =
    {
        ImpellerColor.FromRgb(255, 0, 0),
        ImpellerColor.FromRgb(0, 255, 0),
        ImpellerColor.FromRgb(0, 0, 255),
    };

    private static readonly float[] ThreeStops = { 0f, 0.5f, 1f };

    [Fact]
    public void LinearGradient_with_multiple_stops_is_created()
    {
        using var source = ImpellerColorSource.CreateLinearGradientNew(
            new ImpellerPoint { X = 0, Y = 0 },
            new ImpellerPoint { X = 100, Y = 0 },
            ThreeColors,
            ThreeStops,
            ImpellerTileMode.kImpellerTileModeClamp);

        Assert.NotNull(source);
    }

    [Fact]
    public void RadialGradient_with_multiple_stops_is_created()
    {
        using var source = ImpellerColorSource.CreateRadialGradientNew(
            new ImpellerPoint { X = 50, Y = 50 }, 50f, ThreeColors, ThreeStops,
            ImpellerTileMode.kImpellerTileModeClamp);

        Assert.NotNull(source);
    }

    [Fact]
    public void SweepGradient_with_multiple_stops_is_created()
    {
        using var source = ImpellerColorSource.CreateSweepGradientNew(
            new ImpellerPoint { X = 50, Y = 50 }, 0f, 360f, ThreeColors, ThreeStops,
            ImpellerTileMode.kImpellerTileModeClamp);

        Assert.NotNull(source);
    }

    [Fact]
    public void ConicalGradient_with_multiple_stops_is_created()
    {
        using var source = ImpellerColorSource.CreateConicalGradientNew(
            new ImpellerPoint { X = 30, Y = 30 }, 10f,
            new ImpellerPoint { X = 60, Y = 60 }, 50f,
            ThreeColors, ThreeStops, ImpellerTileMode.kImpellerTileModeClamp);

        Assert.NotNull(source);
    }

    [Fact]
    public void Mismatched_color_and_stop_counts_throw()
    {
        var ex = Assert.Throws<ArgumentException>(() => ImpellerColorSource.CreateLinearGradientNew(
            new ImpellerPoint { X = 0, Y = 0 },
            new ImpellerPoint { X = 100, Y = 0 },
            ThreeColors,
            new float[] { 0f, 1f }, // only two stops for three colors
            ImpellerTileMode.kImpellerTileModeClamp));

        Assert.Equal("stops", ex.ParamName);
    }

    [Fact]
    public void Empty_gradient_throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => ImpellerColorSource.CreateLinearGradientNew(
            new ImpellerPoint { X = 0, Y = 0 },
            new ImpellerPoint { X = 100, Y = 0 },
            ReadOnlySpan<ImpellerColor>.Empty,
            ReadOnlySpan<float>.Empty,
            ImpellerTileMode.kImpellerTileModeClamp));

        Assert.Equal("colors", ex.ParamName);
    }
}
