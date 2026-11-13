using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NImpeller;
using static Sandbox.Scenes.Draw;

namespace Sandbox.Scenes;

/// <summary>
/// After the Aiks <c>CanRenderGroupOpacity</c> and <c>TranslucentSaveLayer*DrawsCorrectly</c> playgrounds:
/// the same two overlapping shapes composited through different layer configurations.
/// </summary>
public sealed class LayersScene : IScene, IDisposable
{
    public string Name => "Layers";
    public string CommandLineName => "layers";
    public string Description => "Per-draw vs group opacity, nested display lists, and translucent SaveLayers with color, matrix, image filters and blend modes";

    private const int Cols = 4, Rows = 2;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ImpellerTypographyContext _tc = ImpellerTypographyContext.New()!;

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        float t = (float)_clock.Elapsed.TotalSeconds;
        Background(b, ImpellerColor.FromRgb(30, 32, 40));

        float k = MathF.Max(1f, p.Height / 640f);
        float pad = 14f * k, header = 20f * k;
        float cw = (p.Width - pad * (Cols + 1)) / Cols;
        float ch = (p.Height - pad * (Rows + 1)) / Rows - header;
        if (cw <= 1f || ch <= 1f) return;
        float alpha = 0.35f + 0.3f * (0.5f + 0.5f * MathF.Sin(t * 1.2f));

        string[] labels =
        [
            "Per-draw alpha", "Group opacity (SaveLayer)", "DrawDisplayList opacity", "Nested layers x3",
            "Layer + blend color filter", "Layer + grayscale matrix", "Layer + blur image filter", "Layer blend: Multiply",
        ];
        for (int i = 0; i < labels.Length; i++)
        {
            float x = pad + (i % Cols) * (cw + pad);
            float y = pad + (i / Cols) * (ch + header + pad);
            Label(b, _tc, labels[i], x, y, 13f * k, ImpellerColor.FromRgb(190, 194, 206));
            var cell = Rect(x, y + header, cw, ch);

            b.Save();
            b.ClipRect(cell, ImpellerClipOperation.kImpellerClipOperationIntersect);
            Stripes(b, cell, 10f * k);
            DrawCell(b, i, cell, alpha, t);
            b.Restore();
        }
    }

    private static void DrawCell(ImpellerDisplayListBuilder b, int index, ImpellerRect cell, float alpha, float t)
    {
        switch (index)
        {
            case 0:
                Shapes(b, cell, alpha, t);
                break;
            case 1:
                using (var layer = Fill(new ImpellerColor { Alpha = alpha }))
                {
                    b.SaveLayer(cell, layer, null);
                    Shapes(b, cell, 1f, t);
                    b.Restore();
                }
                break;
            case 2:
                using (var inner = ImpellerDisplayListBuilder.New(cell)!)
                {
                    Shapes(inner, cell, 1f, t);
                    using var dl = inner.CreateDisplayListNew()!;
                    b.DrawDisplayList(dl, alpha);
                }
                break;
            case 3:
                using (var layer = Fill(new ImpellerColor { Alpha = MathF.Cbrt(alpha) }))
                {
                    for (int i = 0; i < 3; i++) b.SaveLayer(cell, layer, null);
                    Shapes(b, cell, 1f, t);
                    for (int i = 0; i < 3; i++) b.Restore();
                }
                break;
            case 4:
                using (var filter = ImpellerColorFilter.CreateBlendNew(ImpellerColor.FromRgb(255, 200, 0), ImpellerBlendMode.kImpellerBlendModeModulate)!)
                using (var layer = Fill(new ImpellerColor { Alpha = alpha + 0.3f }))
                {
                    layer.SetColorFilter(filter);
                    b.SaveLayer(cell, layer, null);
                    Shapes(b, cell, 1f, t);
                    b.Restore();
                }
                break;
            case 5:
                using (var filter = Grayscale())
                using (var layer = Fill(new ImpellerColor { Alpha = alpha + 0.3f }))
                {
                    layer.SetColorFilter(filter);
                    b.SaveLayer(cell, layer, null);
                    Shapes(b, cell, 1f, t);
                    b.Restore();
                }
                break;
            case 6:
                using (var filter = ImpellerImageFilter.CreateBlurNew(6f, 6f, ImpellerTileMode.kImpellerTileModeDecal)!)
                using (var layer = Fill(new ImpellerColor { Alpha = alpha + 0.3f }))
                {
                    layer.SetImageFilter(filter);
                    b.SaveLayer(cell, layer, null);
                    Shapes(b, cell, 1f, t);
                    b.Restore();
                }
                break;
            default:
                using (var layer = Fill(new ImpellerColor { Alpha = 1f }))
                {
                    layer.SetBlendMode(ImpellerBlendMode.kImpellerBlendModeMultiply);
                    b.SaveLayer(cell, layer, null);
                    Shapes(b, cell, 1f, t);
                    b.Restore();
                }
                break;
        }
    }

    private static void Shapes(ImpellerDisplayListBuilder b, ImpellerRect cell, float alpha, float t)
    {
        float s = MathF.Min(cell.Width, cell.Height) * 0.5f;
        float cx = cell.X + cell.Width / 2f, cy = cell.Y + cell.Height / 2f;
        float d = s * 0.1f * MathF.Sin(t * 0.8f);
        using (var red = Fill(ImpellerColor.FromArgb((int)(alpha * 255), 235, 60, 60)))
        {
            b.DrawOval(Rect(cx - s * 0.85f + d, cy - s / 2f, s, s), red);
        }
        using var blue = Fill(ImpellerColor.FromArgb((int)(alpha * 255), 60, 120, 240));
        b.DrawRoundedRect(Rect(cx - s * 0.15f - d, cy - s / 2f, s, s), Radii(s * 0.2f), blue);
    }

    private static void Stripes(ImpellerDisplayListBuilder b, ImpellerRect r, float band)
    {
        using var light = Fill(ImpellerColor.FromRgb(225, 225, 230));
        using var dark = Fill(ImpellerColor.FromRgb(70, 72, 80));
        b.DrawRect(r, light);
        for (float x = r.X; x < r.X + r.Width; x += band * 2f)
        {
            b.DrawRect(Rect(x, r.Y, band, r.Height), dark);
        }
    }

    private static ImpellerColorFilter Grayscale()
    {
        var matrix = new ImpellerColorMatrix();
        ReadOnlySpan<float> v =
        [
            0.2126f, 0.7152f, 0.0722f, 0, 0,
            0.2126f, 0.7152f, 0.0722f, 0, 0,
            0.2126f, 0.7152f, 0.0722f, 0, 0,
            0, 0, 0, 1, 0,
        ];
        v.CopyTo(MemoryMarshal.CreateSpan(ref Unsafe.As<ImpellerColorMatrix, float>(ref matrix), 20));
        return ImpellerColorFilter.CreateColorMatrixNew(matrix)!;
    }

    public void Dispose() => _tc.Dispose();
}
