using NImpeller.Tests.Headless;
using Xunit;

namespace NImpeller.Tests;

[Collection(ImpellerGLCollection.Name)]
public sealed class TextureInteropTests
{
    private const int PerChannelTolerance = 8;
    private const double MaxDiffFraction = 0.0001;

    private readonly ImpellerGLFixture _gl;

    public TextureInteropTests(ImpellerGLFixture gl) => _gl = gl;

    [Fact]
    public void GL_texture_round_trips_and_draws()
    {
        RenderGate.Require(_gl);

        var (image, createdHandle, reportedHandle) = _gl.RenderTextureInterop(320, 240);

        // GetOpenGLHandle must round-trip the adopted texture name.
        Assert.Equal(createdHandle, reportedHandle);
        Assert.True(image.HasContent(), "Texture draw produced an empty image.");

        GoldenAssert.Matches("textureinterop", image, PerChannelTolerance, MaxDiffFraction);
    }
}
