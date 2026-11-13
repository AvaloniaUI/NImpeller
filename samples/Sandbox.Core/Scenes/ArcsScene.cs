using System.Diagnostics;
using NImpeller;
using static Sandbox.Scenes.Draw;

namespace Sandbox.Scenes;

/// <summary>
/// Ports of the Aiks arc farm playgrounds (<c>FilledArcsRenderCorrectly</c>, <c>StrokedArcsRenderCorrectlyWith*</c>, ...)
/// cycling every few seconds, next to <c>CanRenderLinesWithCapsAnglesAndAlphas</c>.
/// </summary>
public sealed class ArcsScene : IScene, IDisposable
{
    public string Name => "Arcs";
    public string CommandLineName => "arcs";
    public string Description => "The Aiks arc farm (every start/sweep pair) cycling through fill, translucency, use-center, caps and joins, plus lines at many caps, angles and alphas";

    private const float FarmSize = 700f, LinesWidth = 800f, LinesHeight = 400f;

    private sealed record Variant(string Name, bool Stroke, bool UseCenter, bool FullCircles, float VerticalScale,
        ImpellerStrokeCap Cap, ImpellerStrokeJoin Join, float Alpha);

    private static readonly Variant[] Variants =
    [
        new("FilledArcsRenderCorrectly", false, false, false, 1f, ImpellerStrokeCap.kImpellerStrokeCapButt, ImpellerStrokeJoin.kImpellerStrokeJoinMiter, 1f),
        new("TranslucentFilledArcsRenderCorrectly", false, false, false, 1f, ImpellerStrokeCap.kImpellerStrokeCapButt, ImpellerStrokeJoin.kImpellerStrokeJoinMiter, 0.5f),
        new("FilledArcsRenderCorrectlyWithCenter", false, true, false, 1f, ImpellerStrokeCap.kImpellerStrokeCapButt, ImpellerStrokeJoin.kImpellerStrokeJoinMiter, 1f),
        new("NonSquareFilledArcsRenderCorrectly", false, false, false, 0.5f, ImpellerStrokeCap.kImpellerStrokeCapButt, ImpellerStrokeJoin.kImpellerStrokeJoinMiter, 1f),
        new("StrokedArcsRenderCorrectlyWithButtEnds", true, false, false, 1f, ImpellerStrokeCap.kImpellerStrokeCapButt, ImpellerStrokeJoin.kImpellerStrokeJoinMiter, 1f),
        new("StrokedArcsRenderCorrectlyWithSquareEnds", true, false, false, 1f, ImpellerStrokeCap.kImpellerStrokeCapSquare, ImpellerStrokeJoin.kImpellerStrokeJoinMiter, 1f),
        new("...WithTranslucencyAndRoundEnds", true, false, false, 1f, ImpellerStrokeCap.kImpellerStrokeCapRound, ImpellerStrokeJoin.kImpellerStrokeJoinMiter, 0.5f),
        new("...WithBevelJoinsAndCenter", true, true, false, 1f, ImpellerStrokeCap.kImpellerStrokeCapButt, ImpellerStrokeJoin.kImpellerStrokeJoinBevel, 1f),
        new("...WithMiterJoinsAndCenter", true, true, false, 1f, ImpellerStrokeCap.kImpellerStrokeCapButt, ImpellerStrokeJoin.kImpellerStrokeJoinMiter, 1f),
        new("...WithRoundJoinsAndCenter", true, true, false, 1f, ImpellerStrokeCap.kImpellerStrokeCapButt, ImpellerStrokeJoin.kImpellerStrokeJoinRound, 1f),
        new("StrokedArcsCoverFullArcWithButtEnds (missing rows: AddArc 360° bug)", true, false, true, 1f, ImpellerStrokeCap.kImpellerStrokeCapButt, ImpellerStrokeJoin.kImpellerStrokeJoinMiter, 1f),
    ];

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ImpellerTypographyContext _tc = ImpellerTypographyContext.New()!;

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        float t = (float)_clock.Elapsed.TotalSeconds;
        Background(b, ImpellerColor.FromRgb(255, 255, 255));

        float k = Draw.Scale(p);
        float pad = 12f * k, header = 20f * k;
        float farmW = (p.Width - pad * 3f) * 0.5f, linesW = p.Width - pad * 3f - farmW;
        float h = p.Height - pad * 2f - header;
        if (farmW <= 1f || h <= 1f) return;

        var variant = Variants[(int)(t / 4f) % Variants.Length];
        Label(b, _tc, $"{variant.Name}  ({Array.IndexOf(Variants, variant) + 1}/{Variants.Length}, every 4 s)", pad, pad, 12f * k);
        float s = MathF.Min(farmW / FarmSize, h / FarmSize);
        b.Save();
        b.Translate(pad, pad + header);
        b.Scale(s, s);
        ArcFarm(b, variant);
        b.Restore();

        float lx = pad * 2f + farmW;
        Label(b, _tc, "CanRenderLinesWithCapsAnglesAndAlphas", lx, pad, 12f * k);
        float ls = MathF.Min(linesW / LinesWidth, h / LinesHeight);
        b.Save();
        b.Translate(lx, pad + header);
        b.ClipRect(Rect(0, 0, linesW, LinesHeight * ls), ImpellerClipOperation.kImpellerClipOperationIntersect);
        b.Scale(ls, ls);
        Lines(b);
        b.Restore();
    }

    private static void ArcFarm(ImpellerDisplayListBuilder b, Variant v)
    {
        var color = new ImpellerColor { Blue = 1f, Alpha = v.Alpha };
        using var paint = v.Stroke ? Stroke(color, 6f) : Fill(color);
        paint.SetStrokeCap(v.Cap);
        paint.SetStrokeJoin(v.Join);

        b.Save();
        b.Translate(50, 50);
        var bounds = Rect(0, 0, 42, 42 * v.VerticalScale);
        for (int start = 0; start <= 360; start += 30)
        {
            b.Save();
            for (int sweep = 30; sweep <= 360; sweep += 30)
            {
                using var arc = Arc(bounds, start, v.FullCircles ? 360 : sweep, v.UseCenter);
                b.DrawPath(arc, paint);
                b.Translate(50, 0);
            }
            b.Restore();
            b.Translate(0, 50);
        }
        b.Restore();
    }

    private static ImpellerPath Arc(ImpellerRect r, float startDegrees, float sweepDegrees, bool useCenter)
    {
        if (!useCenter)
        {
            return Path(pb => pb.AddArc(r, startDegrees, startDegrees + sweepDegrees));
        }

        float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, rx = r.Width / 2f, ry = r.Height / 2f;
        return Path(pb =>
        {
            pb.MoveTo(Point(cx, cy));
            float a0 = startDegrees * MathF.PI / 180f;
            pb.LineTo(Point(cx + MathF.Cos(a0) * rx, cy + MathF.Sin(a0) * ry));
            int segments = (int)MathF.Ceiling(sweepDegrees / 90f);
            float step = sweepDegrees * MathF.PI / 180f / segments;
            float handle = 4f / 3f * MathF.Tan(step / 4f);
            for (int i = 0; i < segments; i++)
            {
                float a = a0 + i * step, b2 = a + step;
                pb.CubicCurveTo(
                    Point(cx + (MathF.Cos(a) - handle * MathF.Sin(a)) * rx, cy + (MathF.Sin(a) + handle * MathF.Cos(a)) * ry),
                    Point(cx + (MathF.Cos(b2) + handle * MathF.Sin(b2)) * rx, cy + (MathF.Sin(b2) - handle * MathF.Cos(b2)) * ry),
                    Point(cx + MathF.Cos(b2) * rx, cy + MathF.Sin(b2) * ry));
            }
            pb.Close();
        });
    }

    private static void Lines(ImpellerDisplayListBuilder b)
    {
        using (var black = Fill(ImpellerColor.FromRgb(0, 0, 0)))
        {
            b.DrawRect(Rect(0, 0, LinesWidth, LinesHeight), black);
        }
        const float length = 30f, spacing = 50f;
        ImpellerStrokeCap[] caps = [ImpellerStrokeCap.kImpellerStrokeCapButt, ImpellerStrokeCap.kImpellerStrokeCapSquare, ImpellerStrokeCap.kImpellerStrokeCapRound];
        float[] alphas = [1f, 0.75f, 0.5f, 0.25f];
        float[] angles = [0f, 3f, 30f, 45f, 60f, 87f, 90f];

        b.Save();
        foreach (var cap in caps)
        {
            b.Translate(spacing, 0);
            foreach (var alpha in alphas)
            {
                b.Translate(spacing, 0);
                using var paint = Stroke(new ImpellerColor { Red = 1, Green = 1, Blue = 1, Alpha = alpha }, 10f);
                paint.SetStrokeCap(cap);
                b.Save();
                foreach (var degrees in angles)
                {
                    b.Translate(0, spacing);
                    float a = degrees * MathF.PI / 180f;
                    b.DrawLine(Point(0, 0), Point(MathF.Cos(a) * length, MathF.Sin(a) * length), paint);
                }
                b.Restore();
            }
        }
        b.Restore();
    }

    public void Dispose() => _tc.Dispose();
}
