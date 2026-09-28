using NImpeller;
using Xunit;

namespace NImpeller.Tests.Unit;

public sealed class PathMeasureTests
{
    private static ImpellerPath Build(Action<ImpellerPathBuilder> build)
    {
        using var builder = ImpellerPathBuilder.New()!;
        build(builder);
        return builder.TakePathNew(ImpellerFillType.kImpellerFillTypeNonZero)!;
    }

    private static ImpellerPoint P(float x, float y) => new() { X = x, Y = y };

    [Fact]
    public void Measures_length_position_and_tangent_of_a_line()
    {
        using var line = Build(b =>
        {
            b.MoveTo(P(0, 0));
            b.LineTo(P(30, 40));
        });
        using var measure = ImpellerPathMeasure.New(line, false)!;

        Assert.Equal(50, measure.GetLength(), 4);
        Assert.True(measure.GetPositionAndTangent(25, out var position, out var tangent));
        Assert.Equal(15, position.X, 4);
        Assert.Equal(20, position.Y, 4);
        Assert.Equal(0.6f, tangent.X, 4);
        Assert.Equal(0.8f, tangent.Y, 4);

        // Distances are clamped.
        Assert.True(measure.GetPositionAndTangent(100, out position, out _));
        Assert.Equal(30, position.X, 4);
    }

    [Fact]
    public void CreateSegmentNew_extracts_part_of_the_contour()
    {
        using var line = Build(b =>
        {
            b.MoveTo(P(0, 0));
            b.LineTo(P(30, 40));
        });
        using var measure = ImpellerPathMeasure.New(line, false)!;

        using var segment = measure.CreateSegmentNew(10, 35, true)!;
        using var segmentMeasure = ImpellerPathMeasure.New(segment, false)!;
        Assert.Equal(25, segmentMeasure.GetLength(), 3);
        segment.GetTightBounds(out var bounds);
        Assert.Equal(6, bounds.X, 3);
        Assert.Equal(8, bounds.Y, 3);

        // Reversed segments are null.
        Assert.Null(measure.CreateSegmentNew(30, 20, true));
    }

    [Fact]
    public void Measures_circles()
    {
        using var circle = Build(b => b.AddOval(new ImpellerRect(0, 0, 100, 100)));
        using var measure = ImpellerPathMeasure.New(circle, false)!;
        // Curves are measured by approximation.
        Assert.Equal(100 * MathF.PI, measure.GetLength(), 1f);
    }

    [Fact]
    public void NextContour_walks_the_contours()
    {
        using var path = Build(b =>
        {
            b.MoveTo(P(0, 0));
            b.LineTo(P(10, 0));
            b.MoveTo(P(0, 10));
            b.LineTo(P(0, 30));
            b.LineTo(P(20, 30));
        });
        using var measure = ImpellerPathMeasure.New(path, false)!;
        Assert.Equal(10, measure.GetLength(), 4);
        Assert.True(measure.NextContour());
        Assert.Equal(40, measure.GetLength(), 4);
        Assert.False(measure.NextContour());
        Assert.Equal(0, measure.GetLength());
        Assert.False(measure.GetPositionAndTangent(0, out _, out _));

        using var closed = ImpellerPathMeasure.New(path, true)!;
        Assert.True(closed.NextContour());
        Assert.Equal(40 + MathF.Sqrt(800), closed.GetLength(), 3);
    }
}
