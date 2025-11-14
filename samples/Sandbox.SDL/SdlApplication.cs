using Silk.NET.SDL;
using Silk.NET.OpenGLES;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NImpeller;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;

namespace Sandbox;

public unsafe class SdlApplication
{
    public enum GraphicsApi
    {
        OpenGL,
        Vulkan
    }

    private readonly Sdl _sdl;
    private Window* _window;
    private ImpellerContext _impellerContext = null!;
    private int _fbo;
    private ImpellerVulkanSwapchain _vulkanSwapchain = null!;
    private ImpellerSurface? _surface;
    private ImpellerISize _surfaceSize;
    private readonly GraphicsApi _apiType;
    private IScene _scene = null!;
    private readonly Stopwatch _stopwatch;
    private int _frames;
    private int _fps;
    private readonly ILogger<SdlApplication> _logger;

    public SdlApplication(GraphicsApi apiType = GraphicsApi.OpenGL, ILogger<SdlApplication>? logger = null)
    {
        _sdl = Sdl.GetApi();
        _apiType = apiType;
        _stopwatch = Stopwatch.StartNew();
        _logger = logger ?? NullLogger<SdlApplication>.Instance;
    }

    public bool Initialize(int width = 1600, int height = 900, string title = "NImpeller on SDL")
    {
        if (_sdl.Init(Sdl.InitVideo) < 0)
        {
            _logger.LogError("SDL initialization failed: {Error}", _sdl.GetErrorS());
            return false;
        }

        if (_apiType == GraphicsApi.OpenGL)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                _sdl.GLSetAttribute(GLattr.ContextMajorVersion, 3);
                _sdl.GLSetAttribute(GLattr.ContextMinorVersion, 3);
                _sdl.GLSetAttribute(GLattr.ContextProfileMask, (int)GLprofile.Core);
                _sdl.GLSetAttribute(GLattr.ContextFlags, (int)GLcontextFlag.ForwardCompatibleFlag);
            }
            else
            {
                _sdl.GLSetAttribute(GLattr.ContextMajorVersion, 3);
                _sdl.GLSetAttribute(GLattr.ContextMinorVersion, 0);
                _sdl.GLSetAttribute(GLattr.ContextProfileMask, (int)GLprofile.ES);
            }
        }

        var windowFlags = WindowFlags.Shown | WindowFlags.Resizable;
        if (_apiType == GraphicsApi.OpenGL)
            windowFlags |= WindowFlags.Opengl;
        else if (_apiType == GraphicsApi.Vulkan)
            windowFlags |= WindowFlags.Vulkan;

        _window = _sdl.CreateWindow(
            title,
            Sdl.WindowposCentered,
            Sdl.WindowposCentered,
            width,
            height,
            (uint)windowFlags
        );

        if (_window == null)
        {
            _logger.LogError("Window creation failed: {Error}", _sdl.GetErrorS());
            _sdl.Quit();
            return false;
        }

        if (_apiType == GraphicsApi.OpenGL)
        {
            var context = _sdl.GLCreateContext(_window);
            if (context == null)
            {
                _logger.LogError("OpenGL context creation failed: {Error}", _sdl.GetErrorS());
                _sdl.DestroyWindow(_window);
                _sdl.Quit();
                return false;
            }

            var gl = GL.GetApi(new LamdaNativeContext(s => (IntPtr)_sdl.GLGetProcAddress(s)));

            _sdl.GLMakeCurrent(_window, context);
            _sdl.GLSetSwapInterval(0);
            _impellerContext = ImpellerContext.CreateOpenGLESNew(name =>
            {
                _logger.LogDebug("Loading OpenGL function: {FunctionName}", name);
                return (IntPtr)_sdl.GLGetProcAddress(name);
            })!;
            int fbo = 0;
            gl.GetInteger(GLEnum.FramebufferBinding, &fbo);
            _fbo = fbo;
        }
        else if (_apiType == GraphicsApi.Vulkan)
        {
            uint extensionCount;
            byte* extensions;
            _sdl.VulkanGetInstanceExtensions(_window, &extensionCount, &extensions);
            var vkGetProcAddress = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)_sdl.VulkanGetVkGetInstanceProcAddr();
            _impellerContext = ImpellerContext.CreateVulkanNew((instance, proc) => vkGetProcAddress(instance, proc), false)!;

            var info = _impellerContext.GetVulkanInfo()!.Value;

            VkNonDispatchableHandle surfaceHandle;
            _sdl.VulkanCreateSurface(_window, new(info.Vk_instance), &surfaceHandle);
            _vulkanSwapchain = _impellerContext.VulkanSwapchainCreateNew(new IntPtr((long)surfaceHandle.Handle))!;
        }

        return true;
    }

    public void SetScene(IScene scene)
    {
        _scene = scene;
    }

    public void Run()
    {
        bool running = true;

        while (running)
        {
            Event evt;
            while (_sdl.PollEvent(&evt) != 0)
            {
                if (evt.Type == (uint)EventType.Quit)
                {
                    running = false;
                }
            }

            int width, height;
            _sdl.GetWindowSize(_window, &width, &height);

            var windowSize = new ImpellerISize(width, height);
            if (_apiType == GraphicsApi.OpenGL)
            {
                if (_surface == null || windowSize != _surfaceSize)
                {
                    _surface?.Dispose();
                    _surface = _impellerContext.SurfaceCreateWrappedFBONew((ulong)_fbo,
                        ImpellerPixelFormat.kImpellerPixelFormatRGBA8888, windowSize)!;
                    _surfaceSize = windowSize;
                }
            }

            ImpellerDisplayList displayList;
            using (var drawListBuilder = ImpellerDisplayListBuilder.New(new ImpellerRect(100, 100, width, height))!)
            {
                if (_stopwatch.Elapsed.TotalSeconds > 1)
                {
                    _fps = (int)(_frames / _stopwatch.Elapsed.TotalSeconds);
                    _frames = 0;
                    _stopwatch.Restart();
                    _sdl.SetWindowTitle(_window, "FPS: " + _fps);
                }

                _frames++;

                _scene.Render(_impellerContext, drawListBuilder, new SceneParameters()
                {
                    Width = width,
                    Height = height
                });

                displayList = drawListBuilder.CreateDisplayListNew()!;
            }

            using (displayList)
            {
                if (_apiType == GraphicsApi.OpenGL)
                {
                    _surface?.DrawDisplayList(displayList);
                    _sdl.GLSwapWindow(_window);
                }
                else if (_apiType == GraphicsApi.Vulkan)
                {
                    using (_surface = _vulkanSwapchain.AcquireNextSurfaceNew()!)
                    {
                        _surface.DrawDisplayList(displayList);
                        _surface.Present();
                    }
                }
            }
        }

        Process.GetCurrentProcess().Kill();
    }
}
