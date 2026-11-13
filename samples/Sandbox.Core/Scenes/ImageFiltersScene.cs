using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NImpeller;
using static Sandbox.Scenes.Draw;

namespace Sandbox.Scenes;

/// <summary>Port of the interop <c>CanDrawImage</c> playground plus the image, color and mask filters the C API exposes.</summary>
public sealed class ImageFiltersScene : IScene, IDisposable
{
    public string Name => "Image Filters";
    public string CommandLineName => "imagefilters";
    public string Description => "A CPU-generated texture drawn plain and through blur, dilate, erode, matrix, composed, color-matrix and mask-blur filters";

    private const int TextureSize = SampleTexture.Size;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ImpellerTypographyContext _tc = ImpellerTypographyContext.New()!;
    private readonly SampleTexture _texture = new();

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        float t = (float)_clock.Elapsed.TotalSeconds;
        float pulse = 0.5f + 0.5f * MathF.Sin(t * 1.5f);
        Background(b, ImpellerColor.FromRgb(24, 26, 32));

        float k = MathF.Max(1f, p.Height / 640f);
        float pad = 16f * k, header = 22f * k;
        const int cols = 4, rows = 2;
        float cw = (p.Width - pad * (cols + 1)) / cols;
        float ch = (p.Height - pad * (rows + 1)) / rows - header;
        if (cw <= 1f || ch <= 1f) return;

        var texture = _texture.Get(context);
        string[] labels = ["Original", "Blur", "Dilate", "Erode", "Matrix", "Erode + blur", "Sepia color matrix", "Mask blur"];

        for (int i = 0; i < labels.Length; i++)
        {
            float x = pad + (i % cols) * (cw + pad);
            float y = pad + (i / cols) * (ch + header + pad);
            Label(b, _tc, labels[i], x, y, 13f * k, ImpellerColor.FromRgb(170, 176, 190));
            y += header;

            float size = MathF.Min(cw, ch);
            var dst = Rect(x + (cw - size) / 2f, y + (ch - size) / 2f, size, size);

            b.Save();
            b.ClipRect(Rect(x, y, cw, ch), ImpellerClipOperation.kImpellerClipOperationIntersect);
            if (i == 4)
            {
                b.Translate(dst.X + size / 2f, dst.Y + size / 2f);
                dst = Rect(-size / 2f, -size / 2f, size, size);
            }
            using var paint = ImpellerPaint.New()!;
            using var filter = CreateFilter(i, t, pulse, k);
            if (filter is not null) paint.SetImageFilter(filter);
            using var sepia = i == 6 ? Sepia() : null;
            if (sepia is not null) paint.SetColorFilter(sepia);

            if (i == 7)
            {
                DrawMaskBlur(b, texture, paint, dst, 2f + 10f * pulse * k);
            }
            else
            {
                b.DrawTextureRect(texture, Rect(0, 0, TextureSize, TextureSize), dst,
                    ImpellerTextureSampling.kImpellerTextureSamplingLinear, paint);
            }
            b.Restore();
        }
    }

    private static ImpellerImageFilter? CreateFilter(int index, float t, float pulse, float k)
    {
        switch (index)
        {
            case 1:
                return ImpellerImageFilter.CreateBlurNew(1f + 9f * pulse * k, 1f + 9f * pulse * k, ImpellerTileMode.kImpellerTileModeDecal);
            case 2:
                return ImpellerImageFilter.CreateDilateNew(k * (0.5f + 3f * pulse), k * (0.5f + 3f * pulse));
            case 3:
                return ImpellerImageFilter.CreateErodeNew(k * (0.5f + 3f * pulse), k * (0.5f + 3f * pulse));
            case 4:
                var m = Matrix4x4.CreateRotationZ(MathF.Sin(t) * 0.5f) * Matrix4x4.CreateScale(0.8f, 0.8f, 1f);
                return ImpellerImageFilter.CreateMatrixNew(m, ImpellerTextureSampling.kImpellerTextureSamplingLinear);
            case 5:
                using (var erode = ImpellerImageFilter.CreateErodeNew(2f * k, 2f * k)!)
                using (var blur = ImpellerImageFilter.CreateBlurNew(k * (0.5f + 4f * pulse), k * (0.5f + 4f * pulse), ImpellerTileMode.kImpellerTileModeDecal)!)
                {
                    return blur.CreateComposeNew(erode);
                }
            default:
                return null;
        }
    }

    private static void DrawMaskBlur(ImpellerDisplayListBuilder b, ImpellerTexture texture, ImpellerPaint paint, ImpellerRect dst, float sigma)
    {
        float s = dst.Width / TextureSize;
        var m = Matrix4x4.CreateScale(s, s, 1f) * Matrix4x4.CreateTranslation(dst.X, dst.Y, 0f);
        using var source = texture.ColorSourceCreateImageNew(ImpellerTileMode.kImpellerTileModeClamp,
            ImpellerTileMode.kImpellerTileModeClamp, ImpellerTextureSampling.kImpellerTextureSamplingLinear, m)!;
        using var mask = ImpellerMaskFilter.CreateBlurNew(ImpellerBlurStyle.kImpellerBlurStyleNormal, sigma)!;
        paint.SetColorSource(source);
        paint.SetMaskFilter(mask);
        float inset = dst.Width * 0.12f;
        b.DrawRoundedRect(Rect(dst.X + inset, dst.Y + inset, dst.Width - inset * 2f, dst.Height - inset * 2f),
            Radii(dst.Width * 0.15f), paint);
    }

    private static ImpellerColorFilter Sepia()
    {
        var matrix = new ImpellerColorMatrix();
        ReadOnlySpan<float> v =
        [
            0.393f, 0.769f, 0.189f, 0, 0,
            0.349f, 0.686f, 0.168f, 0, 0,
            0.272f, 0.534f, 0.131f, 0, 0,
            0, 0, 0, 1, 0,
        ];
        v.CopyTo(MemoryMarshal.CreateSpan(ref Unsafe.As<ImpellerColorMatrix, float>(ref matrix), 20));
        return ImpellerColorFilter.CreateColorMatrixNew(matrix)!;
    }

    public void Dispose()
    {
        _texture.Dispose();
        _tc.Dispose();
    }
}
