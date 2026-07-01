using System.Numerics;
using NImpeller;
using Xunit;

namespace NImpeller.Tests.Unit;

public sealed class StructHelperTests
{
    [Fact]
    public void FromRgb_sets_opaque_normalized_channels()
    {
        var c = ImpellerColor.FromRgb(255, 0, 0);
        Assert.Equal(1.0f, c.Red);
        Assert.Equal(0.0f, c.Green);
        Assert.Equal(0.0f, c.Blue);
        Assert.Equal(1.0f, c.Alpha); // FromRgb implies fully opaque
    }

    [Fact]
    public void FromArgb_normalizes_all_four_channels()
    {
        var c = ImpellerColor.FromArgb(128, 10, 20, 30);
        Assert.Equal(128 / 255.0f, c.Alpha);
        Assert.Equal(10 / 255.0f, c.Red);
        Assert.Equal(20 / 255.0f, c.Green);
        Assert.Equal(30 / 255.0f, c.Blue);
    }

    [Fact]
    public void Rect_int_ctor_assigns_fields()
    {
        var r = new ImpellerRect(1, 2, 3, 4);
        Assert.Equal(1f, r.X);
        Assert.Equal(2f, r.Y);
        Assert.Equal(3f, r.Width);
        Assert.Equal(4f, r.Height);
    }

    [Fact]
    public void ISize_int_ctor_assigns_fields()
    {
        var s = new ImpellerISize(640, 480);
        Assert.Equal(640L, s.Width);
        Assert.Equal(480L, s.Height);
    }

    [Fact]
    public void Matrix_implicitly_converts_from_Matrix4x4()
    {
        var m = Matrix4x4.CreateScale(2, 3, 1);
        ImpellerMatrix im = m; // implicit operator
        Assert.Equal(m, im.Matrix);
    }

    [Fact]
    public void TextDecoration_factories_set_type_and_solid_style()
    {
        var color = ImpellerColor.FromRgb(0, 0, 255);

        var underline = ImpellerTextDecoration.Underline(color, 2.0f);
        Assert.Equal((int)ImpellerTextDecorationType.kImpellerTextDecorationTypeUnderline, underline.Types);
        Assert.Equal(ImpellerTextDecorationStyle.kImpellerTextDecorationStyleSolid, underline.Style);
        Assert.Equal(2.0f, underline.Thickness_multiplier);

        var lineThrough = ImpellerTextDecoration.LineThrough(color);
        Assert.Equal((int)ImpellerTextDecorationType.kImpellerTextDecorationTypeLineThrough, lineThrough.Types);
        Assert.Equal(1.0f, lineThrough.Thickness_multiplier); // default
    }
}
