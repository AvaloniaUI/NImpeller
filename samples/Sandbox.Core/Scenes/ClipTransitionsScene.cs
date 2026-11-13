using System.Diagnostics;
using NImpeller;
using static Sandbox.Scenes.Draw;

namespace Sandbox.Scenes;

/// <summary>
/// Port of the interop <c>ClearsOpenGLStancilStateAfterTransition</c> playground, extended with many clip save/restore
/// transitions per frame. Every outline and the bottom bar are drawn unclipped, so a leaked clip shows as missing pixels.
/// </summary>
public sealed class ClipTransitionsScene : IScene, IDisposable
{
    public string Name => "Clip Transitions";
    public string CommandLineName => "cliptransitions";
    public string Description => "Clip/stencil regression: the interop stencil-transition test plus animated clip save/restore, nesting and layers; outlines and the bottom bar must stay intact";

    private const int Cols = 6, Rows = 4;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ImpellerTypographyContext _tc = ImpellerTypographyContext.New()!;

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        float t = (float)_clock.Elapsed.TotalSeconds;
        Background(b, ImpellerColor.FromRgb(20, 22, 28));

        float k = Draw.Scale(p);
        float pad = 14f * k, bar = 22f * k, left = MathF.Min(230f * k, p.Width * 0.3f);
        float gridW = p.Width - left - pad * 2f;
        float gridH = p.Height - pad * 3f - bar;
        if (gridW <= 1f || gridH <= 1f) return;

        DrawInteropCase(b, pad, pad, k);

        float cw = (gridW - pad * (Cols - 1)) / Cols;
        float ch = (gridH - pad * (Rows - 1)) / Rows;
        for (int i = 0; i < Cols * Rows; i++)
        {
            var cell = Rect(left + pad + (i % Cols) * (cw + pad), pad + (i / Cols) * (ch + pad), cw, ch);
            DrawCell(b, i, cell, t, k);
        }

        using var barPaint = Fill(ImpellerColor.FromRgb(60, 200, 120));
        b.DrawRect(Rect(pad, p.Height - pad - bar, p.Width - pad * 2f, bar), barPaint);
        Label(b, _tc, "This bar and every outline must be complete", pad * 2f, p.Height - pad - bar + 3f * k, 13f * k,
            ImpellerColor.FromRgb(10, 40, 20));
    }

    private void DrawInteropCase(ImpellerDisplayListBuilder b, float x, float y, float k)
    {
        Label(b, _tc, "Interop stencil test", x, y, 13f * k, ImpellerColor.FromRgb(170, 176, 190));
        Label(b, _tc, "Red rect must be fully clipped", x, y + 16f * k, 11f * k, ImpellerColor.FromRgb(130, 136, 150));

        b.Save();
        b.Translate(x, y + 30f * k);
        b.Scale(k * 0.9f, k * 0.9f);
        var rect = Rect(10, 20, 100, 200);
        using (var blue = Fill(new ImpellerColor { Blue = 1f, Alpha = 1f }))
        {
            b.DrawRect(rect, blue);
        }
        b.Translate(110, 210);
        b.ClipRect(rect, ImpellerClipOperation.kImpellerClipOperationDifference);
        using (var red = Fill(new ImpellerColor { Red = 1f, Alpha = 1f }))
        {
            b.DrawRect(rect, red);
        }
        b.Restore();
    }

    private static void DrawCell(ImpellerDisplayListBuilder b, int index, ImpellerRect r, float t, float k)
    {
        const ImpellerClipOperation intersect = ImpellerClipOperation.kImpellerClipOperationIntersect;
        const ImpellerClipOperation difference = ImpellerClipOperation.kImpellerClipOperationDifference;

        float phase = t * 1.2f + index * 0.37f;
        float cx = r.X + r.Width / 2f + MathF.Cos(phase) * r.Width * 0.2f;
        float cy = r.Y + r.Height / 2f + MathF.Sin(phase * 1.3f) * r.Height * 0.2f;
        float rad = MathF.Min(r.Width, r.Height) * 0.32f;
        var inner = Rect(cx - rad, cy - rad, rad * 2f, rad * 2f);

        uint before = b.GetSaveCount();
        b.Save();
        b.ClipRect(r, intersect);
        switch (index % 8)
        {
            case 0: b.ClipRect(inner, intersect); break;
            case 1: b.ClipOval(inner, intersect); break;
            case 2: b.ClipRoundedRect(inner, Radii(rad * 0.4f), intersect); break;
            case 3:
                using (var star = Star(cx, cy, rad, rad * 0.45f, 5, phase))
                {
                    b.ClipPath(star, intersect);
                }
                break;
            case 4: b.ClipRect(inner, difference); break;
            case 5: b.ClipOval(inner, difference); break;
            case 6:
                b.Save();
                b.ClipOval(inner, intersect);
                b.Save();
                b.ClipRect(Rect(cx - rad, cy - rad * 0.5f, rad * 2f, rad), intersect);
                b.ClipOval(Rect(cx - rad * 0.3f, cy - rad * 0.3f, rad * 0.6f, rad * 0.6f), difference);
                break;
            default:
                b.ClipOval(inner, intersect);
                using (var layer = Fill(new ImpellerColor { Alpha = 0.6f }))
                {
                    b.SaveLayer(r, layer, null);
                }
                break;
        }

        using (var fill = Fill(Draw.Hsv(index / (float)(Cols * Rows), 0.65f, 0.95f)))
        {
            b.DrawRect(r, fill);
        }
        b.RestoreToCount(before);

        using var outline = Stroke(ImpellerColor.FromRgb(235, 235, 240), 1.5f * k);
        b.DrawRect(r, outline);
    }

    public void Dispose() => _tc.Dispose();
}
