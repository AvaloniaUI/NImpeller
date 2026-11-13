using System.Diagnostics;
using System.Numerics;
using NImpeller;
using static Sandbox.Scenes.Draw;

namespace Sandbox.Scenes;

/// <summary>
/// Ports of the Aiks <c>CanPerformSkew</c>, <c>CanDrawPerspectiveTransformWithClips</c>, <c>PerspectiveRectangle</c>,
/// <c>ImageColorSourceEffectTransform</c>, <c>DrawImageRectSrcOutsideBounds</c> and
/// <c>CanRenderWideStrokedRectWithoutOverlap</c> playgrounds.
/// </summary>
public sealed class TransformsAndImagesScene : IScene, IDisposable
{
    public string Name => "Transforms & Images";
    public string CommandLineName => "transformsimages";
    public string Description => "Skew, perspective with clips, full perspective rect, image color source transforms, out-of-bounds source rects and wide translucent strokes";

    private static readonly (string Label, float Width, float Height)[] Cells =
    [
        ("CanPerformSkew", 600, 600),
        ("CanDrawPerspectiveTransformWithClips", 600, 600),
        ("PerspectiveRectangle", 800, 800),
        ("ImageColorSourceEffectTransform", 360, 360),
        ("DrawImageRectSrcOutsideBounds", 800, 800),
        ("CanRenderWideStrokedRectWithoutOverlap", 850, 650),
    ];

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ImpellerTypographyContext _tc = ImpellerTypographyContext.New()!;
    private readonly SampleTexture _texture = new();

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        float t = (float)_clock.Elapsed.TotalSeconds;
        Background(b, ImpellerColor.FromRgb(40, 42, 50));

        float k = Draw.Scale(p);
        float pad = 10f * k, header = 18f * k;
        const int cols = 3, rows = 2;
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
            using (var bg = Fill(ImpellerColor.FromRgb(255, 255, 255)))
            {
                b.DrawPaint(bg);
            }
            b.Translate(ox, oy);
            b.Scale(s, s);
            DrawCell(b, i, texture, t);
            b.Restore();
        }
    }

    private static void DrawCell(ImpellerDisplayListBuilder b, int index, ImpellerTexture texture, float t)
    {
        switch (index)
        {
            case 0:
            {
                var skew = Matrix4x4.Identity;
                skew.M21 = 2f;
                skew.M12 = 5f;
                b.Transform(skew);
                using var red = Fill(ImpellerColor.FromRgb(255, 0, 0));
                b.DrawRect(Rect(0, 0, 100, 100), red);
                break;
            }
            case 1:
            {
                b.Translate(300, 300);
                b.Save();
                using (var green = Fill(ImpellerColor.FromRgb(0, 255, 0))) b.DrawPaint(green);
                b.ClipRect(Rect(-180, -180, 360, 360), ImpellerClipOperation.kImpellerClipOperationDifference);
                using (var black = Fill(ImpellerColor.FromRgb(0, 0, 0))) b.DrawPaint(black);
                b.Restore();

                b.Save();
                b.ClipOval(Rect(-200, -200, 400, 400), ImpellerClipOperation.kImpellerClipOperationIntersect);
                var persp = Matrix4x4.Identity;
                persp.M34 = 0.003f;
                b.Transform(Matrix4x4.CreateRotationY(-1f + t % 3f) * persp);
                using (var plain = ImpellerPaint.New()!)
                {
                    float half = SampleTexture.Size / 2f * 1.5f;
                    b.DrawTextureRect(texture, Rect(0, 0, SampleTexture.Size, SampleTexture.Size), Rect(-half, -half, half * 2f, half * 2f),
                        ImpellerTextureSampling.kImpellerTextureSamplingLinear, plain);
                }
                b.Restore();
                using var blue = Fill(new ImpellerColor { Blue = 1f, Alpha = 0.4f });
                b.DrawOval(Rect(-230, -230, 460, 460), blue);
                break;
            }
            case 2:
            {
                float val = (58f + 20f * MathF.Sin(t)) * -0.00005f;
                var m = Matrix4x4.Identity;
                m.M41 = 400f;
                m.M42 = 400f;
                m.M24 = val;
                m.M44 = 2.2f;
                b.Transform(m);
                b.ClipRect(Rect(0, 0, 400, 800), ImpellerClipOperation.kImpellerClipOperationIntersect);
                using (var blue = Fill(ImpellerColor.FromRgb(0, 0, 255))) b.DrawRect(Rect(0, 0, 400, 800), blue);
                using var white = Fill(new ImpellerColor { Red = 1, Green = 1, Blue = 1, Alpha = 0.5f });
                b.DrawPaint(white);
                break;
            }
            case 3:
            {
                const ImpellerTileMode repeat = ImpellerTileMode.kImpellerTileModeRepeat;
                const ImpellerTextureSampling nearest = ImpellerTextureSampling.kImpellerTextureSamplingNearestNeighbor;
                using (var src = texture.ColorSourceCreateImageNew(repeat, repeat, nearest, Matrix4x4.CreateTranslation(50, 50, 0))!)
                using (var paint = ImpellerPaint.New()!)
                {
                    paint.SetColorSource(src);
                    b.DrawRect(Rect(0, 0, 100, 100), paint);
                }

                b.Save();
                b.Rotate(45);
                var shear = Matrix4x4.Identity;
                shear.M12 = -1f;
                shear.M21 = 1f;
                using (var src = texture.ColorSourceCreateImageNew(repeat, repeat, nearest, shear)!)
                using (var paint = ImpellerPaint.New()!)
                {
                    paint.SetColorSource(src);
                    b.DrawRect(Rect(100, 0, 100, 100), paint);
                }
                b.Restore();

                b.Save();
                b.Translate(100, 0);
                b.Scale(100, 100);
                using (var src = texture.ColorSourceCreateImageNew(repeat, repeat, nearest, Matrix4x4.CreateScale(0.005f, 0.005f, 1f))!)
                using (var paint = ImpellerPaint.New()!)
                {
                    paint.SetColorSource(src);
                    b.DrawRect(Rect(0, 0, 1, 1), paint);
                }
                b.Restore();

                b.Save();
                b.Translate(150, 150);
                float fh = MathF.Tan(0.25f), fw = fh, near = 0.05f, far = 1f;
                var perspective = new Matrix4x4(
                    1f / fw, 0, 0, 0,
                    0, 1f / fh, 0, 0,
                    0, 0, far / (far - near), 1,
                    0, 0, -(far * near) / (far - near), 0);
                using (var src = texture.ColorSourceCreateImageNew(repeat, repeat, nearest, perspective)!)
                using (var paint = ImpellerPaint.New()!)
                {
                    paint.SetColorSource(src);
                    b.DrawRect(Rect(0, 0, 200, 200), paint);
                }
                b.Restore();
                break;
            }
            case 4:
            {
                float size = SampleTexture.Size;
                var dst = Rect(100, 100, 600, 600);
                using (var grey = Fill(ImpellerColor.FromRgb(128, 128, 128))) b.DrawRect(dst, grey);
                using var plain = ImpellerPaint.New()!;
                b.DrawTextureRect(texture, Rect(size * 0.25f, size * 0.4f, size, size), dst,
                    ImpellerTextureSampling.kImpellerTextureSamplingNearestNeighbor, plain);
                break;
            }
            default:
            {
                using var paint = Stroke(new ImpellerColor { Blue = 1f, Alpha = 0.5f }, 30f);
                (ImpellerStrokeJoin Join, float X)[] joins =
                [
                    (ImpellerStrokeJoin.kImpellerStrokeJoinBevel, 100), (ImpellerStrokeJoin.kImpellerStrokeJoinRound, 350),
                    (ImpellerStrokeJoin.kImpellerStrokeJoinMiter, 600),
                ];
                foreach (var (join, x) in joins)
                {
                    paint.SetStrokeJoin(join);
                    paint.SetStrokeWidth(30f);
                    b.DrawRect(Rect(x, 100, 100, 100), paint);
                    b.DrawRect(Rect(x + 150, 100, 10, 100), paint);
                    b.DrawRect(Rect(x, 250, 100, 10), paint);
                    paint.SetStrokeWidth(110f);
                    b.DrawRect(Rect(x, 400, 100, 100), paint);
                }
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
