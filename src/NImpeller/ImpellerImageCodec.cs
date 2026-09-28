using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NImpeller;

public unsafe partial class ImpellerImageDecoder
{
    /// <summary>
    /// Creates a decoder for compressed image data (PNG, JPEG, WebP, GIF, BMP, WBMP or ICO). The
    /// data is copied.
    /// </summary>
    /// <returns>The decoder, or <c>null</c> if the data isn't a supported image.</returns>
    public static ImpellerImageDecoder? Create(ReadOnlySpan<byte> data)
    {
        var mapping = ImpellerTypeface.CopyToMapping(data, out var userData);
        var ret = UnsafeNativeMethods.ImpellerImageDecoderNew(&mapping, userData);
        if (ret.IsInvalid)
        {
            ret.Dispose();
            return null;
        }
        return new ImpellerImageDecoder(ret);
    }

    /// <summary>The size of the image in pixels.</summary>
    public ImpellerISize Size
    {
        get
        {
            GetSize(out var size);
            return size;
        }
    }

    /// <summary>
    /// Decodes the image to premultiplied RGBA8888 rows, top to bottom, scaled to
    /// <paramref name="size"/> (or the image size when <c>null</c>).
    /// </summary>
    /// <returns><c>true</c> if the image was decoded.</returns>
    public bool Decode(Span<byte> destination, ulong rowBytes, ImpellerISize? size = null)
    {
        var target = size ?? Size;
        if (target.Width <= 0 || target.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(size));
        if (rowBytes < (ulong)target.Width * 4)
            throw new ArgumentOutOfRangeException(nameof(rowBytes));
        if ((ulong)destination.Length < rowBytes * (ulong)(target.Height - 1) + (ulong)target.Width * 4)
            throw new ArgumentException("The destination is too small.", nameof(destination));
        fixed (byte* dst = destination)
            return UnsafeNativeMethods.ImpellerImageDecoderDecode(Handle, &target, (IntPtr)dst, rowBytes) != 0;
    }
}

/// <summary>Encodes pixels to compressed image formats.</summary>
public static unsafe class ImpellerImageEncoder
{
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Write(IntPtr data, ulong size, IntPtr userData)
    {
        var state = (EncodeState)GCHandle.FromIntPtr(userData).Target!;
        if (state.Error != null)
            return;
        try
        {
            state.Stream.Write(new ReadOnlySpan<byte>((void*)data, checked((int)size)));
        }
        catch (Exception e)
        {
            state.Error = e;
        }
    }

    private sealed class EncodeState(Stream stream)
    {
        public Stream Stream { get; } = stream;
        public Exception? Error { get; set; }
    }

    /// <summary>
    /// Encodes premultiplied RGBA8888 pixels (rows top to bottom) into <paramref name="stream"/>.
    /// </summary>
    /// <param name="quality">
    /// For PNG, the zlib level from 0 (none) to 9 (smallest). For JPEG and WebP, 0 to 100 (WebP at 100 is lossless).
    /// </param>
    /// <returns><c>false</c> if the format isn't available in this build or encoding failed.</returns>
    public static bool Encode(ReadOnlySpan<byte> pixels, ImpellerISize size, ulong rowBytes,
        ImpellerImageFormat format, uint quality, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (size.Width <= 0 || size.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(size));
        if (rowBytes < (ulong)size.Width * 4 ||
            (ulong)pixels.Length < rowBytes * (ulong)(size.Height - 1) + (ulong)size.Width * 4)
            throw new ArgumentException("The pixels don't cover the image.", nameof(pixels));

        var state = new EncodeState(stream);
        var handle = GCHandle.Alloc(state);
        try
        {
            bool ok;
            fixed (byte* p = pixels)
            {
                ok = UnsafeNativeMethods.ImpellerImageEncode((IntPtr)p, &size, rowBytes, format, quality,
                    (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, ulong, IntPtr, void>)&Write,
                    GCHandle.ToIntPtr(handle)) != 0;
            }
            if (state.Error != null)
                throw new IOException("Writing the encoded image failed.", state.Error);
            return ok;
        }
        finally
        {
            handle.Free();
        }
    }
}
