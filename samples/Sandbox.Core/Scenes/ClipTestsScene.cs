using System.Diagnostics;
using NImpeller;
using static Sandbox.Scenes.Draw;

namespace Sandbox.Scenes;

/// <summary>Ports of every Aiks clip playground (aiks_dl_clip_unittests.cc), one per cell.</summary>
public sealed class ClipTestsScene : IScene, IDisposable
{
    public string Name => "Clip Tests";
    public string CommandLineName => "cliptests";
    public string Description => "The Aiks clip playgrounds: nested, difference, contiguous restores, transformed, framebuffer blends, dilated layers, clipped backdrop, rotated clip";

    private static readonly (string Label, float Width, float Height)[] Cells =
    [
        ("CanRenderNestedClips", 800, 800),
        ("CanRenderDifferenceClips", 800, 800),
        ("ContiguousClipRestores (all green)", 400, 400),
        ("ClipsUseCurrentTransform", 600, 600),
        ("FramebufferBlendsRespectClips", 300, 300),
        ("DilatedContentOutsideClip", 160, 100),
        ("Clipped backdrop (rrect clip)", 400, 400),
        ("Rotated rounded-rect clip", 400, 400),
    ];

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ImpellerTypographyContext _tc = ImpellerTypographyContext.New()!;

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

        for (int i = 0; i < Cells.Length; i++)
        {
            float x = pad + (i % cols) * (cw + pad);
            float y = pad + (i / cols) * (ch + header + pad);
            Label(b, _tc, Cells[i].Label, x, y, 11f * k, ImpellerColor.FromRgb(200, 204, 214));

            float s = MathF.Min(cw / Cells[i].Width, ch / Cells[i].Height);
            float w = Cells[i].Width * s, h = Cells[i].Height * s;
            b.Save();
            b.ClipRect(Rect(x + (cw - w) / 2f, y + header + (ch - h) / 2f, w, h), ImpellerClipOperation.kImpellerClipOperationIntersect);
            b.Translate(x + (cw - w) / 2f, y + header + (ch - h) / 2f);
            b.Scale(s, s);
            using (var bg = Fill(ImpellerColor.FromRgb(255, 255, 255)))
            {
                b.DrawPaint(bg);
            }
            DrawCell(b, i, t);
            b.Restore();
        }
    }

    private static void DrawCell(ImpellerDisplayListBuilder b, int index, float t)
    {
        const ImpellerClipOperation intersect = ImpellerClipOperation.kImpellerClipOperationIntersect;
        const ImpellerClipOperation difference = ImpellerClipOperation.kImpellerClipOperationDifference;
        switch (index)
        {
            case 0:
            {
                b.Save();
                using (var c1 = Circle(200, 400, 300)) b.ClipPath(c1, intersect);
                b.Restore();
                using (var c2 = Circle(600, 400, 300)) b.ClipPath(c2, intersect);
                using (var c3 = Circle(400, 600, 300)) b.ClipPath(c3, intersect);
                using var paint = Fill(ImpellerColor.FromRgb(255, 0, 255));
                b.DrawRect(Rect(200, 200, 400, 400), paint);
                break;
            }
            case 1:
            {
                b.Translate(400, 400);
                using (var c = Circle(0, 0, 200)) b.ClipPath(c, intersect);
                b.Save();
                using (var e1 = Circle(-100, -50, 30)) b.ClipPath(e1, difference);
                using (var e2 = Circle(100, -50, 30)) b.ClipPath(e2, difference);
                using (var mouth = Path(pb =>
                       {
                           pb.MoveTo(Point(-100, 50));
                           pb.QuadraticCurveTo(Point(0, 150), Point(100, 50));
                       }))
                {
                    b.ClipPath(mouth, difference);
                }
                using (var yellow = Fill(ImpellerColor.FromRgb(255, 255, 0)))
                {
                    b.DrawRect(Rect(-1000, -1000, 2000, 2000), yellow);
                }
                b.Restore();
                using var hat = Path(pb =>
                {
                    pb.MoveTo(Point(200, -200));
                    pb.LineTo(Point(-200, -200));
                    pb.LineTo(Point(-200, -40));
                    pb.CubicCurveTo(Point(0, -40), Point(0, -80), Point(200, -80));
                });
                using var maroon = Fill(ImpellerColor.FromRgb(128, 0, 0));
                b.DrawPath(hat, maroon);
                break;
            }
            case 2:
            {
                using (var red = Fill(ImpellerColor.FromRgb(255, 0, 0))) b.DrawPaint(red);
                b.Save();
                b.ClipRect(Rect(100, 100, 100, 100), intersect);
                b.ClipRect(Rect(300, 300, 100, 100), intersect);
                b.Restore();
                using (var green = Fill(ImpellerColor.FromRgb(0, 255, 0))) b.DrawPaint(green);
                break;
            }
            case 3:
            {
                ImpellerColor[] colors =
                [
                    ImpellerColor.FromRgb(255, 255, 255), ImpellerColor.FromRgb(0, 0, 0), ImpellerColor.FromRgb(135, 206, 235),
                    ImpellerColor.FromRgb(255, 0, 0), ImpellerColor.FromRgb(255, 255, 0),
                ];
                b.Translate(300, 300);
                for (int i = 0; i < 15; i++)
                {
                    b.Scale(0.8f, 0.8f);
                    using var c = Circle(0, 0, 300);
                    b.ClipPath(c, intersect);
                    using var paint = Fill(colors[i % colors.Length]);
                    b.DrawRect(Rect(-300, -300, 600, 600), paint);
                }
                break;
            }
            case 4:
            {
                using (var c = Circle(150, 150, 50)) b.ClipPath(c, intersect);
                using (var red = Fill(ImpellerColor.FromRgb(255, 0, 0)))
                {
                    red.SetBlendMode(ImpellerBlendMode.kImpellerBlendModeMultiply);
                    b.DrawRect(Rect(100, 100, 100, 100), red);
                }
                using var green = Fill(ImpellerColor.FromRgb(0, 255, 0));
                b.DrawOval(Rect(100, 100, 100, 100), green);
                break;
            }
            case 5:
            {
                using (var black = Fill(ImpellerColor.FromRgb(0, 0, 0)))
                {
                    black.SetBlendMode(ImpellerBlendMode.kImpellerBlendModeSource);
                    b.DrawPaint(black);
                }
                b.ClipRect(Rect(20, 20, 100, 60), intersect);
                using var dilate = ImpellerImageFilter.CreateDilateNew(10, 10)!;
                using var layer = ImpellerPaint.New()!;
                layer.SetImageFilter(dilate);
                b.SaveLayer(Rect(0, 0, 160, 100), layer, null);
                using (var white = Fill(ImpellerColor.FromRgb(255, 255, 255)))
                {
                    b.DrawRect(Rect(15, 45, 4, 10), white);
                }
                b.Restore();
                break;
            }
            case 6:
            {
                using (var black = Fill(ImpellerColor.FromRgb(0, 0, 0))) b.DrawPaint(black);
                var rect = Rect(50, 50, 300, 300);
                b.ClipRoundedRect(rect, Radii(80), intersect);
                using (var child = ImpellerDisplayListBuilder.New(Rect(0, 0, 400, 400))!)
                {
                    child.ClipRect(Rect(0, 0, 400, 400), intersect);
                    using (var blue = Fill(ImpellerColor.FromRgb(100, 149, 237))) child.DrawRect(Rect(50, 50, 150, 300), blue);
                    using (var orange = Fill(ImpellerColor.FromRgb(255, 165, 0))) child.DrawRect(Rect(200, 50, 150, 300), orange);
                    using var dl = child.CreateDisplayListNew()!;
                    b.DrawDisplayList(dl, 1f);
                }
                using var blur = ImpellerImageFilter.CreateBlurNew(20, 20, ImpellerTileMode.kImpellerTileModeClamp)!;
                b.SaveLayer(rect, null, blur);
                using (var white = Fill(ImpellerColor.FromArgb(160, 255, 255, 255))) b.DrawPaint(white);
                b.Restore();
                break;
            }
            default:
            {
                b.Translate(200, 200);
                b.Rotate(t * 25f);
                b.ClipRoundedRect(Rect(-120, -80, 240, 160), Radii(40), intersect);
                b.Rotate(-t * 25f);
                for (int i = 0; i < 12; i++)
                {
                    using var paint = Fill(Hsv(i / 12f, 0.7f, 0.95f));
                    b.DrawRect(Rect(-200 + i * 34f, -200, 34f, 400), paint);
                }
                break;
            }
        }
    }

    private static ImpellerPath Circle(float cx, float cy, float r) => Path(pb => pb.AddOval(Rect(cx - r, cy - r, r * 2f, r * 2f)));

    public void Dispose() => _tc.Dispose();
}
