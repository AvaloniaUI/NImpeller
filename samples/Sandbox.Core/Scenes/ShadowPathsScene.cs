using System.Diagnostics;
using System.Numerics;
using NImpeller;
using static Sandbox.Scenes.Draw;

namespace Sandbox.Scenes;

/// <summary>
/// After the Aiks <c>DrawShadowCanOptimize*</c>, <c>DrawShadowDoesNotOptimize*</c> and
/// <c>CanDraw{Rotated,NonuniformScale,Perspective}ConvexShadow</c> playgrounds. Clockwise and counter-clockwise
/// versions of the same shape should cast identical shadows.
/// </summary>
public sealed class ShadowPathsScene : IScene, IDisposable
{
    public string Name => "Shadow Paths";
    public string CommandLineName => "shadowpaths";
    public string Description => "DrawShadow on convex shapes in both windings, non-convex and multi-contour paths, and rotated, stretched and perspective occluders";

    private const float Unit = 400f;

    private static readonly string[] Labels =
    [
        "Triangle CW", "Triangle CCW", "Octagon CW", "Octagon CCW",
        "Cubic blob CW", "Cubic blob CCW", "Multi-radii rrect", "Extra MoveTos + colinear",
        "Hourglass", "Inner-outer spiral", "Multiple contours", "Quadratic CW",
        "Rotated", "Non-uniform scale", "Perspective rect", "Perspective pentagon",
    ];

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ImpellerTypographyContext _tc = ImpellerTypographyContext.New()!;

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        float t = (float)_clock.Elapsed.TotalSeconds;
        Background(b, ImpellerColor.FromRgb(255, 255, 255));

        float k = Draw.Scale(p);
        float pad = 10f * k, header = 16f * k;
        const int cols = 4, rows = 4;
        float cw = (p.Width - pad * (cols + 1)) / cols;
        float ch = (p.Height - pad * (rows + 1)) / rows - header;
        if (cw <= 1f || ch <= 1f) return;
        float s = MathF.Min(cw, ch) / Unit;
        float elevation = 45f + 25f * MathF.Sin(t);

        for (int i = 0; i < Labels.Length; i++)
        {
            float x = pad + (i % cols) * (cw + pad);
            float y = pad + (i / cols) * (ch + header + pad);
            Label(b, _tc, Labels[i], x, y, 11f * k);

            b.Save();
            b.ClipRect(Rect(x, y + header, cw, ch), ImpellerClipOperation.kImpellerClipOperationIntersect);
            b.Translate(x + (cw - Unit * s) / 2f, y + header);
            b.Scale(s, s);
            if (i >= 12) ApplyTransform(b, i, t);
            using var path = Shape(i);
            b.DrawShadow(path, ImpellerColor.FromArgb(160, 0, 0, 0), elevation, 0, 1f);
            using var fill = Fill(ImpellerColor.FromRgb(235, 238, 245));
            b.DrawPath(path, fill);
            using var outline = Stroke(ImpellerColor.FromRgb(120, 130, 150), 2f);
            b.DrawPath(path, outline);
            b.Restore();
        }
    }

    private static void ApplyTransform(ImpellerDisplayListBuilder b, int index, float t)
    {
        var center = new Vector3(200, 200, 0);
        var m = index switch
        {
            12 => Matrix4x4.CreateRotationZ(t * 0.5f, center),
            13 => Matrix4x4.CreateScale(1f + 0.5f * MathF.Sin(t), 1f - 0.4f * MathF.Sin(t), 1f, center),
            _ => Perspective(t),
        };
        b.Transform(m);
    }

    private static Matrix4x4 Perspective(float t)
    {
        var persp = Matrix4x4.Identity;
        persp.M34 = 0.0015f;
        return Matrix4x4.CreateTranslation(-200, -200, 0)
               * Matrix4x4.CreateRotationY(MathF.PI / 4f * MathF.Sin(t * 0.7f))
               * persp
               * Matrix4x4.CreateTranslation(200, 200, 0);
    }

    private static ImpellerPath Shape(int index) => index switch
    {
        0 => Polygon([(200, 100), (300, 300), (100, 300)]),
        1 => Polygon([(200, 100), (100, 300), (300, 300)]),
        2 => RegularPolygon(8, clockwise: true),
        3 => RegularPolygon(8, clockwise: false),
        4 => Blob(clockwise: true),
        5 => Blob(clockwise: false),
        6 => Path(pb => pb.AddRoundedRect(Rect(100, 100, 200, 200), Radii(60, 10, 40, 0))),
        7 => Path(pb =>
        {
            pb.MoveTo(Point(50, 50));
            pb.MoveTo(Point(100, 100));
            pb.LineTo(Point(200, 100));
            pb.LineTo(Point(300, 100));
            pb.LineTo(Point(300, 200));
            pb.LineTo(Point(300, 300));
            pb.LineTo(Point(100, 300));
            pb.Close();
        }),
        8 => Polygon([(100, 100), (300, 300), (100, 300), (300, 100)]),
        9 => Spiral(),
        10 => Path(pb =>
        {
            pb.AddRect(Rect(80, 80, 110, 110));
            pb.AddOval(Rect(210, 210, 110, 110));
        }),
        11 => Path(pb =>
        {
            pb.MoveTo(Point(100, 300));
            pb.QuadraticCurveTo(Point(200, 40), Point(300, 300));
            pb.Close();
        }),
        12 or 13 => RegularPolygon(5, clockwise: true),
        14 => Path(pb => pb.AddRect(Rect(150, 150, 100, 125))),
        _ => Polygon([(150, 150), (250, 150), (250, 250), (200, 275), (150, 250)]),
    };

    private static ImpellerPath Polygon((float X, float Y)[] points) => Path(pb =>
    {
        pb.MoveTo(Point(points[0].X, points[0].Y));
        for (int i = 1; i < points.Length; i++) pb.LineTo(Point(points[i].X, points[i].Y));
        pb.Close();
    });

    private static ImpellerPath RegularPolygon(int sides, bool clockwise)
    {
        var points = new (float, float)[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = (clockwise ? 1 : -1) * i * MathF.Tau / sides - MathF.PI / 2f;
            points[i] = (200 + MathF.Cos(a) * 100, 200 + MathF.Sin(a) * 100);
        }
        return Polygon(points);
    }

    private static ImpellerPath Blob(bool clockwise) => Path(pb =>
    {
        float d = clockwise ? 1f : -1f;
        pb.MoveTo(Point(200, 100));
        pb.CubicCurveTo(Point(200 + d * 90, 100), Point(200 + d * 110, 200), Point(200 + d * 100, 260));
        pb.CubicCurveTo(Point(200 + d * 80, 310), Point(200 - d * 80, 310), Point(200 - d * 100, 260));
        pb.CubicCurveTo(Point(200 - d * 110, 200), Point(200 - d * 90, 100), Point(200, 100));
        pb.Close();
    });

    private static ImpellerPath Spiral() => Path(pb =>
    {
        const int steps = 20;
        pb.MoveTo(Point(300, 200));
        for (int i = 1; i < steps * 2; i++)
        {
            float a = MathF.Tau * i / steps;
            float r = 80f + MathF.Abs(i - steps);
            pb.LineTo(Point(200 + MathF.Cos(a) * r, 200 + MathF.Sin(a) * r));
        }
        pb.Close();
    });

    public void Dispose() => _tc.Dispose();
}
