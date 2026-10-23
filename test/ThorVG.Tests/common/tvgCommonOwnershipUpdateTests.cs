using System;
using Xunit;

namespace ThorVG.Tests;

public class tvgCommonOwnershipUpdateTests
{
    private sealed class OwnedNode : IInlistNode<OwnedNode>, IDisposable
    {
        public OwnedNode? Prev { get; set; }
        public OwnedNode? Next { get; set; }
        public int Releases;
        public void Dispose() => Releases++;
    }

    [Fact]
    public void IntrusiveListFreeReleasesOwnedNodesAndNotRemovedNodes()
    {
        var first = new OwnedNode();
        var removed = new OwnedNode();
        var last = new OwnedNode();
        using var list = new Inlist<OwnedNode>();
        list.Back(first); list.Back(removed); list.Back(last);
        list.Remove(removed);
        list.Free();
        Assert.Equal(1, first.Releases);
        Assert.Equal(0, removed.Releases);
        Assert.Equal(1, last.Releases);
        Assert.Null(first.Prev); Assert.Null(first.Next);
        Assert.Null(last.Prev); Assert.Null(last.Next);
        Assert.True(list.Empty()); Assert.Equal(0u, list.Count);
        list.Free();
        Assert.Equal(1, first.Releases); Assert.Equal(1, last.Releases);
    }

    [Fact]
    public unsafe void NativeWordAllocationsZeroAndPreserveReallocatedBytes()
    {
        IntPtr ptr = TvgAllocator.Calloc((nuint)37, (nuint)3);
        try
        {
            var bytes = new Span<byte>((void*)ptr, 111);
            foreach (byte b in bytes) Assert.Equal(0, b);
            for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)i;
            ptr = TvgAllocator.Realloc(ptr, (nuint)223);
            for (int i = 0; i < 111; i++) Assert.Equal((byte)i, ((byte*)ptr)[i]);
        }
        finally { TvgAllocator.Free(ptr); }
        ptr = TvgAllocator.Malloc((nuint)19);
        Assert.NotEqual(IntPtr.Zero, ptr);
        TvgAllocator.Free(ptr);
        TvgAllocator.Free(IntPtr.Zero);
    }

    [Fact]
    public void NativeCallocRejectsMultiplicationOverflowWithoutWrapping()
    {
        Assert.Throws<OutOfMemoryException>(() => TvgAllocator.Calloc(nuint.MaxValue, (nuint)2));
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("a much longer native identifier")]
    [InlineData("žvaigždė")]
    [InlineData("🙂")]
    public void NativeIdentifiersUseThePlatformUnsignedLongWidth(string value)
    {
        ulong legacy = TvgCompressor.Djb2Encode(value);
        ulong expected = OperatingSystem.IsWindows() || IntPtr.Size == 4 ? (uint)legacy : legacy;
        Assert.Equal(expected, TvgCompressor.Djb2EncodeNative(value));
        Assert.Equal((uint)expected, Accessor.Id(value));
    }
}
