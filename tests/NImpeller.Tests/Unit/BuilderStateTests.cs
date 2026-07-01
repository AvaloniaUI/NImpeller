using NImpeller;
using Xunit;

namespace NImpeller.Tests.Unit;

public sealed class BuilderStateTests
{
    [Fact]
    public void New_returns_a_builder()
    {
        using var builder = ImpellerDisplayListBuilder.New(new ImpellerRect(0, 0, 100, 100));
        Assert.NotNull(builder);
    }

    [Fact]
    public void Save_and_restore_adjust_the_save_count()
    {
        using var builder = ImpellerDisplayListBuilder.New(new ImpellerRect(0, 0, 100, 100))!;

        Assert.Equal(1u, builder.GetSaveCount()); // starts at 1

        builder.Save();
        Assert.Equal(2u, builder.GetSaveCount());

        builder.Save();
        Assert.Equal(3u, builder.GetSaveCount());

        builder.Restore();
        Assert.Equal(2u, builder.GetSaveCount());

        builder.RestoreToCount(1);
        Assert.Equal(1u, builder.GetSaveCount());
    }

    [Fact]
    public void CreateDisplayListNew_produces_a_display_list()
    {
        using var builder = ImpellerDisplayListBuilder.New(new ImpellerRect(0, 0, 100, 100))!;
        using var paint = ImpellerPaint.New()!;
        paint.SetColor(ImpellerColor.FromRgb(255, 255, 255));
        builder.DrawRect(new ImpellerRect(0, 0, 10, 10), paint);

        using var displayList = builder.CreateDisplayListNew();
        Assert.NotNull(displayList);
    }
}
