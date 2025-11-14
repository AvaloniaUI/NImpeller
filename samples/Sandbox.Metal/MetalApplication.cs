using System.Runtime.Versioning;
using NImpeller;
using Sandbox.MacInterop;
using SharpMetal.Metal;
using SharpMetal.ObjectiveCCore;

namespace Sandbox;

[SupportedOSPlatform("macos")]
public class MetalApplication
{
    private NSWindow? _window;
    private IScene _scene = null!;

    public void SetScene(IScene scene)
    {
        _scene = scene;
    }

    public void Run(int width = 800, int height = 600, string title = "NImpeller on Metal")
    {
        // Initialize Objective-C runtime, via SharpMetal
        ObjectiveC.LinkMetal();
        ObjectiveC.LinkCoreGraphics();
        ObjectiveC.LinkAppKit();
        ObjectiveC.LinkMetalKit();

        // Set up NSApplication
        var nsApplication = new NSApplication();
        var appDelegate = new NSApplicationDelegate(nsApplication);
        nsApplication.SetDelegate(appDelegate);

        var windowCreated = false;
        appDelegate.OnApplicationDidFinishLaunching += notification =>
        {
            if (windowCreated) return;
            windowCreated = true;

            var rect = new NSRect(100, 100, width, height);
            _window = new NSWindow(
                rect,
                (ulong)(NSStyleMask.Titled |
                        NSStyleMask.Resizable |
                        NSStyleMask.Closable |
                        NSStyleMask.Miniaturizable));

            var device = MTLDevice.CreateSystemDefaultDevice();

            // Create MTKView with NImpeller renderer
            var mtkView = new MTKView(rect, device)
            {
                ColorPixelFormat = MTLPixelFormat.BGRA8Unorm,
                ClearColor = new MTLClearColor { red = 0.0, green = 0.0, blue = 0.0, alpha = 1.0 },
                Delegate = MTKViewDelegate.Init<ImpellerMetalRenderer>(device)
            };

            ImpellerMetalRenderer.CurrentScene = _scene;
            ImpellerMetalRenderer.CurrentWindow = _window;

            _window.SetContentView(mtkView);
            _window.Title = title;
            _window.MakeKeyAndOrderFront();

            var app = new NSApplication(notification.Object);
            app.ActivateIgnoringOtherApps(true);
        };

        appDelegate.OnApplicationWillFinishLaunching += notification =>
        {
            var app = new NSApplication(notification.Object);
            app.SetActivationPolicy(NSApplicationActivationPolicy.Regular);
        };

        nsApplication.Run();
    }
}
