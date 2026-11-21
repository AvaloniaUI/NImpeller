using System;
using System.Runtime.InteropServices;

namespace NImpeller;

/// <summary>
/// Keeps a callback alive for one native call and passes it as the callback's user data.
/// </summary>
readonly struct ImpellerCallbackScope : IDisposable
{
    private readonly GCHandle _handle;

    public ImpellerCallbackScope(Delegate callback) => _handle = GCHandle.Alloc(callback);

    public IntPtr UserData => GCHandle.ToIntPtr(_handle);

    public static T Get<T>(IntPtr userData) where T : Delegate => (T)GCHandle.FromIntPtr(userData).Target!;

    public void Dispose() => _handle.Free();
}
