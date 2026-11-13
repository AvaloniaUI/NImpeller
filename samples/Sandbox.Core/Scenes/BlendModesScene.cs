using System.Diagnostics;
using NImpeller;
using static Sandbox.Scenes.Draw;

namespace Sandbox.Scenes;

/// <summary>Every blend mode, after the Aiks <c>PaintBlendModeIsRespected</c> and <c>ColorFilterBlend</c> playgrounds.</summary>
public sealed class BlendModesScene : IScene, IDisposable
{
    public string Name => "Blend Modes";
    public string CommandLineName => "blendmodes";
    public string Description => "All 29 blend modes: a gradient source over a destination circle inside an isolated layer, plus the same modes as a blend color filter";

    private const int Cols = 6;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ImpellerTypographyContext _tc = ImpellerTypographyContext.New()!;
    private bool _asColorFilter;
    private double _lastSwitch;

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        double now = _clock.Elapsed.TotalSeconds;
        float t = (float)now;
        if (now - _lastSwitch > 6)
        {
            _asColorFilter = !_asColorFilter;
            _lastSwitch = now;
        }

        Background(b, ImpellerColor.FromRgb(36, 38, 46));
        var modes = Enum.GetValues<ImpellerBlendMode>();

        float k = MathF.Max(1f, p.Height / 640f);
        float pad = 10f * k, header = 22f * k, label = 16f * k;
        int rows = (modes.Length + Cols - 1) / Cols;
        float cw = (p.Width - pad * (Cols + 1)) / Cols;
        float ch = (p.Height - header - pad * (rows + 1)) / rows - label;
        if (cw <= 1f || ch <= 1f) return;

        Label(b, _tc, _asColorFilter
                ? "Blend color filter (orange) applied to the circle; switches every 6 s"
                : "Paint blend mode: gradient rect over circle, inside a SaveLayer; switches every 6 s",
            pad, pad, 13f * k, ImpellerColor.FromRgb(200, 204, 214));

        for (int i = 0; i < modes.Length; i++)
        {
            float x = pad + (i % Cols) * (cw + pad);
            float y = header + pad + (i / Cols) * (ch + label + pad);
            var cell = Rect(x, y, cw, ch);
            Checkerboard(b, cell, 8f * k);

            if (_asColorFilter) DrawColorFilter(b, cell, modes[i]);
            else DrawPaintBlend(b, cell, modes[i], t);

            Label(b, _tc, modes[i].ToString()["kImpellerBlendMode".Length..], x, y + ch + 2f * k, 12f * k,
                ImpellerColor.FromRgb(200, 204, 214));
        }
    }

    private static void DrawPaintBlend(ImpellerDisplayListBuilder b, ImpellerRect cell, ImpellerBlendMode mode, float t)
    {
        float s = MathF.Min(cell.Width, cell.Height);
        float cx = cell.X + cell.Width / 2f, cy = cell.Y + cell.Height / 2f;

        b.SaveLayer(cell, null, null);
        using (var dst = Fill(ImpellerColor.FromRgb(40, 140, 240)))
        {
            b.DrawOval(Rect(cx - s * 0.42f, cy - s * 0.42f, s * 0.6f, s * 0.6f), dst);
        }

        float dx = MathF.Sin(t) * s * 0.08f;
        var src = Rect(cx - s * 0.12f + dx, cy - s * 0.12f, s * 0.55f, s * 0.55f);
        ReadOnlySpan<ImpellerColor> colors =
        [
            ImpellerColor.FromArgb(255, 250, 80, 40),
            ImpellerColor.FromArgb(160, 255, 220, 40),
        ];
        ReadOnlySpan<float> stops = [0f, 1f];
        using var gradient = ImpellerColorSource.CreateLinearGradientNew(
            Point(src.X, src.Y), Point(src.X + src.Width, src.Y + src.Height), colors, stops,
            ImpellerTileMode.kImpellerTileModeClamp)!;
        using var paint = ImpellerPaint.New()!;
        paint.SetColorSource(gradient);
        paint.SetBlendMode(mode);
        b.DrawRect(src, paint);
        b.Restore();
    }

    private static void DrawColorFilter(ImpellerDisplayListBuilder b, ImpellerRect cell, ImpellerBlendMode mode)
    {
        float s = MathF.Min(cell.Width, cell.Height) * 0.8f;
        // Null for modes where the filter is a no-op (e.g. Destination).
        using var filter = ImpellerColorFilter.CreateBlendNew(ImpellerColor.FromArgb(200, 250, 140, 30), mode);
        using var paint = Fill(ImpellerColor.FromRgb(40, 140, 240));
        if (filter is not null) paint.SetColorFilter(filter);
        b.DrawOval(Rect(cell.X + (cell.Width - s) / 2f, cell.Y + (cell.Height - s) / 2f, s, s), paint);
    }

    private static void Checkerboard(ImpellerDisplayListBuilder b, ImpellerRect r, float size)
    {
        using var light = Fill(ImpellerColor.FromRgb(90, 92, 100));
        using var dark = Fill(ImpellerColor.FromRgb(64, 66, 74));
        b.DrawRect(r, dark);
        b.Save();
        b.ClipRect(r, ImpellerClipOperation.kImpellerClipOperationIntersect);
        for (int row = 0; row * size < r.Height; row++)
        {
            for (int col = row % 2; col * size < r.Width; col += 2)
            {
                b.DrawRect(Rect(r.X + col * size, r.Y + row * size, size, size), light);
            }
        }
        b.Restore();
    }

    public void Dispose() => _tc.Dispose();
}
