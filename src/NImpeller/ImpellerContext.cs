using System;
// ReSharper disable InconsistentNaming

namespace NImpeller;

public unsafe partial class ImpellerContext
{
    public ImpellerContextVulkanInfo? GetVulkanInfo()
    {
        ImpellerContextVulkanInfo info;
        if (UnsafeNativeMethods.ImpellerContextGetVulkanInfo(Handle, &info) == 0)
            return null;
        return info;
    }
}
