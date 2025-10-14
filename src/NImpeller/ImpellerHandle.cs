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
    
    public static T RetainFromNative<T>(IntPtr ptr) where T : ImpellerHandle
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
