using System;
using NImpeller;
using Xunit;

namespace NImpeller.Tests.Unit;

public sealed class UnmanagedMemoryTests
{
    [Fact]
    public void Copies_bytes_and_reports_length()
    {
        byte[] bytes = { 1, 2, 3, 4, 5 };
        using var mem = new ImpellerUnmanagedMemory(bytes);

        Assert.Equal((ulong)bytes.Length, mem.Length);
        unsafe
        {
            Assert.True(mem.Data != null);
            for (int i = 0; i < bytes.Length; i++)
            {
                Assert.Equal(bytes[i], mem.Data[i]);
            }
        }
    }

    [Fact]
    public void Owns_an_independent_copy_of_the_source()
    {
        byte[] bytes = { 10, 20, 30 };
        using var mem = new ImpellerUnmanagedMemory(bytes);

        // Mutating the caller's array must not affect the native copy.
        bytes[0] = 99;
        unsafe
        {
            Assert.Equal((byte)10, mem.Data[0]);
        }
    }

    [Fact]
    public void Empty_input_yields_zero_length_but_non_null_pointer()
    {
        using var mem = new ImpellerUnmanagedMemory(ReadOnlySpan<byte>.Empty);

        Assert.Equal(0UL, mem.Length);
        unsafe
        {
            Assert.True(mem.Data != null);
        }
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var mem = new ImpellerUnmanagedMemory(new byte[] { 1 });
        mem.Dispose();
        mem.Dispose(); // must not double-free / throw
        unsafe
        {
            Assert.True(mem.Data == null);
        }
    }
}
