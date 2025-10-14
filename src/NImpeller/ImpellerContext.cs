using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
// ReSharper disable InconsistentNaming

namespace NImpeller;

public unsafe partial class ImpellerContext
{

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static IntPtr GetProcAddressCallback(IntPtr proc, IntPtr userData)
    {
        var name = Marshal.PtrToStringAnsi(proc);
        return ((Func<string, IntPtr>)GCHandle.FromIntPtr(userData).Target!)(name!);
    }
    
    public static ImpellerContext? CreateOpenGLESNew(Func<string, IntPtr> getProcAddress)
    {
        var handle = GCHandle.Alloc(getProcAddress);
        var res = UnsafeNativeMethods.ImpellerContextCreateOpenGLESNew(UnsafeNativeMethods.ImpellerVersion,
            (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)&GetProcAddressCallback,
            GCHandle.ToIntPtr(handle));
        handle.Free();
        return res != null! ? new ImpellerContext(res) : null;
    }
}