using Android.Content.PM;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using Sandbox.Android.Rendering;
using Sandbox.Scenes;

namespace Sandbox.Android;

/// <summary>
/// Full-screen scene view: a <see cref="SurfaceView"/> rendered by a <see cref="SceneRenderer"/>,
/// with a status overlay. Rotation and resizes reuse the activity, so the renderer only sees size
/// changes. Exported so scenes can be started directly:
/// <c>adb shell am start -n com.nimpeller.sandbox/.RenderActivity --es scene shaders --es api Vulkan</c>
/// </summary>
[Activity(
    Name = "com.nimpeller.sandbox.RenderActivity",
    Label = "Scene",
    Theme = "@android:style/Theme.DeviceDefault.NoActionBar",
    Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout |
                           ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden |
                           ConfigChanges.UiMode | ConfigChanges.Density)]
public sealed class RenderActivity : Activity, ISurfaceHolderCallback
{
    public const string SceneExtra = "scene";
    public const string ApiExtra = "api";

    private SceneRenderer? _renderer;
    private TextView _status = null!;
    private string _label = "";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var sceneName = Intent?.GetStringExtra(SceneExtra) ?? "mmark";
        var api = Enum.TryParse<GraphicsApi>(Intent?.GetStringExtra(ApiExtra), out var parsed) ? parsed : GraphicsApi.OpenGLES;
        var scene = SceneCatalog.CreateAll().FirstOrDefault(s => s.CommandLineName == sceneName);
        if (scene is null)
        {
            Toast.MakeText(this, $"Unknown scene '{sceneName}'.", ToastLength.Long)!.Show();
            Finish();
            return;
        }

        _label = $"{scene.Name} · {(api == GraphicsApi.Vulkan ? "Vulkan" : "OpenGL ES")}";
        Title = scene.Name;

        var surfaceView = new SurfaceView(this);
        surfaceView.Holder!.AddCallback(this);

        _status = new TextView(this) { Text = _label };
        _status.SetTextColor(Color.White);
        _status.SetBackgroundColor(Color.Argb(160, 0, 0, 0));
        var pad = (int)(8 * Resources!.DisplayMetrics!.Density);
        _status.SetPadding(pad, pad / 2, pad, pad / 2);

        var root = new FrameLayout(this);
        root.AddView(surfaceView);
        root.AddView(_status, new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent,
            GravityFlags.Top | GravityFlags.Start) { LeftMargin = pad, TopMargin = pad });
        SetContentView(root);
        HideSystemBars();

        _renderer = new SceneRenderer(scene, api, Resources.DisplayMetrics.Density);
        _renderer.StatusUpdated += (_, e) => RunOnUiThread(() =>
            _status.Text = $"{_label}\n{e.Status.CurrentFps} FPS · {e.Status.TotalFrames} frames");
        _renderer.Failed += (_, e) => RunOnUiThread(() =>
            _status.Text = $"{_label}\n{e.GetType().Name}: {e.Message}");
        _renderer.Start();
    }

    private void HideSystemBars()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            var controller = Window!.InsetsController;
            if (controller is null)
                return;
            controller.Hide(WindowInsets.Type.StatusBars() | WindowInsets.Type.NavigationBars());
            controller.SystemBarsBehavior = (int)WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
        }
        else
        {
            Window!.DecorView.SystemUiFlags = (SystemUiFlags.Fullscreen |
                SystemUiFlags.HideNavigation | SystemUiFlags.ImmersiveSticky | SystemUiFlags.LayoutStable |
                SystemUiFlags.LayoutFullscreen | SystemUiFlags.LayoutHideNavigation);
        }
    }

    public void SurfaceCreated(ISurfaceHolder holder) => _renderer?.SurfaceCreated(holder.Surface!);

    public void SurfaceChanged(ISurfaceHolder holder, Format format, int width, int height)
    {
        // The renderer reads the window size every frame.
    }

    public void SurfaceDestroyed(ISurfaceHolder holder) => _renderer?.SurfaceDestroyed();

    protected override void OnDestroy()
    {
        _renderer?.Stop();
        _renderer?.Dispose();
        _renderer = null;
        base.OnDestroy();
    }
}
