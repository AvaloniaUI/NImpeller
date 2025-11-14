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
    private int _frames;
    private int _fps;

    public static IScene? CurrentScene { get; set; }
    public static NSWindow? CurrentWindow { get; set; }

    public ImpellerMetalRenderer(MTLDevice device)
    {
        _context = ImpellerContext.CreateMetalNew()!;
        _stopwatch = Stopwatch.StartNew();
        _frames = 0;
        _fps = 0;
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
            _frames = 0;
            _stopwatch.Restart();
            if (CurrentWindow != null)
            {
                CurrentWindow.Title = $"NImpeller on Metal - FPS: {_fps}";
            }
        }

        _frames++;

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