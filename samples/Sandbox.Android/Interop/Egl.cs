using System.Runtime.InteropServices;

namespace Sandbox.Android.Interop;

/// <summary>The slice of libEGL the GLES backend needs.</summary>
internal static unsafe partial class Egl
{
    private const string Lib = "EGL";

    public const int EGL_NONE = 0x3038;
    public const int EGL_RED_SIZE = 0x3024;
    public const int EGL_GREEN_SIZE = 0x3023;
    public const int EGL_BLUE_SIZE = 0x3022;
    public const int EGL_ALPHA_SIZE = 0x3021;
    public const int EGL_DEPTH_SIZE = 0x3025;
    public const int EGL_STENCIL_SIZE = 0x3026;
    public const int EGL_SURFACE_TYPE = 0x3033;
    public const int EGL_RENDERABLE_TYPE = 0x3040;
    public const int EGL_WINDOW_BIT = 0x0004;
    public const int EGL_OPENGL_ES3_BIT = 0x0040;
    public const int EGL_CONTEXT_CLIENT_VERSION = 0x3098;

    [LibraryImport(Lib)] public static partial IntPtr eglGetDisplay(IntPtr displayId);
    [LibraryImport(Lib)] public static partial uint eglInitialize(IntPtr display, int* major, int* minor);
    [LibraryImport(Lib)] public static partial uint eglTerminate(IntPtr display);
    [LibraryImport(Lib)] public static partial uint eglChooseConfig(IntPtr display, int* attribs, IntPtr* configs, int configSize, int* numConfig);
    [LibraryImport(Lib)] public static partial IntPtr eglCreateContext(IntPtr display, IntPtr config, IntPtr shareContext, int* attribs);
    [LibraryImport(Lib)] public static partial uint eglDestroyContext(IntPtr display, IntPtr context);
    [LibraryImport(Lib)] public static partial IntPtr eglCreateWindowSurface(IntPtr display, IntPtr config, IntPtr window, int* attribs);
    [LibraryImport(Lib)] public static partial uint eglDestroySurface(IntPtr display, IntPtr surface);
    [LibraryImport(Lib)] public static partial uint eglMakeCurrent(IntPtr display, IntPtr draw, IntPtr read, IntPtr context);
    [LibraryImport(Lib)] public static partial uint eglSwapBuffers(IntPtr display, IntPtr surface);
    [LibraryImport(Lib)] public static partial int eglGetError();
    [LibraryImport(Lib)] public static partial IntPtr eglGetProcAddress(IntPtr procName);

    public static InvalidOperationException Failure(string call) =>
        new($"{call} failed (EGL error 0x{eglGetError():X4}).");
}
