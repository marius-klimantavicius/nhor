using System;
using System.IO;
using Xunit;

namespace ThorVG.Tests;

public class tvgLoaderStateUpdateTests
{
    private sealed class UnsupportedLoader : Loader
    {
        public UnsupportedLoader() : base(FileType.Raw) { }
    }

    private sealed class CacheWorkerLoader : Loader
    {
        public System.Threading.Tasks.Task? Worker;
        public bool CompletedDuringClose;

        public CacheWorkerLoader() : base(FileType.Raw) { }

        public override bool Close()
        {
            Worker = System.Threading.Tasks.Task.Run(() =>
            {
                var font = LoaderMgr.AnyFont();
                if (font != null) LoaderMgr.Retrieve(font);
            });
            CompletedDuringClose = Worker.Wait(TimeSpan.FromSeconds(5));
            return true;
        }
    }

    [Fact]
    public void CachedFilenameRetrievalUsesTheSameCStringAsLoading()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "legacy-upstream", "resources", "test.png");
        var ops = new LoaderOps(Type.Picture, Ownership.Borrow);
        var loader = LoaderMgr.Loader(path, ops, out Result result);
        try
        {
            Assert.Equal(Result.Success, result);
            Assert.NotNull(loader);
            Assert.Equal(0, loader!.sharing);
            Assert.True(LoaderMgr.Retrieve(path + "\0ignored.jpg"));
            Assert.Equal(0, loader.sharing);
            var alias = LoaderMgr.Loader(path + "\0ignored.svg", ops, out result);
            Assert.Same(loader, alias);
            Assert.Equal(1, loader.sharing);
            Assert.True(LoaderMgr.Retrieve(alias));
            Assert.Equal(0, loader.sharing);
        }
        finally { if (loader != null) LoaderMgr.Retrieve(loader); }
    }

    [Fact]
    public void LoaderRetrievalAllowsCacheAccessWhileCloseWaitsForAWorker()
    {
        Assert.Equal(Result.Success, Initializer.Init(2));
        var loader = new CacheWorkerLoader();
        try
        {
            Assert.True(LoaderMgr.Retrieve(loader));
            loader.Worker!.GetAwaiter().GetResult();
            Assert.True(loader.CompletedDuringClose);
        }
        finally { Initializer.Term(); }
    }

    [Fact]
    public void DefaultAndRawEncodedEntriesReturnNativeNonSupportWithoutChangingOwner()
    {
        var loader = new UnsupportedLoader { owner = Ownership.Transfer };
        var ops = new LoaderOps(Type.Picture, Ownership.Copy);
        Assert.Equal(Result.NonSupport, loader.OpenResult("missing", ops));
        Assert.Equal(Result.NonSupport, loader.OpenResult(new byte[] { 1, 2, 3, 4 }, 4, ops));
        Assert.Equal(Ownership.Transfer, loader.owner);
        Assert.Equal(Result.NonSupport, new RawLoader().OpenResult(new byte[] { 1, 2, 3, 4 }, 4, ops));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FileReadingPreservesNativeBytesAndEmptyFileFailure(bool text)
    {
        string path = Path.Combine(Path.GetTempPath(), "thorvg-read-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllBytes(path, System.Array.Empty<byte>());
            Assert.Null(Loader.ReadFile(path, text));
            var bytes = new byte[] { 0xef, 0xbb, 0xbf, 0xff, 0xc0, 0xaf, 13, 10, 116, 101, 120, 116, 0x1a, 97, 102, 116, 101, 114 };
            File.WriteAllBytes(path, bytes);
            var expected = text && OperatingSystem.IsWindows()
                ? new byte[] { 0xef, 0xbb, 0xbf, 0xff, 0xc0, 0xaf, 10, 116, 101, 120, 116 }
                : bytes;
            Assert.Equal(expected, Loader.ReadFile(path, text));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void InvalidPngAndJpegHeadersPreserveTheirDistinctNativeOwnershipOrder()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var ops = new LoaderOps(Type.Picture, Ownership.Copy);
        var png = new PngLoader { owner = Ownership.Transfer };
        var jpg = new JpgLoader();
        try
        {
            Assert.Equal(Result.InvalidArguments, png.OpenResult(bytes, 4, ops));
            Assert.Equal(Ownership.Transfer, png.owner);
            Assert.Equal(Result.InvalidArguments, jpg.OpenResult(bytes, 4, ops));
            Assert.Equal(Ownership.Copy, jpg.owner);
        }
        finally { LoaderMgr.Retrieve(png); LoaderMgr.Retrieve(jpg); }
    }

    [Fact]
    public void ReloadingBorrowedFontCreatesAnIndependentOwnedCacheEntry()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "legacy-upstream", "resources", "DMSans.otf"));
        string name = "font-borrow-reload-" + Guid.NewGuid().ToString("N");
        var first = LoaderMgr.Loader(name, bytes, (uint)bytes.Length, "otf", false)!;
        Loader? second = null;
        try
        {
            Assert.NotNull(first);
            Assert.Equal(Ownership.Borrow, first.owner);
            Assert.Equal(0, first.sharing);
            second = LoaderMgr.Loader(name, bytes, (uint)bytes.Length, "otf", true);
            Assert.NotNull(second);
            Assert.NotSame(first, second);
            Assert.Equal(Ownership.Copy, second!.owner);
            Assert.Equal(1, first.sharing);
            Assert.Equal(0, second.sharing);
        }
        finally
        {
            // Native lookup adds one sharing reference even when a borrowed
            // entry is replaced. Release that reference and then each cache.
            LoaderMgr.Retrieve(first);
            LoaderMgr.Retrieve(first);
            if (second != null) LoaderMgr.Retrieve(second);
        }
    }

    [Theory]
    [InlineData("GIF")]
    [InlineData("Gif")]
    public void SaverFilenameSelectionUsesNativeCaseSensitiveExtension(string extension)
    {
        using var saver = Saver.Gen();
        var shape = Shape.Gen();
        shape.Ref();
        try
        {
            Assert.Equal(Result.NonSupport, saver.Save(shape, "unused." + extension));
            Assert.Equal(Result.InsufficientCondition, saver.Sync());
            Assert.Equal(1, shape.RefCnt());
        }
        finally { shape.Unref(); }
    }

    [Fact]
    public void FontCacheSharingPreservesTheNativeUnsigned16BitBoundary()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "legacy-upstream", "resources", "DMSans.otf"));
        string name = "font-sharing-width-" + Guid.NewGuid().ToString("N");
        var loader = LoaderMgr.Loader(name, bytes, (uint)bytes.Length, "otf", true)!;
        try
        {
            Assert.NotNull(loader);
            loader.sharing = 65534;
            foreach (int expected in new[] { 65535, 0, 1, 2 })
            {
                Assert.Same(loader, LoaderMgr.Font(name));
                Assert.Equal(expected, loader.sharing);
            }
            Assert.False(loader.Close());
            Assert.Equal(1, loader.sharing);
        }
        finally
        {
            loader.sharing = 0;
            LoaderMgr.Retrieve(loader);
        }
    }
}
