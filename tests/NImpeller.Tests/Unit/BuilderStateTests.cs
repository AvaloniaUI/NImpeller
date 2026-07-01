using System.Numerics;
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
    public void GetTransform_starts_at_identity()
    {
        using var builder = ImpellerDisplayListBuilder.New(new ImpellerRect(0, 0, 100, 100))!;

        builder.GetTransform(out var transform);

        Assert.Equal(Matrix4x4.Identity, transform.Matrix);
    }

    [Fact]
    public void GetTransform_reflects_scale_and_translate()
    {
        using var builder = ImpellerDisplayListBuilder.New(new ImpellerRect(0, 0, 100, 100))!;

        builder.Scale(2, 2);
        builder.GetTransform(out var scaled);
        Assert.Equal(Matrix4x4.CreateScale(2, 2, 1), scaled.Matrix);

        using var builder2 = ImpellerDisplayListBuilder.New(new ImpellerRect(0, 0, 100, 100))!;
        builder2.Translate(30, 45);
        builder2.GetTransform(out var translated);
        Assert.Equal(Matrix4x4.CreateTranslation(30, 45, 0), translated.Matrix);
    }

    [Fact]
    public void GetTransform_follows_save_and_restore()
    {
        using var builder = ImpellerDisplayListBuilder.New(new ImpellerRect(0, 0, 100, 100))!;

        builder.Save();
        builder.Scale(3, 3);
        builder.GetTransform(out var inside);
        Assert.Equal(Matrix4x4.CreateScale(3, 3, 1), inside.Matrix);

        builder.Restore();
        builder.GetTransform(out var restored);
        Assert.Equal(Matrix4x4.Identity, restored.Matrix);
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
