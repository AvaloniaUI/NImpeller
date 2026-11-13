using System.Diagnostics;
using NImpeller;
using static Sandbox.Scenes.Draw;

namespace Sandbox.Scenes;

/// <summary>After the Aiks <c>CanRender*Gradient{Clamp,Repeat,Mirror,Decal}</c>, stop and <c>GradientStrokesRenderCorrectly</c> playgrounds.</summary>
public sealed class GradientTileModesScene : IScene, IDisposable
{
    public string Name => "Gradient Tile Modes";
    public string CommandLineName => "gradienttiles";
    public string Description => "Linear, radial, sweep and conical gradients in every tile mode, plus many/uneven/overlapping stops, gradient strokes and mask blur";

    private static readonly (ImpellerTileMode Mode, string Name)[] Modes =
    [
        (ImpellerTileMode.kImpellerTileModeClamp, "Clamp"),
        (ImpellerTileMode.kImpellerTileModeRepeat, "Repeat"),
        (ImpellerTileMode.kImpellerTileModeMirror, "Mirror"),
        (ImpellerTileMode.kImpellerTileModeDecal, "Decal"),
    ];

    private static readonly string[] Kinds = ["Linear", "Radial", "Sweep", "Conical"];

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ImpellerTypographyContext _tc = ImpellerTypographyContext.New()!;

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        float t = (float)_clock.Elapsed.TotalSeconds;
        Background(b, ImpellerColor.FromRgb(28, 30, 38));

        float k = Draw.Scale(p);
        float pad = 10f * k, labelW = 64f * k, header = 18f * k;
        const int cols = 5, rows = 5;
        float cw = (p.Width - labelW - pad * (cols + 1)) / cols;
        float ch = (p.Height - header - pad * (rows + 1)) / rows;
        if (cw <= 1f || ch <= 1f) return;
        var muted = ImpellerColor.FromRgb(170, 176, 190);

        string[] colLabels = ["Clamp", "Repeat", "Mirror", "Decal", "Uneven stops"];
        for (int c = 0; c < cols; c++)
        {
            Label(b, _tc, colLabels[c], labelW + pad + c * (cw + pad), pad * 0.5f, 12f * k, muted);
        }

        for (int r = 0; r < Kinds.Length; r++)
        {
            float y = header + pad + r * (ch + pad);
            Label(b, _tc, Kinds[r], pad, y + ch / 2f - 8f * k, 13f * k, muted);
            for (int c = 0; c < cols; c++)
            {
                var cell = Rect(labelW + pad + c * (cw + pad), y, cw, ch);
                bool uneven = c == 4;
                using var source = Gradient(r, cell, uneven ? ImpellerTileMode.kImpellerTileModeRepeat : Modes[c].Mode, uneven, t);
                using var paint = ImpellerPaint.New()!;
                paint.SetColorSource(source);
                b.DrawRoundedRect(cell, Radii(8f * k), paint);
            }
        }

        float ly = header + pad + 4 * (ch + pad);
        Label(b, _tc, "Extras", pad, ly + ch / 2f - 8f * k, 13f * k, muted);
        string[] extras = ["Overlapping stops", "7 colors", "Gradient strokes", "Linear + mask blur", "Decal + color filter"];
        for (int c = 0; c < cols; c++)
        {
            var cell = Rect(labelW + pad + c * (cw + pad), ly, cw, ch);
            DrawExtra(b, c, cell, t, k);
            Label(b, _tc, extras[c], cell.X + 4f * k, cell.Y + cell.Height - 16f * k, 11f * k, ImpellerColor.FromRgb(240, 240, 245));
        }
    }

    private static ImpellerColorSource Gradient(int kind, ImpellerRect cell, ImpellerTileMode mode, bool uneven, float t)
    {
        ReadOnlySpan<ImpellerColor> colors =
        [
            ImpellerColor.FromRgb(245, 70, 80),
            ImpellerColor.FromRgb(250, 210, 60),
            ImpellerColor.FromRgb(60, 140, 250),
        ];
        ReadOnlySpan<float> stops = uneven ? [0f, 0.15f, 1f] : [0f, 0.5f, 1f];
        float cx = cell.X + cell.Width / 2f, cy = cell.Y + cell.Height / 2f;
        float r = MathF.Min(cell.Width, cell.Height) * 0.25f;
        float wobble = MathF.Sin(t * 0.8f) * r * 0.3f;
        return (kind switch
        {
            0 => ImpellerColorSource.CreateLinearGradientNew(Point(cx - r + wobble, cy - r * 0.3f), Point(cx + r * 0.3f + wobble, cy + r * 0.3f), colors, stops, mode),
            1 => ImpellerColorSource.CreateRadialGradientNew(Point(cx + wobble, cy), r * 0.8f, colors, stops, mode),
            2 => ImpellerColorSource.CreateSweepGradientNew(Point(cx, cy), 30f + t * 20f % 360f, 150f + t * 20f % 360f, colors, stops, mode),
            _ => ImpellerColorSource.CreateConicalGradientNew(Point(cx - r * 0.3f + wobble * 0.5f, cy), r * 0.1f, Point(cx + r * 0.2f, cy), r * 0.9f, colors, stops, mode),
        })!;
    }

    private static void DrawExtra(ImpellerDisplayListBuilder b, int index, ImpellerRect cell, float t, float k)
    {
        var start = Point(cell.X, cell.Y);
        var end = Point(cell.X + cell.Width, cell.Y + cell.Height);
        switch (index)
        {
            case 0:
            {
                ReadOnlySpan<ImpellerColor> colors =
                [
                    ImpellerColor.FromRgb(255, 0, 0), ImpellerColor.FromRgb(255, 0, 0),
                    ImpellerColor.FromRgb(0, 0, 255), ImpellerColor.FromRgb(0, 0, 255),
                ];
                float mid = 0.5f + 0.2f * MathF.Sin(t);
                ReadOnlySpan<float> stops = [0f, mid, mid, 1f];
                using var source = ImpellerColorSource.CreateLinearGradientNew(start, end, colors, stops, ImpellerTileMode.kImpellerTileModeClamp)!;
                using var paint = ImpellerPaint.New()!;
                paint.SetColorSource(source);
                b.DrawRect(cell, paint);
                break;
            }
            case 1:
            {
                Span<ImpellerColor> colors = stackalloc ImpellerColor[7];
                Span<float> stops = stackalloc float[7];
                for (int i = 0; i < 7; i++)
                {
                    colors[i] = Hsv(i / 7f + t * 0.05f, 0.8f, 1f);
                    stops[i] = i / 6f;
                }
                using var source = ImpellerColorSource.CreateLinearGradientNew(start, Point(cell.X + cell.Width, cell.Y), colors, stops, ImpellerTileMode.kImpellerTileModeClamp)!;
                using var paint = ImpellerPaint.New()!;
                paint.SetColorSource(source);
                b.DrawRect(cell, paint);
                break;
            }
            case 2:
            {
                using var source = TwoColor(start, end);
                using var paint = Stroke(ImpellerColor.FromRgb(255, 255, 255), 8f * k);
                paint.SetColorSource(source);
                paint.SetStrokeCap(ImpellerStrokeCap.kImpellerStrokeCapRound);
                float inset = 14f * k;
                b.DrawOval(Rect(cell.X + inset, cell.Y + inset, cell.Width - inset * 2f, cell.Height - inset * 2f), paint);
                b.DrawLine(Point(cell.X + inset, cell.Y + inset), Point(cell.X + cell.Width - inset, cell.Y + cell.Height - inset), paint);
                break;
            }
            case 3:
            {
                using var source = TwoColor(start, end);
                using var mask = ImpellerMaskFilter.CreateBlurNew(ImpellerBlurStyle.kImpellerBlurStyleNormal, (4f + 4f * MathF.Sin(t)) * k + 1f)!;
                using var paint = ImpellerPaint.New()!;
                paint.SetColorSource(source);
                paint.SetMaskFilter(mask);
                float inset = 14f * k;
                b.DrawRoundedRect(Rect(cell.X + inset, cell.Y + inset, cell.Width - inset * 2f, cell.Height - inset * 2f), Radii(10f * k), paint);
                break;
            }
            default:
            {
                ReadOnlySpan<ImpellerColor> colors = [ImpellerColor.FromRgb(255, 0, 0), ImpellerColor.FromRgb(0, 255, 0)];
                ReadOnlySpan<float> stops = [0f, 1f];
                float cx = cell.X + cell.Width / 2f;
                using var source = ImpellerColorSource.CreateLinearGradientNew(Point(cx - cell.Width * 0.2f, cell.Y), Point(cx + cell.Width * 0.2f, cell.Y),
                    colors, stops, ImpellerTileMode.kImpellerTileModeDecal)!;
                using var filter = ImpellerColorFilter.CreateBlendNew(ImpellerColor.FromRgb(60, 60, 255), ImpellerBlendMode.kImpellerBlendModeScreen)!;
                using var paint = ImpellerPaint.New()!;
                paint.SetColorSource(source);
                paint.SetColorFilter(filter);
                b.DrawRect(cell, paint);
                break;
            }
        }
    }

    private static ImpellerColorSource TwoColor(ImpellerPoint start, ImpellerPoint end)
    {
        ReadOnlySpan<ImpellerColor> colors = [ImpellerColor.FromRgb(255, 120, 0), ImpellerColor.FromRgb(120, 0, 255)];
        ReadOnlySpan<float> stops = [0f, 1f];
        return ImpellerColorSource.CreateLinearGradientNew(start, end, colors, stops, ImpellerTileMode.kImpellerTileModeClamp)!;
    }

    public void Dispose() => _tc.Dispose();
}
