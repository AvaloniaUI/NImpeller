using System;

namespace NImpeller;

public unsafe partial class ImpellerContext
{
    /// <summary>
    /// Copies pixels of a texture created with <see cref="TextureCreateRenderTargetNew"/> into
    /// <paramref name="destination"/> as top-down, premultiplied RGBA8888 rows. Blocks until the
    /// rendering into the texture has finished.
    /// </summary>
    /// <param name="texture">The texture.</param>
    /// <param name="region">The region to read. It must be inside the texture.</param>
    /// <param name="destination">Receives <paramref name="rowBytes"/> times the region height bytes.</param>
    /// <param name="rowBytes">The distance between row starts, at least four times the region width.</param>
    /// <returns><c>true</c> if the pixels were copied.</returns>
    public bool TextureReadPixels(ImpellerTexture texture, ImpellerIRect region, Span<byte> destination,
        ulong rowBytes)
    {
        ArgumentNullException.ThrowIfNull(texture);
        if (region.Width <= 0 || region.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(region));
        if (rowBytes < (ulong)region.Width * 4)
            throw new ArgumentOutOfRangeException(nameof(rowBytes));
        if ((ulong)destination.Length < rowBytes * (ulong)(region.Height - 1) + (ulong)region.Width * 4)
            throw new ArgumentException("The destination is too small for the region.", nameof(destination));
        fixed (byte* dst = destination)
        {
            return (UnsafeNativeMethods.ImpellerTextureReadPixels(Handle, texture.Handle, &region, (IntPtr)dst,
                rowBytes) & 0xFF) != 0;
        }
    }
}
