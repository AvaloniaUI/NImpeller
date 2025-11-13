using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
// ReSharper disable InconsistentNaming

namespace NImpeller;

public unsafe partial class ImpellerContext
{

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static IntPtr GetProcAddressCallback(IntPtr proc, IntPtr userData)
    {
        return ((Func<IntPtr, IntPtr>)GCHandle.FromIntPtr(userData).Target!)(proc!);
    }
    
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static IntPtr GetVulkanProcAddressCallback(IntPtr vkInstance, IntPtr proc, IntPtr userData)
    {
        return ((Func<IntPtr, IntPtr, IntPtr>)GCHandle.FromIntPtr(userData).Target!)(vkInstance, proc!);
    }



    public static ImpellerContext? CreateVulkanNew(Func<IntPtr, string, IntPtr> getProcAddress, bool enableValidation)
        => CreateVulkanNew((IntPtr vkInstance, IntPtr proc) =>
            getProcAddress(vkInstance, Marshal.PtrToStringAnsi(proc)!), enableValidation);
    
    public static ImpellerContext? CreateVulkanNew(Func<IntPtr, IntPtr, IntPtr> getProcAddress, bool enableValidation)
    {
        var handle = GCHandle.Alloc(getProcAddress);
        var settings = new ImpellerContextVulkanSettings
        {
            User_data = GCHandle.ToIntPtr(handle),
            Enable_vulkan_validation = enableValidation ? 1 : 0,
            Proc_address_callback = (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr>)&GetVulkanProcAddressCallback,
        };
        var res = UnsafeNativeMethods.ImpellerContextCreateVulkanNew(UnsafeNativeMethods.ImpellerVersion, &settings);
        handle.Free();
        return res != null! ? new ImpellerContext(res) : null;
    }

    public static ImpellerContext? CreateOpenGLESNew(Func<string, IntPtr> getProcAddress)
        => CreateOpenGLESNew((IntPtr name) => getProcAddress(Marshal.PtrToStringAnsi(name)!));
    
    public static ImpellerContext? CreateOpenGLESNew(Func<IntPtr, IntPtr> getProcAddress)
    {
        var handle = GCHandle.Alloc(getProcAddress);
        var res = UnsafeNativeMethods.ImpellerContextCreateOpenGLESNew(UnsafeNativeMethods.ImpellerVersion,
            (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)&GetProcAddressCallback,
            GCHandle.ToIntPtr(handle));
        handle.Free();
        return res != null! ? new ImpellerContext(res) : null;
    }

    public static ImpellerContext? CreateMetalNew()
    {
        var res = UnsafeNativeMethods.ImpellerContextCreateMetalNew(UnsafeNativeMethods.ImpellerVersion);
        return res != null! ? new ImpellerContext(res) : null;
    }

    public ImpellerContextVulkanInfo? GetVulkanInfo()
    {
        ImpellerContextVulkanInfo info;
        if (UnsafeNativeMethods.ImpellerContextGetVulkanInfo(Handle, &info) == 0)
            return null;
        return info;
    }
}