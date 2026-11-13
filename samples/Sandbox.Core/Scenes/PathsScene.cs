using System.Diagnostics;
using NImpeller;
using static Sandbox.Scenes.Draw;

namespace Sandbox.Scenes;

/// <summary>
/// Ports of the Aiks <c>CanRenderDifferencePaths</c>, <c>CanRenderOverlappingMultiContourPath</c>, <c>CanDrawAnOpenPath</c>
/// and <c>TwoContourPathWithSinglePointContour</c> playgrounds and the interop <c>CanGetPathBounds</c> test.
/// </summary>
public sealed class PathsScene : IScene, IDisposable
{
    public string Name => "Paths";
    public string CommandLineName => "paths";
    public string Description => "Fill rules, multi-contour and open paths, single-point contours, path bounds, and CopyPathNew vs TakePathNew";

    private const float Unit = 300f;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ImpellerTypographyContext _tc = ImpellerTypographyContext.New()!;
    private readonly SampleTexture _texture = new();

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        float t = (float)_clock.Elapsed.TotalSeconds;
        Background(b, ImpellerColor.FromRgb(246, 246, 248));

        float k = Draw.Scale(p);
        float pad = 14f * k, header = 20f * k;
        const int cols = 3, rows = 2;
        float cw = (p.Width - pad * (cols + 1)) / cols;
        float ch = (p.Height - pad * (rows + 1)) / rows - header;
        if (cw <= 1f || ch <= 1f) return;
        float s = MathF.Min(cw, ch) / Unit;

        string[] labels =
        [
            "Odd fill: rrect minus circle", "Pentagram: NonZero vs Odd", "Triangle + circle contours",
            "Open path, single-point contour", "", "CopyPathNew (blue) vs TakePathNew (red)",
        ];
        for (int i = 0; i < labels.Length; i++)
        {
            float x = pad + (i % cols) * (cw + pad);
            float y = pad + (i / cols) * (ch + header + pad);
            b.Save();
            b.ClipRect(Rect(x, y + header, cw, ch), ImpellerClipOperation.kImpellerClipOperationIntersect);
            b.Translate(x + (cw - Unit * s) / 2f, y + header);
            b.Scale(s, s);
            string label = labels[i];
            switch (i)
            {
                case 0: Difference(b, _texture.Get(context)); break;
                case 1: Pentagrams(b, t); break;
                case 2: MultiContour(b); break;
                case 3: OpenPaths(b); break;
                case 4: label = Bounds(b, t); break;
                default: CopyVsTake(b); break;
            }
            b.Restore();
            Label(b, _tc, label, x, y, 13f * k);
        }
    }

    private static void Difference(ImpellerDisplayListBuilder b, ImpellerTexture texture)
    {
        using (var plain = ImpellerPaint.New()!)
        {
            b.DrawTextureRect(texture, Rect(0, 0, SampleTexture.Size, SampleTexture.Size), Rect(10, 10, 280, 280),
                ImpellerTextureSampling.kImpellerTextureSamplingLinear, plain);
        }
        var radii = new ImpellerRoundingRadii
        {
            Top_left = Point(50, 25),
            Top_right = Point(25, 50),
            Bottom_left = Point(25, 50),
            Bottom_right = Point(50, 25),
        };
        using var path = Path(pb =>
        {
            pb.AddRoundedRect(Rect(50, 50, 200, 200), radii);
            pb.AddOval(Rect(100, 100, 100, 100));
        }, ImpellerFillType.kImpellerFillTypeOdd);
        using var red = Fill(ImpellerColor.FromRgb(255, 0, 0));
        b.DrawPath(path, red);
    }

    private static void Pentagrams(ImpellerDisplayListBuilder b, float t)
    {
        foreach (var (fill, cx) in new[] { (ImpellerFillType.kImpellerFillTypeNonZero, 75f), (ImpellerFillType.kImpellerFillTypeOdd, 225f) })
        {
            using var path = Path(pb =>
            {
                for (int i = 0; i < 5; i++)
                {
                    float a = t * 0.3f - MathF.PI / 2f + i * 4f * MathF.PI / 5f;
                    var pt = Point(cx + MathF.Cos(a) * 70f, 150f + MathF.Sin(a) * 70f);
                    if (i == 0) pb.MoveTo(pt);
                    else pb.LineTo(pt);
                }
                pb.Close();
            }, fill);
            using var paint = Fill(ImpellerColor.FromRgb(120, 60, 220));
            b.DrawPath(path, paint);
        }
    }

    private static void MultiContour(ImpellerDisplayListBuilder b)
    {
        const float h = 100f;
        b.Translate(100, 100);
        using var red = Fill(ImpellerColor.FromRgb(255, 0, 0));
        using (var path = Path(pb =>
               {
                   pb.MoveTo(Point(0, h));
                   pb.LineTo(Point(-h / 2f, 0));
                   pb.LineTo(Point(h / 2f, 0));
                   pb.Close();
                   pb.AddRoundedRect(Rect(-h / 2f, -h / 2f, h, h), Radii(50));
               }))
        {
            b.DrawPath(path, red);
        }

        b.Translate(110, 0);
        using (var triangle = Path(pb =>
               {
                   pb.MoveTo(Point(0, h));
                   pb.LineTo(Point(-h / 2f, 0));
                   pb.LineTo(Point(h / 2f, 0));
                   pb.Close();
               }))
        using (var circle = Path(pb => pb.AddOval(Rect(-h / 2f, -h / 2f, h, h))))
        {
            b.DrawPath(triangle, red);
            b.DrawPath(circle, red);
        }
    }

    private static void OpenPaths(ImpellerDisplayListBuilder b)
    {
        using (var open = Path(pb =>
               {
                   pb.MoveTo(Point(50, 50));
                   pb.LineTo(Point(50, 100));
                   pb.LineTo(Point(100, 100));
                   pb.LineTo(Point(100, 50));
               }))
        using (var paint = Stroke(ImpellerColor.FromRgb(255, 0, 0), 10f))
        {
            b.DrawPath(open, paint);
        }

        using var twoContours = Path(pb =>
        {
            pb.MoveTo(Point(100, 150));
            pb.LineTo(Point(150, 200));
            pb.MoveTo(Point(200, 250));
            pb.LineTo(Point(200, 250));
        });
        using var round = Stroke(ImpellerColor.FromRgb(255, 0, 0), 15f);
        round.SetStrokeCap(ImpellerStrokeCap.kImpellerStrokeCapRound);
        b.DrawPath(twoContours, round);
    }

    private static string Bounds(ImpellerDisplayListBuilder b, float t)
    {
        using var path = Path(pb =>
        {
            pb.MoveTo(Point(40, 150));
            pb.CubicCurveTo(Point(100, 150 - 120 * MathF.Sin(t)), Point(200, 150 + 120 * MathF.Cos(t * 0.7f)), Point(260, 150));
            pb.LineTo(Point(150, 270));
            pb.Close();
        });
        using var fill = Fill(ImpellerColor.FromRgb(40, 170, 120));
        b.DrawPath(path, fill);
        path.GetBounds(out var r);
        using var box = Stroke(ImpellerColor.FromRgb(230, 60, 60), 2f);
        b.DrawRect(r, box);
        return $"GetBounds: x {r.X:0} y {r.Y:0} w {r.Width:0} h {r.Height:0}";
    }

    private static void CopyVsTake(ImpellerDisplayListBuilder b)
    {
        using var pb = ImpellerPathBuilder.New()!;
        pb.AddRect(Rect(30, 30, 110, 110));
        using var copy = pb.CopyPathNew(ImpellerFillType.kImpellerFillTypeNonZero)!;
        pb.AddOval(Rect(160, 160, 110, 110));
        using var taken = pb.TakePathNew(ImpellerFillType.kImpellerFillTypeNonZero)!;
        using var empty = pb.TakePathNew(ImpellerFillType.kImpellerFillTypeNonZero)!;

        using var red = Stroke(ImpellerColor.FromRgb(230, 40, 40), 12f);
        b.DrawPath(taken, red);
        using var blue = Fill(ImpellerColor.FromRgb(40, 110, 230));
        b.DrawPath(copy, blue);
        using var green = Stroke(ImpellerColor.FromRgb(0, 200, 0), 4f);
        b.DrawPath(empty, green);
    }

    public void Dispose()
    {
        _texture.Dispose();
        _tc.Dispose();
    }
}
