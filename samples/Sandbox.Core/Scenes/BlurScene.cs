using System.Diagnostics;
using NImpeller;
using static Sandbox.Scenes.Draw;

namespace Sandbox.Scenes;

/// <summary>
/// After the Aiks <c>GradientOvalStrokeMaskBlur*</c>, <c>CanRenderClippedBlur</c>, <c>GaussianBlurRotatedAndClipped</c>,
/// <c>GaussianBlurOneDimension</c> and <c>CanRenderMaskBlurHugeSigma</c> playgrounds.
/// </summary>
public sealed class BlurScene : IScene, IDisposable
{
    public string Name => "Blur";
    public string CommandLineName => "blur";
    public string Description => "Mask blur styles on rotated gradient strokes, and clipped, rotated, one-axis and huge-sigma blurs";

    private const float Unit = 300f;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ImpellerTypographyContext _tc = ImpellerTypographyContext.New()!;
    private readonly SampleTexture _texture = new();

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        float t = (float)_clock.Elapsed.TotalSeconds;
        Background(b, ImpellerColor.FromRgb(240, 240, 244));

        float k = Draw.Scale(p);
        float pad = 12f * k, header = 20f * k;
        const int cols = 4, rows = 2;
        float cw = (p.Width - pad * (cols + 1)) / cols;
        float ch = (p.Height - pad * (rows + 1)) / rows - header;
        if (cw <= 1f || ch <= 1f) return;
        float s = MathF.Min(cw, ch) / Unit;
        var texture = _texture.Get(context);

        (ImpellerBlurStyle Style, string Name)[] styles =
        [
            (ImpellerBlurStyle.kImpellerBlurStyleNormal, "Normal"),
            (ImpellerBlurStyle.kImpellerBlurStyleSolid, "Solid"),
            (ImpellerBlurStyle.kImpellerBlurStyleOuter, "Outer"),
            (ImpellerBlurStyle.kImpellerBlurStyleInner, "Inner"),
        ];
        string[] bottom = ["Clipped blur (decal)", "Rotated + clipped blur", "One-axis backdrop blur", "Huge sigma mask blur"];

        for (int i = 0; i < cols * rows; i++)
        {
            float x = pad + (i % cols) * (cw + pad);
            float y = pad + (i / cols) * (ch + header + pad);
            Label(b, _tc, i < cols ? $"Gradient stroke, mask blur {styles[i].Name}" : bottom[i - cols], x, y, 12f * k);

            var cell = Rect(x, y + header, cw, ch);
            b.Save();
            b.ClipRect(cell, ImpellerClipOperation.kImpellerClipOperationIntersect);
            b.Translate(x + (cw - Unit * s) / 2f, y + header);
            b.Scale(s, s);
            switch (i)
            {
                case < 4: GradientStroke(b, styles[i].Style, t); break;
                case 4: ClippedBlur(b, t); break;
                case 5: RotatedClipped(b, texture, t); break;
                case 6: OneDimension(b, texture, t); break;
                default: HugeSigma(b, t); break;
            }
            b.Restore();
        }
    }

    private static void GradientStroke(ImpellerDisplayListBuilder b, ImpellerBlurStyle style, float t)
    {
        b.Translate(150, 150);
        b.Rotate(t * 15f);
        ReadOnlySpan<ImpellerColor> colors = [ImpellerColor.FromRgb(0, 120, 255), ImpellerColor.FromRgb(255, 60, 120)];
        ReadOnlySpan<float> stops = [0f, 1f];
        using var source = ImpellerColorSource.CreateLinearGradientNew(Point(-100, 0), Point(100, 0), colors, stops,
            ImpellerTileMode.kImpellerTileModeClamp)!;
        using var mask = ImpellerMaskFilter.CreateBlurNew(style, 8f + 6f * MathF.Sin(t))!;
        using var paint = Stroke(ImpellerColor.FromRgb(255, 255, 255), 24f);
        paint.SetColorSource(source);
        paint.SetMaskFilter(mask);
        b.DrawOval(Rect(-100, -70, 200, 140), paint);
    }

    private static void ClippedBlur(ImpellerDisplayListBuilder b, float t)
    {
        b.ClipRect(Rect(40, 60, 200, 200), ImpellerClipOperation.kImpellerClipOperationIntersect);
        using var filter = ImpellerImageFilter.CreateBlurNew(20, 20, ImpellerTileMode.kImpellerTileModeDecal)!;
        using var paint = Fill(ImpellerColor.FromRgb(0, 200, 0));
        paint.SetImageFilter(filter);
        float r = 100f, cx = 190f + 20f * MathF.Sin(t), cy = 190f;
        b.DrawOval(Rect(cx - r, cy - r, r * 2f, r * 2f), paint);
        using var frame = Stroke(ImpellerColor.FromRgb(160, 160, 170), 2f);
        b.DrawRect(Rect(40, 60, 200, 200), frame);
    }

    private static void RotatedClipped(ImpellerDisplayListBuilder b, ImpellerTexture texture, float t)
    {
        b.ClipRect(Rect(40, 80, 220, 140), ImpellerClipOperation.kImpellerClipOperationIntersect);
        b.Translate(150, 150);
        b.Scale(0.8f, 0.8f);
        b.Rotate(25f + 10f * MathF.Sin(t));
        using var filter = ImpellerImageFilter.CreateBlurNew(12, 12, ImpellerTileMode.kImpellerTileModeDecal)!;
        using var paint = ImpellerPaint.New()!;
        paint.SetImageFilter(filter);
        float half = SampleTexture.Size / 2f;
        b.DrawTextureRect(texture, Rect(0, 0, SampleTexture.Size, SampleTexture.Size), Rect(-half, -half, half * 2f, half * 2f),
            ImpellerTextureSampling.kImpellerTextureSamplingLinear, paint);
    }

    private static void OneDimension(ImpellerDisplayListBuilder b, ImpellerTexture texture, float t)
    {
        using (var plain = ImpellerPaint.New()!)
        {
            b.DrawTextureRect(texture, Rect(0, 0, SampleTexture.Size, SampleTexture.Size), Rect(20, 20, 260, 260),
                ImpellerTextureSampling.kImpellerTextureSamplingLinear, plain);
        }
        b.ClipRect(Rect(20, 150, 260, 130), ImpellerClipOperation.kImpellerClipOperationIntersect);
        using var paint = ImpellerPaint.New()!;
        paint.SetBlendMode(ImpellerBlendMode.kImpellerBlendModeSource);
        using var filter = ImpellerImageFilter.CreateBlurNew(4f + 20f * (0.5f + 0.5f * MathF.Sin(t)), 0.01f,
            ImpellerTileMode.kImpellerTileModeClamp)!;
        b.SaveLayer(Rect(0, 0, Unit, Unit), paint, filter);
        b.Restore();
    }

    private static void HugeSigma(ImpellerDisplayListBuilder b, float t)
    {
        using var mask = ImpellerMaskFilter.CreateBlurNew(ImpellerBlurStyle.kImpellerBlurStyleNormal, 60f + 40f * MathF.Sin(t))!;
        using var paint = Fill(ImpellerColor.FromRgb(240, 80, 30));
        paint.SetMaskFilter(mask);
        b.DrawRoundedRect(Rect(75, 75, 150, 150), Radii(20), paint);
    }

    public void Dispose()
    {
        _texture.Dispose();
        _tc.Dispose();
    }
}
