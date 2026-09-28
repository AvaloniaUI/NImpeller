using System.Numerics;
using NImpeller;
using Xunit;

namespace NImpeller.Tests.Unit;

public sealed class PathOpsTests
{
    private static ImpellerPath Rect(float x, float y, float w, float h,
        ImpellerFillType fill = ImpellerFillType.kImpellerFillTypeNonZero)
    {
        using var builder = ImpellerPathBuilder.New()!;
        builder.AddRect(new ImpellerRect { X = x, Y = y, Width = w, Height = h });
        return builder.TakePathNew(fill)!;
    }

    private static ImpellerPath Line(float x0, float y0, float x1, float y1)
    {
        using var builder = ImpellerPathBuilder.New()!;
        builder.MoveTo(new ImpellerPoint { X = x0, Y = y0 });
        builder.LineTo(new ImpellerPoint { X = x1, Y = y1 });
        return builder.TakePathNew(ImpellerFillType.kImpellerFillTypeNonZero)!;
    }

    private static bool Contains(ImpellerPath path, float x, float y) =>
        path.ContainsPoint(new ImpellerPoint { X = x, Y = y });

    private static ImpellerRect TightBounds(ImpellerPath path)
    {
        path.GetTightBounds(out var bounds);
        return bounds;
    }

    [Fact]
    public void GetTightBounds_excludes_control_points()
    {
        using var builder = ImpellerPathBuilder.New()!;
        builder.MoveTo(new ImpellerPoint { X = 0, Y = 0 });
        builder.QuadraticCurveTo(new ImpellerPoint { X = 50, Y = 100 }, new ImpellerPoint { X = 100, Y = 0 });
        using var path = builder.TakePathNew(ImpellerFillType.kImpellerFillTypeNonZero)!;

        path.GetBounds(out var bounds);
        Assert.Equal(100, bounds.Height);
        Assert.Equal(50, TightBounds(path).Height, 3);
    }

    [Fact]
    public void ContainsPoint_honours_the_fill_type()
    {
        using var builder = ImpellerPathBuilder.New()!;
        builder.AddRect(new ImpellerRect(0, 0, 100, 100));
        builder.AddRect(new ImpellerRect(25, 25, 50, 50));
        using var odd = builder.CopyPathNew(ImpellerFillType.kImpellerFillTypeOdd)!;
        using var nonZero = odd.CreateWithFillTypeNew(ImpellerFillType.kImpellerFillTypeNonZero)!;

        Assert.Equal(ImpellerFillType.kImpellerFillTypeOdd, odd.GetFillType());
        Assert.Equal(ImpellerFillType.kImpellerFillTypeNonZero, nonZero.GetFillType());
        Assert.True(Contains(odd, 10, 10));
        Assert.False(Contains(odd, 50, 50));
        Assert.True(Contains(nonZero, 50, 50));
        Assert.False(Contains(nonZero, 150, 50));
    }

    [Fact]
    public void IsEmpty_reports_paths_without_segments()
    {
        using var builder = ImpellerPathBuilder.New()!;
        using var empty = builder.TakePathNew(ImpellerFillType.kImpellerFillTypeNonZero)!;
        using var rect = Rect(0, 0, 1, 1);
        Assert.True(empty.IsEmpty());
        Assert.False(rect.IsEmpty());
    }

    [Fact]
    public void CreateTransformedNew_maps_every_point()
    {
        using var rect = Rect(0, 0, 10, 10);
        ImpellerMatrix m = Matrix4x4.CreateScale(2, 3, 1) * Matrix4x4.CreateTranslation(5, 7, 0);
        using var transformed = rect.CreateTransformedNew(m)!;
        Assert.Equal(new ImpellerRect { X = 5, Y = 7, Width = 20, Height = 30 }, TightBounds(transformed));
    }

    [Theory]
    [InlineData(ImpellerPathOp.kImpellerPathOpIntersect, false, true, false)]
    [InlineData(ImpellerPathOp.kImpellerPathOpUnion, true, true, true)]
    [InlineData(ImpellerPathOp.kImpellerPathOpDifference, true, false, false)]
    [InlineData(ImpellerPathOp.kImpellerPathOpReverseDifference, false, false, true)]
    [InlineData(ImpellerPathOp.kImpellerPathOpXor, true, false, true)]
    public void CreateOpNew_combines_filled_areas(ImpellerPathOp op, bool onlyA, bool both, bool onlyB)
    {
        using var a = Rect(0, 0, 100, 100);
        using var b = Rect(50, 50, 100, 100);
        using var result = a.CreateOpNew(b, op);
        Assert.NotNull(result);
        Assert.Equal(onlyA, Contains(result, 25, 25));
        Assert.Equal(both, Contains(result, 75, 75));
        Assert.Equal(onlyB, Contains(result, 125, 125));
    }

    [Fact]
    public void CreateOpNew_of_disjoint_paths_is_empty()
    {
        using var a = Rect(0, 0, 10, 10);
        using var b = Rect(20, 20, 10, 10);
        using var result = a.CreateOpNew(b, ImpellerPathOp.kImpellerPathOpIntersect);
        Assert.NotNull(result);
        Assert.True(result.IsEmpty());
    }

    [Fact]
    public void CreateStrokedNew_outlines_the_stroke()
    {
        using var line = Line(10, 50, 110, 50);
        var stroke = new ImpellerStrokeParameters
        {
            Width = 10,
            Cap = ImpellerStrokeCap.kImpellerStrokeCapButt,
            Join = ImpellerStrokeJoin.kImpellerStrokeJoinMiter,
            Miter_limit = 4,
        };
        using var outline = line.CreateStrokedNew(stroke, 1)!;
        Assert.Equal(new ImpellerRect { X = 10, Y = 45, Width = 100, Height = 10 }, TightBounds(outline));
        Assert.True(Contains(outline, 50, 52));
        Assert.False(Contains(outline, 50, 57));

        stroke.Cap = ImpellerStrokeCap.kImpellerStrokeCapSquare;
        using var square = line.CreateStrokedNew(stroke, 1)!;
        Assert.Equal(110, TightBounds(square).Width);
    }

    [Fact]
    public void CreateStrokedNew_returns_null_for_hairlines()
    {
        using var line = Line(0, 0, 10, 0);
        Assert.Null(line.CreateStrokedNew(new ImpellerStrokeParameters { Width = 0, Miter_limit = 4 }, 1));
    }

    [Fact]
    public void CreateDashedNew_keeps_the_on_intervals()
    {
        using var line = Line(0, 0, 100, 0);
        using var dashed = line.CreateDashedNew([10f, 10f], 0)!;
        Assert.Equal(90, TightBounds(dashed).Width, 3);

        var stroke = new ImpellerStrokeParameters { Width = 2, Miter_limit = 4 };
        using var outline = dashed.CreateStrokedNew(stroke, 1)!;
        Assert.True(Contains(outline, 5, 0));
        Assert.False(Contains(outline, 15, 0));
        Assert.True(Contains(outline, 25, 0));

        using var shifted = line.CreateDashedNew([10f, 10f], 5)!;
        using var shiftedOutline = shifted.CreateStrokedNew(stroke, 1)!;
        Assert.True(Contains(shiftedOutline, 2, 0));
        Assert.False(Contains(shiftedOutline, 7, 0));
    }

    [Fact]
    public void CreateDashedNew_rejects_odd_interval_counts()
    {
        using var line = Line(0, 0, 100, 0);
        Assert.Null(line.CreateDashedNew([10f, 10f, 10f], 0));
    }

    [Fact]
    public void AddPath_appends_contours_with_an_optional_transform()
    {
        using var rect = Rect(0, 0, 10, 10);
        using var builder = ImpellerPathBuilder.New()!;
        builder.AddPath(rect);
        builder.AddPath(rect, Matrix4x4.CreateTranslation(20, 0, 0));
        using var both = builder.TakePathNew(ImpellerFillType.kImpellerFillTypeNonZero)!;

        Assert.Equal(30, TightBounds(both).Width);
        Assert.True(Contains(both, 5, 5));
        Assert.False(Contains(both, 15, 5));
        Assert.True(Contains(both, 25, 5));
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 50)]
    public void SvgArcTo_sweeps_in_the_requested_direction(bool clockwise, float expectedTop)
    {
        using var builder = ImpellerPathBuilder.New()!;
        builder.MoveTo(new ImpellerPoint { X = 0, Y = 50 });
        builder.SvgArcTo(new ImpellerSize { Width = 50, Height = 50 }, 0, false, clockwise,
            new ImpellerPoint { X = 100, Y = 50 });
        using var arc = builder.TakePathNew(ImpellerFillType.kImpellerFillTypeNonZero)!;

        var bounds = TightBounds(arc);
        Assert.Equal(expectedTop, bounds.Y, 2);
        Assert.Equal(100, bounds.Width, 2);
        Assert.Equal(50, bounds.Height, 2);
    }
}
