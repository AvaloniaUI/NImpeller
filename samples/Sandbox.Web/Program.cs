using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NImpeller;
using Sandbox;
using Sandbox.Scenes;

namespace Sandbox.Web;

// Entry point does nothing: main.js drives the app through the JSExport'ed WebApp methods.
internal static class Program
{
    private static void Main() { }
}

[SupportedOSPlatform("browser")]
public static partial class WebApp
{
    private static readonly IScene[] Scenes =
    [
        new CirclingSquares(),
        new AnimatedGradientsScene(),
        new MMarkScene(),
        new ParagraphScene(),
    ];

    // txt falls back to this family when a text style doesn't name one (see txt/platform.cc), so
    // registering the bundled font under it makes font-agnostic scenes render.
    private const string DefaultFontFamilyAlias = "Arial";

    private static ImpellerContext? _context;
    private static ImpellerTypographyContext? _typography;
    private static ImpellerSurface? _surface;
    private static ImpellerISize _surfaceSize;
    private static IScene _scene = Scenes[0];
    private static int _width;
    private static int _height;

    private static readonly Stopwatch FpsClock = new();
    private static int _framesSinceReport;

    [JSImport("setStatus", "main.js")]
    private static partial void SetStatus(string text);

    [JSExport]
    public static bool Initialize(string canvasSelector, int width, int height, string sceneName)
    {
        _width = width;
        _height = height;
        _scene = Scenes.FirstOrDefault(s =>
                     s.CommandLineName.Equals(sceneName, StringComparison.OrdinalIgnoreCase) ||
                     s.Name.Equals(sceneName, StringComparison.OrdinalIgnoreCase))
                 ?? Scenes[0];

        int result = NativeGlue.CreateContext(canvasSelector, width, height);
        if (result != 1)
        {
            Console.Error.WriteLine($"WebGL 2 context creation failed: EMSCRIPTEN_RESULT {result}");
            return false;
        }

        // Impeller asks for every GLES entrypoint by name; hand it Emscripten's libGL resolver.
        _context = ImpellerContext.CreateOpenGLESNew((IntPtr name) => NativeGlue.GetProcAddress(name));
        if (_context == null)
        {
            Console.Error.WriteLine("ImpellerContextCreateOpenGLESNew failed.");
            return false;
        }

        _typography = CreateTypographyContext();

        SetStatus($"NImpeller · scene: {_scene.Name}");
        FpsClock.Start();
        return true;
    }

    /// <summary>
    /// The wasm build of Impeller has no system font manager, so every font must be registered
    /// explicitly. Loads the embedded Noto Sans and exposes it both under its own family name and
    /// as the default family.
    /// </summary>
    private static ImpellerTypographyContext CreateTypographyContext()
    {
        var typography = ImpellerTypographyContext.New()!;

        using var stream = typeof(WebApp).Assembly.GetManifestResourceStream("NotoSans-Regular.ttf")
                           ?? throw new InvalidOperationException("Embedded font resource missing.");
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);

        // RegisterFont copies the bytes, so one buffer serves both registrations.
        using var memory = new ImpellerUnmanagedMemory(bytes);
        if (!typography.RegisterFont(memory) || !typography.RegisterFont(memory, DefaultFontFamilyAlias))
        {
            Console.Error.WriteLine("Failed to register the embedded font.");
        }

        return typography;
    }

    [JSExport]
    public static void RenderFrame(double timestampMs)
    {
        if (_context == null)
        {
            return;
        }

        var size = new ImpellerISize(_width, _height);
        if (_surface == null || size != _surfaceSize)
        {
            _surface?.Dispose();
            // WebGL's default framebuffer is FBO 0.
            _surface = _context.SurfaceCreateWrappedFBONew(0, ImpellerPixelFormat.kImpellerPixelFormatRGBA8888, size)!;
            _surfaceSize = size;
        }

        ImpellerDisplayList displayList;
        using (var builder = ImpellerDisplayListBuilder.New(new ImpellerRect(0, 0, _width, _height))!)
        {
            _scene.Render(_context, builder, new SceneParameters
            {
                Width = _width,
                Height = _height,
                TypographyContext = _typography,
            });
            displayList = builder.CreateDisplayListNew()!;
        }

        using (displayList)
        {
            _surface.DrawDisplayList(displayList);
        }

        _framesSinceReport++;
        if (FpsClock.Elapsed.TotalSeconds >= 1)
        {
            var fps = _framesSinceReport / FpsClock.Elapsed.TotalSeconds;
            SetStatus($"NImpeller · scene: {_scene.Name} · {fps:F0} fps");
            _framesSinceReport = 0;
            FpsClock.Restart();
        }
    }
}

/// <summary>P/Invokes into native/nimpeller_web.c, which is linked statically into dotnet.native.wasm.</summary>
internal static unsafe partial class NativeGlue
{
    [DllImport("nimpeller_web", EntryPoint = "nimpeller_web_create_context")]
    private static extern int CreateContextNative(byte* selector, int width, int height);

    [DllImport("nimpeller_web", EntryPoint = "nimpeller_web_get_proc_address")]
    private static extern IntPtr GetProcAddressNative(IntPtr name);

    public static int CreateContext(string selector, int width, int height)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(selector + "\0");
        fixed (byte* p = bytes)
        {
            return CreateContextNative(p, width, height);
        }
    }

    public static IntPtr GetProcAddress(IntPtr name) => GetProcAddressNative(name);
}
