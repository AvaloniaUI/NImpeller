using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NImpeller;

/// <summary>
/// On iOS, tvOS and Mac Catalyst the native library is <c>impeller.framework</c>, which P/Invoke does not probe for
/// <c>"impeller"</c>: this loads it from <c>@rpath</c>. Other platforms use the default probing.
/// </summary>
static class ImpellerNativeLibrary
{
    const string LibraryName = "impeller";
    const string FrameworkPath = "@rpath/impeller.framework/impeller";

    static IntPtr s_handle;

#pragma warning disable CA2255 // The resolver must be in place before the first P/Invoke of this assembly.
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Register()
    {
        // IsIOS is also true on Mac Catalyst.
        if (OperatingSystem.IsIOS() || OperatingSystem.IsTvOS())
            NativeLibrary.SetDllImportResolver(typeof(ImpellerNativeLibrary).Assembly, Resolve);
    }

    static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (name != LibraryName)
            return IntPtr.Zero;
        if (s_handle == IntPtr.Zero && NativeLibrary.TryLoad(FrameworkPath, out var handle))
            s_handle = handle;
        return s_handle;
    }
}
