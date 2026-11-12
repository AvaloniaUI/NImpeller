using System.Runtime.InteropServices;

namespace Sandbox.Android.Interop;

/// <summary>The slice of libvulkan the Vulkan backend needs; everything else goes through Impeller.</summary>
internal static unsafe partial class Vulkan
{
    private const int VK_STRUCTURE_TYPE_ANDROID_SURFACE_CREATE_INFO_KHR = 1000008000;

    [LibraryImport("vulkan")]
    public static partial IntPtr vkGetInstanceProcAddr(IntPtr instance, IntPtr name);

    [StructLayout(LayoutKind.Sequential)]
    private struct VkAndroidSurfaceCreateInfoKHR
    {
        public int SType;
        public IntPtr PNext;
        public uint Flags;
        public IntPtr Window;
    }

    /// <summary>Creates a VkSurfaceKHR for <paramref name="nativeWindow"/> on <paramref name="instance"/>.</summary>
    public static ulong CreateAndroidSurface(IntPtr instance, IntPtr nativeWindow)
    {
        var name = Marshal.StringToHGlobalAnsi("vkCreateAndroidSurfaceKHR");
        IntPtr proc;
        try
        {
            proc = vkGetInstanceProcAddr(instance, name);
        }
        finally
        {
            Marshal.FreeHGlobal(name);
        }

        if (proc == IntPtr.Zero)
            throw new InvalidOperationException("vkCreateAndroidSurfaceKHR is not available on this Vulkan instance.");

        var info = new VkAndroidSurfaceCreateInfoKHR
        {
            SType = VK_STRUCTURE_TYPE_ANDROID_SURFACE_CREATE_INFO_KHR,
            Window = nativeWindow,
        };
        ulong surface;
        var create = (delegate* unmanaged<IntPtr, VkAndroidSurfaceCreateInfoKHR*, IntPtr, ulong*, int>)proc;
        var result = create(instance, &info, IntPtr.Zero, &surface);
        if (result != 0)
            throw new InvalidOperationException($"vkCreateAndroidSurfaceKHR failed (VkResult {result}).");
        return surface;
    }
}
