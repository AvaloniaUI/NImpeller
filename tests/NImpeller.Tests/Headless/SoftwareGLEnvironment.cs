using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NImpeller.Tests.Headless;

/// <summary>
/// Forces the headless GL stack onto Mesa's software rasterizer (llvmpipe) through EGL before
/// anything loads SDL, EGL or Mesa. Golden images are only stable on llvmpipe.
/// </summary>
/// <remarks>
/// On Unix, <see cref="Environment.SetEnvironmentVariable(string, string?)"/> only updates .NET's
/// managed copy of the environment; native libraries read <c>environ</c> through <c>getenv</c> and
/// never see it. So we call libc <c>setenv</c> as well. This runs as a module initializer so it
/// applies however the tests are launched (dotnet test, the test executable, or an IDE).
/// </remarks>
internal static partial class SoftwareGLEnvironment
{
    private static readonly (string Name, string Value)[] Variables =
    [
        ("__EGL_VENDOR_LIBRARY_FILENAMES", "/usr/share/glvnd/egl_vendor.d/50_mesa.json"),
        ("LIBGL_ALWAYS_SOFTWARE", "1"),
        ("GALLIUM_DRIVER", "llvmpipe"),
        ("MESA_LOADER_DRIVER_OVERRIDE", "llvmpipe"),
        ("SDL_VIDEO_X11_FORCE_EGL", "1"),
    ];

    [ModuleInitializer]
    internal static void Apply()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        foreach (var (name, value) in Variables)
        {
            if (setenv(name, value, overwrite: 1) != 0)
            {
                throw new InvalidOperationException(
                    $"setenv({name}) failed with errno {Marshal.GetLastPInvokeError()}");
            }

            Environment.SetEnvironmentVariable(name, value);
        }
    }

    [LibraryImport("libc", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int setenv(string name, string value, int overwrite);
}
