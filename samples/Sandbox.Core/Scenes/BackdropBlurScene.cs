using System.Diagnostics;
using System.Numerics;
using NImpeller;
using static Sandbox.Scenes.Draw;

namespace Sandbox.Scenes;

/// <summary>Ports of the Aiks <c>CanRenderBackdropBlur</c> and <c>CanRenderNestedBackdropBlur</c> playgrounds, plus a frosted card.</summary>
public sealed class BackdropBlurScene : IScene, IDisposable
{
    public string Name => "Backdrop Blur";
    public string CommandLineName => "backdropblur";
    public string Description => "SaveLayer backdrop filters: a blurred rounded clip, nested backdrop blurs, and a tinted frosted-glass card over animated content";

    private const float Content = 450f, ContentHeight = 330f;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ImpellerTypographyContext _tc = ImpellerTypographyContext.New()!;

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        float t = (float)_clock.Elapsed.TotalSeconds;
        Background(b, ImpellerColor.FromRgb(255, 255, 255));

        float k = Draw.Scale(p);
        float pad = 14f * k, header = 20f * k;
        float cw = (p.Width - pad * 3f) / 2f;
        float ch = (p.Height - pad * 3f) / 2f - header;
        if (cw <= 1f || ch <= 1f) return;
        float s = MathF.Min(cw / Content, ch / ContentHeight);
        float offset = 50f * MathF.Sin(t * MathF.Tau * 0.25f);

        string[] labels = ["CanRenderBackdropBlur", "CanRenderNestedBackdropBlur", "Frosted card", "MatrixBackdropFilter"];
        for (int i = 0; i < labels.Length; i++)
        {
            float x = pad + (i % 2) * (cw + pad);
            float y = pad + (i / 2) * (ch + header + pad);
            Label(b, _tc, labels[i], x, y, 13f * k);

            b.Save();
            b.ClipRect(Rect(x, y + header, cw, ch), ImpellerClipOperation.kImpellerClipOperationIntersect);
            b.Translate(x + (cw - Content * s) / 2f, y + header);
            b.Scale(s, s);
            switch (i)
            {
                case 0:
                    DrawCircles(b, offset);
                    Backdrop(b);
                    break;
                case 1:
                    DrawCircles(b, offset);
                    Nested(b);
                    break;
                case 2:
                    DrawCircles(b, offset);
                    Frosted(b, t);
                    break;
                default:
                    MatrixBackdrop(b, cw / s, ch / s);
                    break;
            }
            b.Restore();
        }
    }

    private static void DrawCircles(ImpellerDisplayListBuilder b, float offset)
    {
        Circle(b, 100 + offset, 100, 50, ImpellerColor.FromRgb(100, 149, 237));
        Circle(b, 300, 200 + offset, 100, ImpellerColor.FromRgb(173, 255, 47));
        Circle(b, 140, 170, 75, ImpellerColor.FromRgb(139, 0, 139));
        Circle(b, 180 + offset, 120 + offset, 100, ImpellerColor.FromRgb(255, 69, 0));
    }

    private static void Circle(ImpellerDisplayListBuilder b, float cx, float cy, float r, ImpellerColor color)
    {
        using var paint = Fill(color);
        b.DrawOval(Rect(cx - r, cy - r, r * 2f, r * 2f), paint);
    }

    private static void Backdrop(ImpellerDisplayListBuilder b)
    {
        b.ClipRoundedRect(Rect(75, 50, 300, 225), Radii(20), ImpellerClipOperation.kImpellerClipOperationIntersect);
        using var paint = ImpellerPaint.New()!;
        paint.SetBlendMode(ImpellerBlendMode.kImpellerBlendModeSource);
        using var blur = ImpellerImageFilter.CreateBlurNew(30, 30, ImpellerTileMode.kImpellerTileModeClamp)!;
        b.SaveLayer(Rect(0, 0, Content, Content), paint, blur);
        b.Restore();
    }

    private static void Nested(ImpellerDisplayListBuilder b)
    {
        using var blur1 = ImpellerImageFilter.CreateBlurNew(15, 15, ImpellerTileMode.kImpellerTileModeClamp)!;
        b.SaveLayer(Rect(0, 0, Content, Content), null, blur1);

        using (var tint = Fill(ImpellerColor.FromArgb(25, 255, 255, 255)))
        {
            b.DrawPaint(tint);
        }

        using var blur2 = ImpellerImageFilter.CreateBlurNew(10, 10, ImpellerTileMode.kImpellerTileModeClamp)!;
        b.Save();
        b.ClipRect(Rect(150, 150, 300, 300), ImpellerClipOperation.kImpellerClipOperationIntersect);
        b.SaveLayer(Rect(0, 0, Content, Content), null, blur2);
        b.Restore();
        b.Restore();

        b.Restore();
    }

    private void Frosted(ImpellerDisplayListBuilder b, float t)
    {
        var card = Rect(60, 120 + 40f * MathF.Sin(t * 0.8f), 330, 190);
        b.Save();
        b.ClipRoundedRect(card, Radii(28), ImpellerClipOperation.kImpellerClipOperationIntersect);
        using var blur = ImpellerImageFilter.CreateBlurNew(20, 20, ImpellerTileMode.kImpellerTileModeClamp)!;
        b.SaveLayer(card, null, blur);
        using (var tint = Fill(ImpellerColor.FromArgb(70, 255, 255, 255)))
        {
            b.DrawPaint(tint);
        }
        b.Restore();
        b.Restore();

        using (var border = Stroke(ImpellerColor.FromArgb(160, 255, 255, 255), 2f))
        {
            b.DrawRoundedRect(card, Radii(28), border);
        }
        using var title = Paragraph(_tc, "Frosted glass", 36f, ImpellerColor.FromRgb(20, 20, 30), card.Width - 48f);
        b.DrawParagraph(title, Point(card.X + 24f, card.Y + 24f));
        using var body = Paragraph(_tc, "Backdrop blur, white tint, and a border drawn after the layer.", 20f,
            ImpellerColor.FromRgb(40, 40, 50), card.Width - 48f);
        b.DrawParagraph(body, Point(card.X + 24f, card.Y + 80f));
    }

    private static void MatrixBackdrop(ImpellerDisplayListBuilder b, float w, float h)
    {
        using (var black = Fill(ImpellerColor.FromRgb(0, 0, 0)))
        {
            b.DrawRect(Rect(-w, 0, w * 3f, h), black);
        }
        b.SaveLayer(Rect(0, 0, Content, Content), null, null);
        using (var rectPaint = Stroke(ImpellerColor.FromRgb(255, 0, 0), 4f))
        {
            b.DrawRect(Rect(0, 0, 300, 300), rectPaint);
        }
        using (var paint = Fill(new ImpellerColor { Green = 1f, Alpha = 0.5f }))
        {
            paint.SetBlendMode(ImpellerBlendMode.kImpellerBlendModePlus);
            b.DrawOval(Rect(100, 100, 200, 200), paint);
        }
        float edge = 100f + 100f / MathF.Sqrt(2f);
        var m = Matrix4x4.CreateTranslation(-100, -100, 0) * Matrix4x4.CreateScale(0.5f, 0.5f, 1f)
                * Matrix4x4.CreateTranslation(edge, edge, 0);
        using var filter = ImpellerImageFilter.CreateMatrixNew(m, ImpellerTextureSampling.kImpellerTextureSamplingLinear)!;
        b.SaveLayer(Rect(0, 0, Content, Content), null, filter);
        b.Restore();
        b.Restore();
    }

    public void Dispose() => _tc.Dispose();
}
