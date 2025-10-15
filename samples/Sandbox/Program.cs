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

static unsafe  class Program
{
    static void Main(string[] args)
    {
        NativeLibrary.SetDllImportResolver(typeof(ImpellerContext).Assembly, (name, assembly, path) =>
        {
            if (name == "impeller")
                return NativeLibrary.Load(
                    args[0]);
            return IntPtr.Zero;
        });
        var sdl = Sdl.GetApi();

        if (sdl.Init(Sdl.InitVideo) < 0)
        {
            Console.WriteLine($"SDL initialization failed: {sdl.GetErrorS()}");
            return;
        }

        sdl.GLSetAttribute(GLattr.ContextMajorVersion, 3);
        sdl.GLSetAttribute(GLattr.ContextMinorVersion, 0);
        sdl.GLSetAttribute(GLattr.ContextProfileMask, (int)GLprofile.ES);
        
        

        var window = sdl.CreateWindow(
            "NImpeller on SDL",
            Sdl.WindowposCentered,
            Sdl.WindowposCentered,
            1600,
            900,
            (uint)(WindowFlags.Opengl | WindowFlags.Shown)
        );

        
        
        if (window == null)
        {
            Console.WriteLine($"Window creation failed: {sdl.GetErrorS()}");
            sdl.Quit();
            return;
        }
        

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
        var impellerContext = ImpellerContext.CreateOpenGLESNew(name =>
        {
            Console.WriteLine(name);
            return (IntPtr)sdl.GLGetProcAddress(name);
        })!;

        int fbo;
        gl.GetInteger(GLEnum.FramebufferBinding, &fbo);
        var renderer = Marshal.PtrToStringUTF8((IntPtr)gl.GetString(GLEnum.Renderer));
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
            if(surface == null || windowSize != surfaceSize)
            {
                surface?.Dispose();
                surface = impellerContext.SurfaceCreateWrappedFBONew((ulong)fbo,
                    ImpellerPixelFormat.kImpellerPixelFormatRGBA8888, windowSize)!;
                surfaceSize = windowSize;
            }
            
            
            gl.Viewport(0, 0, (uint)width, (uint)height);


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
                
                
                
                using var displayList = drawListBuilder.CreateDisplayListNew()!;
                surface.DrawDisplayList(displayList);
            }

            
            
            sdl.GLSwapWindow(window);

        }

        sdl.GLDeleteContext(context);
        sdl.DestroyWindow(window);
        sdl.Quit();
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

