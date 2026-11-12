using System.Runtime.InteropServices;

namespace Sandbox.Android.Interop;

/// <summary>ANativeWindow functions from libandroid.</summary>
internal static partial class NativeWindow
{
    private const string Lib = "android";

    /// <summary>Returns an ANativeWindow with one reference held for the caller.</summary>
    [LibraryImport(Lib)] public static partial IntPtr ANativeWindow_fromSurface(IntPtr jniEnv, IntPtr surface);
    [LibraryImport(Lib)] public static partial void ANativeWindow_release(IntPtr window);
    [LibraryImport(Lib)] public static partial int ANativeWindow_getWidth(IntPtr window);
    [LibraryImport(Lib)] public static partial int ANativeWindow_getHeight(IntPtr window);
}
