using System;
using NImpeller;
using Xunit;

namespace NImpeller.Tests.Unit;

/// <summary>
/// Handle/dispose semantics of the wrapper objects. CPU-only. Conceptually mirrors Flutter's
/// object_unittests.cc (refcount/lifetime), adapted to the .NET SafeHandle/IDisposable model
/// this binding uses.
/// </summary>
public sealed class LifetimeTests
{
    [Fact]
    public void Dispose_is_idempotent()
    {
        var paint = ImpellerPaint.New()!;
        paint.Dispose();
        paint.Dispose(); // must not throw
    }

    [Fact]
    public void Builder_dispose_is_idempotent()
    {
        var builder = ImpellerDisplayListBuilder.New(new ImpellerRect(0, 0, 1, 1))!;
        builder.Dispose();
        builder.Dispose();
    }

    [Fact]
    public void ParagraphBuilder_AddText_rejects_null()
    {
        using var typography = ImpellerTypographyContext.New()!;
        using var builder = typography.ParagraphBuilderNew()!;
        Assert.Throws<ArgumentNullException>(() => builder.AddText(null!));
    }

    [Fact]
    public void ParagraphBuilder_AddText_after_dispose_throws_ObjectDisposed()
    {
        using var typography = ImpellerTypographyContext.New()!;
        var builder = typography.ParagraphBuilderNew()!;
        builder.Dispose();
        Assert.Throws<ObjectDisposedException>(() => builder.AddText("hello"));
    }
}
