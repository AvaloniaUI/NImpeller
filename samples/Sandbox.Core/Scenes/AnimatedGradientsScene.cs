using System;
using System.Diagnostics;
using NImpeller;

namespace Sandbox.Scenes;

/// <summary>
/// Demonstrates the span-based multi-stop gradient factories on <see cref="ImpellerColorSource"/>
/// by animating both their geometry and their color stops every frame. 
/// A 2x2 grid shows one gradient kind per cell:
///   • linear  — the gradient axis spins around the cell centre
///   • radial  — the centre orbits and the radius pulses
///   • sweep   — the wheel rotates (animated start angle)
///   • conical — the focal circle drifts and the outer radius pulses
/// The colors are a rainbow whose hue scrolls over time, so every frame rebuilds real multi-stop gradients.
/// </summary>
public class AnimatedGradientsScene : IScene
{
    public string Name => "Animated Gradients";
    public string CommandLineName => "gradients";
    public string Description =>
        "Animated multi-stop linear, radial, sweep, and conical gradients built with the span-based ImpellerColorSource factories";

    private readonly Stopwatch _clock = Stopwatch.StartNew();

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder scene, SceneParameters p)
    {
        float t = (float)_clock.Elapsed.TotalSeconds;

        using (var bg = ImpellerPaint.New()!)
        {
            bg.SetColor(ImpellerColor.FromRgb(14, 16, 22));
            scene.DrawPaint(bg);
        }

        // 2x2 grid of cells with uniform padding.
        const float pad = 16f;
        float cellW = (p.Width - pad * 3f) / 2f;
        float cellH = (p.Height - pad * 3f) / 2f;
        if (cellW <= 1f || cellH <= 1f)
        {
            return; // Window too small to lay out the grid.
        }

        DrawLinear(scene, Rect(pad, pad, cellW, cellH), t);
        DrawRadial(scene, Rect(pad * 2f + cellW, pad, cellW, cellH), t);
        DrawSweep(scene, Rect(pad, pad * 2f + cellH, cellW, cellH), t);
        DrawConical(scene, Rect(pad * 2f + cellW, pad * 2f + cellH, cellW, cellH), t);
    }

    // A linear gradient whose axis rotates around the cell centre.
    private static void DrawLinear(ImpellerDisplayListBuilder b, ImpellerRect cell, float t)
    {
        float cx = cell.X + cell.Width / 2f, cy = cell.Y + cell.Height / 2f;
        float reach = MathF.Min(cell.Width, cell.Height) * 0.5f;
        float a = t * 0.6f;
        var start = new ImpellerPoint { X = cx - MathF.Cos(a) * reach, Y = cy - MathF.Sin(a) * reach };
        var end = new ImpellerPoint { X = cx + MathF.Cos(a) * reach, Y = cy + MathF.Sin(a) * reach };

        var colors = Rainbow(5, hueScroll: t * 0.12f);
        var stops = EvenStops(colors.Length);

        using var src = ImpellerColorSource.CreateLinearGradientNew(
            start, end, colors, stops, ImpellerTileMode.kImpellerTileModeClamp)!;
        FillCell(b, cell, src);
    }

    // A radial gradient whose centre orbits and whose radius pulses.
    private static void DrawRadial(ImpellerDisplayListBuilder b, ImpellerRect cell, float t)
    {
        float cx = cell.X + cell.Width / 2f, cy = cell.Y + cell.Height / 2f;
        float m = MathF.Min(cell.Width, cell.Height);
        float orbit = m * 0.16f;
        var center = new ImpellerPoint { X = cx + MathF.Cos(t * 1.3f) * orbit, Y = cy + MathF.Sin(t * 1.3f) * orbit };
        float radius = m * 0.5f * (0.75f + 0.2f * MathF.Sin(t * 2f));

        var colors = Rainbow(4, hueScroll: t * 0.2f);
        var stops = EvenStops(colors.Length);

        using var src = ImpellerColorSource.CreateRadialGradientNew(
            center, radius, colors, stops, ImpellerTileMode.kImpellerTileModeClamp)!;
        FillCell(b, cell, src);
    }

    // A sweep gradient that spins via an animated start angle. Because the rainbow's first and last
    // stop share a hue (0 and 1 wrap in HSV), the full 360-degree wheel is seamless as it rotates.
    private static void DrawSweep(ImpellerDisplayListBuilder b, ImpellerRect cell, float t)
    {
        float cx = cell.X + cell.Width / 2f, cy = cell.Y + cell.Height / 2f;
        var center = new ImpellerPoint { X = cx, Y = cy };
        float startDegrees = (t * 40f) % 360f;

        var colors = Rainbow(7, hueScroll: 0f);
        var stops = EvenStops(colors.Length);

        using var src = ImpellerColorSource.CreateSweepGradientNew(
            center, startDegrees, startDegrees + 360f, colors, stops,
            ImpellerTileMode.kImpellerTileModeClamp)!;
        FillCell(b, cell, src);
    }

    // A conical gradient whose focal (start) circle drifts while the outer radius pulses.
    private static void DrawConical(ImpellerDisplayListBuilder b, ImpellerRect cell, float t)
    {
        float cx = cell.X + cell.Width / 2f, cy = cell.Y + cell.Height / 2f;
        float m = MathF.Min(cell.Width, cell.Height);
        var startCenter = new ImpellerPoint { X = cx + MathF.Cos(t) * m * 0.15f, Y = cy + MathF.Sin(t) * m * 0.15f };
        float startRadius = m * 0.05f;
        var endCenter = new ImpellerPoint { X = cx, Y = cy };
        float endRadius = m * 0.5f * (0.7f + 0.25f * MathF.Sin(t * 1.5f));

        var colors = Rainbow(5, hueScroll: t * 0.15f);
        var stops = EvenStops(colors.Length);

        using var src = ImpellerColorSource.CreateConicalGradientNew(
            startCenter, startRadius, endCenter, endRadius, colors, stops,
            ImpellerTileMode.kImpellerTileModeClamp)!;
        FillCell(b, cell, src);
    }

    private static void FillCell(ImpellerDisplayListBuilder b, ImpellerRect cell, ImpellerColorSource src)
    {
        using var paint = ImpellerPaint.New()!;
        paint.SetColorSource(src);
        b.DrawRect(cell, paint);
    }

    // n evenly spaced stops in [0, 1].
    private static float[] EvenStops(int n)
    {
        var stops = new float[n];
        for (int i = 0; i < n; i++)
        {
            stops[i] = n == 1 ? 0f : (float)i / (n - 1);
        }
        return stops;
    }

    // A rainbow of n colors; hue runs 0..1 across the ramp (so ends match) plus a scrolling offset.
    private static ImpellerColor[] Rainbow(int n, float hueScroll)
    {
        var colors = new ImpellerColor[n];
        for (int i = 0; i < n; i++)
        {
            float hue = (n == 1 ? 0f : (float)i / (n - 1)) + hueScroll;
            colors[i] = Hsv(hue, 0.85f, 1f);
        }
        return colors;
    }

    // HSV → ImpellerColor. Hue wraps modulo 1.
    private static ImpellerColor Hsv(float h, float s, float v)
    {
        h = h - MathF.Floor(h); // wrap into [0, 1)
        float sector = h * 6f;
        int i = (int)MathF.Floor(sector) % 6;
        float f = sector - MathF.Floor(sector);
        float p = v * (1f - s);
        float q = v * (1f - f * s);
        float u = v * (1f - (1f - f) * s);

        (float r, float g, float b) = i switch
        {
            0 => (v, u, p),
            1 => (q, v, p),
            2 => (p, v, u),
            3 => (p, q, v),
            4 => (u, p, v),
            _ => (v, p, q),
        };

        return new ImpellerColor { Red = r, Green = g, Blue = b, Alpha = 1f };
    }

    private static ImpellerRect Rect(float x, float y, float w, float h) =>
        new() { X = x, Y = y, Width = w, Height = h };
}
