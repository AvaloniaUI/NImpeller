using System;
using System.Runtime.InteropServices;

namespace NImpeller;

/// <summary>
/// A block of unmanaged, natively-addressable, memory handed to Impeller.
/// </summary>
/// <remarks>
/// The pointer returned by <see cref="Data"/> must stay valid and fixed for the duration of the
/// Impeller call that consumes it. The simplest way to satisfy that is
/// <see cref="ImpellerUnmanagedMemory"/>, which copies a span into native memory it owns.
/// </remarks>
public interface IImpellerUnmanagedMemory
{
    /// <summary>Pointer to the first byte of the memory.</summary>
    unsafe byte* Data { get; }

    /// <summary>The number of bytes available at <see cref="Data"/>.</summary>
    ulong Length { get; }
}

/// <summary>
/// An <see cref="IImpellerUnmanagedMemory"/> backed by a native buffer that this object owns.
/// The buffer is a private copy of the bytes supplied at construction, so the caller's source
/// span/array can be freely reused or collected. Free it with <see cref="Dispose"/> once Impeller
/// no longer needs the data.
/// </summary>
public sealed unsafe class ImpellerUnmanagedMemory : IImpellerUnmanagedMemory, IDisposable
{
    private byte* _data;
    private readonly ulong _length;

    /// <summary>Allocates native memory and copies <paramref name="data"/> into it.</summary>
    public ImpellerUnmanagedMemory(ReadOnlySpan<byte> data)
    {
        _length = (ulong)data.Length;
        if (data.Length == 0)
        {
            // NativeMemory.Alloc(0) is implementation-defined; keep Data non-null and stable.
            _data = (byte*)NativeMemory.Alloc(1);
            return;
        }

        _data = (byte*)NativeMemory.Alloc((nuint)data.Length);
        data.CopyTo(new Span<byte>(_data, data.Length));
    }

    /// <inheritdoc />
    public byte* Data => _data;

    /// <inheritdoc />
    public ulong Length => _length;

    /// <summary>Frees the native buffer. Do not call while Impeller may still read the data.</summary>
    public void Dispose()
    {
        if (_data != null)
        {
            NativeMemory.Free(_data);
            _data = null;
        }
    }
}

internal partial struct ImpellerMapping
{
    /// <summary>
    /// Holds a natively-allocated <see cref="ImpellerMapping"/> for the duration of a single interop
    /// call. Only the small mapping header is owned here; the pixel/font/shader bytes it points at
    /// belong to the source <see cref="IImpellerUnmanagedMemory"/> and outlive this scope.
    /// </summary>
    public unsafe struct Marshalled : IDisposable
    {
        private ImpellerMapping* _value;

        internal Marshalled(ImpellerMapping* value) => _value = value;

        public ImpellerMapping* Value => _value;

        public void Dispose()
        {
            if (_value != null)
            {
                NativeMemory.Free(_value);
                _value = null;
            }
        }
    }

    public static unsafe Marshalled Marshal(IImpellerUnmanagedMemory contents)
    {
        ArgumentNullException.ThrowIfNull(contents);

        var mapping = (ImpellerMapping*)NativeMemory.Alloc((nuint)sizeof(ImpellerMapping));
        mapping->Data = contents.Data;
        mapping->Length = contents.Length;
        mapping->On_release = IntPtr.Zero;
        return new Marshalled(mapping);
    }
}
