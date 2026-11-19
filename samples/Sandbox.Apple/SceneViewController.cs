using System.Diagnostics;
using CoreGraphics;
using Metal;
using MetalKit;
using NImpeller;
using Sandbox.Scenes;

namespace Sandbox.Apple;

/// <summary>
/// Draws the Sandbox.Core scenes into an <see cref="MTKView"/> with Impeller's Metal backend. A tap (or a click of the
/// Siri Remote) shows the next scene. For unattended runs, SANDBOX_SCENE picks the first scene by its command-line
/// name, and SANDBOX_CYCLE=&lt;seconds&gt; moves to the next scene on a timer and writes one line per scene to the
/// console: <c>SIMCTL_CHILD_SANDBOX_CYCLE=3 xcrun simctl launch --console booted com.avaloniaui.nimpeller.sandbox</c>.
/// </summary>
public sealed class SceneViewController : UIViewController
{
    readonly IScene[] _scenes = SceneCatalog.CreateAll();
    readonly UILabel _label = new()
    {
        TextColor = UIColor.White,
        BackgroundColor = UIColor.FromWhiteAlpha(0, 0.6f),
        Lines = 0,
    };

    MTKView? _view;
    Renderer? _renderer;
    int _index;

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        var device = MTLDevice.SystemDefault ?? throw new InvalidOperationException("No Metal device");
        _view = new MTKView(View!.Bounds, device)
        {
            ColorPixelFormat = MTLPixelFormat.BGRA8Unorm,
            FramebufferOnly = false,
            PreferredFramesPerSecond = 60,
            AutoresizingMask = UIViewAutoresizing.FlexibleDimensions,
        };
        View.AddSubview(_view);
        View.AddSubview(_label);

        var first = Environment.GetEnvironmentVariable("SANDBOX_SCENE");
        if (first != null)
            _index = Math.Max(0, Array.FindIndex(_scenes, s => s.CommandLineName == first));
        var cycle = double.TryParse(Environment.GetEnvironmentVariable("SANDBOX_CYCLE"), out var seconds) ? seconds : 0;

        _renderer = new Renderer(this, cycle);
        _view.Delegate = _renderer;
        View.AddGestureRecognizer(new UITapGestureRecognizer(Next));
        Console.WriteLine($"SANDBOX start {_scenes.Length} scenes, first {_scenes[_index].CommandLineName}");
    }

    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();
        var safe = View!.SafeAreaInsets;
        var size = _label.SizeThatFits(new CGSize(View.Bounds.Width - 48, nfloat.MaxValue));
        _label.Frame = new CGRect(safe.Left + 12, safe.Top + 12, size.Width, size.Height);
    }

    IScene Scene => _scenes[_index];

    void Next() => _index = (_index + 1) % _scenes.Length;

    void ShowStatus(string text)
    {
        _label.Text = text;
        View!.SetNeedsLayout();
    }

    sealed class Renderer : MTKViewDelegate
    {
        readonly SceneViewController _owner;
        readonly ImpellerContext _context;
        readonly double _cycle;
        readonly Stopwatch _sceneClock = Stopwatch.StartNew();
        readonly Stopwatch _fpsClock = Stopwatch.StartNew();
        IScene? _current;
        int _frames;
        int _sceneFrames;
        int _fps;
        int _visited;
        string? _error;

        public Renderer(SceneViewController owner, double cycle)
        {
            _owner = owner;
            _cycle = cycle;
            _context = ImpellerContext.CreateMetalNew() ?? throw new InvalidOperationException("No Impeller Metal context");
        }

        public override void DrawableSizeWillChange(MTKView view, CGSize size)
        {
        }

        public override void Draw(MTKView view)
        {
            var scene = _owner.Scene;
            if (!ReferenceEquals(scene, _current))
            {
                _current = scene;
                _sceneFrames = 0;
                _error = null;
                _sceneClock.Restart();
            }

            var drawable = view.CurrentDrawable;
            if (drawable == null)
                return;
            var width = (int)drawable.Texture.Width;
            var height = (int)drawable.Texture.Height;
            try
            {
                using (var builder = ImpellerDisplayListBuilder.New(new ImpellerRect(0, 0, width, height))!)
                {
                    scene.Render(_context, builder, new SceneParameters { Width = width, Height = height });
                    using var list = builder.CreateDisplayListNew()!;
                    using var surface = _context.SurfaceCreateWrappedMetalDrawableNew(drawable.Handle)
                        ?? throw new InvalidOperationException("Impeller could not wrap the drawable");
                    surface.DrawDisplayList(list);
                }
                drawable.Present();
                _sceneFrames++;
            }
            catch (Exception e)
            {
                if (_error == null)
                    Console.WriteLine($"SANDBOX error {scene.CommandLineName}: {e}");
                _error = $"{e.GetType().Name}: {e.Message}";
            }

            _frames++;
            if (_fpsClock.Elapsed.TotalSeconds >= 1)
            {
                _fps = (int)(_frames / _fpsClock.Elapsed.TotalSeconds);
                _frames = 0;
                _fpsClock.Restart();
                _owner.ShowStatus($"{scene.Name} ({_owner._index + 1}/{_owner._scenes.Length})\n" +
                                  $"{width}x{height} · {_fps} FPS{(_error != null ? "\n" + _error : "")}");
            }

            if (_cycle > 0 && _sceneClock.Elapsed.TotalSeconds >= _cycle)
            {
                Console.WriteLine($"SANDBOX scene {scene.CommandLineName} {(_error == null ? "ok" : "failed")} " +
                                  $"frames={_sceneFrames} size={width}x{height}");
                if (++_visited == _owner._scenes.Length)
                    Console.WriteLine($"SANDBOX done {_visited} scenes");
                _owner.Next();
            }
        }
    }
}
