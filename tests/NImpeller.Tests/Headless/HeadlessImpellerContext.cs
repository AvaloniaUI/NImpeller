using System.Collections.Concurrent;
using NImpeller;
using NImpeller.Tests.Scenes;
using Silk.NET.Core.Contexts;
using Silk.NET.OpenGLES;
using Silk.NET.SDL;
using Thread = System.Threading.Thread;
using PixelFormat = Silk.NET.OpenGLES.PixelFormat;
using PixelType = Silk.NET.OpenGLES.PixelType;

namespace NImpeller.Tests.Headless;

internal sealed unsafe class HeadlessImpellerContext : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;

    private Sdl _sdl = null!;
    private GL _gl = null!;
    private Window* _window;
    private void* _glContext;
    private ImpellerContext _impeller = null!;

    public bool Available { get; private set; }

    public string? Error { get; private set; }

    public string? RendererName { get; private set; }

    public bool IsSoftwareRenderer
    {
        get
        {
            var r = RendererName?.ToLowerInvariant() ?? "";
            return r.Contains("llvmpipe") || r.Contains("softpipe") || r.Contains("swrast") || r.Contains("software");
        }
    }

    public HeadlessImpellerContext()
    {
        _thread = new Thread(ThreadMain) { IsBackground = true, Name = "Impeller-GL" };
        using var ready = new ManualResetEventSlim(false);
        _thread.Start(ready);
        ready.Wait();
    }

    private void ThreadMain(object? arg)
    {
        var ready = (ManualResetEventSlim)arg!;
        try
        {
            InitializeGL();
            Available = true;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Available = false;
        }
        finally
        {
            ready.Set();
        }

        if (!Available)
        {
            return;
        }

        foreach (var action in _queue.GetConsumingEnumerable())
        {
            action();
        }

        TeardownGL();
    }

    private void InitializeGL()
    {
        _sdl = Sdl.GetApi();
        if (_sdl.Init(Sdl.InitVideo) < 0)
        {
            throw new InvalidOperationException($"SDL_Init(VIDEO) failed: {_sdl.GetErrorS()}");
        }

        // GLES 3.0.
        _sdl.GLSetAttribute(GLattr.ContextMajorVersion, 3);
        _sdl.GLSetAttribute(GLattr.ContextMinorVersion, 0);
        _sdl.GLSetAttribute(GLattr.ContextProfileMask, (int)GLprofile.ES);

        _window = _sdl.CreateWindow(
            "NImpeller.Tests",
            Sdl.WindowposUndefined,
            Sdl.WindowposUndefined,
            16,
            16,
            (uint)(WindowFlags.Hidden | WindowFlags.Opengl));
        if (_window == null)
        {
            throw new InvalidOperationException($"SDL_CreateWindow failed: {_sdl.GetErrorS()}");
        }

        _glContext = _sdl.GLCreateContext(_window);
        if (_glContext == null)
        {
            throw new InvalidOperationException($"SDL_GL_CreateContext failed: {_sdl.GetErrorS()}");
        }

        _sdl.GLMakeCurrent(_window, _glContext);
        _gl = GL.GetApi(new LamdaNativeContext(s => (IntPtr)_sdl.GLGetProcAddress(s)));
        RendererName = _gl.GetStringS(StringName.Renderer);

        _impeller = ImpellerContext.CreateOpenGLESNew(name => (IntPtr)_sdl.GLGetProcAddress(name))
            ?? throw new InvalidOperationException("ImpellerContext.CreateOpenGLESNew returned null");
    }

    private void TeardownGL()
    {
        try { _impeller?.Dispose(); } catch { /* best effort */ }
        if (_glContext != null) { _sdl.GLDeleteContext(_glContext); }
        if (_window != null) { _sdl.DestroyWindow(_window); }
        _sdl?.Quit();
    }

    public byte[] Render(IScene scene, int width, int height) =>
        Invoke(() => RenderToFbo(width, height,
            builder => scene.Render(_impeller, builder, new SceneParameters { Width = width, Height = height })));

    public TextureInteropResult RenderTextureInterop(int width, int height) =>
        Invoke(() => RenderTextureInteropCore(width, height));

    private byte[] RenderToFbo(int width, int height, Action<ImpellerDisplayListBuilder> record)
    {
        uint fbo = _gl.GenFramebuffer();
        uint tex = _gl.GenTexture();
        try
        {
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            _gl.BindTexture(TextureTarget.Texture2D, tex);
            _gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgba8,
                (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
            _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D, tex, 0);

            var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != GLEnum.FramebufferComplete)
            {
                throw new InvalidOperationException($"Offscreen framebuffer incomplete: {status}");
            }

            using var surface = _impeller.SurfaceCreateWrappedFBONew((ulong)fbo,
                ImpellerPixelFormat.kImpellerPixelFormatRGBA8888, new ImpellerISize(width, height))
                ?? throw new InvalidOperationException("SurfaceCreateWrappedFBONew returned null");

            ImpellerDisplayList displayList;
            using (var builder = ImpellerDisplayListBuilder.New(new ImpellerRect(0, 0, width, height))!)
            {
                record(builder);
                displayList = builder.CreateDisplayListNew()!;
            }

            using (displayList)
            {
                surface.DrawDisplayList(displayList);
            }

            // Make sure Impeller's GL commands have completed before we read the texels back.
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            _gl.Finish();

            var pixels = new byte[width * height * 4];
            fixed (byte* p = pixels)
            {
                _gl.ReadPixels(0, 0, (uint)width, (uint)height, PixelFormat.Rgba, PixelType.UnsignedByte, p);
            }

            return FlipVertically(pixels, width, height);
        }
        finally
        {
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            _gl.DeleteFramebuffer(fbo);
            _gl.DeleteTexture(tex);
        }
    }

    private TextureInteropResult RenderTextureInteropCore(int width, int height)
    {
        byte[] checker =
        {
            255, 255, 255, 255,   0,   0,   0, 255,
              0,   0,   0, 255, 255, 255, 255, 255,
        };

        uint glTex = _gl.GenTexture();
        try
        {
            _gl.BindTexture(TextureTarget.Texture2D, glTex);
            fixed (byte* p = checker)
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgba8,
                    2, 2, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
            }
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);

            var descriptor = new ImpellerTextureDescriptor
            {
                Pixel_format = ImpellerPixelFormat.kImpellerPixelFormatRGBA8888,
                Size = new ImpellerISize(2, 2),
                Mip_count = 1,
            };

            ulong reportedHandle;
            byte[] pixels;
            using (var texture = _impeller.TextureCreateWithOpenGLTextureHandleNew(descriptor, glTex)
                ?? throw new InvalidOperationException("TextureCreateWithOpenGLTextureHandleNew returned null"))
            {
                reportedHandle = texture.GetOpenGLHandle();

                pixels = RenderToFbo(width, height, builder =>
                {
                    using var background = ImpellerPaint.New()!;
                    background.SetColor(ImpellerColor.FromRgb(30, 30, 30));
                    builder.DrawPaint(background);

                    using var paint = ImpellerPaint.New()!;
                    builder.DrawTextureRect(
                        texture,
                        new ImpellerRect(0, 0, 2, 2),
                        new ImpellerRect(20, 20, width - 40, height - 40),
                        ImpellerTextureSampling.kImpellerTextureSamplingNearestNeighbor,
                        paint);
                });
            }

            return new TextureInteropResult(pixels, glTex, reportedHandle);
        }
        finally
        {
            _gl.DeleteTexture(glTex);
        }
    }

    // glReadPixels returns rows bottom-to-top; images are top-to-bottom.
    private static byte[] FlipVertically(byte[] src, int width, int height)
    {
        int stride = width * 4;
        var dst = new byte[src.Length];
        for (int y = 0; y < height; y++)
        {
            Array.Copy(src, (height - 1 - y) * stride, dst, y * stride, stride);
        }
        return dst;
    }

    // Avoid hangs by enforcing a timeout. If it doesn't complete, bail and fail.
    private static readonly TimeSpan InvokeTimeout = TimeSpan.FromSeconds(30);

    private T Invoke<T>(Func<T> fn, [System.Runtime.CompilerServices.CallerMemberName] string op = "")
    {
        if (!Available)
        {
            throw new InvalidOperationException(Error ?? "Headless GL context is unavailable.");
        }

        Exception? error = null;
        T result = default!;
        var done = new ManualResetEventSlim(false);
        _queue.Add(() =>
        {
            try { result = fn(); }
            catch (Exception ex) { error = ex; }
            finally { done.Set(); }
        });

        if (!done.Wait(InvokeTimeout))
        {
            throw new TimeoutException(
                $"Headless GL operation '{op}' did not complete within {InvokeTimeout.TotalSeconds:0}s " +
                "(a native call likely deadlocked).");
        }

        done.Dispose();

        if (error != null)
        {
            throw new InvalidOperationException("Headless render failed: " + error.Message, error);
        }
        return result;
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        if (_thread.IsAlive)
        {
            _thread.Join(TimeSpan.FromSeconds(10));
        }
        _queue.Dispose();
    }
}
