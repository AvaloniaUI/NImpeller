using NImpeller;
using Sandbox.Android.Interop;

namespace Sandbox.Android.Rendering;

/// <summary>
/// Vulkan through Impeller's own instance. Each attached window gets a VkSurfaceKHR, which the
/// Impeller swapchain takes ownership of and destroys with it.
/// </summary>
/// <remarks>
/// Impeller's C API creates the swapchain at 1x1, and on Android it does not resize it from the
/// surface extent (see TODO(163070) in impeller's khr_swapchain_vk.cc). Until impeller.h can set the
/// swapchain size, Vulkan frames on Android show a single stretched pixel.
/// </remarks>
internal sealed class VulkanBackend : IRenderBackend
{
    private readonly ImpellerContext _context;
    private readonly IntPtr _instance;
    private ImpellerVulkanSwapchain? _swapchain;

    public VulkanBackend()
    {
        _context = ImpellerContext.CreateVulkanNew(
                (Func<IntPtr, IntPtr, IntPtr>)((instance, name) => Vulkan.vkGetInstanceProcAddr(instance, name)),
                enableValidation: false)
            ?? throw new InvalidOperationException("Impeller could not create a Vulkan context.");
        _instance = _context.GetVulkanInfo()?.Vk_instance
            ?? throw new InvalidOperationException("The Impeller context is not a Vulkan context.");
    }

    public ImpellerContext Context => _context;

    public void Attach(IntPtr nativeWindow)
    {
        var surface = Vulkan.CreateAndroidSurface(_instance, nativeWindow);
        _swapchain = _context.VulkanSwapchainCreateNew((IntPtr)(long)surface)
            ?? throw new InvalidOperationException("Impeller could not create a Vulkan swapchain.");
    }

    public void Detach()
    {
        _swapchain?.Dispose();
        _swapchain = null;
    }

    public void Present(ImpellerDisplayList displayList, int width, int height)
    {
        // The swapchain tracks the window size itself and recreates its images when it changes.
        using var surface = _swapchain?.AcquireNextSurfaceNew();
        if (surface is null)
            return;
        surface.DrawDisplayList(displayList);
        surface.Present();
    }

    public void Dispose()
    {
        Detach();
        _context.Dispose();
    }
}
