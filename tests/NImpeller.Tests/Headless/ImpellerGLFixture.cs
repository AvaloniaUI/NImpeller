using NImpeller.Tests.Scenes;
using Xunit;

namespace NImpeller.Tests.Headless;

public sealed class ImpellerGLFixture : IDisposable
{
    private readonly HeadlessImpellerContext _context = new();

    public bool Available => _context.Available;

    public string? Error => _context.Error;

    public string? RendererName => _context.RendererName;

    public bool IsSoftwareRenderer => _context.IsSoftwareRenderer;

    public RawImage Render(IScene scene, int width, int height) =>
        new(width, height, _context.Render(scene, width, height));

    public (RawImage Image, ulong CreatedHandle, ulong ReportedHandle) RenderTextureInterop(int width, int height)
    {
        var result = _context.RenderTextureInterop(width, height);
        return (new RawImage(width, height, result.Pixels), result.CreatedHandle, result.ReportedHandle);
    }

    public void Dispose() => _context.Dispose();
}

internal readonly record struct TextureInteropResult(byte[] Pixels, ulong CreatedHandle, ulong ReportedHandle);

[CollectionDefinition(Name)]
public sealed class ImpellerGLCollection : ICollectionFixture<ImpellerGLFixture>
{
    public const string Name = "Impeller GL";
}
