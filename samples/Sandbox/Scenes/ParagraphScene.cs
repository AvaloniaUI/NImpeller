using System.Diagnostics;
using System.Numerics;
using NImpeller;

namespace Sandbox.Scenes;

#nullable disable

public class ParagraphScene : IScene
{
    Stopwatch st = Stopwatch.StartNew();
    public void Render(ImpellerContext context, ImpellerDisplayListBuilder scene, SceneParameters sceneParameters)
    {
        using var paint = ImpellerPaint.New();
        using var typographyContext = ImpellerTypographyContext.New();
        paint.SetColor(ImpellerColor.FromRgb(255, 255, 255));
        paint.SetDrawStyle(ImpellerDrawStyle.kImpellerDrawStyleFill);
        scene.DrawPaint(paint);

        DrawTextExample1(scene, typographyContext, 10, 10);

        DrawTextExample2(scene, typographyContext, 10, 80);

        DrawTextExample3(scene, typographyContext, 10, 150);

        DrawTextExample4(scene, typographyContext, 10, 250);

        DrawTextExample5(scene, typographyContext, 10, 350);

        DrawTextExample6(scene, typographyContext, 10, 450);

        DrawTextExampleCJK(scene, typographyContext, 10, 480);
    }

    static void DrawTextExample1(
       ImpellerDisplayListBuilder builder,
       ImpellerTypographyContext context,
       float x,
       float y)
    {
        using var paragraphBuilder = context.ParagraphBuilderNew();
        using var style = ImpellerParagraphStyle.New();
        using var paint = ImpellerPaint.New();

        paint.SetColor(ImpellerColor.FromRgb(0, 0, 0));
        style.SetForeground(paint);
        style.SetFontSize(24);

        paragraphBuilder.PushStyle(style);
        paragraphBuilder.AddText("Font Sizes: 12pt, 18pt, 24pt, 36pt");

        using var paragraph = paragraphBuilder.BuildParagraphNew(width: 600);
        builder.DrawParagraph(paragraph, new ImpellerPoint { X = x, Y = y });
    }

    static void DrawTextExample2(
        ImpellerDisplayListBuilder builder,
        ImpellerTypographyContext context,
        float x,
        float y)
    {
        using var paragraphBuilder = context.ParagraphBuilderNew();
        using var lightStyle = ImpellerParagraphStyle.New();
        using var normalStyle = ImpellerParagraphStyle.New();
        using var boldStyle = ImpellerParagraphStyle.New();
        using var paint = ImpellerPaint.New();

        paint.SetColor(ImpellerColor.FromRgb(0, 0, 0));

        // Light
        lightStyle.SetForeground(paint);
        lightStyle.SetFontSize(20);
        lightStyle.SetFontWeight(ImpellerFontWeight.kImpellerFontWeight300);

        // Normal
        normalStyle.SetForeground(paint);
        normalStyle.SetFontSize(20);
        normalStyle.SetFontWeight(ImpellerFontWeight.kImpellerFontWeight400);

        // Bold
        boldStyle.SetForeground(paint);
        boldStyle.SetFontSize(20);
        boldStyle.SetFontWeight(ImpellerFontWeight.kImpellerFontWeight700);

        paragraphBuilder.PushStyle(lightStyle);
        paragraphBuilder.AddText("Light ");
        paragraphBuilder.PopStyle();

        paragraphBuilder.PushStyle(normalStyle);
        paragraphBuilder.AddText("Normal ");
        paragraphBuilder.PopStyle();

        paragraphBuilder.PushStyle(boldStyle);
        paragraphBuilder.AddText("Bold");

        using var paragraph = paragraphBuilder.BuildParagraphNew(width: 600);
        builder.DrawParagraph(paragraph, new ImpellerPoint { X = x, Y = y });
    }

    static void DrawTextExample3(
        ImpellerDisplayListBuilder builder,
        ImpellerTypographyContext context,
        float x,
        float y)
    {
        float yOffset = y;
        using var paint = ImpellerPaint.New();
        paint.SetColor(ImpellerColor.FromRgb(0, 0, 0));

        // Left aligned
        using (var paragraphBuilder = context.ParagraphBuilderNew())
        using (var style = ImpellerParagraphStyle.New())
        {
            style.SetForeground(paint);
            style.SetFontSize(16);
            style.SetTextAlignment(ImpellerTextAlignment.kImpellerTextAlignmentLeft);

            paragraphBuilder.PushStyle(style);
            paragraphBuilder.AddText("Left aligned text");

            using var paragraph = paragraphBuilder.BuildParagraphNew(width: 300);
            builder.DrawParagraph(paragraph, new ImpellerPoint { X = x, Y = yOffset });
            yOffset += 30;
        }

        // Center aligned
        using (var paragraphBuilder = context.ParagraphBuilderNew())
        using (var style = ImpellerParagraphStyle.New())
        {
            style.SetForeground(paint);
            style.SetFontSize(16);
            style.SetTextAlignment(ImpellerTextAlignment.kImpellerTextAlignmentCenter);

            paragraphBuilder.PushStyle(style);
            paragraphBuilder.AddText("Center aligned text");

            using var paragraph = paragraphBuilder.BuildParagraphNew(width: 300);
            builder.DrawParagraph(paragraph, new ImpellerPoint { X = x, Y = yOffset });
            yOffset += 30;
        }

        // Right aligned
        using (var paragraphBuilder = context.ParagraphBuilderNew())
        using (var style = ImpellerParagraphStyle.New())
        {
            style.SetForeground(paint);
            style.SetFontSize(16);
            style.SetTextAlignment(ImpellerTextAlignment.kImpellerTextAlignmentRight);

            paragraphBuilder.PushStyle(style);
            paragraphBuilder.AddText("Right aligned text");

            using var paragraph = paragraphBuilder.BuildParagraphNew(width: 300);
            builder.DrawParagraph(paragraph, new ImpellerPoint { X = x, Y = yOffset });
        }
    }

    static void DrawTextExample4(
    ImpellerDisplayListBuilder builder,
    ImpellerTypographyContext context,
    float x,
    float y)
    {
        float yOffset = y;
        using var paint = ImpellerPaint.New();
        paint.SetColor(ImpellerColor.FromRgb(0, 0, 0));

        // Underline
        using (var paragraphBuilder = context.ParagraphBuilderNew())
        using (var style = ImpellerParagraphStyle.New())
        {
            style.SetForeground(paint);
            style.SetFontSize(18);
            style.SetTextDecoration(ImpellerTextDecoration.Underline(ImpellerColor.FromRgb(255, 0, 0)));

            paragraphBuilder.PushStyle(style);
            paragraphBuilder.AddText("Underlined text");

            using var paragraph = paragraphBuilder.BuildParagraphNew(width: 400);
            builder.DrawParagraph(paragraph, new ImpellerPoint { X = x, Y = yOffset });
            yOffset += 35;
        }

        // Line-through
        using (var paragraphBuilder = context.ParagraphBuilderNew())
        using (var style = ImpellerParagraphStyle.New())
        {
            style.SetForeground(paint);
            style.SetFontSize(18);
            style.SetTextDecoration(ImpellerTextDecoration.LineThrough(ImpellerColor.FromRgb(0, 0, 255)));

            paragraphBuilder.PushStyle(style);
            paragraphBuilder.AddText("Strikethrough text");

            using var paragraph = paragraphBuilder.BuildParagraphNew(width: 400);
            builder.DrawParagraph(paragraph, new ImpellerPoint { X = x, Y = yOffset });
            yOffset += 35;
        }

        // Combined decorations
        using (var paragraphBuilder = context.ParagraphBuilderNew())
        using (var style = ImpellerParagraphStyle.New())
        {
            style.SetForeground(paint);
            style.SetFontSize(25);
            var decoration = new ImpellerTextDecoration(
                ImpellerTextDecorationType.kImpellerTextDecorationTypeUnderline | ImpellerTextDecorationType.kImpellerTextDecorationTypeOverline,
                ImpellerTextDecorationStyle.kImpellerTextDecorationStyleSolid,
                ImpellerColor.FromRgb(0, 255, 0));
            style.SetTextDecoration(decoration);

            paragraphBuilder.PushStyle(style);
            paragraphBuilder.AddText("Underline + Overline");

            using var paragraph = paragraphBuilder.BuildParagraphNew(width: 400);
            builder.DrawParagraph(paragraph, new ImpellerPoint { X = x, Y = yOffset });
        }
    }

    static void DrawTextExample5(
        ImpellerDisplayListBuilder builder,
        ImpellerTypographyContext context,
        float x,
        float y)
    {
        using var paragraphBuilder = context.ParagraphBuilderNew();
        using var style = ImpellerParagraphStyle.New();
        using var paint = ImpellerPaint.New();

        paint.SetColor(ImpellerColor.FromRgb(0, 0, 0));
        style.SetForeground(paint);
        style.SetFontSize(16);
        style.SetHeight(1.2f); // Line height multiplier

        paragraphBuilder.PushStyle(style);
        paragraphBuilder.AddText(
            "This is a longer paragraph that demonstrates text wrapping. " +
            "When text exceeds the maximum width constraint, it automatically " +
            "wraps to the next line. The line height can be controlled with " +
            "the SetHeight method.");

        using var paragraph = paragraphBuilder.BuildParagraphNew(width: 400);
        builder.DrawParagraph(paragraph, new ImpellerPoint { X = x, Y = y });
    }

    static void DrawTextExample6(
        ImpellerDisplayListBuilder builder,
        ImpellerTypographyContext context,
        float x,
        float y)
    {
        using var paragraphBuilder = context.ParagraphBuilderNew();
        using var normalStyle = ImpellerParagraphStyle.New();
        using var boldStyle = ImpellerParagraphStyle.New();
        using var coloredStyle = ImpellerParagraphStyle.New();
        using var paint = ImpellerPaint.New();
        using var redPaint = ImpellerPaint.New();

        paint.SetColor(ImpellerColor.FromRgb(0, 0, 0));
        redPaint.SetColor(ImpellerColor.FromRgb(255, 0, 0));

        // Normal style
        normalStyle.SetForeground(paint);
        normalStyle.SetFontSize(18);
        normalStyle.SetFontWeight(ImpellerFontWeight.kImpellerFontWeight300);

        // Bold style
        boldStyle.SetForeground(paint);
        boldStyle.SetFontSize(18);
        boldStyle.SetFontWeight(ImpellerFontWeight.kImpellerFontWeight700);

        // Colored style
        coloredStyle.SetForeground(redPaint);
        coloredStyle.SetFontSize(18);
        coloredStyle.SetFontWeight(ImpellerFontWeight.kImpellerFontWeight500);

        // Build rich text with style stack
        paragraphBuilder.PushStyle(normalStyle);
        paragraphBuilder.AddText("Rich text with ");

        paragraphBuilder.PushStyle(boldStyle);
        paragraphBuilder.AddText("bold");
        paragraphBuilder.PopStyle();

        paragraphBuilder.AddText(" and ");

        paragraphBuilder.PushStyle(coloredStyle);
        paragraphBuilder.AddText("colored");
        paragraphBuilder.PopStyle();

        paragraphBuilder.AddText(" text!");

        using var paragraph = paragraphBuilder.BuildParagraphNew(width: 500);
        builder.DrawParagraph(paragraph, new ImpellerPoint { X = x, Y = y });
    }

    static void DrawTextExampleCJK(ImpellerDisplayListBuilder builder,
        ImpellerTypographyContext context,
        float x,
        float y)
    {
        using var paragraphBuilder = context.ParagraphBuilderNew();
        using var style = ImpellerParagraphStyle.New();
        using var paint = ImpellerPaint.New();

        paint.SetColor(ImpellerColor.FromRgb(0, 0, 0));
        style.SetForeground(paint);
        style.SetFontSize(16);
        style.SetHeight(1.2f); // Line height multiplier

        paragraphBuilder.PushStyle(style);
        paragraphBuilder.AddText("こんにちは、世界！");
        using var paragraph = paragraphBuilder.BuildParagraphNew(width: 400);
        builder.DrawParagraph(paragraph, new ImpellerPoint { X = x, Y = y });
    }
}