using System;

namespace NImpeller;

public unsafe partial class ImpellerPath
{
    /// <summary>
    /// Creates the dashed center line of this path. Stroke the result to get the dashes.
    /// </summary>
    /// <param name="intervals">Alternating "on" and "off" lengths. The count must be even and at least two.</param>
    /// <param name="phase">The offset into the pattern at the start of each contour.</param>
    /// <returns>The dashed path, or <c>null</c> if the pattern is invalid.</returns>
    public ImpellerPath? CreateDashedNew(ReadOnlySpan<float> intervals, float phase)
    {
        fixed (float* p = intervals)
        {
            var ret = UnsafeNativeMethods.ImpellerPathCreateDashedNew(Handle, p, (uint)intervals.Length, phase);
            if (ret.IsInvalid)
            {
                ret.Dispose();
                return null;
            }
            return new ImpellerPath(ret);
        }
    }
}
