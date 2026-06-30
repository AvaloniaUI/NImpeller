using NImpeller.Tests.Headless;
using Xunit;

namespace NImpeller.Tests;

/// <summary>
/// GL-texture interop: create a real GL texture, adopt it into an ImpellerTexture, draw it, and
/// read the result back. Mirrors Flutter's interop CanCreateOpenGLImage (handle round-trip) and
/// CanDrawImage. This is the texture path that works today — unlike TextureCreateWithContentsNew,
/// which is blocked by the unimplemented ImpellerMapping marshaller (see BindingGapTests).
/// </summary>
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
        Assert.SkipUnless(_gl.Available,
            $"No headless GL context (run under xvfb-run or set SDL_VIDEODRIVER=offscreen). Reason: {_gl.Error}");
        Assert.True(_gl.IsSoftwareRenderer,
            $"Expected a software renderer (llvmpipe) but got '{_gl.RendererName}'. Ensure test.runsettings is applied. See README.");

        var (image, createdHandle, reportedHandle) = _gl.RenderTextureInterop(320, 240);

        // GetOpenGLHandle must round-trip the adopted texture name.
        Assert.Equal(createdHandle, reportedHandle);
        Assert.True(image.HasContent(), "Texture draw produced an empty image.");

        GoldenAssert.Matches("textureinterop", image, PerChannelTolerance, MaxDiffFraction);
    }
}
