using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NImpeller;

public unsafe partial class ImpellerTypographyContext
{
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static void FreeFontData(IntPtr data) => NativeMemory.Free((void*)data);

    /// <summary>
    /// Registers a font from in-memory font data.
    /// </summary>
    /// <param name="contents">
    /// The font file bytes. Copied internally, so <paramref name="contents"/> may be disposed as
    /// soon as this call returns.
    /// </param>
    /// <param name="familyNameAlias">
    /// An optional family name to register the font under. Pass <c>null</c> to use the font's own
    /// family name.
    /// </param>
    /// <returns><c>true</c> if the font was registered successfully.</returns>
    public bool RegisterFont(IImpellerUnmanagedMemory contents, string? familyNameAlias = null)
    {
        ArgumentNullException.ThrowIfNull(contents);

        var length = contents.Length;
        int intLength = checked((int)length);

        if (length != 0 && contents.Data == null)
        {
            throw new ArgumentException(
                "Memory reports a non-zero Length but a null Data pointer (is it disposed?).", nameof(contents));
        }

        // Alloc at least one byte so the copy is a valid, freeable pointer even for empty input.
        var copy = (byte*)NativeMemory.Alloc((nuint)(length == 0 ? 1 : length));
        if (length != 0)
        {
            new ReadOnlySpan<byte>(contents.Data, intLength).CopyTo(new Span<byte>(copy, intLength));
        }

        var mapping = new ImpellerMapping
        {
            Data = copy,
            Length = length,
            On_release = (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, void>)&FreeFontData,
        };

        // The release callback's user_data baton is the copy pointer it must free.
        return UnsafeNativeMethods.ImpellerTypographyContextRegisterFont(
            Handle, &mapping, (IntPtr)copy, familyNameAlias!) != 0;
    }
}
