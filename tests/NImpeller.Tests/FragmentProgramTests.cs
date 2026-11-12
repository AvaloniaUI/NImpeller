using NImpeller.Tests.Headless;
using NImpeller.Tests.Scenes;
using Xunit;

namespace NImpeller.Tests;

/// <summary>
/// Fragment-program color sources and image filters against a real context. Rendering is covered
/// by the <c>fragmentprogram</c> golden scene.
/// </summary>
[Collection(ImpellerGLCollection.Name)]
public sealed class FragmentProgramTests
{
    private readonly ImpellerGLFixture _gl;

    public FragmentProgramTests(ImpellerGLFixture gl) => _gl = gl;

    [Fact]
    public void Program_loads_from_impellerc_output()
    {
        using var program = FragmentProgramScene.LoadProgram("tinted_texture");
        Assert.NotNull(program);
    }

    [Fact]
    public void ColorSource_is_created_for_the_context_backend()
    {
        RenderGate.Require(_gl);
        using var program = FragmentProgramScene.LoadProgram("tinted_texture");
        using var texture = FragmentProgramScene.CreateQuadTexture(_gl.Context);
        using var source = ImpellerColorSource.CreateFragmentProgramNew(
            _gl.Context, program, new[] { texture }, new float[] { 1, 1, 1, 1, 1, 1 });
        Assert.NotNull(source);
    }

    [Fact]
    public void ColorSource_rejects_null_sampler()
    {
        RenderGate.Require(_gl);
        using var program = FragmentProgramScene.LoadProgram("tinted_texture");
        Assert.Throws<ArgumentNullException>(() => ImpellerColorSource.CreateFragmentProgramNew(
            _gl.Context, program, new ImpellerTexture[] { null! }, new float[6]));
    }

    [Fact]
    public void ImageFilter_requires_input_sampler_and_size_uniform()
    {
        RenderGate.Require(_gl);
        using var program = FragmentProgramScene.LoadProgram("invert_filter");
        using var texture = FragmentProgramScene.CreateQuadTexture(_gl.Context);

        Assert.Throws<ArgumentException>(() => ImpellerImageFilter.CreateFragmentProgramNew(
            _gl.Context, program, System.ReadOnlySpan<ImpellerTexture>.Empty, new float[2]));
        Assert.Throws<ArgumentException>(() => ImpellerImageFilter.CreateFragmentProgramNew(
            _gl.Context, program, new[] { texture }, new float[1]));

        using var filter = ImpellerImageFilter.CreateFragmentProgramNew(
            _gl.Context, program, new[] { texture }, new float[2]);
        Assert.NotNull(filter);
    }
}
