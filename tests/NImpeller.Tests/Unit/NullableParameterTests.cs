using NImpeller;
using Xunit;

namespace NImpeller.Tests.Unit;

/// <summary>
/// Parameters the header marks <c>IMPELLER_NULLABLE</c> accept <c>null</c>. CPU-only: recording a
/// display list needs no GPU context.
/// </summary>
public sealed class NullableParameterTests
{
    [Fact]
    public void DisplayListBuilder_New_accepts_null_cull_rect()
    {
        using var builder = ImpellerDisplayListBuilder.New(null);
        Assert.NotNull(builder);
    }

    [Fact]
    public void SaveLayer_accepts_null_paint_and_backdrop()
    {
        using var builder = ImpellerDisplayListBuilder.New(new ImpellerRect(0, 0, 10, 10))!;
        builder.SaveLayer(new ImpellerRect(0, 0, 10, 10), null, null);
        Assert.Equal(2u, builder.GetSaveCount());
        builder.Restore();
        using var displayList = builder.CreateDisplayListNew();
        Assert.NotNull(displayList);
    }

    [Fact]
    public void ParagraphStyle_SetEllipsis_accepts_null()
    {
        using var style = ImpellerParagraphStyle.New()!;
        style.SetEllipsis("…");
        style.SetEllipsis(null);
    }
}
