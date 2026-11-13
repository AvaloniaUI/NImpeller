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

    // GLFW only dlopens libvulkan from the default search path, which excludes Homebrew's /opt/homebrew/lib.
    private static readonly string[] MacVulkanLoaders =
    [
        "/opt/homebrew/lib/libvulkan.1.dylib",
        "/usr/local/lib/libvulkan.1.dylib",
    ];

    private void InitMacVulkanLoader()
    {
        if (MacVulkanLoaders.FirstOrDefault(File.Exists) is not { } path) return;
        var loader = NativeLibrary.Load(path);
        var getInstanceProcAddr = NativeLibrary.GetExport(loader, "vkGetInstanceProcAddr");
        var init = (delegate* unmanaged[Cdecl]<IntPtr, void>)_glfw.Context.GetProcAddress("glfwInitVulkanLoader");
        init(getInstanceProcAddr);
    }

    public bool Initialize(int width, int height, string title = "NImpeller on GLFW")
    {
        if (apiType == GraphicsApi.Vulkan && RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            InitMacVulkanLoader();
        }

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

        if (apiType == GraphicsApi.Vulkan)
        {
            if (!_glfw.VulkanSupported())
            {
                _logger.LogError("GLFW could not find a Vulkan loader.");
                _glfw.Terminate();
                return false;
            }
            _glfw.WindowHint(WindowHintClientApi.ClientApi, ClientApi.NoApi);
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
                var context = ImpellerContext.CreateVulkanNew(
                    (instance, procNamePtr) => _glfw.GetInstanceProcAddress(new VkHandle(instance), (byte*)procNamePtr),
                    false);
                if (context?.GetVulkanInfo() is not { } info)
                {
                    _logger.LogError("Impeller could not create a Vulkan context.");
                    return false;
                }
                _impellerContext = context;

                VkNonDispatchableHandle surfaceHandle;
                var result = _glfw.CreateWindowSurface(new VkHandle(info.Vk_instance), _window, null, &surfaceHandle);
                if (result != 0)
                {
                    _logger.LogError("glfwCreateWindowSurface failed: {Result}", result);
                    return false;
                }

                var swapchain = _impellerContext.VulkanSwapchainCreateNew(new IntPtr((long)surfaceHandle.Handle));
                if (swapchain is null)
                {
                    _logger.LogError("Impeller could not create a Vulkan swapchain.");
                    return false;
                }
                _vulkanSwapchain = swapchain;
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
