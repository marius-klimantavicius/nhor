using System;
using System.Text;
using Xunit;

namespace ThorVG.Tests
{
    public class tvgSvgLoaderLifetimeTests
    {
        [Fact]
        public void CachedPicturesOwnIndependentVectorTrees()
        {
            Initializer.Init();
            var data = Encoding.UTF8.GetBytes("<svg viewBox='0 0 20 20'><rect width='20' height='20' fill='red'/></svg>");
            var first = Picture.Gen(); var second = Picture.Gen();
            try
            {
                Assert.Equal(Result.Success, first.Load(data, (uint)data.Length, "svg"));
                Assert.Equal(Result.Success, second.Load(data, (uint)data.Length, "svg"));
                first.SetSize(40, 40); second.SetSize(10, 10);
                Assert.Equal(Result.Success, first.Bounds(out _, out _, out var firstW, out _));
                Assert.Equal(Result.Success, second.Bounds(out _, out _, out var secondW, out _));
                Assert.Equal(40, firstW); Assert.Equal(10, secondW);
                Paint.Rel(first);
                Assert.Equal(Result.Success, second.Bounds(out _, out _, out secondW, out _));
                Assert.Equal(10, secondW);
            }
            finally { Paint.Rel(first); Paint.Rel(second); Initializer.Term(); }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void BorrowedSourceIsConsumedAtReadAndCopiedSourceIsIndependent(bool copy)
        {
            Initializer.Init();
            var source = Encoding.UTF8.GetBytes("<svg viewBox='0 0 16 16'><rect width='16' height='16' fill='#ff0000'/></svg>");
            var loader = new SvgLoaderInstance();
            using var canvas = SwCanvas.Gen()!;
            try
            {
                Assert.True(loader.Open(source, (uint)source.Length, new PictureOps(null, null, false), copy));
                int color = Encoding.UTF8.GetString(source).IndexOf("#ff0000", StringComparison.Ordinal);
                Encoding.UTF8.GetBytes("#0000ff").CopyTo(source.AsSpan(color, 7));
                Assert.True(loader.Read()); Assert.True(loader.Sync());
                var scene = Assert.IsType<Scene>(loader.GetPaint());
                scene.Ref();
                var pixels = new uint[256]; canvas.Target(pixels, 16, 16, 16, ColorSpace.ABGR8888); canvas.Add(scene);
                Assert.Equal(Result.Success, canvas.Draw(true)); Assert.Equal(Result.Success, canvas.Sync());
                Assert.Equal(copy ? 0xff0000ffu : 0xffff0000u, pixels[8 * 16 + 8]);
                canvas.Remove();
                Assert.True(LoaderMgr.Retrieve(loader));
                Assert.Single(scene.Paints());
                scene.Unref();
                Assert.Empty(scene.Paints());
            }
            finally { canvas.Dispose(); Initializer.Term(); }
        }

        [Fact]
        public void MissingViewboxCompletesBoundsDuringOpenAndInvalidInputsFail()
        {
            Initializer.Init();
            var loader = new SvgLoaderInstance();
            try
            {
                var data = Encoding.UTF8.GetBytes("<svg><rect x='3' y='5' width='20' height='10' fill='red'/></svg>");
                Assert.True(loader.Open(data, (uint)data.Length, new PictureOps(null, null, false), true));
                Assert.Equal(20, loader.w); Assert.Equal(10, loader.h);
                Assert.False(loader.Open(data, (uint)data.Length + 1, null, false));
                Assert.False(loader.Open(data, (uint)data.Length, new LoaderOps(ThorVG.Type.Text, Ownership.Borrow), false));
            }
            finally { LoaderMgr.Retrieve(loader); Initializer.Term(); }
        }
    }
}
