using System;
using System.Diagnostics;
using System.IO;
using NImpeller;

namespace Sandbox.Scenes;

/// <summary>
/// Demonstrates custom fragment shaders compiled with <c>impellerc</c>:
///   • left  — <see cref="ImpellerColorSource.CreateFragmentProgramNew(ImpellerContext, ImpellerFragmentProgram, ReadOnlySpan{ImpellerTexture}, ReadOnlySpan{float})"/>
///             fills a rect with an animated plasma (Shaders/plasma.frag)
///   • right — <see cref="ImpellerImageFilter.CreateFragmentProgramNew(ImpellerContext, ImpellerFragmentProgram, ReadOnlySpan{ImpellerTexture}, ReadOnlySpan{float})"/>
///             ripples a layer of orbiting shapes (Shaders/ripple.frag)
/// Uniforms are passed as floats in declaration order and rebuilt every frame to animate.
/// </summary>
public sealed class FragmentProgramScene : IScene, IDisposable
{
    public string Name => "Fragment Programs";
    public string CommandLineName => "shaders";
    public string Description =>
        "Custom impellerc-compiled shaders used as an animated color source (plasma) and image filter (ripple)";

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Lazy<ImpellerFragmentProgram?> _plasma = new(() => LoadProgram("plasma"));
    private readonly Lazy<ImpellerFragmentProgram?> _ripple = new(() => LoadProgram("ripple"));

    // The image filter's sampler 0 is replaced by the layer contents at draw time, but the API still
    // needs a texture there. It belongs to the context it was created with.
    private ImpellerContext? _placeholderContext;
    private ImpellerTexture? _placeholder;

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder scene, SceneParameters p)
    {
        float t = (float)_clock.Elapsed.TotalSeconds;

        using (var bg = ImpellerPaint.New()!)
        {
            bg.SetColor(ImpellerColor.FromRgb(14, 16, 22));
            scene.DrawPaint(bg);
        }

        const float pad = 16f;
        float cellW = (p.Width - pad * 3f) / 2f;
        float cellH = p.Height - pad * 2f;
        if (cellW <= 1f || cellH <= 1f)
        {
            return; // Window too small to lay out the cells.
        }

        DrawPlasma(context, scene, pad, pad, cellW, cellH, t);
        DrawRipple(context, scene, pad * 2f + cellW, pad, cellW, cellH, t);
    }

    private void DrawPlasma(ImpellerContext context, ImpellerDisplayListBuilder b, float x, float y, float w, float h, float t)
    {
        if (_plasma.Value is not { } program)
        {
            return;
        }

        // uSize (vec2), uTime (float).
        ReadOnlySpan<float> uniforms = [w, h, t];
        using var source = ImpellerColorSource.CreateFragmentProgramNew(context, program, [], uniforms);
        if (source is null)
        {
            return; // The program has no shader for this backend.
        }

        using var paint = ImpellerPaint.New()!;
        paint.SetColorSource(source);

        // FlutterFragCoord() is in local coordinates, so draw at the origin of a translated canvas
        // to make the shader see 0..uSize across the rect.
        b.Save();
        b.Translate(x, y);
        b.DrawRoundedRect(Rect(0, 0, w, h), Radii(18f), paint);
        b.Restore();
    }

    private void DrawRipple(ImpellerContext context, ImpellerDisplayListBuilder b, float x, float y, float w, float h, float t)
    {
        if (_ripple.Value is not { } program)
        {
            return;
        }

        // uSize (vec2) is written by Impeller with the layer size; uTime (float) is ours.
        ReadOnlySpan<float> uniforms = [0f, 0f, t];
        using var filter = ImpellerImageFilter.CreateFragmentProgramNew(
            context, program, [GetPlaceholder(context)], uniforms);
        if (filter is null)
        {
            return; // The program has no shader for this backend.
        }

        using var layerPaint = ImpellerPaint.New()!;
        layerPaint.SetImageFilter(filter);

        b.SaveLayer(Rect(x, y, w, h), layerPaint, null);
        DrawOrbitingShapes(b, x, y, w, h, t);
        b.Restore();
    }

    // A panel of rings of colored dots orbiting the centre: plenty of edges for the ripple to bend.
    private static void DrawOrbitingShapes(ImpellerDisplayListBuilder b, float x, float y, float w, float h, float t)
    {
        using var paint = ImpellerPaint.New()!;
        paint.SetColor(ImpellerColor.FromRgb(30, 34, 46));
        b.DrawRoundedRect(Rect(x, y, w, h), Radii(18f), paint);

        float cx = x + w / 2f, cy = y + h / 2f;
        float maxR = MathF.Min(w, h) * 0.45f;
        const int rings = 5;
        for (int ring = 1; ring <= rings; ring++)
        {
            float r = maxR * ring / rings;
            int dots = 6 + ring * 4;
            float spin = t * (ring % 2 == 0 ? 0.4f : -0.3f);
            float dotR = maxR * 0.06f;
            for (int i = 0; i < dots; i++)
            {
                float a = spin + i * MathF.Tau / dots;
                float hue = (float)i / dots + ring * 0.13f;
                paint.SetColor(Hsv(hue, 0.65f, 0.95f));
                b.DrawOval(Rect(cx + MathF.Cos(a) * r - dotR, cy + MathF.Sin(a) * r - dotR, dotR * 2f, dotR * 2f), paint);
            }
        }
    }

    private ImpellerTexture GetPlaceholder(ImpellerContext context)
    {
        if (_placeholder is null || !ReferenceEquals(_placeholderContext, context))
        {
            _placeholder?.Dispose();
            using var mem = new ImpellerUnmanagedMemory([0, 0, 0, 0]);
            var descriptor = new ImpellerTextureDescriptor
            {
                Pixel_format = ImpellerPixelFormat.kImpellerPixelFormatRGBA8888,
                Size = new ImpellerISize(1, 1),
                Mip_count = 1,
            };
            _placeholder = context.TextureCreateWithContentsNew(descriptor, mem)
                ?? throw new InvalidOperationException("Could not create the placeholder texture.");
            _placeholderContext = context;
        }

        return _placeholder;
    }

    private static ImpellerFragmentProgram? LoadProgram(string name)
    {
        using var stream = typeof(FragmentProgramScene).Assembly.GetManifestResourceStream($"Shaders/{name}.frag.iplr")
            ?? throw new InvalidOperationException($"Missing embedded shader '{name}'.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        using var mem = new ImpellerUnmanagedMemory(copy.GetBuffer().AsSpan(0, (int)copy.Length));
        return ImpellerFragmentProgram.New(mem);
    }

    private static ImpellerRect Rect(float x, float y, float w, float h) =>
        new() { X = x, Y = y, Width = w, Height = h };

    private static ImpellerRoundingRadii Radii(float r)
    {
        var p = new ImpellerPoint { X = r, Y = r };
        return new ImpellerRoundingRadii { Top_left = p, Top_right = p, Bottom_left = p, Bottom_right = p };
    }

    private static ImpellerColor Hsv(float h, float s, float v)
    {
        h -= MathF.Floor(h);
        float c = v * s, hp = h * 6f, xc = c * (1f - MathF.Abs(hp % 2f - 1f)), m = v - c;
        (float r, float g, float b) = (int)hp switch
        {
            0 => (c, xc, 0f),
            1 => (xc, c, 0f),
            2 => (0f, c, xc),
            3 => (0f, xc, c),
            4 => (xc, 0f, c),
            _ => (c, 0f, xc),
        };
        return new ImpellerColor { Red = r + m, Green = g + m, Blue = b + m, Alpha = 1f };
    }

    public void Dispose()
    {
        _placeholder?.Dispose();
        if (_plasma.IsValueCreated) _plasma.Value?.Dispose();
        if (_ripple.IsValueCreated) _ripple.Value?.Dispose();
    }
}
