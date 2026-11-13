using System.Diagnostics;
using NImpeller;
using static Sandbox.Scenes.Draw;

namespace Sandbox.Scenes;

/// <summary>Ports of the Aiks <c>StrokeCapsAndJoins</c>, <c>FatStrokeArc</c> and <c>CanRenderCurvedStrokes</c> playgrounds, plus a miter limit sweep.</summary>
public sealed class StrokesScene : IScene, IDisposable
{
    public string Name => "Strokes";
    public string CommandLineName => "strokes";
    public string Description => "Stroke caps on lines and zero-length contours, joins at 0-180 degree turns, a fat stroked arc, miter limits and thick/thin curves";

    private const float CapsWidth = 800f, CapsHeight = 600f;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ImpellerTypographyContext _tc = ImpellerTypographyContext.New()!;

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        float t = (float)_clock.Elapsed.TotalSeconds;
        Background(b, ImpellerColor.FromRgb(17, 17, 17));

        float k = Draw.Scale(p);
        float pad = 14f * k, header = 20f * k;
        float leftW = (p.Width - pad * 3f) * 0.62f;
        float rightW = p.Width - pad * 3f - leftW;
        float h = p.Height - pad * 2f - header;
        if (leftW <= 1f || rightW <= 1f || h <= 1f) return;
        var muted = ImpellerColor.FromRgb(170, 176, 190);

        Label(b, _tc, "StrokeCapsAndJoins: butt/round/square caps, then bevel/round/miter joins", pad, pad, 13f * k, muted);
        float s = MathF.Min(leftW / CapsWidth, h / CapsHeight);
        b.Save();
        b.Translate(pad, pad + header);
        b.Scale(s, s);
        CapsAndJoins(b);
        b.Restore();

        float x = pad * 2f + leftW;
        float cellH = (p.Height - pad * 4f) / 3f - header;
        string[] labels = ["FatStrokeArc (animated width and sweep)", "Miter limit sweep (corners widen to the right)", "Thick and thin curves"];
        for (int i = 0; i < 3; i++)
        {
            float y = pad + i * (cellH + header + pad);
            Label(b, _tc, labels[i], x, y, 13f * k, muted);
            var cell = Rect(x, y + header, rightW, cellH);
            b.Save();
            b.ClipRect(cell, ImpellerClipOperation.kImpellerClipOperationIntersect);
            switch (i)
            {
                case 0: FatArc(b, cell, t); break;
                case 1: Miter(b, cell, t, k); break;
                default: Curves(b, cell, t, k); break;
            }
            b.Restore();
        }
    }

    private static void CapsAndJoins(ImpellerDisplayListBuilder b)
    {
        b.Translate(100, 0);
        b.Save();
        foreach (var cap in new[] { ImpellerStrokeCap.kImpellerStrokeCapButt, ImpellerStrokeCap.kImpellerStrokeCapRound, ImpellerStrokeCap.kImpellerStrokeCapSquare })
        {
            using var path = Path(pb =>
            {
                pb.MoveTo(Point(20, 50)); pb.LineTo(Point(50, 50));
                pb.MoveTo(Point(120, 50)); pb.LineTo(Point(120, 80));
                pb.MoveTo(Point(180, 50)); pb.LineTo(Point(180, 50));
            });
            StrokeTwice(b, path, cap, ImpellerStrokeJoin.kImpellerStrokeJoinBevel);
            b.Translate(250, 0);
        }
        b.Restore();

        b.Translate(0, 100);
        b.Save();
        foreach (var join in new[] { ImpellerStrokeJoin.kImpellerStrokeJoinBevel, ImpellerStrokeJoin.kImpellerStrokeJoinRound, ImpellerStrokeJoin.kImpellerStrokeJoinMiter })
        {
            using var path = Path(pb =>
            {
                (float, float, float, float, float, float)[] turns =
                [
                    (20, 50, 50, 50, 80, 50), (20, 150, 50, 150, 50, 180), (20, 250, 50, 250, 70, 270),
                    (20, 350, 50, 350, 30, 370), (20, 450, 50, 450, 20, 450),
                    (120, 80, 150, 80, 180, 80), (120, 180, 150, 180, 150, 150), (120, 280, 150, 280, 170, 260),
                    (120, 380, 150, 380, 130, 360), (120, 480, 150, 480, 120, 480),
                ];
                foreach (var (x0, y0, x1, y1, x2, y2) in turns)
                {
                    pb.MoveTo(Point(x0, y0));
                    pb.LineTo(Point(x1, y1));
                    pb.LineTo(Point(x2, y2));
                }
            });
            StrokeTwice(b, path, ImpellerStrokeCap.kImpellerStrokeCapSquare, join);
            b.Translate(250, 0);
        }
        b.Restore();
    }

    private static void StrokeTwice(ImpellerDisplayListBuilder b, ImpellerPath path, ImpellerStrokeCap cap, ImpellerStrokeJoin join)
    {
        using var red = Stroke(ImpellerColor.FromRgb(255, 0, 0), 20f);
        red.SetStrokeCap(cap);
        red.SetStrokeJoin(join);
        b.DrawPath(path, red);
        using var yellow = Stroke(ImpellerColor.FromRgb(255, 255, 0), 1f);
        b.DrawPath(path, yellow);
    }

    private static void FatArc(ImpellerDisplayListBuilder b, ImpellerRect cell, float t)
    {
        float size = MathF.Min(cell.Width, cell.Height) * 0.3f;
        float width = size * (0.2f + 1.6f * (0.5f + 0.5f * MathF.Sin(t * 0.7f)));
        var rect = Rect(cell.X + cell.Width / 2f - size / 2f, cell.Y + cell.Height / 2f - size / 2f, size, size);
        using var red = Fill(ImpellerColor.FromRgb(255, 0, 0));
        b.DrawRect(rect, red);
        float start = t * 40f % 360f, sweep = 30f + 240f * (0.5f + 0.5f * MathF.Sin(t * 0.5f));
        using var arc = Path(pb => pb.AddArc(rect, start, start + sweep));
        using var white = Stroke(ImpellerColor.FromRgb(255, 255, 255), width);
        b.DrawPath(arc, white);
    }

    private void Miter(ImpellerDisplayListBuilder b, ImpellerRect cell, float t, float k)
    {
        float limit = 1f + 9f * (0.5f + 0.5f * MathF.Sin(t * 0.8f));
        using var path = Path(pb =>
        {
            int n = 7;
            for (int i = 0; i <= n; i++)
            {
                float x = cell.X + cell.Width * (0.06f + 0.88f * (i * (i + 1) / 2f) / (n * (n + 1) / 2f));
                float y = cell.Y + cell.Height * (i % 2 == 0 ? 0.75f : 0.25f);
                if (i == 0) pb.MoveTo(Point(x, y));
                else pb.LineTo(Point(x, y));
            }
        });
        using var paint = Stroke(ImpellerColor.FromRgb(90, 170, 255), 12f * k);
        paint.SetStrokeJoin(ImpellerStrokeJoin.kImpellerStrokeJoinMiter);
        paint.SetStrokeMiter(limit);
        b.DrawPath(path, paint);
        using var spine = Stroke(ImpellerColor.FromRgb(255, 255, 0), 1f);
        b.DrawPath(path, spine);
        Label(b, _tc, $"miter limit {limit:0.0}", cell.X + 4f * k, cell.Y + cell.Height - 18f * k, 12f * k,
            ImpellerColor.FromRgb(170, 176, 190));
    }

    private static void Curves(ImpellerDisplayListBuilder b, ImpellerRect cell, float t, float k)
    {
        float cy = cell.Y + cell.Height / 2f;
        float r = MathF.Min(cell.Width * 0.22f, cell.Height * 0.4f);
        using (var circle = Path(pb => pb.AddOval(Rect(cell.X + cell.Width * 0.25f - r, cy - r, r * 2f, r * 2f))))
        using (var thick = Stroke(ImpellerColor.FromRgb(255, 0, 0), r * 0.25f))
        {
            b.DrawPath(circle, thick);
        }

        float x0 = cell.X + cell.Width * 0.52f, x1 = cell.X + cell.Width * 0.96f;
        float wave = cell.Height * 0.35f * MathF.Sin(t);
        for (int i = 0; i < 4; i++)
        {
            float w = i switch { 0 => 0.5f, 1 => 1f, 2 => 4f * k, _ => 16f * k };
            float yo = (i - 1.5f) * cell.Height * 0.18f;
            using var curve = Path(pb =>
            {
                pb.MoveTo(Point(x0, cy + yo));
                pb.CubicCurveTo(Point(x0 + (x1 - x0) / 3f, cy + yo - wave), Point(x0 + (x1 - x0) * 2f / 3f, cy + yo + wave), Point(x1, cy + yo));
            });
            using var paint = Stroke(Hsv(0.1f + i * 0.15f, 0.7f, 1f), w);
            paint.SetStrokeCap(ImpellerStrokeCap.kImpellerStrokeCapRound);
            b.DrawPath(curve, paint);
        }
    }

    public void Dispose() => _tc.Dispose();
}
