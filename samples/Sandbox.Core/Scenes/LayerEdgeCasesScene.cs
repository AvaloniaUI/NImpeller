using System.Numerics;
using NImpeller;
using static Sandbox.Scenes.Draw;

namespace Sandbox.Scenes;

/// <summary>Ports of the Aiks save-layer and matrix-filter edge cases from aiks_dl_unittests.cc, one per cell.</summary>
public sealed class LayerEdgeCasesScene : IScene, IDisposable
{
    public string Name => "Layer Edge Cases";
    public string CommandLineName => "layeredgecases";
    public string Description => "Aiks edge cases: collapsed subpass paints, translucent layers with filters and advanced blends, destructive layers, matrix layer filters, transparent shadows and backdrop restores";

    private static readonly ImpellerRect Unbounded = Rect(-2000, -2000, 4000, 4000);

    private static readonly (string Label, float Width, float Height)[] Cells =
    [
        ("CollapsedDrawPaintInSubpass", 400, 400),
        ("...InSubpassBackdropFilter", 400, 400),
        ("TranslucentSaveLayer", 500, 900),
        ("...WithBlendColorFilter", 500, 900),
        ("...WithColorAndImageFilter", 500, 900),
        ("...WithAdvancedBlendMode", 400, 400),
        ("TinyOverlappingSubpasses (green)", 200, 200),
        ("CanRenderDestructiveSaveLayer", 400, 400),
        ("StrokedPathWithMoveToThenClose", 500, 500),
        ("MatrixImageFilterMagnify (shifted)", 560, 560),
        ("MatrixSaveLayerFilter", 400, 400),
        ("TransparentShadow (empty)", 400, 200),
        ("BackdropRestoreFirstClip", 150, 150),
    ];

    private readonly ImpellerTypographyContext _tc = ImpellerTypographyContext.New()!;
    private readonly SampleTexture _texture = new();

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        Background(b, ImpellerColor.FromRgb(40, 42, 50));

        float k = Draw.Scale(p);
        float pad = 8f * k, header = 16f * k;
        const int cols = 5, rows = 3;
        float cw = (p.Width - pad * (cols + 1)) / cols;
        float ch = (p.Height - pad * (rows + 1)) / rows - header;
        if (cw <= 1f || ch <= 1f) return;
        var texture = _texture.Get(context);

        for (int i = 0; i < Cells.Length; i++)
        {
            float x = pad + (i % cols) * (cw + pad);
            float y = pad + (i / cols) * (ch + header + pad);
            Label(b, _tc, Cells[i].Label, x, y, 10f * k, ImpellerColor.FromRgb(200, 204, 214));

            float s = MathF.Min(cw / Cells[i].Width, ch / Cells[i].Height);
            float w = Cells[i].Width * s, h = Cells[i].Height * s;
            float ox = x + (cw - w) / 2f, oy = y + header + (ch - h) / 2f;
            b.Save();
            b.ClipRect(Rect(ox, oy, w, h), ImpellerClipOperation.kImpellerClipOperationIntersect);
            using (var bg = Fill(ImpellerColor.FromRgb(0, 0, 0)))
            {
                bg.SetBlendMode(ImpellerBlendMode.kImpellerBlendModeSource);
                b.DrawPaint(bg);
            }
            b.Translate(ox, oy);
            b.Scale(s, s);
            uint count = b.GetSaveCount();
            DrawCell(b, i, texture);
            b.RestoreToCount(count);
            b.Restore();
        }
    }

    private static void DrawCell(ImpellerDisplayListBuilder b, int index, ImpellerTexture texture)
    {
        var blue = ImpellerColor.FromRgb(0, 0, 255);
        var green = ImpellerColor.FromRgb(0, 255, 0);
        var red = ImpellerColor.FromRgb(255, 0, 0);
        switch (index)
        {
            case 0:
            case 1:
            {
                using (var yellow = Fill(ImpellerColor.FromRgb(255, 255, 0)))
                {
                    yellow.SetBlendMode(ImpellerBlendMode.kImpellerBlendModeSource);
                    b.DrawPaint(yellow);
                }
                if (index == 0)
                {
                    using var save = ImpellerPaint.New()!;
                    save.SetBlendMode(ImpellerBlendMode.kImpellerBlendModeMultiply);
                    b.SaveLayer(Unbounded, save, null);
                }
                else
                {
                    using var blur = ImpellerImageFilter.CreateBlurNew(20, 20, ImpellerTileMode.kImpellerTileModeDecal)!;
                    b.SaveLayer(Unbounded, null, blur);
                }
                using var cornflower = Fill(new ImpellerColor { Red = 100f / 255f, Green = 149f / 255f, Blue = 237f / 255f, Alpha = index == 0 ? 0.75f : 1f });
                b.DrawPaint(cornflower);
                b.Restore();
                break;
            }
            case 2:
            case 3:
            case 4:
            {
                using (var paint = Fill(blue)) b.DrawRect(Rect(100, 100, 300, 300), paint);
                using var save = Fill(ImpellerColor.FromArgb(128, 0, 0, 0));
                using var filter = index == 2 ? null : ImpellerColorFilter.CreateBlendNew(red, ImpellerBlendMode.kImpellerBlendModeDestinationOver);
                if (filter is not null) save.SetColorFilter(filter);
                if (index != 2)
                {
                    b.Save();
                    b.ClipRect(Rect(100, 500, 300, 300), ImpellerClipOperation.kImpellerClipOperationIntersect);
                }
                b.SaveLayer(Unbounded, save, null);
                using (var paint = Fill(blue)) b.DrawRect(Rect(100, 500, 300, 300), paint);
                b.Restore();
                if (index != 2) b.Restore();
                break;
            }
            case 5:
            {
                using (var paint = Fill(red)) b.DrawRect(Rect(0, 0, 400, 400), paint);
                using (var save = Fill(new ImpellerColor { Alpha = 0.5f }))
                {
                    save.SetBlendMode(ImpellerBlendMode.kImpellerBlendModeLighten);
                    b.SaveLayer(Unbounded, save, null);
                }
                using (var paint = Fill(green)) b.DrawOval(Rect(100, 100, 200, 200), paint);
                b.Restore();
                break;
            }
            case 6:
            {
                using (var paint = Fill(red)) b.DrawPaint(paint);
                using var yellow = Fill(ImpellerColor.FromRgb(255, 255, 0));
                for (int i = 0; i < 2; i++)
                {
                    b.SaveLayer(Unbounded, null, null);
                    b.DrawOval(Rect(99.9f, 99.9f, 0.2f, 0.2f), yellow);
                    b.Restore();
                }
                using (var paint = Fill(green)) b.DrawPaint(paint);
                break;
            }
            case 7:
            {
                using (var paint = Fill(red)) b.DrawPaint(paint);
                using (var save = ImpellerPaint.New()!)
                {
                    save.SetBlendMode(ImpellerBlendMode.kImpellerBlendModeSource);
                    b.SaveLayer(Unbounded, save, null);
                }
                using (var paint = Fill(green)) b.DrawOval(Rect(200, 200, 200, 200), paint);
                b.Restore();
                break;
            }
            case 8:
            {
                using var path = Path(pb =>
                {
                    pb.MoveTo(Point(0, 400));
                    pb.LineTo(Point(0, 0));
                    pb.LineTo(Point(400, 0));
                    pb.MoveTo(Point(0, 400));
                    pb.Close();
                });
                b.Translate(50, 50);
                using var paint = Stroke(blue, 10);
                paint.SetStrokeCap(ImpellerStrokeCap.kImpellerStrokeCapRound);
                b.DrawPath(path, paint);
                break;
            }
            case 9:
            {
                b.Translate(20, 20);
                using var magnify = ImpellerImageFilter.CreateMatrixNew(Matrix4x4.CreateScale(2f, 2f, 1f), ImpellerTextureSampling.kImpellerTextureSamplingLinear)!;
                using (var layer = ImpellerPaint.New()!)
                {
                    layer.SetImageFilter(magnify);
                    b.SaveLayer(Unbounded, layer, null);
                }
                using (var half = Fill(new ImpellerColor { Alpha = 0.5f }))
                {
                    b.DrawTextureRect(texture, Rect(0, 0, SampleTexture.Size, SampleTexture.Size), Rect(0, 0, SampleTexture.Size, SampleTexture.Size),
                        ImpellerTextureSampling.kImpellerTextureSamplingLinear, half);
                }
                b.Restore();
                break;
            }
            case 10:
            {
                b.SaveLayer(Unbounded, null, null);
                using var plus = Fill(new ImpellerColor { Green = 1f, Alpha = 0.5f });
                plus.SetBlendMode(ImpellerBlendMode.kImpellerBlendModePlus);
                b.DrawOval(Rect(100, 100, 200, 200), plus);
                float edge = 200f + 100f / MathF.Sqrt(2f);
                var m = Matrix4x4.CreateTranslation(-200, -200, 0) * Matrix4x4.CreateScale(0.5f, 0.5f, 1f) * Matrix4x4.CreateTranslation(edge, edge, 0);
                using var filter = ImpellerImageFilter.CreateMatrixNew(m, ImpellerTextureSampling.kImpellerTextureSamplingLinear)!;
                using (var layer = ImpellerPaint.New()!)
                {
                    layer.SetImageFilter(filter);
                    b.SaveLayer(Unbounded, layer, null);
                }
                b.DrawOval(Rect(100, 100, 200, 200), plus);
                b.Restore();
                b.Restore();
                break;
            }
            case 11:
            {
                b.Scale(1.618f, 1.618f);
                using var path = Path(pb => pb.AddRect(Rect(0, 0, 200, 100)));
                b.DrawShadow(path, new ImpellerColor(), 15, 0, 1);
                break;
            }
            default:
            {
                b.ClipRect(Rect(50, 50, 50, 50), ImpellerClipOperation.kImpellerClipOperationDifference);
                using (var paint = Fill(red)) b.DrawRect(Rect(0, 0, 100, 100), paint);
                uint count = b.GetSaveCount();
                b.Save();
                b.ClipRect(Rect(0, 0, 100, 100), ImpellerClipOperation.kImpellerClipOperationIntersect);
                using (var blur = ImpellerImageFilter.CreateBlurNew(10, 10, ImpellerTileMode.kImpellerTileModeDecal)!)
                {
                    b.SaveLayer(Unbounded, null, blur);
                }
                b.RestoreToCount(count);
                using (var paint = Fill(green)) b.DrawRect(Rect(0, 0, 100, 100), paint);
                break;
            }
        }
    }

    public void Dispose()
    {
        _texture.Dispose();
        _tc.Dispose();
    }
}
