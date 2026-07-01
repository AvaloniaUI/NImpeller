using NImpeller.Tests.Headless;
using Xunit;

namespace NImpeller.Tests;

/// <summary>
/// Decides whether a rendering test should run, skip, or fail when the headless GL context isn't
/// usable.
/// </summary>
internal static class RenderGate
{
    // GitHub Actions and most CI providers set CI=true.
    public static bool IsCI =>
        string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

    public static void Require(ImpellerGLFixture gl)
    {
        string reason = $"No headless GL context. Reason: {gl.Error}";
        if (IsCI)
        {
            Assert.True(gl.Available, reason);
        }
        else
        {
            Assert.SkipUnless(gl.Available, reason);
        }

        Assert.True(gl.IsSoftwareRenderer,
            $"Expected a software renderer (llvmpipe) but got '{gl.RendererName}'. " +
            "Ensure test.runsettings is applied (it is by default via dotnet test). See README.");
    }
}
