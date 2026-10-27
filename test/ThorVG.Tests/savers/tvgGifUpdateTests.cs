using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using Xunit;

namespace ThorVG.Tests
{
    public class tvgGifUpdateTests
    {
        [Fact]
        public unsafe void EncoderFilenameUsesNativeCStringTermination()
        {
            var output = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".gif");
            try
            {
                var writer = new GifWriter();
                Assert.True(GifEncoder.GifBegin(writer, output + "\0ignored.png", 2, 2, 3));
                byte[] pixels = { 1, 2, 3, 255, 20, 30, 40, 255, 50, 60, 70, 255, 80, 90, 100, 255 };
                fixed (byte* data = pixels) Assert.True(GifEncoder.GifWriteFrame(writer, data, 2, 2, 3, false));
                Assert.True(GifEncoder.GifEnd(writer));
                Assert.Equal("2cf689fbbf99a77e1fa1d08db294617493664c7f1968643d0aa564ab0f6a0dc3",
                    Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output))).ToLowerInvariant());
            }
            finally { File.Delete(output); }
        }

        [Fact]
        public void PublicSaverFilenameUsesNativeCStringTermination()
        {
            var output = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".gif");
            Assert.Equal(Result.Success, Initializer.Init(0));
            using var animation = Animation.Gen();
            using var saver = Saver.Gen();
            try
            {
                Assert.Equal(Result.Success, animation.GetPicture().Load(Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "lottie-trim-source.lot")));
                animation.GetPicture().SetSize(64, 64);
                Assert.Equal(Result.Success, saver.Save(animation, output + "\0ignored.png", 100, 7));
                Assert.Equal(Result.Success, saver.Sync());
                Assert.Equal((ushort)0, animation.GetPicture().RefCnt());
                Verify(output, "save", 7, false);
            }
            finally
            {
                saver.Dispose(); animation.Dispose(); File.Delete(output);
                Assert.Equal(Result.Success, Initializer.Term());
            }
        }

        [Fact]
        public void RejectedNullModuleFilenameStillClosesThePreviousAnimation()
        {
            var output = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".gif");
            Assert.Equal(Result.Success, Initializer.Init(0));
            using var first = Animation.Gen();
            using var second = Animation.Gen();
            using var module = new GifSaver();
            try
            {
                var fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "lottie-trim-source.lot");
                Assert.Equal(Result.Success, first.GetPicture().Load(fixture));
                first.GetPicture().SetSize(64, 64);
                Assert.Equal(Result.Success, second.GetPicture().Load(fixture));
                second.GetPicture().SetSize(32, 32);
                Assert.True(module.Save(first, null, output, 100, 1));
                Assert.Equal((ushort)1, first.GetPicture().RefCnt());
                Assert.False(module.Save(second, null, null!, 100, 1));
                Assert.Equal((ushort)0, first.GetPicture().RefCnt());
                Assert.Equal((ushort)1, second.GetPicture().RefCnt());
                Assert.True(module.Close());
            }
            finally
            {
                module.Dispose(); first.Dispose(); second.Dispose(); File.Delete(output);
                Assert.Equal(Result.Success, Initializer.Term());
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public unsafe void ErrorBoxQuantizationMatchesPinnedNativeBytes(int scenario)
        {
            var output = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".gif");
            try
            {
                const uint w = 32, h = 32;
                var pixels = new byte[w * h * 4];
                var writer = new GifWriter();
                Assert.True(GifEncoder.GifBegin(writer, output, w, h, 3));
                for (int frame = 0; frame < 6; ++frame)
                {
                    int step = frame == 2 ? 1 : frame;
                    for (int i = 0; i < w * h; ++i)
                    {
                        pixels[i * 4] = scenario == 1 ? (byte)40 : (byte)(i * 13 + step * 17);
                        pixels[i * 4 + 1] = scenario == 1 ? (byte)80 : (byte)(i * 7 + step * 19);
                        pixels[i * 4 + 2] = scenario == 1 ? (byte)120 : (byte)((i / 8) * 31 + step * 23);
                        pixels[i * 4 + 3] = scenario == 2 ? (byte)((i + step) % 4 == 0 ? 126 : ((i + step) % 4 == 1 ? 127 : 255)) : (byte)(scenario == 3 && frame == 0 ? 0 : 255);
                    }
                    fixed (byte* data = pixels) Assert.True(GifEncoder.GifWriteFrame(writer, data, w, h, (uint)frame + 1, scenario >= 2));
                }
                Assert.True(GifEncoder.GifEnd(writer));
                Verify(output, "encode", scenario, false);
            }
            finally { File.Delete(output); }
        }

        [Theory]
        [InlineData(0u, 0u, false)] [InlineData(0u, 7u, false)] [InlineData(0u, 30u, false)] [InlineData(0u, 120u, false)]
        [InlineData(0u, 0u, true)] [InlineData(0u, 7u, true)] [InlineData(0u, 30u, true)] [InlineData(0u, 120u, true)]
        [InlineData(2u, 0u, false)] [InlineData(2u, 7u, false)] [InlineData(2u, 30u, false)] [InlineData(2u, 120u, false)]
        [InlineData(2u, 0u, true)] [InlineData(2u, 7u, true)] [InlineData(2u, 30u, true)] [InlineData(2u, 120u, true)]
        public void PublicSaverMatchesNativeTimingAndReleasesItsAnimation(uint threads, uint fps, bool background)
        {
            var output = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".gif");
            Assert.Equal(Result.Success, Initializer.Init(threads));
            using var animation = Animation.Gen();
            using var saver = Saver.Gen();
            try
            {
                Assert.Equal(Result.Success, animation.GetPicture().Load(Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "lottie-trim-source.lot")));
                animation.GetPicture().SetSize(64, 64);
                if (background)
                {
                    var bg = Shape.Gen(); bg.AppendRect(0, 0, 64, 64); bg.SetFill(20, 40, 80, 255);
                    Assert.Equal(Result.Success, saver.Background(bg));
                }
                Assert.Equal(Result.Success, saver.Save(animation, output, 100, fps));
                Assert.Equal(Result.Success, saver.Sync());
                Assert.Equal((ushort)0, animation.GetPicture().RefCnt());
                Verify(output, "save", (int)fps, background);
            }
            finally
            {
                saver.Dispose(); animation.Dispose(); File.Delete(output);
                Assert.Equal(Result.Success, Initializer.Term());
            }
        }

        private static void Verify(string path, string mode, int scenario, bool background)
        {
            using var provenance = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "gif-provenance.json")));
            JsonElement expected = default;
            foreach (var candidate in provenance.RootElement.GetProperty("cases").EnumerateArray())
                if (candidate.GetProperty("mode").GetString() == mode && candidate.GetProperty("scenario").GetInt32() == scenario && candidate.GetProperty("background").GetBoolean() == background) expected = candidate;
            var bytes = File.ReadAllBytes(path);
            Assert.Equal(expected.GetProperty("bytes").GetInt32(), bytes.Length);
            Assert.Equal(expected.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
            using var stream = new SKMemoryStream(bytes);
            using var decoded = SKCodec.Create(stream);
            Assert.NotNull(decoded);
            var delays = expected.GetProperty("delaysCentiseconds");
            Assert.Equal(delays.GetArrayLength(), decoded.FrameCount);
            var info = new SKImageInfo(decoded.Info.Width, decoded.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            using var frame = new SKBitmap(info);
            for (int i = 0; i < decoded.FrameCount; ++i)
            {
                Assert.Equal(delays[i].GetInt32() * 10, decoded.FrameInfo[i].Duration);
                // With no prior frame, the independent decoder reconstructs the
                // complete displayed RGBA frame, including transparent deltas.
                Assert.Equal(SKCodecResult.Success, decoded.GetPixels(info, frame.GetPixels(), new SKCodecOptions(i)));
                int size = mode == "encode" ? 32 : 64;
                Assert.Equal(size, frame.Width);
                Assert.Equal(size, frame.Height);
                var pixels = frame.Bytes;
                Assert.Equal(size * size * 4, pixels.Length);
                if (mode == "encode" && scenario == 1)
                {
                    for (int pixel = 0; pixel < pixels.Length; pixel += 4)
                        Assert.Equal(new byte[] { 40, 80, 120, 255 }, pixels.AsSpan(pixel, 4).ToArray());
                }
                if (mode == "encode" && scenario == 3 && i == 0)
                {
                    for (int pixel = 3; pixel < pixels.Length; pixel += 4)
                        Assert.Equal(0, pixels[pixel]);
                }
            }
        }
    }
}
