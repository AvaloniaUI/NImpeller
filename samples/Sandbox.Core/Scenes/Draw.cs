using NImpeller;

namespace Sandbox.Scenes;

internal static class Draw
{
    public static ImpellerRect Rect(float x, float y, float w, float h) =>
        new() { X = x, Y = y, Width = w, Height = h };

    public static ImpellerPoint Point(float x, float y) => new() { X = x, Y = y };

    public static ImpellerRoundingRadii Radii(float r) => Radii(r, r, r, r);

    public static ImpellerRoundingRadii Radii(float topLeft, float topRight, float bottomRight, float bottomLeft) => new()
    {
        Top_left = Point(topLeft, topLeft),
        Top_right = Point(topRight, topRight),
        Bottom_right = Point(bottomRight, bottomRight),
        Bottom_left = Point(bottomLeft, bottomLeft),
    };

    public static ImpellerPaint Fill(ImpellerColor color)
    {
        var paint = ImpellerPaint.New()!;
        paint.SetColor(color);
        return paint;
    }

    public static ImpellerPaint Stroke(ImpellerColor color, float width)
    {
        var paint = Fill(color);
        paint.SetDrawStyle(ImpellerDrawStyle.kImpellerDrawStyleStroke);
        paint.SetStrokeWidth(width);
        return paint;
    }

    public static void Background(ImpellerDisplayListBuilder b, ImpellerColor color)
    {
        using var paint = Fill(color);
        b.DrawPaint(paint);
    }

    public static ImpellerPath Star(float cx, float cy, float outer, float inner, int points,
        float rotation = 0f, ImpellerFillType fill = ImpellerFillType.kImpellerFillTypeNonZero)
    {
        using var pb = ImpellerPathBuilder.New()!;
        for (int i = 0; i < points * 2; i++)
        {
            float r = i % 2 == 0 ? outer : inner;
            float a = rotation - MathF.PI / 2f + i * MathF.PI / points;
            var pt = Point(cx + MathF.Cos(a) * r, cy + MathF.Sin(a) * r);
            if (i == 0) pb.MoveTo(pt);
            else pb.LineTo(pt);
        }
        pb.Close();
        return pb.TakePathNew(fill)!;
    }

    public static ImpellerPath Path(Action<ImpellerPathBuilder> build)
    {
        using var pb = ImpellerPathBuilder.New()!;
        build(pb);
        return pb.TakePathNew(ImpellerFillType.kImpellerFillTypeNonZero)!;
    }

    public static ImpellerParagraph Paragraph(ImpellerTypographyContext tc, string text, float size, ImpellerColor color,
        float width, Action<ImpellerParagraphStyle>? configure = null)
    {
        using var style = ImpellerParagraphStyle.New()!;
        using var fg = Fill(color);
        style.SetForeground(fg);
        style.SetFontSize(size);
        configure?.Invoke(style);
        using var builder = tc.ParagraphBuilderNew()!;
        builder.PushStyle(style);
        builder.AddText(text);
        return builder.BuildParagraphNew(width)!;
    }

    public static void Label(ImpellerDisplayListBuilder b, ImpellerTypographyContext tc, string text, float x, float y,
        float size = 13f, ImpellerColor? color = null)
    {
        using var p = Paragraph(tc, text, size, color ?? ImpellerColor.FromRgb(120, 124, 136), 10_000f);
        b.DrawParagraph(p, Point(x, y));
    }

    public static ImpellerColor Hsv(float h, float s, float v, float a = 1f)
    {
        h -= MathF.Floor(h);
        float c = v * s, hp = h * 6f, xc = c * (1f - MathF.Abs(hp % 2f - 1f)), m = v - c;
        (float r, float g, float bl) = (int)hp switch
        {
            0 => (c, xc, 0f),
            1 => (xc, c, 0f),
            2 => (0f, c, xc),
            3 => (0f, xc, c),
            4 => (xc, 0f, c),
            _ => (c, 0f, xc),
        };
        return new ImpellerColor { Red = r + m, Green = g + m, Blue = bl + m, Alpha = a };
    }
}
