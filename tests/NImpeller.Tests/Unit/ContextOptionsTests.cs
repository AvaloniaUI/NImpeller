using Xunit;

namespace NImpeller.Tests.Unit;

public sealed class ContextOptionsTests
{
    [Fact]
    public void Options_can_be_set_and_released()
    {
        using var options = ImpellerContextOptions.New();
        Assert.NotNull(options);
        options.SetUseSDFs(true);
        options.SetTopLeftDefaultFramebufferOrigin(true);
    }
}
