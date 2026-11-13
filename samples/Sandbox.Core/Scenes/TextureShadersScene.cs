using System.Diagnostics;
using System.Numerics;
using NImpeller;
using static Sandbox.Scenes.Draw;

namespace Sandbox.Scenes;

/// <summary>
/// Port of the interop <c>CanCreateFragmentProgramColorFilters</c> playground (a fragment program sampling a texture),
/// plus an animated sampler shader and the image color source in each tile mode.
/// </summary>
public sealed class TextureShadersScene : IScene, IDisposable
{
    public string Name => "Texture Shaders";
    public string CommandLineName => "textureshaders";
    public string Description => "Fragment programs that sample a texture (upstream port and animated swirl), and image color sources in clamp/repeat/mirror/decal";

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ImpellerTypographyContext _tc = ImpellerTypographyContext.New()!;
    private readonly SampleTexture _texture = new();
    private readonly Lazy<ImpellerFragmentProgram?> _sample = new(() => LoadProgram("texture_sample"));
    private readonly Lazy<ImpellerFragmentProgram?> _swirl = new(() => LoadProgram("swirl"));

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        float t = (float)_clock.Elapsed.TotalSeconds;
        Background(b, ImpellerColor.FromRgb(24, 26, 32));

        float k = Draw.Scale(p);
        float pad = 16f * k, header = 20f * k;
        float topH = (p.Height - pad * 3f) * 0.55f - header;
        float botH = (p.Height - pad * 3f) * 0.45f - header;
        float halfW = (p.Width - pad * 3f) / 2f;
        float quarterW = (p.Width - pad * 5f) / 4f;
        if (halfW <= 1f || topH <= 1f || botH <= 1f) return;

        var texture = _texture.Get(context);
        var muted = ImpellerColor.FromRgb(170, 176, 190);

        Label(b, _tc, "texture_sample.frag (upstream interop_runtime_stage_cs)", pad, pad, 13f * k, muted);
        DrawProgram(context, b, _sample.Value, texture, Rect(pad, pad + header, halfW, topH), []);
        Label(b, _tc, "swirl.frag (uSize, uTime, uTexture)", pad * 2f + halfW, pad, 13f * k, muted);
        DrawProgram(context, b, _swirl.Value, texture, Rect(pad * 2f + halfW, pad + header, halfW, topH), [t]);

        (ImpellerTileMode Mode, string Name)[] modes =
        [
            (ImpellerTileMode.kImpellerTileModeClamp, "Clamp"),
            (ImpellerTileMode.kImpellerTileModeRepeat, "Repeat"),
            (ImpellerTileMode.kImpellerTileModeMirror, "Mirror"),
            (ImpellerTileMode.kImpellerTileModeDecal, "Decal"),
        ];
        float y = pad * 2f + header + topH;
        for (int i = 0; i < modes.Length; i++)
        {
            float x = pad + i * (quarterW + pad);
            Label(b, _tc, $"Image color source: {modes[i].Name}", x, y, 13f * k, muted);
            var cell = Rect(x, y + header, quarterW, botH);
            float s = MathF.Min(quarterW, botH) / SampleTexture.Size * 0.45f;
            var m = Matrix4x4.CreateScale(s, s, 1f)
                    * Matrix4x4.CreateRotationZ(MathF.Sin(t * 0.5f) * 0.3f)
                    * Matrix4x4.CreateTranslation(cell.X + cell.Width * 0.3f, cell.Y + cell.Height * 0.3f, 0f);
            using var source = texture.ColorSourceCreateImageNew(modes[i].Mode, modes[i].Mode,
                ImpellerTextureSampling.kImpellerTextureSamplingLinear, m)!;
            using var paint = ImpellerPaint.New()!;
            paint.SetColorSource(source);
            b.DrawRoundedRect(cell, Radii(12f * k), paint);
        }
    }

    private static void DrawProgram(ImpellerContext context, ImpellerDisplayListBuilder b, ImpellerFragmentProgram? program,
        ImpellerTexture texture, ImpellerRect r, ReadOnlySpan<float> extra)
    {
        if (program is null) return;
        Span<float> uniforms = stackalloc float[2 + extra.Length];
        uniforms[0] = r.Width;
        uniforms[1] = r.Height;
        extra.CopyTo(uniforms[2..]);
        using var source = ImpellerColorSource.CreateFragmentProgramNew(context, program, [texture], uniforms);
        if (source is null) return;

        using var paint = ImpellerPaint.New()!;
        paint.SetColorSource(source);
        b.Save();
        b.Translate(r.X, r.Y);
        b.DrawRect(Rect(0, 0, r.Width, r.Height), paint);
        b.Restore();
    }

    private static ImpellerFragmentProgram? LoadProgram(string name)
    {
        using var stream = typeof(TextureShadersScene).Assembly.GetManifestResourceStream($"Shaders/{name}.frag.iplr")
            ?? throw new InvalidOperationException($"Missing embedded shader '{name}'.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        using var mem = new ImpellerUnmanagedMemory(copy.GetBuffer().AsSpan(0, (int)copy.Length));
        return ImpellerFragmentProgram.New(mem);
    }

    public void Dispose()
    {
        _texture.Dispose();
        _tc.Dispose();
        if (_sample.IsValueCreated) _sample.Value?.Dispose();
        if (_swirl.IsValueCreated) _swirl.Value?.Dispose();
    }
}
