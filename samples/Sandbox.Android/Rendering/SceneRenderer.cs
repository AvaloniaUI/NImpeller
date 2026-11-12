using System.Diagnostics;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using NImpeller;
using Sandbox.Android.Interop;

namespace Sandbox.Android.Rendering;

/// <summary>
/// Renders an <see cref="IScene"/> on a dedicated thread that runs its own <see cref="Looper"/> and is
/// paced by <see cref="Choreographer"/> vsync callbacks. The graphics backend and every Impeller
/// object live on that thread; the UI thread talks to it through <see cref="Invoke"/>.
/// </summary>
internal sealed class SceneRenderer : Java.Lang.Object, Choreographer.IFrameCallback, IApplication
{
    private const string Tag = "NImpeller";

    private readonly IScene _scene;
    private readonly GraphicsApi _api;
    private readonly float _density;
    private readonly ManualResetEventSlim _ready = new();
    private readonly Stopwatch _fpsClock = new();
    private readonly Stopwatch _runTime = new();

    private Thread? _thread;
    private Looper? _looper;
    private Handler? _handler;
    private IRenderBackend? _backend;
    private IntPtr _nativeWindow;
    private bool _framesScheduled;
    private int _frames;
    private long _totalFrames;
    private int _fps;

    public SceneRenderer(IScene scene, GraphicsApi api, float density)
    {
        _scene = scene;
        _api = api;
        _density = density;
    }

    public event EventHandler<StatusUpdatedEventArgs>? StatusUpdated;

    /// <summary>Raised on the render thread when the backend or a frame fails.</summary>
    public event EventHandler<Exception>? Failed;

    public ApplicationStatus GetStatus() => new()
    {
        CurrentFps = _fps,
        TotalFrames = _totalFrames,
        RunTime = _runTime.Elapsed,
    };

    /// <summary>Starts the render thread and waits until it accepts work.</summary>
    public void Start()
    {
        _thread = new Thread(Run) { Name = "NImpeller render", IsBackground = true };
        _thread.Start();
        _ready.Wait();
    }

    /// <summary>The render thread body: a Looper that processes surface events and vsync callbacks.</summary>
    public void Run()
    {
        Looper.Prepare();
        _looper = Looper.MyLooper()!;
        _handler = new Handler(_looper);
        _ready.Set();
        Looper.Loop();
    }

    /// <summary>Attaches the backend to <paramref name="surface"/> and starts rendering.</summary>
    public void SurfaceCreated(Surface surface)
    {
        // ANativeWindow_fromSurface needs the calling thread's JNIEnv, so resolve it here.
        var window = NativeWindow.ANativeWindow_fromSurface(JNIEnv.Handle, surface.Handle);
        Invoke(() =>
        {
            try
            {
                _backend ??= _api == GraphicsApi.Vulkan ? new VulkanBackend() : new GlesBackend();
                _backend.Attach(window);
                _nativeWindow = window;
                _runTime.Start();
                _fpsClock.Restart();
                ScheduleFrame();
            }
            catch (Exception e)
            {
                NativeWindow.ANativeWindow_release(window);
                Fail(e);
            }
        });
    }

    /// <summary>
    /// Stops rendering and detaches from the window. Returns only once the window is no longer used,
    /// as <see cref="ISurfaceHolderCallback.SurfaceDestroyed"/> requires.
    /// </summary>
    public void SurfaceDestroyed()
    {
        Invoke(() =>
        {
            _framesScheduled = false;
            Choreographer.Instance!.RemoveFrameCallback(this);
            _runTime.Stop();
            if (_nativeWindow == IntPtr.Zero)
                return;
            _backend?.Detach();
            NativeWindow.ANativeWindow_release(_nativeWindow);
            _nativeWindow = IntPtr.Zero;
        });
    }

    /// <summary>Releases the backend and ends the render thread.</summary>
    public void Stop()
    {
        if (_looper is null)
            return;
        SurfaceDestroyed();
        Invoke(() =>
        {
            (_scene as IDisposable)?.Dispose();
            _backend?.Dispose();
            _backend = null;
            _looper.QuitSafely();
        });
        _thread?.Join();
        _looper = null;
    }

    public void DoFrame(long frameTimeNanos)
    {
        if (!_framesScheduled)
            return;
        _framesScheduled = false;

        try
        {
            RenderFrame();
            ScheduleFrame();
        }
        catch (Exception e)
        {
            Fail(e);
        }
    }

    private void RenderFrame()
    {
        int width = NativeWindow.ANativeWindow_getWidth(_nativeWindow);
        int height = NativeWindow.ANativeWindow_getHeight(_nativeWindow);
        if (width <= 0 || height <= 0)
            return;

        ImpellerDisplayList displayList;
        using (var builder = ImpellerDisplayListBuilder.New(new ImpellerRect(0, 0, width, height))!)
        {
            // Scenes are laid out in density-independent pixels, like the desktop Sandbox.
            builder.Scale(_density, _density);
            _scene.Render(_backend!.Context, builder, new SceneParameters
            {
                Width = (int)(width / _density),
                Height = (int)(height / _density),
            });
            displayList = builder.CreateDisplayListNew()!;
        }

        using (displayList)
        {
            _backend.Present(displayList, width, height);
        }

        _frames++;
        _totalFrames++;
        if (_fpsClock.Elapsed.TotalSeconds >= 1)
        {
            _fps = (int)(_frames / _fpsClock.Elapsed.TotalSeconds);
            _frames = 0;
            _fpsClock.Restart();
            StatusUpdated?.Invoke(this, new StatusUpdatedEventArgs(GetStatus()));
        }
    }

    private void ScheduleFrame()
    {
        if (_framesScheduled || _nativeWindow == IntPtr.Zero)
            return;
        _framesScheduled = true;
        Choreographer.Instance!.PostFrameCallback(this);
    }

    private void Fail(Exception e)
    {
        Log.Error(Tag, $"Rendering stopped: {e}");
        _framesScheduled = false;
        Failed?.Invoke(this, e);
    }

    /// <summary>Runs <paramref name="action"/> on the render thread and waits for it to finish.</summary>
    private void Invoke(Action action)
    {
        if (Looper.MyLooper() == _looper)
        {
            action();
            return;
        }

        using var done = new ManualResetEventSlim();
        Exception? error = null;
        _handler!.Post(() =>
        {
            try { action(); }
            catch (Exception e) { error = e; }
            finally { done.Set(); }
        });
        done.Wait();
        if (error is not null)
            throw new InvalidOperationException("The render thread failed.", error);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Stop();
            _ready.Dispose();
        }
        base.Dispose(disposing);
    }
}
