using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace ThorVG.Tests
{
    public class tvgLottieUpdateTests
    {
        [Theory]
        [InlineData(0u)]
        [InlineData(2u)]
        public void RepeatedSyncAndFrameRenderingRemainStable(uint threads)
        {
            Assert.Equal(Result.Success, Initializer.Init(threads));
            using var animation = Animation.Gen();
            var picture = animation.GetPicture();
            using var canvas = SwCanvas.Gen()!;
            try
            {
                var directory = Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update");
                Assert.Equal(Result.Success, picture.Load(Path.Combine(directory, "lottie-trim-source.lot")));
                picture.SetSize(96, 96);
                uint[] pixels = new uint[96 * 96];
                Assert.Equal(Result.Success, canvas.Target(pixels, 96, 96, 96, ColorSpace.ABGR8888));
                Assert.Equal(Result.Success, canvas.Add(picture));
                using var provenance = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "lottie-provenance.json")));
                foreach (var expected in provenance.RootElement.GetProperty("expected").EnumerateArray())
                {
                    int frame = expected.GetProperty("frame").GetInt32();
                    Assert.Equal(frame == 0 ? Result.InsufficientCondition : Result.Success, animation.Frame(frame));
                    for (int repeat = 0; repeat < 3; ++repeat)
                    {
                        Assert.Equal(Result.Success, canvas.Update());
                        Assert.Equal(Result.Success, canvas.Draw(true));
                        Assert.Equal(Result.Success, canvas.Sync());
                        var hash = Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(pixels.AsSpan()))).ToLowerInvariant();
                        Assert.Equal(expected.GetProperty("sha256").GetString(), hash);
                        int shapes = 0;
                        Accessor.Gen().Set(picture, (paint, data) => { if (paint is Shape) ++shapes; return true; });
                        Assert.Equal(3, shapes);
                    }
                }
            }
            finally
            {
                canvas.Remove();
                animation.Dispose();
                canvas.Dispose();
                Assert.Equal(Result.Success, Initializer.Term());
            }
        }
    }
}
