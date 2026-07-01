using System;

namespace NImpeller;

public partial class ImpellerColorSource
{
    // The generated bindings model the native colors/stops parameters as single by-value scalars
    // (the header passes them as bare pointers with a separate stop_count), so a real multi-stop
    // gradient can only be built through these hand-written span overloads. colors[i] is placed at
    // the normalized position stops[i]; the two spans must be the same non-zero length.

    /// <summary>Creates a linear gradient color source running from <paramref name="startPoint"/> to <paramref name="endPoint"/>.</summary>
    public static unsafe ImpellerColorSource? CreateLinearGradientNew(
        ImpellerPoint startPoint,
        ImpellerPoint endPoint,
        ReadOnlySpan<ImpellerColor> colors,
        ReadOnlySpan<float> stops,
        ImpellerTileMode tileMode,
        ImpellerMatrix? transformation = null)
    {
        ValidateStops(colors, stops);
        fixed (ImpellerColor* colorsPtr = colors)
        fixed (float* stopsPtr = stops)
        {
            var matrix = transformation.GetValueOrDefault();
            var ret = UnsafeNativeMethods.ImpellerColorSourceCreateLinearGradientNew(
                &startPoint, &endPoint, (uint)colors.Length, colorsPtr, stopsPtr, tileMode,
                transformation.HasValue ? &matrix : null);
            return Wrap(ret);
        }
    }

    /// <summary>Creates a radial gradient color source centered at <paramref name="center"/> with the given <paramref name="radius"/>.</summary>
    public static unsafe ImpellerColorSource? CreateRadialGradientNew(
        ImpellerPoint center,
        float radius,
        ReadOnlySpan<ImpellerColor> colors,
        ReadOnlySpan<float> stops,
        ImpellerTileMode tileMode,
        ImpellerMatrix? transformation = null)
    {
        ValidateStops(colors, stops);
        fixed (ImpellerColor* colorsPtr = colors)
        fixed (float* stopsPtr = stops)
        {
            var matrix = transformation.GetValueOrDefault();
            var ret = UnsafeNativeMethods.ImpellerColorSourceCreateRadialGradientNew(
                &center, radius, (uint)colors.Length, colorsPtr, stopsPtr, tileMode,
                transformation.HasValue ? &matrix : null);
            return Wrap(ret);
        }
    }

    /// <summary>Creates a conical gradient color source between two circles.</summary>
    public static unsafe ImpellerColorSource? CreateConicalGradientNew(
        ImpellerPoint startCenter,
        float startRadius,
        ImpellerPoint endCenter,
        float endRadius,
        ReadOnlySpan<ImpellerColor> colors,
        ReadOnlySpan<float> stops,
        ImpellerTileMode tileMode,
        ImpellerMatrix? transformation = null)
    {
        ValidateStops(colors, stops);
        fixed (ImpellerColor* colorsPtr = colors)
        fixed (float* stopsPtr = stops)
        {
            var matrix = transformation.GetValueOrDefault();
            var ret = UnsafeNativeMethods.ImpellerColorSourceCreateConicalGradientNew(
                &startCenter, startRadius, &endCenter, endRadius, (uint)colors.Length, colorsPtr, stopsPtr, tileMode,
                transformation.HasValue ? &matrix : null);
            return Wrap(ret);
        }
    }

    /// <summary>Creates a sweep gradient color source centered at <paramref name="center"/>, sweeping from <paramref name="start"/> to <paramref name="end"/> degrees.</summary>
    public static unsafe ImpellerColorSource? CreateSweepGradientNew(
        ImpellerPoint center,
        float start,
        float end,
        ReadOnlySpan<ImpellerColor> colors,
        ReadOnlySpan<float> stops,
        ImpellerTileMode tileMode,
        ImpellerMatrix? transformation = null)
    {
        ValidateStops(colors, stops);
        fixed (ImpellerColor* colorsPtr = colors)
        fixed (float* stopsPtr = stops)
        {
            var matrix = transformation.GetValueOrDefault();
            var ret = UnsafeNativeMethods.ImpellerColorSourceCreateSweepGradientNew(
                &center, start, end, (uint)colors.Length, colorsPtr, stopsPtr, tileMode,
                transformation.HasValue ? &matrix : null);
            return Wrap(ret);
        }
    }

    private static void ValidateStops(ReadOnlySpan<ImpellerColor> colors, ReadOnlySpan<float> stops)
    {
        if (colors.Length == 0)
        {
            throw new ArgumentException("A gradient needs at least one color stop.", nameof(colors));
        }

        if (colors.Length != stops.Length)
        {
            throw new ArgumentException(
                $"colors ({colors.Length}) and stops ({stops.Length}) must have the same length.", nameof(stops));
        }
    }

    private static ImpellerColorSource? Wrap(ImpellerColorSourceHandle ret) =>
        ret == null ? null : new ImpellerColorSource(ret);
}
