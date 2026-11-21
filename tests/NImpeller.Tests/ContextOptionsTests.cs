using NImpeller.Tests.Headless;
using NImpeller.Tests.Scenes;
using Xunit;

namespace NImpeller.Tests;

[Collection(ImpellerGLCollection.Name)]
public sealed class ContextOptionsTests
{
    private readonly ImpellerGLFixture _gl;

    public ContextOptionsTests(ImpellerGLFixture gl) => _gl = gl;

    [Fact]
    public void Sdfs_change_how_shapes_are_drawn()
    {
        RenderGate.Require(_gl);

        var scene = new PrimitivesScene();
        var plain = _gl.Render(scene, 360, 280);
        var sdfs = _gl.RenderWithSdfs(scene, 360, 280);

        Assert.NotEqual(plain.Pixels, sdfs.Pixels);
    }
}
