using Android.Content;
using Android.Views;
using Android.Widget;
using Sandbox.Android.Rendering;
using Sandbox.Scenes;

namespace Sandbox.Android;

/// <summary>Lists the shared Sandbox scenes; tapping one opens it in <see cref="RenderActivity"/>.</summary>
[Activity(Name = "com.nimpeller.sandbox.MainActivity", Label = "NImpeller Sandbox", MainLauncher = true, Exported = true)]
public sealed class MainActivity : Activity
{
    private readonly IScene[] _scenes = SceneCatalog.CreateAll();
    private RadioButton _vulkan = null!;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // fitsSystemWindows replaces a view's padding with the insets, so pad an inner layout.
        var frame = new FrameLayout(this);
        frame.SetFitsSystemWindows(true);
        var padding = Dp(16);
        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetPadding(padding, padding / 2, padding, 0);
        frame.AddView(root);

        var apis = new RadioGroup(this) { Orientation = Orientation.Horizontal };
        var gles = new RadioButton(this) { Text = "OpenGL ES", Id = View.GenerateViewId() };
        _vulkan = new RadioButton(this) { Text = "Vulkan", Id = View.GenerateViewId() };
        apis.AddView(gles);
        apis.AddView(_vulkan);
        apis.Check(gles.Id);
        root.AddView(apis);

        var list = new ListView(this) { Adapter = new SceneAdapter(this, _scenes) };
        list.ItemClick += (_, e) => Open(_scenes[e.Position]);
        root.AddView(list, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

        SetContentView(frame);
    }

    private void Open(IScene scene)
    {
        var intent = new Intent(this, typeof(RenderActivity));
        intent.PutExtra(RenderActivity.SceneExtra, scene.CommandLineName);
        intent.PutExtra(RenderActivity.ApiExtra, (_vulkan.Checked ? GraphicsApi.Vulkan : GraphicsApi.OpenGLES).ToString());
        StartActivity(intent);
    }

    private int Dp(float value) => (int)(value * Resources!.DisplayMetrics!.Density);

    private sealed class SceneAdapter(Context context, IScene[] scenes)
        : ArrayAdapter<IScene>(context, global::Android.Resource.Layout.SimpleListItem2, scenes)
    {
        public override View GetView(int position, View? convertView, ViewGroup parent)
        {
            var view = convertView ?? LayoutInflater.From(Context)!.Inflate(
                global::Android.Resource.Layout.SimpleListItem2, parent, false)!;
            var scene = GetItem(position)!;
            view.FindViewById<TextView>(global::Android.Resource.Id.Text1)!.Text = scene.Name;
            view.FindViewById<TextView>(global::Android.Resource.Id.Text2)!.Text = scene.Description;
            return view;
        }
    }
}
