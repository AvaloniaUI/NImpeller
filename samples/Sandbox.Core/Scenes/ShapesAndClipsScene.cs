using System.Diagnostics;
using NImpeller;
using static Sandbox.Scenes.Draw;

namespace Sandbox.Scenes;

/// <summary>Port of the interop <c>CanCreateShapes</c> playground, plus dashed lines, rounded-rect differences and clips.</summary>
public sealed class ShapesAndClipsScene : IScene, IDisposable
{
    public string Name => "Shapes & Clips";
    public string CommandLineName => "shapes";
    public string Description => "Filled and stroked primitives, dashed lines, DrawRoundedRectDifference, and rect/oval/rounded-rect/path clips in intersect and difference modes";

    private const int Cols = 6;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ImpellerTypographyContext _tc = ImpellerTypographyContext.New()!;

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        float t = (float)_clock.Elapsed.TotalSeconds;
        Background(b, ImpellerColor.FromRgb(246, 246, 248));

        float k = MathF.Max(1f, p.Height / 640f);
        float pad = 14f * k, header = 20f * k;
        const int rows = 4;
        float cw = (p.Width - pad * (Cols + 1)) / Cols;
        float ch = (p.Height - pad * (rows + 1)) / rows - header;
        if (cw <= 1f || ch <= 1f) return;
        float s = MathF.Min(cw, ch) * 0.85f;

        string[] rowLabels = ["Filled primitives", "Stroked primitives", "Dashed lines and rounded-rect difference", "Clips: intersect x4, difference, even-odd path"];
        for (int row = 0; row < rows; row++)
        {
            float y = pad + row * (ch + header + pad);
            Label(b, _tc, rowLabels[row], pad, y, 13f * k);
            y += header;

            for (int col = 0; col < Cols; col++)
            {
                float x = pad + col * (cw + pad);
                var cell = Rect(x + (cw - s) / 2f, y + (ch - s) / 2f, s, s);
                switch (row)
                {
                    case 0:
                        using (var fill = Fill(ImpellerColor.FromRgb(230, 40, 40)))
                        {
                            fill.SetStrokeWidth(10f * k);
                            DrawPrimitive(b, col, cell, fill);
                        }
                        break;
                    case 1:
                        using (var stroke = Stroke(ImpellerColor.FromRgb(40, 90, 220), 6f * k))
                        {
                            stroke.SetStrokeJoin(ImpellerStrokeJoin.kImpellerStrokeJoinRound);
                            stroke.SetStrokeCap(ImpellerStrokeCap.kImpellerStrokeCapRound);
                            DrawPrimitive(b, col, cell, stroke);
                        }
                        break;
                    case 2:
                        DrawDashesAndDifference(b, col, cell, t, k);
                        break;
                    default:
                        DrawClip(b, col, cell, t, k);
                        break;
                }
            }
        }
    }

    private static void DrawPrimitive(ImpellerDisplayListBuilder b, int index, ImpellerRect r, ImpellerPaint paint)
    {
        switch (index)
        {
            case 0:
                b.DrawRect(r, paint);
                break;
            case 1:
                b.DrawOval(r, paint);
                break;
            case 2:
                b.DrawLine(Point(r.X, r.Y), Point(r.X + r.Width, r.Y + r.Height), paint);
                break;
            case 3:
                b.DrawRoundedRect(r, Radii(r.Width * 0.25f, 0f, r.Width * 0.25f, 0f), paint);
                break;
            case 4:
                using (var oval = Path(pb => pb.AddOval(r)))
                {
                    b.DrawPath(oval, paint);
                }
                break;
            default:
                using (var curve = Path(pb =>
                       {
                           pb.MoveTo(Point(r.X, r.Y + r.Height));
                           pb.CubicCurveTo(Point(r.X, r.Y), Point(r.X + r.Width, r.Y + r.Height), Point(r.X + r.Width, r.Y));
                           pb.QuadraticCurveTo(Point(r.X + r.Width / 2f, r.Y + r.Height * 0.4f), Point(r.X, r.Y + r.Height));
                           pb.Close();
                       }))
                {
                    b.DrawPath(curve, paint);
                }
                break;
        }
    }

    private static void DrawDashesAndDifference(ImpellerDisplayListBuilder b, int index, ImpellerRect r, float t, float k)
    {
        if (index < 4)
        {
            using var paint = Stroke(Hsv(0.55f + index * 0.1f, 0.7f, 0.75f), (2f + index * 2f) * k);
            paint.SetStrokeCap(index % 2 == 0 ? ImpellerStrokeCap.kImpellerStrokeCapButt : ImpellerStrokeCap.kImpellerStrokeCapRound);
            float on = (4f + index * 4f) * k, off = (4f + index * 3f) * k;
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, len = r.Width / 2f;
            for (int i = 0; i < 3; i++)
            {
                float a = t * 0.4f + i * MathF.PI / 3f;
                float dx = MathF.Cos(a) * len, dy = MathF.Sin(a) * len;
                b.DrawDashedLine(Point(cx - dx, cy - dy), Point(cx + dx, cy + dy), on, off, paint);
            }
            return;
        }

        using var fill = Fill(index == 4 ? ImpellerColor.FromRgb(240, 150, 30) : ImpellerColor.FromRgb(30, 170, 120));
        float inset = r.Width * (0.2f + 0.08f * MathF.Sin(t * 1.5f));
        var inner = Rect(r.X + inset, r.Y + inset, r.Width - inset * 2f, r.Height - inset * 2f);
        if (index == 4)
        {
            b.DrawRoundedRectDifference(r, Radii(r.Width * 0.2f), inner, Radii(inner.Width * 0.15f), fill);
        }
        else
        {
            b.DrawRoundedRectDifference(r, Radii(r.Width / 2f), inner, Radii(inner.Width / 2f), fill);
        }
    }

    private static void DrawClip(ImpellerDisplayListBuilder b, int index, ImpellerRect r, float t, float k)
    {
        using (var frame = Stroke(ImpellerColor.FromRgb(200, 200, 205), 1f * k))
        {
            b.DrawRect(r, frame);
        }

        const ImpellerClipOperation intersect = ImpellerClipOperation.kImpellerClipOperationIntersect;
        b.Save();
        switch (index)
        {
            case 0:
                b.ClipRect(Rect(r.X + r.Width * 0.15f, r.Y + r.Height * 0.25f, r.Width * 0.7f, r.Height * 0.5f), intersect);
                break;
            case 1:
                b.ClipOval(r, intersect);
                break;
            case 2:
                b.ClipRoundedRect(r, Radii(r.Width * 0.3f), intersect);
                break;
            case 3:
                using (var star = Star(r.X + r.Width / 2f, r.Y + r.Height / 2f, r.Width / 2f, r.Width / 4.5f, 5, t * 0.5f))
                {
                    b.ClipPath(star, intersect);
                }
                break;
            case 4:
                b.ClipRect(r, intersect);
                b.ClipOval(Rect(r.X + r.Width * 0.2f, r.Y + r.Height * 0.2f, r.Width * 0.6f, r.Height * 0.6f),
                    ImpellerClipOperation.kImpellerClipOperationDifference);
                break;
            default:
                using (var pentagram = Path(pb =>
                       {
                           for (int i = 0; i < 5; i++)
                           {
                               float a = t * 0.5f - MathF.PI / 2f + i * 4f * MathF.PI / 5f;
                               var pt = Point(r.X + r.Width / 2f + MathF.Cos(a) * r.Width / 2f, r.Y + r.Height / 2f + MathF.Sin(a) * r.Height / 2f);
                               if (i == 0) pb.MoveTo(pt);
                               else pb.LineTo(pt);
                           }
                           pb.Close();
                       }, ImpellerFillType.kImpellerFillTypeOdd))
                {
                    b.ClipPath(pentagram, intersect);
                }
                break;
        }
        DrawStripes(b, r, t, k);
        b.Restore();
    }

    private static void DrawStripes(ImpellerDisplayListBuilder b, ImpellerRect r, float t, float k)
    {
        float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
        b.Save();
        b.Translate(cx, cy);
        b.Rotate(t * 20f);
        float half = r.Width * 0.75f, band = 8f * k;
        int i = 0;
        for (float x = -half; x < half; x += band, i++)
        {
            using var paint = Fill(Hsv(i * 0.04f, 0.7f, 0.95f));
            b.DrawRect(Rect(x, -half, band, half * 2f), paint);
        }
        b.Restore();
    }

    public void Dispose() => _tc.Dispose();
}
