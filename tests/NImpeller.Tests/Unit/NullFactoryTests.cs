using NImpeller;
using Xunit;

namespace NImpeller.Tests.Unit;

/// <summary>
/// Nullable <c>*New</c> factories must surface a native <c>nullptr</c> as a null wrapper. CPU-only.
/// </summary>
public sealed class NullFactoryTests
{
    [Fact]
    public void FragmentProgram_New_returns_null_for_invalid_data()
    {
        // Not an impellerc-compiled runtime stage, so ImpellerFragmentProgramNew returns nullptr.
        using var mem = new ImpellerUnmanagedMemory(new byte[] { 1, 2, 3, 4 });
        Assert.Null(ImpellerFragmentProgram.New(mem));
    }
}
