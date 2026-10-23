using System;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;
namespace ThorVG.Tests;
public class tvgLottieVectorPrepareNativeUpdateTests
{
    [Fact]
    public void EveryVectorSegmentRetainsItsNativePreparedLength()
    {
        string architecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        using var native = new BinaryReader(File.OpenRead(Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "lottie-vector-prepare-" + architecture + "-native-golden.bin")));
        for (int i = 0; i < 4; ++i)
        {
            var property = new LottieVector();
            for (int k = 0; k < 3; ++k)
            {
                var frame = property.NewFrame();
                frame.no = k * 12.3f;
                frame.value = new Point(13.7f + k * 17.3f, -8.3f + k * 13.7f);
                frame.hasTangent = i % 2 != 0;
                frame.outTangent = new Point(3.7f + i * .5f, 7.3f);
                frame.inTangent = new Point(-2.3f, -4.7f);
            }
            property.Prepare();
            for (int k = 0; k < 2; ++k)
                Assert.Equal(native.ReadUInt32(), BitConverter.SingleToUInt32Bits(property.frames![k].length));
        }
        Assert.Equal(native.BaseStream.Length, native.BaseStream.Position);
    }
}
