using System.Diagnostics;
using NImpeller;
using static Sandbox.Scenes.Draw;

namespace Sandbox.Scenes;

/// <summary>
/// Ports of the Aiks runtime-effect playgrounds (aiks_dl_runtime_effect_unittests.cc) using the upstream fixture shaders.
/// The ComposePaint tests use a color-filter image filter, which the C API lacks, so the last cell composes the warp
/// filter with a blur instead.
/// </summary>
public sealed class RuntimeEffectsScene : IScene, IDisposable
{
    public string Name => "Runtime Effects";
    public string CommandLineName => "runtimeeffects";
    public string Description => "The Aiks runtime-effect playgrounds: clipped shader fills, DrawPaint bounds, shader image filters, composed backdrop filters, rotated filters and vec4 array uniforms";

    private static readonly (string Label, float Width, float Height)[] Cells =
    [
        ("CanRenderClippedRuntimeEffects", 400, 400),
        ("DrawPaintTransformsBounds", 400, 400),
        ("CanRenderRuntimeEffectFilter", 400, 400),
        ("ComposeBackdropRuntimeOuterBlurInner", 450, 450),
        ("ClippedBackdropFilterWithShader", 400, 400),
        ("RuntimeEffectImageFilterRotated", 370, 370),
        ("RuntimeEffectVectorArray", 400, 400),
        ("Warp filter composed with blur", 450, 450),
    ];

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ImpellerTypographyContext _tc = ImpellerTypographyContext.New()!;
    private readonly SampleTexture _texture = new();
    private readonly Dictionary<string, ImpellerFragmentProgram?> _programs = new();

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        float t = (float)_clock.Elapsed.TotalSeconds;
        Background(b, ImpellerColor.FromRgb(40, 42, 50));

        float k = Draw.Scale(p);
        float pad = 10f * k, header = 18f * k;
        const int cols = 4, rows = 2;
        float cw = (p.Width - pad * (cols + 1)) / cols;
        float ch = (p.Height - pad * (rows + 1)) / rows - header;
        if (cw <= 1f || ch <= 1f) return;
        var texture = _texture.Get(context);

        for (int i = 0; i < Cells.Length; i++)
        {
            float x = pad + (i % cols) * (cw + pad);
            float y = pad + (i / cols) * (ch + header + pad);
            Label(b, _tc, Cells[i].Label, x, y, 11f * k, ImpellerColor.FromRgb(200, 204, 214));

            float s = MathF.Min(cw / Cells[i].Width, ch / Cells[i].Height);
            float w = Cells[i].Width * s, h = Cells[i].Height * s;
            float ox = x + (cw - w) / 2f, oy = y + header + (ch - h) / 2f;
            b.Save();
            b.ClipRect(Rect(ox, oy, w, h), ImpellerClipOperation.kImpellerClipOperationIntersect);
            using (var bg = Fill(ImpellerColor.FromRgb(26, 26, 26)))
            {
                b.DrawPaint(bg);
            }
            b.Translate(ox, oy);
            b.Scale(s, s);
            DrawCell(context, b, i, texture, t, Cells[i].Width, Cells[i].Height);
            b.Restore();
        }
    }

    private void DrawCell(ImpellerContext context, ImpellerDisplayListBuilder b, int index, ImpellerTexture texture, float t, float w, float h)
    {
        var unbounded = Rect(0, 0, w, h);
        switch (index)
        {
            case 0:
            {
                using var source = Source(context, "runtime_stage_example", [], [400, 400, t * 0.25f]);
                if (source is null) return;
                using var paint = ImpellerPaint.New()!;
                paint.SetColorSource(source);
                b.Save();
                b.ClipRoundedRect(Rect(0, 0, 400, 400), Radii(10), ImpellerClipOperation.kImpellerClipOperationIntersect);
                b.DrawRect(Rect(0, 0, 400, 400), paint);
                b.Restore();
                break;
            }
            case 1:
            {
                using var source = Source(context, "gradient", [], [400, 400]);
                if (source is null) return;
                using var paint = ImpellerPaint.New()!;
                paint.SetColorSource(source);
                b.DrawPaint(paint);
                break;
            }
            case 2:
            {
                using var filter = Filter(context, "runtime_stage_filter_example", texture, [0, 0]);
                if (filter is null) return;
                using var paint = Fill(ImpellerColor.FromRgb(0, 255, 255));
                paint.SetImageFilter(filter);
                b.DrawRect(Rect(0, 0, 400, 400), paint);
                break;
            }
            case 3:
            {
                using (var plain = ImpellerPaint.New()!)
                {
                    b.DrawTextureRect(texture, Rect(0, 0, SampleTexture.Size, SampleTexture.Size), Rect(100, 100, 300, 300),
                        ImpellerTextureSampling.kImpellerTextureSamplingNearestNeighbor, plain);
                }
                using var circle = Filter(context, "runtime_stage_filter_circle", texture, [1, 1, 30, 30]);
                if (circle is null) return;
                float sigma = 11f + 9f * MathF.Sin(t);
                using var blur = ImpellerImageFilter.CreateBlurNew(sigma, sigma, ImpellerTileMode.kImpellerTileModeClamp)!;
                using var backdrop = circle.CreateComposeNew(blur)!;
                using (var save = ImpellerPaint.New()!)
                {
                    save.SetBlendMode(ImpellerBlendMode.kImpellerBlendModeSource);
                    b.SaveLayer(unbounded, save, backdrop);
                }
                b.Restore();
                using var green = Stroke(ImpellerColor.FromRgb(0, 255, 0), 1f);
                b.DrawLine(Point(100, 100), Point(200, 100), green);
                b.DrawLine(Point(100, 100), Point(100, 200), green);
                break;
            }
            case 4:
            {
                using (var white = Fill(ImpellerColor.FromRgb(255, 255, 255))) b.DrawPaint(white);
                using (var red = Fill(ImpellerColor.FromRgb(255, 0, 0))) b.DrawRect(Rect(0, 0, 200, 200), red);
                using (var blue = Fill(ImpellerColor.FromRgb(0, 0, 255))) b.DrawRect(Rect(200, 200, 200, 200), blue);
                using var filter = Filter(context, "runtime_stage_border", texture, [400, 400]);
                if (filter is null) return;
                b.Save();
                b.ClipRect(Rect(66, 66, 268, 268), ImpellerClipOperation.kImpellerClipOperationIntersect);
                using (var save = ImpellerPaint.New()!) b.SaveLayer(unbounded, save, filter);
                b.Restore();
                b.Restore();
                break;
            }
            case 5:
            {
                float size = SampleTexture.Size;
                using var filter = Filter(context, "gradient", texture, [size, size]);
                if (filter is null) return;
                b.Translate(w / 2f, h / 2f);
                b.Rotate(t * 20f);
                b.Translate(-size / 2f, -size / 2f);
                using var paint = ImpellerPaint.New()!;
                paint.SetImageFilter(filter);
                b.DrawTextureRect(texture, Rect(0, 0, size, size), Rect(0, 0, size, size),
                    ImpellerTextureSampling.kImpellerTextureSamplingNearestNeighbor, paint);
                break;
            }
            case 6:
            {
                using var source = Source(context, "runtime_stage_vector_array", [], [400, 400, 0.25f, 0.5f, 0.75f, 1f]);
                if (source is null) return;
                using var paint = ImpellerPaint.New()!;
                paint.SetColorSource(source);
                b.DrawRect(Rect(0, 0, 400, 400), paint);
                break;
            }
            default:
            {
                using var warp = Filter(context, "runtime_stage_filter_warp", texture, [0, 0]);
                if (warp is null) return;
                using var blur = ImpellerImageFilter.CreateBlurNew(2.5f + 2f * MathF.Sin(t), 2.5f + 2f * MathF.Sin(t),
                    ImpellerTileMode.kImpellerTileModeDecal)!;
                using var composed = warp.CreateComposeNew(blur)!;
                b.Translate(50, 50);
                b.Scale(0.7f, 0.7f);
                using var paint = ImpellerPaint.New()!;
                paint.SetImageFilter(composed);
                b.DrawTextureRect(texture, Rect(0, 0, SampleTexture.Size, SampleTexture.Size), Rect(100, 100, 400, 400),
                    ImpellerTextureSampling.kImpellerTextureSamplingNearestNeighbor, paint);
                using var green = Stroke(ImpellerColor.FromRgb(0, 255, 0), 1f);
                b.DrawLine(Point(100, 100), Point(200, 100), green);
                b.DrawLine(Point(100, 100), Point(100, 200), green);
                break;
            }
        }
    }

    private ImpellerColorSource? Source(ImpellerContext context, string name, ReadOnlySpan<ImpellerTexture> samplers, ReadOnlySpan<float> uniforms) =>
        Program(name) is { } program ? ImpellerColorSource.CreateFragmentProgramNew(context, program, samplers, uniforms) : null;

    // Sampler 0 of an image filter is replaced by the filter input; the texture only satisfies the API.
    private ImpellerImageFilter? Filter(ImpellerContext context, string name, ImpellerTexture placeholder, ReadOnlySpan<float> uniforms) =>
        Program(name) is { } program ? ImpellerImageFilter.CreateFragmentProgramNew(context, program, [placeholder], uniforms) : null;

    private ImpellerFragmentProgram? Program(string name)
    {
        if (_programs.TryGetValue(name, out var program)) return program;
        using var stream = typeof(RuntimeEffectsScene).Assembly.GetManifestResourceStream($"Shaders/{name}.frag.iplr")
            ?? throw new InvalidOperationException($"Missing embedded shader '{name}'.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        using var mem = new ImpellerUnmanagedMemory(copy.GetBuffer().AsSpan(0, (int)copy.Length));
        return _programs[name] = ImpellerFragmentProgram.New(mem);
    }

    public void Dispose()
    {
        foreach (var program in _programs.Values) program?.Dispose();
        _texture.Dispose();
        _tc.Dispose();
    }
}
