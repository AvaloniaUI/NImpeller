using System.Numerics;
using NImpeller;
using static Sandbox.Scenes.Draw;

namespace Sandbox.Scenes;

/// <summary>
/// Ports of the Aiks <c>FilledRoundRectsRenderCorrectly</c>, <c>FilledCirclesRenderCorrectly</c>,
/// <c>SolidColorCirclesOvalsRRectsMaskBlurCorrectly</c> and <c>CompareDiffRoundRectAndRoundRect</c> playgrounds.
/// </summary>
public sealed class RoundRectsScene : IScene, IDisposable
{
    public string Name => "Round Rects & Circles";
    public string CommandLineName => "roundrects";
    public string Description => "Elliptical-radius rounded rects with color, gradient and image sources, concentric circles, small-sigma mask blurs, and rrect difference vs clear";

    private static readonly (string Label, float Width, float Height)[] Cells =
    [
        ("FilledRoundRectsRenderCorrectly", 1000, 760),
        ("FilledCirclesRenderCorrectly", 800, 720),
        ("SolidColorCirclesOvalsRRectsMaskBlurCorrectly", 620, 600),
        ("CompareDiffRoundRectAndRoundRect (should match)", 400, 200),
    ];

    private static readonly ImpellerColor[] Colors =
    [
        ImpellerColor.FromRgb(0, 0, 255), ImpellerColor.FromRgb(0, 255, 0), ImpellerColor.FromRgb(220, 20, 60),
    ];

    private readonly ImpellerTypographyContext _tc = ImpellerTypographyContext.New()!;
    private readonly SampleTexture _texture = new();

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        Background(b, ImpellerColor.FromRgb(40, 42, 50));

        float k = Draw.Scale(p);
        float pad = 10f * k, header = 18f * k;
        const int cols = 2, rows = 2;
        float cw = (p.Width - pad * (cols + 1)) / cols;
        float ch = (p.Height - pad * (rows + 1)) / rows - header;
        if (cw <= 1f || ch <= 1f) return;
        var texture = _texture.Get(context);

        for (int i = 0; i < Cells.Length; i++)
        {
            float x = pad + (i % cols) * (cw + pad);
            float y = pad + (i / cols) * (ch + header + pad);
            Label(b, _tc, Cells[i].Label, x, y, 11f * k, ImpellerColor.FromRgb(200, 204, 214));

            float s = MathF.Min(cw / Cells[i].Width, ch / Cells[i].Height);
            float w = Cells[i].Width * s, h = Cells[i].Height * s;
            float ox = x + (cw - w) / 2f, oy = y + header + (ch - h) / 2f;
            b.Save();
            b.ClipRect(Rect(ox, oy, w, h), ImpellerClipOperation.kImpellerClipOperationIntersect);
            b.Translate(ox, oy);
            b.Scale(s, s);
            switch (i)
            {
                case 0: FilledRoundRects(b, texture); break;
                case 1: FilledCircles(b, texture); break;
                case 2: MaskBlurs(b); break;
                default: DiffRoundRect(b); break;
            }
            b.Restore();
        }
    }

    private static ImpellerRoundingRadii Elliptical(float rx, float ry)
    {
        var r = Point(rx, ry);
        return new ImpellerRoundingRadii { Top_left = r, Top_right = r, Bottom_left = r, Bottom_right = r };
    }

    private static ImpellerRect Ltrb(float l, float t, float r, float bottom) => Rect(l, t, r - l, bottom - t);

    private static ImpellerColorSource Sunset(float cx, float cy)
    {
        ReadOnlySpan<ImpellerColor> colors =
        [
            ImpellerColor.FromRgb(0x1f, 0, 0x5c), ImpellerColor.FromRgb(0x5b, 0, 0x60), ImpellerColor.FromRgb(0x87, 0x01, 0x60),
            ImpellerColor.FromRgb(0xac, 0x25, 0x53), ImpellerColor.FromRgb(0xe1, 0x6b, 0x5c), ImpellerColor.FromRgb(0xf3, 0x90, 0x60),
            ImpellerColor.FromRgb(0xff, 0xb5, 0x6b),
        ];
        ReadOnlySpan<float> stops = [0f, 1f / 6f, 2f / 6f, 3f / 6f, 4f / 6f, 5f / 6f, 1f];
        return ImpellerColorSource.CreateRadialGradientNew(Point(cx, cy), 75, colors, stops, ImpellerTileMode.kImpellerTileModeMirror)!;
    }

    private static ImpellerColorSource Image(ImpellerTexture texture, float tx, float ty) =>
        texture.ColorSourceCreateImageNew(ImpellerTileMode.kImpellerTileModeRepeat, ImpellerTileMode.kImpellerTileModeRepeat,
            ImpellerTextureSampling.kImpellerTextureSamplingNearestNeighbor, Matrix4x4.CreateTranslation(tx, ty, 0))!;

    private static void FilledRoundRects(ImpellerDisplayListBuilder b, ImpellerTexture texture)
    {
        using (var white = Fill(ImpellerColor.FromRgb(255, 255, 255))) b.DrawPaint(white);
        int c = 0;
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                using var paint = Fill(Colors[c++ % Colors.Length]);
                b.DrawRoundedRect(Rect(i * 100 + 10, j * 100 + 20, 80, 80), Elliptical(i * 5 + 10, j * 5 + 10), paint);
            }
        }
        using (var paint = Fill(Colors[c++ % Colors.Length])) b.DrawRoundedRect(Rect(10, 420, 380, 80), Elliptical(40, 40), paint);
        using (var paint = Fill(Colors[c++ % Colors.Length])) b.DrawRoundedRect(Rect(410, 20, 80, 380), Elliptical(40, 40), paint);

        using (var src = Sunset(550, 550))
        using (var paint = Fill(new ImpellerColor { Red = 1, Green = 1, Blue = 1, Alpha = 0.1f }))
        {
            paint.SetColorSource(src);
            for (int i = 1; i <= 10; i++)
            {
                int j = 11 - i;
                b.DrawRoundedRect(Ltrb(550 - i * 20, 550 - j * 20, 550 + i * 20, 550 + j * 20), Elliptical(i * 10, j * 10), paint);
            }
        }
        using (var src = Sunset(200, 650))
        using (var paint = Fill(new ImpellerColor { Red = 1, Green = 1, Blue = 1, Alpha = 0.5f }))
        {
            paint.SetColorSource(src);
            b.DrawRoundedRect(Ltrb(100, 610, 300, 690), Elliptical(40, 40), paint);
            b.DrawRoundedRect(Ltrb(160, 550, 240, 750), Elliptical(40, 40), paint);
        }
        using (var src = Image(texture, 520, 20))
        using (var paint = Fill(new ImpellerColor { Red = 1, Green = 1, Blue = 1, Alpha = 0.1f }))
        {
            paint.SetColorSource(src);
            for (int i = 1; i <= 10; i++)
            {
                int j = 11 - i;
                b.DrawRoundedRect(Ltrb(720 - i * 20, 220 - j * 20, 720 + i * 20, 220 + j * 20), Elliptical(i * 10, j * 10), paint);
            }
        }
        using (var src = Image(texture, 800, 300))
        using (var paint = Fill(new ImpellerColor { Red = 1, Green = 1, Blue = 1, Alpha = 0.5f }))
        {
            paint.SetColorSource(src);
            b.DrawRoundedRect(Ltrb(800, 410, 1000, 490), Elliptical(40, 40), paint);
            b.DrawRoundedRect(Ltrb(860, 350, 940, 550), Elliptical(40, 40), paint);
        }
    }

    private static void FilledCircles(ImpellerDisplayListBuilder b, ImpellerTexture texture)
    {
        using (var white = Fill(ImpellerColor.FromRgb(255, 255, 255))) b.DrawPaint(white);
        int c = 0;
        for (int radius = 600; radius > 0; radius -= radius > 30 ? 10 : 2)
        {
            using var paint = Fill(Colors[c++ % Colors.Length]);
            b.DrawOval(Rect(10 - radius, 10 - radius, radius * 2, radius * 2), paint);
        }
        using (var src = Sunset(500, 600))
        using (var paint = ImpellerPaint.New()!)
        {
            paint.SetColorSource(src);
            b.DrawOval(Rect(400, 500, 200, 200), paint);
        }
        using (var src = Image(texture, 700, 200))
        using (var paint = ImpellerPaint.New()!)
        {
            paint.SetColorSource(src);
            b.DrawOval(Rect(600, 100, 200, 200), paint);
        }
    }

    private static void MaskBlurs(ImpellerDisplayListBuilder b)
    {
        using (var white = Fill(ImpellerColor.FromRgb(255, 255, 255))) b.DrawPaint(white);
        using var mask = ImpellerMaskFilter.CreateBlurNew(ImpellerBlurStyle.kImpellerBlurStyleNormal, 1f)!;
        float y = 50f;
        using (var paint = Fill(ImpellerColor.FromRgb(220, 20, 60)))
        {
            paint.SetMaskFilter(mask);
            for (int i = 0; i < 5; i++)
            {
                float x = (i + 1) * 100, r = x / 10f;
                b.DrawRect(Rect(x + 25 - r / 2f, y + r / 2f, r, 60f - r), paint);
            }
        }
        y += 100f;
        using (var paint = Fill(ImpellerColor.FromRgb(0, 0, 255)))
        {
            paint.SetMaskFilter(mask);
            for (int i = 0; i < 5; i++)
            {
                float x = (i + 1) * 100, r = x / 10f;
                b.DrawOval(Rect(x + 25 - r, y + 25 - r, r * 2f, r * 2f), paint);
            }
        }
        y += 100f;
        using (var paint = Fill(ImpellerColor.FromRgb(0, 255, 0)))
        {
            paint.SetMaskFilter(mask);
            for (int i = 0; i < 5; i++)
            {
                float x = (i + 1) * 100, r = x / 10f;
                b.DrawOval(Rect(x + 25 - r / 2f, y + r / 2f, r, 60f - r), paint);
            }
        }
        y += 100f;
        using (var paint = Fill(ImpellerColor.FromRgb(128, 0, 128)))
        {
            paint.SetMaskFilter(mask);
            for (int i = 0; i < 5; i++)
            {
                float x = (i + 1) * 100, r = x / 20f;
                b.DrawRoundedRect(Rect(x, y, 60, 60), Elliptical(r, r), paint);
            }
        }
        y += 100f;
        using (var paint = Fill(ImpellerColor.FromRgb(255, 165, 0)))
        {
            paint.SetMaskFilter(mask);
            for (int i = 0; i < 5; i++)
            {
                float x = (i + 1) * 100, r = x / 20f;
                b.DrawRoundedRect(Rect(x, y, 60, 60), Elliptical(r, 5f), paint);
            }
        }
    }

    private static void DiffRoundRect(ImpellerDisplayListBuilder b)
    {
        using (var black = Fill(ImpellerColor.FromRgb(0, 0, 0)))
        {
            black.SetBlendMode(ImpellerBlendMode.kImpellerBlendModeSource);
            b.DrawPaint(black);
        }
        var outer = Rect(0, 0, 100, 100);
        var outerRadii = Radii(5, 10, 50, 20);
        const float inset = 5f;
        var inner = Rect(inset, inset, 100 - inset * 2f, 100 - inset * 2f);
        var innerRadii = Radii(5 - inset, 10 - inset, 50 - inset, 20 - inset);
        using var sky = Fill(ImpellerColor.FromRgb(135, 206, 235));
        b.Translate(50, 50);
        b.DrawRoundedRectDifference(outer, outerRadii, inner, innerRadii, sky);
        b.Translate(200, 0);
        b.DrawRoundedRect(outer, outerRadii, sky);
        using var clear = ImpellerPaint.New()!;
        clear.SetBlendMode(ImpellerBlendMode.kImpellerBlendModeClear);
        b.DrawRoundedRect(inner, innerRadii, clear);
    }

    public void Dispose()
    {
        _texture.Dispose();
        _tc.Dispose();
    }
}
