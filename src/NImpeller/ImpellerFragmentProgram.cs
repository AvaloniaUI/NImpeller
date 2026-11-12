using System;
using System.Runtime.InteropServices;

namespace NImpeller;

public partial class ImpellerFragmentProgram
{
    internal unsafe delegate T SamplersAndUniformsCall<T>(IntPtr* samplers, ulong samplersCount, byte* data, ulong dataLength);

    /// <summary>
    /// Pins the sampler handles and uniform bytes for the duration of one native call. Impeller
    /// copies the uniforms and takes its own reference on each sampler texture before returning, so
    /// nothing has to stay alive after the call.
    /// </summary>
    internal static unsafe T WithSamplersAndUniforms<T>(
        ReadOnlySpan<ImpellerTexture> samplers,
        ReadOnlySpan<byte> uniforms,
        SamplersAndUniformsCall<T> call)
    {
        // The native side dereferences every sampler without a null check.
        for (var i = 0; i < samplers.Length; i++)
        {
            if (samplers[i] is null)
                throw new ArgumentNullException(nameof(samplers), $"Sampler at index {i} is null.");
        }

        Span<IntPtr> handles = samplers.Length <= 16 ? stackalloc IntPtr[samplers.Length] : new IntPtr[samplers.Length];
        var added = 0;
        try
        {
            for (; added < samplers.Length; added++)
            {
                var success = false;
                samplers[added].Handle.DangerousAddRef(ref success);
                handles[added] = samplers[added].Handle.DangerousGetHandle();
            }

            fixed (IntPtr* samplersPtr = handles)
            fixed (byte* dataPtr = uniforms)
            {
                return call(samplersPtr, (ulong)samplers.Length, dataPtr, (ulong)uniforms.Length);
            }
        }
        finally
        {
            for (var i = 0; i < added; i++)
                samplers[i].Handle.DangerousRelease();
        }
    }

    internal static ReadOnlySpan<byte> AsUniformBytes(ReadOnlySpan<float> uniforms) =>
        MemoryMarshal.AsBytes(uniforms);
}

public partial class ImpellerColorSource
{
    /// <summary>
    /// Creates a color source whose pixels are shaded by <paramref name="program"/>.
    /// </summary>
    /// <param name="context">Selects which backend's shader in the program is used.</param>
    /// <param name="program">A program created from <c>impellerc</c> output.</param>
    /// <param name="samplers">Textures bound to the shader's samplers, in declaration order. Always sampled with clamp tiling.</param>
    /// <param name="uniforms">Uniform data laid out as the shader declares it. Copied during the call.</param>
    /// <returns>The color source, or <c>null</c> if the program has no shader for the context's backend.</returns>
    public static unsafe ImpellerColorSource? CreateFragmentProgramNew(
        ImpellerContext context,
        ImpellerFragmentProgram program,
        ReadOnlySpan<ImpellerTexture> samplers,
        ReadOnlySpan<byte> uniforms)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(program);
        var ret = ImpellerFragmentProgram.WithSamplersAndUniforms(samplers, uniforms,
            (s, sc, d, dl) => UnsafeNativeMethods.ImpellerColorSourceCreateFragmentProgramNew(
                context.Handle, program.Handle, s, sc, d, dl));
        return Wrap(ret);
    }

    /// <inheritdoc cref="CreateFragmentProgramNew(ImpellerContext, ImpellerFragmentProgram, ReadOnlySpan{ImpellerTexture}, ReadOnlySpan{byte})"/>
    public static ImpellerColorSource? CreateFragmentProgramNew(
        ImpellerContext context,
        ImpellerFragmentProgram program,
        ReadOnlySpan<ImpellerTexture> samplers,
        ReadOnlySpan<float> uniforms) =>
        CreateFragmentProgramNew(context, program, samplers, ImpellerFragmentProgram.AsUniformBytes(uniforms));
}

public partial class ImpellerImageFilter
{
    /// <summary>
    /// Creates an image filter that runs <paramref name="program"/> over its input.
    /// </summary>
    /// <remarks>
    /// The shader must declare a <c>vec2</c> size uniform first and a <c>sampler2D</c> for the
    /// filter input first. At draw time Impeller binds the input to sampler 0 and writes the input
    /// size into the first 8 bytes of the uniforms, so <paramref name="samplers"/>[0] is only a
    /// placeholder (any texture) and the leading size bytes can be anything.
    /// </remarks>
    /// <param name="context">Selects which backend's shader in the program is used.</param>
    /// <param name="program">A program created from <c>impellerc</c> output.</param>
    /// <param name="samplers">A placeholder for the filter input, then the shader's other samplers in declaration order. Always sampled with clamp tiling.</param>
    /// <param name="uniforms">Uniform data laid out as the shader declares it, starting with the <c>vec2</c> size. Copied during the call.</param>
    /// <returns>The image filter, or <c>null</c> if the program has no shader for the context's backend.</returns>
    /// <exception cref="ArgumentException">There is no sampler, or fewer than 8 bytes of uniforms.</exception>
    public static unsafe ImpellerImageFilter? CreateFragmentProgramNew(
        ImpellerContext context,
        ImpellerFragmentProgram program,
        ReadOnlySpan<ImpellerTexture> samplers,
        ReadOnlySpan<byte> uniforms)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(program);
        // Impeller accepts these at creation time but the filter then renders nothing.
        if (samplers.Length < 1)
            throw new ArgumentException("An image filter shader needs a sampler for its input; pass a placeholder texture at index 0.", nameof(samplers));
        if (uniforms.Length < 2 * sizeof(float))
            throw new ArgumentException("An image filter shader needs a leading vec2 size uniform (8 bytes).", nameof(uniforms));
        var ret = ImpellerFragmentProgram.WithSamplersAndUniforms(samplers, uniforms,
            (s, sc, d, dl) => UnsafeNativeMethods.ImpellerImageFilterCreateFragmentProgramNew(
                context.Handle, program.Handle, s, sc, d, dl));
        return ImpellerHandle.WrapOrNull(ret, static h => new ImpellerImageFilter(h));
    }

    /// <inheritdoc cref="CreateFragmentProgramNew(ImpellerContext, ImpellerFragmentProgram, ReadOnlySpan{ImpellerTexture}, ReadOnlySpan{byte})"/>
    public static ImpellerImageFilter? CreateFragmentProgramNew(
        ImpellerContext context,
        ImpellerFragmentProgram program,
        ReadOnlySpan<ImpellerTexture> samplers,
        ReadOnlySpan<float> uniforms) =>
        CreateFragmentProgramNew(context, program, samplers, ImpellerFragmentProgram.AsUniformBytes(uniforms));
}
