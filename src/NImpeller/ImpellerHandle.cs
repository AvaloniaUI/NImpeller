using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace NImpeller;

abstract class ImpellerHandle : SafeHandle
{
    protected ImpellerHandle() : base(IntPtr.Zero, true)
    {
    }

    public override bool IsInvalid => handle == IntPtr.Zero;
    private protected abstract void UnsafeRetain();
    private protected abstract void UnsafeRelease();

    protected override bool ReleaseHandle()
    {
        UnsafeRelease();
        return true;
    }

    /// <summary>
    /// Wraps a caller-owned handle returned by a nullable <c>*New</c> function, or returns
    /// <c>null</c> (and disposes the handle) when the native function returned <c>nullptr</c>.
    /// </summary>
    public static TWrapper? WrapOrNull<THandle, TWrapper>(THandle handle, Func<THandle, TWrapper> wrap)
        where THandle : ImpellerHandle
        where TWrapper : class
    {
        if (handle.IsInvalid)
        {
            handle.Dispose();
            return null;
        }

        return wrap(handle);
    }

    public static T RetainFromNative<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] T>(
        IntPtr ptr) where T : ImpellerHandle
    {
        if (ptr == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(ptr));
        }

        var handle = (T)Activator.CreateInstance(typeof(T), true)!;
        handle.SetHandle(ptr);
        handle.UnsafeRetain();
        return handle;
    }
}