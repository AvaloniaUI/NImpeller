using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace NImpeller;

public unsafe partial class ImpellerTypeface
{
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void FreeData(IntPtr data) => NativeMemory.Free((void*)data);

    /// <summary>Makes a native copy of <paramref name="data"/> that Impeller frees when done.</summary>
    internal static ImpellerMapping CopyToMapping(ReadOnlySpan<byte> data, out IntPtr userData)
    {
        var copy = (byte*)NativeMemory.Alloc((nuint)Math.Max(data.Length, 1));
        data.CopyTo(new Span<byte>(copy, data.Length));
        userData = (IntPtr)copy;
        return new ImpellerMapping
        {
            Data = copy,
            Length = (ulong)data.Length,
            On_release = (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, void>)&FreeData,
        };
    }

    /// <summary>
    /// Creates a typeface from font file data (TrueType, OpenType or a collection). The data is
    /// copied.
    /// </summary>
    /// <returns>The typeface, or <c>null</c> if the data isn't a supported font.</returns>
    public static ImpellerTypeface? CreateWithData(ReadOnlySpan<byte> data, uint faceIndex = 0)
    {
        var mapping = CopyToMapping(data, out var userData);
        var ret = UnsafeNativeMethods.ImpellerTypefaceCreateWithDataNew(&mapping, userData, faceIndex);
        if (ret.IsInvalid)
        {
            ret.Dispose();
            return null;
        }
        return new ImpellerTypeface(ret);
    }

    /// <summary>Creates a typeface with different values on the variation axes of a variable font.</summary>
    /// <returns>The new typeface, or <c>null</c> if the variations can't be applied.</returns>
    public ImpellerTypeface? CreateWithVariations(ReadOnlySpan<ImpellerFontVariation> variations)
    {
        fixed (ImpellerFontVariation* p = variations)
        {
            var ret = UnsafeNativeMethods.ImpellerTypefaceCreateWithVariationsNew(Handle, p, (uint)variations.Length);
            if (ret.IsInvalid)
            {
                ret.Dispose();
                return null;
            }
            return new ImpellerTypeface(ret);
        }
    }

    /// <summary>Copies a font table, or returns <c>null</c> if the typeface has no such table.</summary>
    /// <param name="tag">The four byte table tag, packed big-endian.</param>
    public byte[]? GetTableData(uint tag)
    {
        var size = UnsafeNativeMethods.ImpellerTypefaceCopyTableData(Handle, tag, IntPtr.Zero, 0);
        if (size == 0)
            return null;
        var data = new byte[checked((int)size)];
        fixed (byte* p = data)
        {
            var copied = UnsafeNativeMethods.ImpellerTypefaceCopyTableData(Handle, tag, (IntPtr)p, size);
            return copied == size ? data : data.AsSpan(0, (int)copied).ToArray();
        }
    }

    /// <summary>Copies the font file data, or returns <c>null</c> if it isn't available.</summary>
    /// <param name="faceIndex">The index of the face in the data, for font collections.</param>
    public byte[]? GetData(out uint faceIndex)
    {
        uint index = 0;
        var size = UnsafeNativeMethods.ImpellerTypefaceCopyData(Handle, IntPtr.Zero, 0, &index);
        faceIndex = index;
        if (size == 0)
            return null;
        var data = new byte[checked((int)size)];
        fixed (byte* p = data)
        {
            var copied = UnsafeNativeMethods.ImpellerTypefaceCopyData(Handle, (IntPtr)p, size, &index);
            return copied == size ? data : data.AsSpan(0, (int)copied).ToArray();
        }
    }

    /// <summary>The family name of the typeface.</summary>
    public string GetFamilyName() =>
        ReadString((dst, size) => UnsafeNativeMethods.ImpellerTypefaceCopyFamilyName(Handle, dst, size));

    internal delegate ulong CopyStringFunc(sbyte* dst, ulong size);

    // Two-call pattern: the first call returns the size including the terminator.
    internal static string ReadString(CopyStringFunc copy)
    {
        var size = copy(null, 0);
        if (size <= 1)
            return string.Empty;
        var buffer = new byte[checked((int)size)];
        fixed (byte* p = buffer)
            copy((sbyte*)p, size);
        return Encoding.UTF8.GetString(buffer, 0, buffer.Length - 1);
    }
}

public unsafe partial class ImpellerTypographyContext
{
    /// <summary>The names of the registered font families followed by the platform's.</summary>
    public string[] GetFamilyNames()
    {
        var count = UnsafeNativeMethods.ImpellerTypographyContextGetFamilyCount(Handle);
        var names = new string[count];
        for (uint i = 0; i < count; i++)
        {
            var index = i;
            names[i] = ImpellerTypeface.ReadString((dst, size) =>
                UnsafeNativeMethods.ImpellerTypographyContextCopyFamilyName(Handle, index, dst, size));
        }
        return names;
    }

    /// <summary>The styles of the typefaces in a family, or an empty array if there is no such family.</summary>
    public ImpellerTypefaceStyle[] GetFamilyStyles(string family)
    {
        ArgumentNullException.ThrowIfNull(family);
        var styles = new ImpellerTypefaceStyle[
            UnsafeNativeMethods.ImpellerTypographyContextCopyFamilyStyles(Handle, family, null, 0)];
        fixed (ImpellerTypefaceStyle* dst = styles)
            UnsafeNativeMethods.ImpellerTypographyContextCopyFamilyStyles(Handle, family, dst, (uint)styles.Length);
        return styles;
    }
}

public unsafe partial class ImpellerFont
{
    /// <summary>
    /// Gets the bounds of glyphs relative to their baseline origins, scaled to the font size.
    /// </summary>
    public void GetGlyphBounds(ReadOnlySpan<ushort> glyphs, Span<ImpellerRect> bounds)
    {
        if (bounds.Length < glyphs.Length)
            throw new ArgumentException("There must be a rectangle for each glyph.", nameof(bounds));
        fixed (ushort* g = glyphs)
        fixed (ImpellerRect* b = bounds)
            UnsafeNativeMethods.ImpellerFontGetGlyphBounds(Handle, g, (uint)glyphs.Length, b);
    }

    /// <summary>
    /// Prepares glyphs shaped by the caller for drawing many times with
    /// <see cref="ImpellerDisplayListBuilder.DrawGlyphRun"/>. <paramref name="positions"/> are relative to the
    /// origin given when drawing. Returns null for no glyphs.
    /// </summary>
    public ImpellerGlyphRun? GlyphRunNew(ReadOnlySpan<ushort> glyphs, ReadOnlySpan<ImpellerPoint> positions)
    {
        if (positions.Length < glyphs.Length)
            throw new ArgumentException("There must be a position for each glyph.", nameof(positions));
        if (glyphs.IsEmpty)
            return null;
        fixed (ushort* g = glyphs)
        fixed (ImpellerPoint* p = positions)
        {
            var handle = UnsafeNativeMethods.ImpellerGlyphRunNew(Handle, g, p, (uint)glyphs.Length);
            if (handle.IsInvalid)
            {
                handle.Dispose();
                return null;
            }
            return new ImpellerGlyphRun(handle);
        }
    }
}

public unsafe partial class ImpellerDisplayListBuilder
{
    /// <summary>
    /// Draws glyphs shaped by the caller. <paramref name="positions"/> are the baseline origins of
    /// the glyphs relative to <paramref name="origin"/>.
    /// </summary>
    public void DrawGlyphs(ImpellerFont font, ReadOnlySpan<ushort> glyphs, ReadOnlySpan<ImpellerPoint> positions,
        ImpellerPoint origin, ImpellerPaint paint)
    {
        if (positions.Length < glyphs.Length)
            throw new ArgumentException("There must be a position for each glyph.", nameof(positions));
        if (glyphs.IsEmpty)
            return;
        fixed (ushort* g = glyphs)
        fixed (ImpellerPoint* p = positions)
            UnsafeNativeMethods.ImpellerDisplayListBuilderDrawGlyphs(Handle, font.Handle, g, p, (uint)glyphs.Length,
                &origin, paint.Handle);
    }
}

public partial struct ImpellerTypefaceStyle
{
    public ImpellerTypefaceStyle(uint weight, uint width, ImpellerFontSlant slant)
    {
        Weight = weight;
        Width = width;
        Slant = slant;
    }

    /// <summary>Weight 400, normal width, upright.</summary>
    public static ImpellerTypefaceStyle Normal => new(400, 5, ImpellerFontSlant.kImpellerFontSlantUpright);
}
