using System.Diagnostics;
using System.Runtime.Versioning;
using NImpeller;
using Sandbox.MacInterop;
using SharpMetal.Metal;

namespace Sandbox;

[SupportedOSPlatform("macos")]
public class ImpellerMetalRenderer : Sandbox.MacInterop.IRenderer
{
    private readonly ImpellerContext _context;
    private readonly Stopwatch _stopwatch;
    private static readonly Stopwatch _totalRunTime = Stopwatch.StartNew();
    private int _frames;
    private static long _totalFrames;
    private int _fps;
    private static int _currentFps;

    public static IScene? CurrentScene { get; set; }
    public static NSWindow? CurrentWindow { get; set; }
    public static MetalApplication? CurrentApplication { get; set; }

    public ImpellerMetalRenderer(MTLDevice device)
    {
        _context = ImpellerContext.CreateMetalNew()!;
        _stopwatch = Stopwatch.StartNew();
        _frames = 0;
        _fps = 0;
    }

    public static ApplicationStatus GetStatus()
    {
        return new ApplicationStatus
        {
            CurrentFps = _currentFps,
            TotalFrames = _totalFrames,
            RunTime = _totalRunTime.Elapsed
        };
    }

    public static Sandbox.MacInterop.IRenderer Init(MTLDevice device)
    {
        return new ImpellerMetalRenderer(device);
    }

    public void Draw(Sandbox.MacInterop.MTKView view)
    {
        if (CurrentScene == null)
            return;

        var drawable = view.CurrentDrawable;
        if (drawable.NativePtr == IntPtr.Zero)
            return;

        if (_stopwatch.Elapsed.TotalSeconds > 1)
        {
            _fps = (int)(_frames / _stopwatch.Elapsed.TotalSeconds);
            _currentFps = _fps;
            _frames = 0;
            _stopwatch.Restart();
            if (CurrentWindow != null)
            {
                CurrentWindow.Title = $"NImpeller on Metal - FPS: {_fps}";
            }

            // Raise status updated event
            CurrentApplication?.OnStatusUpdated(GetStatus());
        }

        _frames++;
        _totalFrames++;

        var width = (int)drawable.Texture.Width;
        var height = (int)drawable.Texture.Height;

        ImpellerDisplayList displayList;
        using (var drawListBuilder = ImpellerDisplayListBuilder.New(new ImpellerRect(0, 0, width, height))!)
        {
            CurrentScene.Render(_context, drawListBuilder, new SceneParameters()
            {
                Width = width,
                Height = height
            });

            displayList = drawListBuilder.CreateDisplayListNew()!;
        }

        using (displayList)
        {
            using var surface = _context.SurfaceCreateWrappedMetalDrawableNew(drawable.NativePtr)!;
            surface.DrawDisplayList(displayList);
            drawable.Present();
        }
    }
}