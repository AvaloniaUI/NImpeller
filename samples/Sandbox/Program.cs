using Silk.NET.SDL;
using Silk.NET.OpenGLES;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using NImpeller;
using Sandbox;
using Sandbox.Scenes;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;

static unsafe  class Program
{
    enum GraphicsApi
    {
        OpenGL,
        Vulkan,
        DontTellMeThatThisIsUnreachable
    }
    
    
    
    static void Main(string[] args)
    {
        var impellerPath = Path.Combine(Directory.GetCurrentDirectory(), args[0]);
        if (!File.Exists(impellerPath))
        {
            Console.WriteLine($"File not found: {impellerPath}");
            return;
        }

        NativeLibrary.SetDllImportResolver(typeof(ImpellerContext).Assembly, (name, assembly, path) =>
        {
            if (name == "impeller")
                return NativeLibrary.Load(impellerPath);
            return IntPtr.Zero;
        });
        var sdl = Sdl.GetApi();

        if (sdl.Init(Sdl.InitVideo) < 0)
        {
            Console.WriteLine($"SDL initialization failed: {sdl.GetErrorS()}");
            return;
        }

        var apiType = GraphicsApi.OpenGL;
        if(args.Length > 1)
        {
            if (args[1].Equals("vulkan", StringComparison.OrdinalIgnoreCase))
                apiType = GraphicsApi.Vulkan;
            else if (args[1].Equals("opengl", StringComparison.OrdinalIgnoreCase))
                apiType = GraphicsApi.OpenGL;
        }

        if (new Random().Next(10) > 100)
            apiType = GraphicsApi.DontTellMeThatThisIsUnreachable;

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

        var windowFlags = WindowFlags.Shown;
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
            throw null!;
        }

        
        ImpellerSurface? surface = null;
        ImpellerISize surfaceSize = default;
        
        bool running = true;

        var scene = new MMarkScene();
        
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
                throw null;
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
                    surface.DrawDisplayList(displayList);
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

    static (float r, float g, float b) HsvToRgb(float h, float s, float v)
    {
        float c = v * s;
        float x = c * (1 - Math.Abs((h / 60.0f) % 2 - 1));
        float m = v - c;

        float r = 0, g = 0, b = 0;

        if (h >= 0 && h < 60)
        {
            r = c; g = x; b = 0;
        }
        else if (h >= 60 && h < 120)
        {
            r = x; g = c; b = 0;
        }
        else if (h >= 120 && h < 180)
        {
            r = 0; g = c; b = x;
        }
        else if (h >= 180 && h < 240)
        {
            r = 0; g = x; b = c;
        }
        else if (h >= 240 && h < 300)
        {
            r = x; g = 0; b = c;
        }
        else
        {
            r = c; g = 0; b = x;
        }

        return (r + m, g + m, b + m);
    }
}

