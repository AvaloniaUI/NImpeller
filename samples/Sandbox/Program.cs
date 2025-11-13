using Silk.NET.SDL;
using Silk.NET.OpenGLES;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using NImpeller;
using Sandbox;
using Sandbox.Scenes;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using SharpMetal.Metal;
using SharpMetal.ObjectiveCCore;
using Sandbox.MacInterop;

static unsafe  class Program
{
    enum GraphicsApi
    {
        OpenGL,
        Vulkan,
        Metal,
        DontTellMeThatThisIsUnreachable
    }
    
    enum Scenes
    {
        MMark,
        Paragraph,
        CirclingSquares
    }

    static NSWindow? _window;
    
    
    static void Main(string[] args)
    {
        // Parse command-line arguments
        var apiType = GraphicsApi.OpenGL;
        if(args.Length > 1)
        {
            if (args[1].Equals("vulkan", StringComparison.OrdinalIgnoreCase))
                apiType = GraphicsApi.Vulkan;
            else if (args[1].Equals("opengl", StringComparison.OrdinalIgnoreCase))
                apiType = GraphicsApi.OpenGL;
            else if (args[1].Equals("metal", StringComparison.OrdinalIgnoreCase))
                apiType = GraphicsApi.Metal;
        }

        if (new Random().Next(10) > 100)
            apiType = GraphicsApi.DontTellMeThatThisIsUnreachable;

        // Check if Metal is requested and we're on macOS
        if (apiType == GraphicsApi.Metal)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Console.WriteLine("Metal is only supported on macOS");
                return;
            }

            RunMetalApp(args);
            return;
        }

        // For OpenGL and Vulkan, use SDL
        RunSdlApp(args, apiType);
    }

    static void RunSdlApp(string[] args, GraphicsApi apiType)
    {
        var sdl = Sdl.GetApi();

        if (sdl.Init(Sdl.InitVideo) < 0)
        {
            Console.WriteLine($"SDL initialization failed: {sdl.GetErrorS()}");
            return;
        }

        if (apiType == GraphicsApi.OpenGL)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                sdl.GLSetAttribute(GLattr.ContextMajorVersion, 3);
                sdl.GLSetAttribute(GLattr.ContextMinorVersion, 3);
                sdl.GLSetAttribute(GLattr.ContextProfileMask, (int)GLprofile.Core);
                sdl.GLSetAttribute(GLattr.ContextFlags, (int)GLcontextFlag.ForwardCompatibleFlag);
            }
            else
            {
                sdl.GLSetAttribute(GLattr.ContextMajorVersion, 3);
                sdl.GLSetAttribute(GLattr.ContextMinorVersion, 0);
                sdl.GLSetAttribute(GLattr.ContextProfileMask, (int)GLprofile.ES);
            }
        }

        var windowFlags = WindowFlags.Shown | WindowFlags.Resizable;
        if(apiType == GraphicsApi.OpenGL)
            windowFlags |= WindowFlags.Opengl;
        else if (apiType == GraphicsApi.Vulkan) 
            windowFlags |= WindowFlags.Vulkan;
        
        
        var window = sdl.CreateWindow(
            "NImpeller on SDL",
            Sdl.WindowposCentered,
            Sdl.WindowposCentered,
            1600,
            900,
            (uint)windowFlags
        );

        
        
        if (window == null)
        {
            Console.WriteLine($"Window creation failed: {sdl.GetErrorS()}");
            sdl.Quit();
            return;
        }


        ImpellerContext impellerContext;
        int fbo = 0;
        ImpellerVulkanSwapchain vulkanSwapchain = null!;

        if (apiType == GraphicsApi.OpenGL)
        {

            var context = sdl.GLCreateContext(window);
            if (context == null)
            {
                Console.WriteLine($"OpenGL context creation failed: {sdl.GetErrorS()}");
                sdl.DestroyWindow(window);
                sdl.Quit();
                return;
            }

            var gl = GL.GetApi(new LamdaNativeContext(s => (IntPtr)sdl.GLGetProcAddress(s)));

            sdl.GLMakeCurrent(window, context);
            sdl.GLSetSwapInterval(0); // Enable vsync
            impellerContext = ImpellerContext.CreateOpenGLESNew(name =>
            {
                Console.WriteLine(name);
                return (IntPtr)sdl.GLGetProcAddress(name);
            })!;
            gl.GetInteger(GLEnum.FramebufferBinding, &fbo);
        }
        else if (apiType == GraphicsApi.Vulkan)
        {
            uint extensionCount;
            byte* extensions;
            sdl.VulkanGetInstanceExtensions(window, &extensionCount, &extensions);
            var vkGetProcAddress = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)sdl.VulkanGetVkGetInstanceProcAddr();
            impellerContext = ImpellerContext.CreateVulkanNew((instance, proc) => vkGetProcAddress(instance, proc), false)!;

            var info = impellerContext.GetVulkanInfo()!.Value;

            VkNonDispatchableHandle surfaceHandle;
            sdl.VulkanCreateSurface(window, new(info.Vk_instance), &surfaceHandle);
            vulkanSwapchain = impellerContext.VulkanSwapchainCreateNew(new IntPtr((long)surfaceHandle.Handle))!;
        }
        else
        {
            throw new NotSupportedException($"Unsupported API type: {apiType}");
        }

        
        ImpellerSurface? surface = null;
        ImpellerISize surfaceSize = default;
        
        bool running = true;

        Scenes sceneType = Scenes.MMark;
        if (args.Length > 0)
        {
            Enum.TryParse<Scenes>(args[0], true, out sceneType);
        }

        IScene scene = sceneType switch
        {
            Scenes.MMark => new MMarkScene(),
            Scenes.Paragraph => new ParagraphScene(),
            Scenes.CirclingSquares => new CirclingSquares(),
            _ => new MMarkScene()
        };

        var st = Stopwatch.StartNew();
        var frames = 0;
        var fps = 0;
        while (running)
        {
            Event evt;
            while (sdl.PollEvent(&evt) != 0)
            {
                if (evt.Type == (uint)EventType.Quit)
                {
                    running = false;
                }
            }

            int width, height;
            sdl.GetWindowSize(window, &width, &height);

            var windowSize = new ImpellerISize(width, height);
            if (apiType == GraphicsApi.OpenGL)
            {

                if (surface == null || windowSize != surfaceSize)
                {
                    surface?.Dispose();
                    surface = impellerContext.SurfaceCreateWrappedFBONew((ulong)fbo,
                        ImpellerPixelFormat.kImpellerPixelFormatRGBA8888, windowSize)!;
                    surfaceSize = windowSize;
                }

            }
            else if (apiType == GraphicsApi.Vulkan)
            {
                // do nothing
            }
            else
            {
                throw new NotSupportedException($"Unsupported API type: {apiType}");
            }

            //gl.Viewport(0, 0, (uint)width, (uint)height);


            ImpellerDisplayList displayList;
            using (var drawListBuilder = ImpellerDisplayListBuilder.New(new ImpellerRect(100, 100, width, height))!)
            {
                if (st.Elapsed.TotalSeconds > 1)
                {
                    fps = (int)(frames / st.Elapsed.TotalSeconds);
                    frames = 0;
                    st.Restart();
                    sdl.SetWindowTitle(window, "FPS: " + fps);
                }

                frames++;
                
                scene.Render(impellerContext, drawListBuilder, new SceneParameters()
                {
                    Width = width,
                    Height = height
                });
                
                
                
                displayList = drawListBuilder.CreateDisplayListNew()!;
            }

            using (displayList)
            {
                if (apiType == GraphicsApi.OpenGL)
                {
                    surface?.DrawDisplayList(displayList);
                    sdl.GLSwapWindow(window);
                }
                else if (apiType == GraphicsApi.Vulkan)
                {
                    using (surface = vulkanSwapchain.AcquireNextSurfaceNew()!)
                    {
                        surface.DrawDisplayList(displayList);
                        surface.Present();
                    }
                }
            }

        }

        Process.GetCurrentProcess().Kill();
        /* who cares
        sdl.GLDeleteContext(context);
        sdl.DestroyWindow(window);
        sdl.Quit();*/
    }

    [SupportedOSPlatform("macos")]
    static void RunMetalApp(string[] args)
    {
        // Initialize Objective-C runtime, via SharpMetal
        ObjectiveC.LinkMetal();
        ObjectiveC.LinkCoreGraphics();
        ObjectiveC.LinkAppKit();
        ObjectiveC.LinkMetalKit();

        Scenes sceneType = Scenes.MMark;
        if (args.Length > 0)
        {
            Enum.TryParse<Scenes>(args[0], true, out sceneType);
        }

        IScene scene = sceneType switch
        {
            Scenes.MMark => new MMarkScene(),
            Scenes.Paragraph => new ParagraphScene(),
            Scenes.CirclingSquares => new CirclingSquares(),
            _ => new MMarkScene()
        };

        // Set up NSApplication
        var nsApplication = new Sandbox.MacInterop.NSApplication();
        var appDelegate = new Sandbox.MacInterop.NSApplicationDelegate(nsApplication);
        nsApplication.SetDelegate(appDelegate);

        var windowCreated = false;
        appDelegate.OnApplicationDidFinishLaunching += notification =>
        {
            if (windowCreated) return;
            windowCreated = true;

            var rect = new NSRect(100, 100, 800, 600);
            _window = new Sandbox.MacInterop.NSWindow(
                rect,
                (ulong)(Sandbox.MacInterop.NSStyleMask.Titled |
                        Sandbox.MacInterop.NSStyleMask.Resizable |
                        Sandbox.MacInterop.NSStyleMask.Closable |
                        Sandbox.MacInterop.NSStyleMask.Miniaturizable));

            var device = MTLDevice.CreateSystemDefaultDevice();

            // Create MTKView with NImpeller renderer
            var mtkView = new Sandbox.MacInterop.MTKView(rect, device)
            {
                ColorPixelFormat = MTLPixelFormat.BGRA8Unorm,
                ClearColor = new MTLClearColor { red = 0.0, green = 0.0, blue = 0.0, alpha = 1.0 },
                Delegate = Sandbox.MacInterop.MTKViewDelegate.Init<ImpellerRenderer>(device)
            };

            ImpellerRenderer.CurrentScene = scene;

            _window.SetContentView(mtkView);
            _window.Title = "NImpeller on Metal";
            _window.MakeKeyAndOrderFront();

            var app = new Sandbox.MacInterop.NSApplication(notification.Object);
            app.ActivateIgnoringOtherApps(true);
        };

        appDelegate.OnApplicationWillFinishLaunching += notification =>
        {
            var app = new Sandbox.MacInterop.NSApplication(notification.Object);
            app.SetActivationPolicy(Sandbox.MacInterop.NSApplicationActivationPolicy.Regular);
        };

        nsApplication.Run();
    }

    [SupportedOSPlatform("macos")]
    class ImpellerRenderer : Sandbox.MacInterop.IRenderer
    {
        private readonly ImpellerContext _context;
        private readonly Stopwatch _stopwatch;
        private int _frames;
        private int _fps;

        public static IScene? CurrentScene { get; set; }

        public ImpellerRenderer(MTLDevice device)
        {
            _context = ImpellerContext.CreateMetalNew()!;
            _stopwatch = Stopwatch.StartNew();
            _frames = 0;
            _fps = 0;
        }

        public static Sandbox.MacInterop.IRenderer Init(MTLDevice device)
        {
            return new ImpellerRenderer(device);
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
                _window!.Title = $"NImpeller on Metal - FPS: {_fps}";
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
}

