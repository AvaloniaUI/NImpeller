using NImpeller;
using static Sandbox.Scenes.Draw;

namespace Sandbox.Scenes;

/// <summary>Ports of the interop <c>CanCreateDecorations</c>, <c>CanRenderTextAlignments</c> and <c>CanControlEllipses</c> playgrounds.</summary>
public sealed class TextFeaturesScene : IScene, IDisposable
{
    public string Name => "Text Features";
    public string CommandLineName => "textfeatures";
    public string Description => "Text decorations (wavy, double, dotted, dashed), left/center/right/justify alignment with layout boxes, and ellipsis control";

    private const string Lorem = "Lorem ipsum dolor sit amet, consectetur adipiscing elit.";
    private const string Fox = "The quick brown fox jumped over the lazy dog";

    private readonly ImpellerTypographyContext _tc = ImpellerTypographyContext.New()!;

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        Background(b, ImpellerColor.FromRgb(255, 255, 255));

        float k = MathF.Max(1f, p.Height / 640f);
        float pad = 16f * k;
        float width = p.Width - pad * 2f;
        if (width <= 1f) return;

        float y = pad;
        y = DrawDecorations(b, pad, y, width, k) + pad;
        y = DrawAlignments(b, pad, y, width, k) + pad;
        DrawEllipses(b, pad, y, k);
    }

    private float DrawDecorations(ImpellerDisplayListBuilder b, float x, float y, float width, float k)
    {
        Label(b, _tc, "Decorations", x, y, 13f * k);
        y += 20f * k;

        var wavy = new ImpellerTextDecoration(
            ImpellerTextDecorationType.kImpellerTextDecorationTypeLineThrough | ImpellerTextDecorationType.kImpellerTextDecorationTypeUnderline,
            ImpellerTextDecorationStyle.kImpellerTextDecorationStyleWavy,
            new ImpellerColor { Green = 1f, Alpha = 0.75f }, 1.5f);
        using (var para = Paragraph(_tc, "Holy text decorations Batman!", 40f * k, ImpellerColor.FromRgb(255, 0, 0), width,
                   s => s.SetTextDecoration(wavy)))
        {
            b.DrawParagraph(para, Point(x, y));
            y += para.GetHeight() + 10f * k;
        }

        (ImpellerTextDecorationStyle Style, string Name)[] styles =
        [
            (ImpellerTextDecorationStyle.kImpellerTextDecorationStyleSolid, "Solid"),
            (ImpellerTextDecorationStyle.kImpellerTextDecorationStyleDouble, "Double"),
            (ImpellerTextDecorationStyle.kImpellerTextDecorationStyleDotted, "Dotted"),
            (ImpellerTextDecorationStyle.kImpellerTextDecorationStyleDashed, "Dashed"),
        ];
        float colW = width / styles.Length;
        float rowH = 0f;
        for (int i = 0; i < styles.Length; i++)
        {
            var decoration = new ImpellerTextDecoration(
                ImpellerTextDecorationType.kImpellerTextDecorationTypeUnderline | ImpellerTextDecorationType.kImpellerTextDecorationTypeOverline,
                styles[i].Style, Hsv(0.6f + i * 0.1f, 0.8f, 0.8f));
            using var para = Paragraph(_tc, styles[i].Name, 22f * k, ImpellerColor.FromRgb(30, 30, 30), colW,
                s => s.SetTextDecoration(decoration));
            b.DrawParagraph(para, Point(x + i * colW, y + 4f * k));
            rowH = MathF.Max(rowH, para.GetHeight());
        }
        return y + rowH + 4f * k;
    }

    private float DrawAlignments(ImpellerDisplayListBuilder b, float x, float y, float width, float k)
    {
        Label(b, _tc, "Alignment (outer: frame, inner: paragraph max width x height)", x, y, 13f * k);
        y += 20f * k;

        (ImpellerTextAlignment Align, ImpellerColor Color)[] frames =
        [
            (ImpellerTextAlignment.kImpellerTextAlignmentLeft, ImpellerColor.FromRgb(230, 0, 0)),
            (ImpellerTextAlignment.kImpellerTextAlignmentCenter, ImpellerColor.FromRgb(0, 170, 0)),
            (ImpellerTextAlignment.kImpellerTextAlignmentRight, ImpellerColor.FromRgb(0, 0, 230)),
            (ImpellerTextAlignment.kImpellerTextAlignmentJustify, ImpellerColor.FromRgb(160, 0, 180)),
        ];
        float gap = 10f * k;
        float boxW = (width - gap * (frames.Length - 1)) / frames.Length;
        float inset = 10f * k;
        float maxH = 0f;
        var bodies = new List<ImpellerParagraph>();
        try
        {
            foreach (var (align, color) in frames)
            {
                bodies.Add(Paragraph(_tc, Lorem, 16f * k, color, boxW - inset * 2f, s =>
                {
                    s.SetFontWeight(ImpellerFontWeight.kImpellerFontWeight400);
                    s.SetTextAlignment(align);
                }));
                maxH = MathF.Max(maxH, bodies[^1].GetHeight());
            }

            float boxH = maxH + inset * 2f;
            for (int i = 0; i < frames.Length; i++)
            {
                float bx = x + i * (boxW + gap);
                using var stroke = Stroke(frames[i].Color, 1f * k);
                b.DrawRect(Rect(bx, y, boxW, boxH), stroke);
                var pt = Point(bx + inset, y + inset);
                b.DrawParagraph(bodies[i], pt);
                b.DrawRect(Rect(pt.X, pt.Y, bodies[i].GetMaxWidth(), bodies[i].GetHeight()), stroke);
            }
            return y + boxH;
        }
        finally
        {
            foreach (var body in bodies) body.Dispose();
        }
    }

    private void DrawEllipses(ImpellerDisplayListBuilder b, float x, float y, float k)
    {
        Label(b, _tc, "Ellipsis (guide shows the layout width)", x, y, 13f * k);
        y += 20f * k;

        float w = 250f * k;
        (string? Ellipsis, uint MaxLines, ImpellerColor Color, string Name)[] cases =
        [
            ("\U0001F436", 0, ImpellerColor.FromRgb(230, 0, 0), "ellipsis \U0001F436"),
            (null, 0, ImpellerColor.FromRgb(0, 170, 0), "no ellipsis"),
            ("…", 2, ImpellerColor.FromRgb(0, 0, 230), "ellipsis …, max 2 lines"),
        ];

        float colW = w + 40f * k;
        using var guide = Stroke(ImpellerColor.FromRgb(200, 200, 200), 1f * k);
        for (int i = 0; i < cases.Length; i++)
        {
            var c = cases[i];
            float cx = x + i * colW;
            Label(b, _tc, c.Name, cx, y, 12f * k);
            using var para = Paragraph(_tc, Fox, 26f * k, c.Color, w, s =>
            {
                if (c.Ellipsis is not null) s.SetEllipsis(c.Ellipsis);
                if (c.MaxLines > 0) s.SetMaxLines(c.MaxLines);
            });
            var pt = Point(cx, y + 18f * k);
            b.DrawLine(Point(pt.X + w, pt.Y), Point(pt.X + w, pt.Y + para.GetHeight()), guide);
            b.DrawParagraph(para, pt);
        }
    }

    public void Dispose() => _tc.Dispose();
}
