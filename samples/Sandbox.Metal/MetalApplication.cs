using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NImpeller;
using Sandbox.MacInterop;
using SharpMetal.Metal;
using SharpMetal.ObjectiveCCore;

namespace Sandbox;

[SupportedOSPlatform("macos")]
public class MetalApplication : IApplication
{
    public event EventHandler<StatusUpdatedEventArgs>? StatusUpdated;

    private NSWindow? _window;
    private NSWindowDelegate? _windowDelegate;
    private NSApplicationDelegate? _appDelegate;
    private MTKViewDelegate? _mtkViewDelegate;
    private IScene _scene = null!;
    private readonly ILogger<MetalApplication> _logger;

    private int _width;

    private int _height;

    private string _title;
    public MetalApplication(int width, int height, string title, ILogger<MetalApplication>? logger = null)
    {
        _width = width;
        _height = height;
        _title = title;
        _logger = logger ?? NullLogger<MetalApplication>.Instance;
    }

    public ApplicationStatus GetStatus()
    {
        return ImpellerMetalRenderer.GetStatus();
    }

    internal void OnStatusUpdated(ApplicationStatus status)
    {
        StatusUpdated?.Invoke(this, new StatusUpdatedEventArgs(status));
    }

    public void SetScene(IScene scene)
    {
        _scene = scene;
    }

    public void Run()
    {
        // Initialize Objective-C runtime, via SharpMetal
        ObjectiveC.LinkMetal();
        ObjectiveC.LinkCoreGraphics();
        ObjectiveC.LinkAppKit();
        ObjectiveC.LinkMetalKit();

        // Set up NSApplication
        var nsApplication = new NSApplication();
        _appDelegate = new NSApplicationDelegate(nsApplication);
        nsApplication.SetDelegate(_appDelegate);

        var windowCreated = false;
        _appDelegate.OnApplicationDidFinishLaunching += notification =>
        {
            if (windowCreated) return;
            windowCreated = true;

            var rect = new NSRect(100, 100, _width, _height);
            _window = new NSWindow(
                rect,
                (ulong)(NSStyleMask.Titled |
                        NSStyleMask.Resizable |
                        NSStyleMask.Closable |
                        NSStyleMask.Miniaturizable));

            var device = MTLDevice.CreateSystemDefaultDevice();

            // Create MTKView with NImpeller renderer
            // Store the delegate in a field to prevent GC collection
            _mtkViewDelegate = MTKViewDelegate.Init<ImpellerMetalRenderer>(device, _logger);
            var mtkView = new MTKView(rect, device)
            {
                ColorPixelFormat = MTLPixelFormat.BGRA8Unorm,
                ClearColor = new MTLClearColor { red = 0.0, green = 0.0, blue = 0.0, alpha = 1.0 },
                Delegate = _mtkViewDelegate
            };

            ImpellerMetalRenderer.CurrentScene = _scene;
            ImpellerMetalRenderer.CurrentWindow = _window;
            ImpellerMetalRenderer.CurrentApplication = this;

            // Set up window delegate to handle window close event
            _windowDelegate = new NSWindowDelegate();
            _windowDelegate.WindowShouldClose = sender =>
            {
                // Terminate the application when the window closes
                var app = new NSApplication(notification.Object);
                app.Terminate();
                return true;
            };
            _window.SetDelegate(_windowDelegate.NativePtr);

            _window.SetContentView(mtkView);
            _window.Title = _title;
            _window.MakeKeyAndOrderFront();

            var app = new NSApplication(notification.Object);
            app.ActivateIgnoringOtherApps(true);
        };

        _appDelegate.OnApplicationWillFinishLaunching += notification =>
        {
            var app = new NSApplication(notification.Object);
            app.SetActivationPolicy(NSApplicationActivationPolicy.Regular);
        };

        nsApplication.Run();
    }
}
