using System;
using System.IO;
using System.Runtime.InteropServices;
using ThorVG;
using Xunit;

namespace ThorVG.Tests;
public class tvgSvgScenesNativeUpdateTests
{
    [Fact]
    public void OrdinaryLoaderScenesMatchOriginalNativePixels()
    {
        string architecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        string folder = System.IO.Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update");
        using var expected = new BinaryReader(File.OpenRead(System.IO.Path.Combine(folder, "svg-scenes-" + architecture + "-native-golden.bin")));
        string[] inputs = new string[]
        {
            "svg-normal-nested-groups.svg",
            "svg-normal-transform-priority.svg",
            "svg-normal-cross-gradient.svg",
            "svg-normal-root-opacity.svg",
            "svg-normal-root-transform.svg"
        };
        Assert.Equal(Result.Success, Initializer.Init(0));
        try
        {
            foreach (var input in inputs)
            {
                using var animation = Animation.Gen();
                var picture = animation.GetPicture();
                Assert.Equal(Result.Success, picture.Load(System.IO.Path.Combine(folder, input)));
                Assert.Equal(Result.Success, picture.GetSize(out float width, out float height));
                Assert.Equal(160, width);
                Assert.Equal(120, height);
                Assert.Equal(Result.Success, picture.SetSize(192, 128));
                var pixels = new uint[192 * 128];
                using var canvas = SwCanvas.Gen();
                Assert.Equal(Result.Success, canvas.Target(pixels, 192, 192, 128, ColorSpace.ABGR8888));
                Assert.Equal(Result.Success, canvas.Add(picture));
                try
                {
                    Assert.Equal(Result.Success, canvas.Draw(true));
                    Assert.Equal(Result.Success, canvas.Sync());
                    var actual = new byte[pixels.Length * sizeof(uint)];
                    Buffer.BlockCopy(pixels, 0, actual, 0, actual.Length);
                    var native = expected.ReadBytes(actual.Length);
                    Assert.Equal(actual.Length, native.Length);
                    Assert.True(actual.AsSpan().SequenceEqual(native), $"Original native normal SVG scene pixels: {input} ({architecture})");
                }
                finally
                {
                    Assert.Equal(Result.Success, canvas.Remove(picture));
                }
            }

            Assert.Equal(expected.BaseStream.Length, expected.BaseStream.Position);
        }
        finally
        {
            Assert.Equal(Result.Success, Initializer.Term());
        }
    }
}
