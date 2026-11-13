using System.Numerics;
using NImpeller;
using static Sandbox.Scenes.Draw;

namespace Sandbox.Scenes;

/// <summary>Ports of the Aiks save-layer playgrounds from aiks_dl_basic_unittests.cc, one per cell.</summary>
public sealed class SaveLayerTestsScene : IScene, IDisposable
{
    public string Name => "SaveLayer Tests";
    public string CommandLineName => "savelayertests";
    public string Description => "Aiks save-layer playgrounds: layer bounds, sibling bounds, clipped layers, draw order, empty layers, and culling inside translated layers";

    // Stand-in for an unbounded SaveLayer, which the C API cannot express.
    private static readonly ImpellerRect Unbounded = Rect(-2000, -2000, 4000, 4000);

    private static readonly (string Label, float Width, float Height)[] Cells =
    [
        ("CanPerformSaveLayerWithBounds", 150, 150),
        ("SiblingSaveLayerBoundsAreRespected", 100, 100),
        ("CanRenderClippedLayers", 200, 200),
        ("SaveLayerDrawsBehindSubsequentEntities", 100, 100),
        ("EmptySaveLayerIgnoresPaint (all red)", 400, 400),
        ("EmptySaveLayerRendersWithClear", 320, 320),
        ("DrawDisplayList in translated layer", 400, 400),
        ("Nested layer in translated layer", 400, 400),
    ];

    private readonly ImpellerTypographyContext _tc = ImpellerTypographyContext.New()!;
    private readonly SampleTexture _texture = new();

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        Background(b, ImpellerColor.FromRgb(40, 42, 50));

        float k = Draw.Scale(p);
        float pad = 10f * k, header = 18f * k;
        const int cols = 4, rows = 2;
        float cw = (p.Width - pad * (cols + 1)) / cols;
        float ch = (p.Height - pad * (rows + 1)) / rows - header;
        if (cw <= 1f || ch <= 1f) return;

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
            using (var bg = Fill(ImpellerColor.FromRgb(255, 255, 255)))
            {
                b.DrawPaint(bg);
            }
            b.Translate(ox, oy);
            b.Scale(s, s);
            DrawCell(b, i, _texture.Get(context));
            b.Restore();
        }
    }

    private static void DrawCell(ImpellerDisplayListBuilder b, int index, ImpellerTexture texture)
    {
        var green = ImpellerColor.FromRgb(0, 255, 0);
        switch (index)
        {
            case 0:
            {
                using (var save = Fill(ImpellerColor.FromRgb(0, 0, 0))) b.SaveLayer(Rect(0, 0, 50, 50), save, null);
                using (var red = Fill(ImpellerColor.FromRgb(255, 0, 0))) b.DrawRect(Rect(0, 0, 100, 100), red);
                using (var g = Fill(green)) b.DrawRect(Rect(10, 10, 100, 100), g);
                using (var blue = Fill(ImpellerColor.FromRgb(0, 0, 255))) b.DrawRect(Rect(20, 20, 100, 100), blue);
                b.Restore();
                break;
            }
            case 1:
            {
                var rect = Rect(0, 0, 1000, 1000);
                (float Offset, ImpellerColor Color)[] layers =
                [
                    (25, ImpellerColor.FromRgb(0, 0, 0)), (35, green), (45, ImpellerColor.FromRgb(255, 0, 0)),
                ];
                foreach (var (offset, color) in layers)
                {
                    using (var save = ImpellerPaint.New()!) b.SaveLayer(Rect(offset, offset, 25, 25), save, null);
                    using (var paint = Fill(color)) b.DrawRect(rect, paint);
                    b.Restore();
                }
                break;
            }
            case 2:
            {
                using (var circle = Path(pb => pb.AddOval(Rect(50, 50, 100, 100))))
                {
                    b.ClipPath(circle, ImpellerClipOperation.kImpellerClipOperationIntersect);
                }
                using (var save = ImpellerPaint.New()!) b.SaveLayer(Rect(50, 50, 100, 100), save, null);
                using (var white = Fill(ImpellerColor.FromRgb(255, 255, 255))) b.DrawRect(Rect(0, 0, 400, 400), white);
                using (var g = Fill(green))
                {
                    g.SetBlendMode(ImpellerBlendMode.kImpellerBlendModeHardLight);
                    b.DrawRect(Rect(0, 0, 400, 400), g);
                }
                b.Restore();
                break;
            }
            case 3:
            {
                var rect = Rect(25, 25, 25, 25);
                using (var black = Fill(ImpellerColor.FromRgb(0, 0, 0))) b.DrawRect(rect, black);
                b.Translate(10, 10);
                using (var save = ImpellerPaint.New()!) b.SaveLayer(Unbounded, save, null);
                using (var g = Fill(green)) b.DrawRect(rect, g);
                b.Restore();
                b.Translate(10, 10);
                using (var red = Fill(ImpellerColor.FromRgb(255, 0, 0))) b.DrawRect(rect, red);
                break;
            }
            case 4:
            {
                using (var red = Fill(ImpellerColor.FromRgb(255, 0, 0))) b.DrawPaint(red);
                b.ClipRect(Rect(100, 100, 200, 200), ImpellerClipOperation.kImpellerClipOperationIntersect);
                using (var blue = Fill(ImpellerColor.FromRgb(0, 0, 255))) b.SaveLayer(Unbounded, blue, null);
                b.Restore();
                break;
            }
            case 5:
            {
                using (var plain = ImpellerPaint.New()!)
                {
                    b.DrawTextureRect(texture, Rect(0, 0, SampleTexture.Size, SampleTexture.Size), Rect(10, 10, 300, 300),
                        ImpellerTextureSampling.kImpellerTextureSamplingLinear, plain);
                }
                b.ClipRect(Rect(100, 100, 200, 200), ImpellerClipOperation.kImpellerClipOperationIntersect);
                using (var clear = ImpellerPaint.New()!)
                {
                    clear.SetBlendMode(ImpellerBlendMode.kImpellerBlendModeClear);
                    b.SaveLayer(Unbounded, clear, null);
                }
                b.Restore();
                break;
            }
            default:
            {
                using var shift = ImpellerImageFilter.CreateMatrixNew(Matrix4x4.CreateTranslation(300, 0, 0),
                    ImpellerTextureSampling.kImpellerTextureSamplingLinear)!;
                using (var layer = ImpellerPaint.New()!)
                {
                    layer.SetImageFilter(shift);
                    b.SaveLayer(Unbounded, layer, null);
                }
                using var g = Fill(green);
                b.DrawOval(Rect(-180, 20, 160, 160), g);
                if (index == 6)
                {
                    using var inner = ImpellerDisplayListBuilder.New(Unbounded)!;
                    inner.DrawOval(Rect(-180, 220, 160, 160), g);
                    using var dl = inner.CreateDisplayListNew()!;
                    b.DrawDisplayList(dl, 1f);
                }
                else
                {
                    using (var child = ImpellerPaint.New()!) b.SaveLayer(Unbounded, child, null);
                    b.DrawOval(Rect(-180, 220, 160, 160), g);
                    b.Restore();
                }
                b.Restore();
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
