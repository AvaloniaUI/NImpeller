using System.Diagnostics;
using System.Numerics;
using NImpeller;
using static Sandbox.Scenes.Draw;

namespace Sandbox.Scenes;

/// <summary>
/// After the Aiks text playgrounds: <c>ScaledK</c>, <c>TextRotated</c>, <c>TextFrameSubpixelAlignment</c>,
/// <c>CanRenderStrokedTextFrame</c>, <c>TextForegroundShaderWithTransform</c>, <c>CanRenderEmojiTextFrameWithBlur</c>,
/// <c>CanRenderTextInSaveLayer</c>, <c>DrawScaledTextWithPerspectiveNoSaveLayer</c> and <c>TextWithNonUniformScale</c>.
/// </summary>
public sealed class TextTransformsScene : IScene, IDisposable
{
    public string Name => "Text Transforms";
    public string CommandLineName => "texttransforms";
    public string Description => "Glyph rendering under scale, rotation, subpixel offsets, perspective and non-uniform scale, with stroked, gradient, blurred and layered text";

    private const float Unit = 300f;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ImpellerTypographyContext _tc = ImpellerTypographyContext.New()!;

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        float t = (float)_clock.Elapsed.TotalSeconds;
        Background(b, ImpellerColor.FromRgb(250, 250, 252));

        float k = Draw.Scale(p);
        float pad = 12f * k, header = 18f * k;
        const int cols = 3, rows = 3;
        float cw = (p.Width - pad * (cols + 1)) / cols;
        float ch = (p.Height - pad * (rows + 1)) / rows - header;
        if (cw <= 1f || ch <= 1f) return;
        float s = MathF.Min(cw / Unit, ch / (Unit * 0.5f));

        string[] labels =
        [
            "Scale 0.5x-6x", "Rotation", "Subpixel offsets (0.1 px steps)",
            "Stroked text, widths 1/2/4", "Gradient foreground", "Emoji: blue + mask blur (silhouette), alpha",
            "Text in SaveLayer (alpha)", "Perspective", "Non-uniform scale + italic",
        ];
        for (int i = 0; i < labels.Length; i++)
        {
            float x = pad + (i % cols) * (cw + pad);
            float y = pad + (i / cols) * (ch + header + pad);
            Label(b, _tc, labels[i], x, y, 12f * k);
            using (var frame = Stroke(ImpellerColor.FromRgb(225, 225, 232), 1f))
            {
                b.DrawRect(Rect(x, y + header, cw, ch), frame);
            }

            b.Save();
            b.ClipRect(Rect(x, y + header, cw, ch), ImpellerClipOperation.kImpellerClipOperationIntersect);
            b.Translate(x, y + header);
            b.Scale(s, s);
            DrawCell(b, i, cw / s, ch / s, t);
            b.Restore();
        }
    }

    private void DrawCell(ImpellerDisplayListBuilder b, int index, float w, float h, float t)
    {
        var ink = ImpellerColor.FromRgb(20, 20, 30);
        switch (index)
        {
            case 0:
            {
                float scale = 0.5f + 5.5f * (0.5f + 0.5f * MathF.Sin(t * 0.6f));
                b.Translate(10, 10);
                b.Scale(scale, scale);
                using var para = Paragraph(_tc, "K", 24f, ink, 200f);
                b.DrawParagraph(para, Point(0, 0));
                break;
            }
            case 1:
            {
                b.Translate(w / 2f, h / 2f);
                b.Rotate(t * 30f);
                using var para = Paragraph(_tc, "Rotated text", 32f, ink, 400f);
                b.DrawParagraph(para, Point(-para.GetLongestLineWidth() / 2f, -para.GetHeight() / 2f));
                break;
            }
            case 2:
            {
                for (int i = 0; i < 6; i++)
                {
                    using var para = Paragraph(_tc, "Subpixel il|", 18f, ink, 400f);
                    b.DrawParagraph(para, Point(10f + i * 0.1f + (t * 2f % 1f), 6f + i * 22f));
                }
                break;
            }
            case 3:
            {
                float[] widths = [1f, 2f, 4f];
                for (int i = 0; i < widths.Length; i++)
                {
                    float width = widths[i];
                    using var stroke = Stroke(ImpellerColor.FromRgb(200, 30, 60), width);
                    using var para = DrawWith(stroke, "Stroke", 44f, s => s.SetFontWeight(ImpellerFontWeight.kImpellerFontWeight700));
                    b.DrawParagraph(para, Point(10f + i * 4f, 4f + i * 48f));
                }
                break;
            }
            case 4:
            {
                ReadOnlySpan<ImpellerColor> colors = [ImpellerColor.FromRgb(255, 0, 120), ImpellerColor.FromRgb(0, 140, 255), ImpellerColor.FromRgb(0, 200, 120)];
                ReadOnlySpan<float> stops = [0f, 0.5f, 1f];
                float shift = 100f * MathF.Sin(t);
                using var source = ImpellerColorSource.CreateLinearGradientNew(Point(shift, 0), Point(shift + 200, 0), colors, stops,
                    ImpellerTileMode.kImpellerTileModeMirror)!;
                using var paint = ImpellerPaint.New()!;
                paint.SetColorSource(source);
                using var para = DrawWith(paint, "Gradient text", 56f, s => s.SetFontWeight(ImpellerFontWeight.kImpellerFontWeight800));
                b.DrawParagraph(para, Point(10, 10));
                break;
            }
            case 5:
            {
                using var mask = ImpellerMaskFilter.CreateBlurNew(ImpellerBlurStyle.kImpellerBlurStyleNormal, 2f + 6f * (0.5f + 0.5f * MathF.Sin(t)))!;
                using var blurred = Fill(ImpellerColor.FromRgb(0, 0, 255));
                blurred.SetMaskFilter(mask);
                using var para1 = DrawWith(blurred, "😀🚀🎨", 56f);
                b.DrawParagraph(para1, Point(10, 4));
                using var faded = Fill(ImpellerColor.FromArgb(110, 0, 0, 0));
                using var para2 = DrawWith(faded, "😀🚀🎨", 56f);
                b.DrawParagraph(para2, Point(10, 74));
                break;
            }
            case 6:
            {
                using var layer = Fill(new ImpellerColor { Alpha = 0.3f + 0.6f * (0.5f + 0.5f * MathF.Sin(t)) });
                b.SaveLayer(Rect(0, 0, w, h), layer, null);
                using (var bg = Fill(ImpellerColor.FromRgb(255, 220, 120)))
                {
                    b.DrawRoundedRect(Rect(8, 8, w - 16, h - 16), Radii(10), bg);
                }
                using var para = Paragraph(_tc, "Layered text", 44f, ink, w);
                b.DrawParagraph(para, Point(16, 30));
                b.Restore();
                break;
            }
            case 7:
            {
                float cx = w / 2f, cy = h / 2f;
                var persp = Matrix4x4.Identity;
                persp.M24 = 0.003f * MathF.Sin(t * 0.8f);
                persp.M14 = 0.002f * MathF.Cos(t * 0.6f);
                b.Transform(Matrix4x4.CreateTranslation(-cx, -cy, 0) * persp * Matrix4x4.CreateTranslation(cx, cy, 0));
                using var para = Paragraph(_tc, "Perspective", 48f, ink, 400f);
                b.DrawParagraph(para, Point(cx - para.GetLongestLineWidth() / 2f, cy - para.GetHeight() / 2f));
                break;
            }
            default:
            {
                b.Translate(10, 10);
                b.Scale(1f + 0.8f * MathF.Sin(t), 1f - 0.4f * MathF.Sin(t));
                using var para = Paragraph(_tc, "Stretched", 40f, ink, 400f,
                    s => s.SetFontStyle(ImpellerFontStyle.kImpellerFontStyleItalic));
                b.DrawParagraph(para, Point(0, 0));
                break;
            }
        }
    }

    private ImpellerParagraph DrawWith(ImpellerPaint foreground, string text, float size, Action<ImpellerParagraphStyle>? configure = null)
    {
        using var style = ImpellerParagraphStyle.New()!;
        style.SetForeground(foreground);
        style.SetFontSize(size);
        configure?.Invoke(style);
        using var builder = _tc.ParagraphBuilderNew()!;
        builder.PushStyle(style);
        builder.AddText(text);
        return builder.BuildParagraphNew(1000f)!;
    }

    public void Dispose() => _tc.Dispose();
}
