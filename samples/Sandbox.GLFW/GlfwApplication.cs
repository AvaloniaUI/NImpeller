using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NImpeller;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace Sandbox;

public unsafe class GlfwApplication(GlfwApplication.GraphicsApi apiType = GlfwApplication.GraphicsApi.OpenGL,ILogger<GlfwApplication>? logger = null) : IApplication
{
    public enum GraphicsApi
    {
        OpenGL,
        Vulkan
    }
    
    public event EventHandler<StatusUpdatedEventArgs>? StatusUpdated;

    private readonly ILogger<GlfwApplication> _logger = logger ?? NullLogger<GlfwApplication>.Instance;
    private readonly Glfw _glfw = Glfw.GetApi();
    private WindowHandle* _window;
    private ImpellerContext _impellerContext = null!;
    private ImpellerVulkanSwapchain _vulkanSwapchain = null!;
    private ImpellerSurface? _surface;
    private ImpellerISize _surfaceSize;
    private IScene _scene = null!;
    private readonly Stopwatch _fpsStopwatch = Stopwatch.StartNew();
    private readonly Stopwatch _totalRunTime = Stopwatch.StartNew();
    private int _frames;
    private long _totalFrames;
    private int _fbo;
    private int _fps;

    public ApplicationStatus GetStatus()
    {
        return new ApplicationStatus
        {
            CurrentFps = _fps,
            TotalFrames = _totalFrames,
            RunTime = _totalRunTime.Elapsed
        };
    }

    public bool Initialize(int width, int height, string title = "NImpeller on GLFW")
    {
        if (!_glfw.Init())
        {
            _logger.LogError("GLFW initialization failed.");
            return false;
        }

        if (apiType == GraphicsApi.OpenGL)
        {
            _glfw.WindowHint(WindowHintInt.ContextVersionMajor, 3);
            _glfw.WindowHint(WindowHintInt.ContextVersionMinor, 0);
            _glfw.WindowHint(WindowHintClientApi.ClientApi, ClientApi.OpenGLES);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                _glfw.WindowHint(WindowHintInt.ContextVersionMinor, 3);
                _glfw.WindowHint(WindowHintBool.OpenGLForwardCompat, true);
                _glfw.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core);
            }
        }

        _window = _glfw.CreateWindow(width, height, title, null, null);
        if (_window == null)
        {
            _logger.LogError("Window creation failed.");
            _glfw.Terminate();
            return false;
        }
        
        switch (apiType)
        {
            case GraphicsApi.OpenGL:
            {
                _logger.LogInformation("Creating OpenGL ES context...");
                _glfw.MakeContextCurrent(_window);
                _glfw.SwapInterval(0);
                var gl = GL.GetApi(new LamdaNativeContext(s => _glfw.GetProcAddress(s)));

                _impellerContext = ImpellerContext.CreateOpenGLESNew(name =>
                {
                    _logger.LogDebug("Loading OpenGL function: {FuncName}", name);
                    return _glfw.GetProcAddress(name);
                })!;

                gl.GetInteger(GLEnum.FramebufferBinding, out var data);
                _fbo = data;
                break;
            }
            case GraphicsApi.Vulkan:
            {
                _logger.LogInformation("Creating Vulkan context...");
                _impellerContext = ImpellerContext.CreateVulkanNew(
                    (instance, procNamePtr) => _glfw.GetInstanceProcAddress(new VkHandle(instance), (byte*)procNamePtr),
                    false)!;

                VkNonDispatchableHandle surfaceHandle;
                _glfw.CreateWindowSurface(
                    new VkHandle(_impellerContext.GetVulkanInfo()!.Value.Vk_instance),
                    _window,
                    null,
                    &surfaceHandle
                );

                _vulkanSwapchain = _impellerContext.VulkanSwapchainCreateNew(
                    new IntPtr((long)surfaceHandle.Handle))!;
                break;
            }
        }

        return true;
    }

    public void SetScene(IScene scene) => _scene = scene;

    public void Run()
    {
        var running = true;

        while (running)
        {
            _glfw.PollEvents();
            if (_glfw.WindowShouldClose(_window))
                running = false;

            _glfw.GetWindowSize(_window, out var width, out var height);
            var windowSize = new ImpellerISize(width, height);

            if (apiType == GraphicsApi.OpenGL)
            {
                if (_surface == null || windowSize != _surfaceSize)
                {
                    _surface?.Dispose();
                    _surface = _impellerContext.SurfaceCreateWrappedFBONew(
                        (ulong)_fbo,
                        ImpellerPixelFormat.kImpellerPixelFormatRGBA8888,
                        windowSize)!;
                    _surfaceSize = windowSize;
                }
            }

            ImpellerDisplayList displayList;
            using (var builder = ImpellerDisplayListBuilder.New(
                       new ImpellerRect(100, 100, width, height))!)
            {
                if (_fpsStopwatch.Elapsed.TotalSeconds >= 1.0)
                {
                    _fps = (int)(_frames / _fpsStopwatch.Elapsed.TotalSeconds);
                    _frames = 0;
                    _fpsStopwatch.Restart();

                    _glfw.SetWindowTitle(_window, $"FPS: {_fps}");
                    StatusUpdated?.Invoke(this, new StatusUpdatedEventArgs(GetStatus()));
                }

                _frames++;
                _totalFrames++;

                _scene.Render(_impellerContext, builder, new SceneParameters
                {
                    Width = width,
                    Height = height
                });

                displayList = builder.CreateDisplayListNew()!;
            }

            using (displayList)
            {
                switch (apiType)
                {
                    case GraphicsApi.OpenGL:
                        _surface?.DrawDisplayList(displayList);
                        _glfw.SwapBuffers(_window);
                        break;
                    case GraphicsApi.Vulkan:
                    {
                        using (_surface = _vulkanSwapchain.AcquireNextSurfaceNew()!)
                        {
                            _surface.DrawDisplayList(displayList);
                            _surface.Present();
                        }
                        break;
                    }
                }
            }
        }

        Process.GetCurrentProcess().Kill();
    }
}
