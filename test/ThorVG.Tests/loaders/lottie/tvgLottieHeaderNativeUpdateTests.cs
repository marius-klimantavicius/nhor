using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ThorVG.Tests;

public class tvgLottieHeaderNativeUpdateTests
{
    [Fact]
    public void QuickHeaderMatchesEveryDefinedNativeResultAndFieldMutation()
    {
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "fixtures", "thorvg-update", "lottie-header-native-golden.json")));
        bool arm = OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        // The quick-header contract requires an actual worker scheduler.
        // Other tests may retain an initializer reference with zero workers;
        // native nested init deliberately preserves that first configuration.
        int previous = TvgCommon.engineInit;
        uint previousThreads = TaskScheduler.Threads();
        for (int i = 0; i < previous; ++i) Assert.Equal(Result.Success, Initializer.Term());
        // Direct scheduler tests can leave an initialized zero-worker scheduler
        // without an initializer reference. Native Init preserves that instance.
        // Start this worker-dependent fixture from an explicitly fresh scheduler.
        TaskScheduler.Term();
        Assert.Equal(Result.Success, Initializer.Init(2));
        try
        {
            Assert.Equal(2u, TaskScheduler.Threads());
            foreach (var row in data.RootElement.GetProperty("cases").EnumerateArray())
            {
                var loader = new LottieLoader { segmentEnd = data.RootElement.GetProperty("initialSegmentEnd").GetSingle() };
                try
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(row.GetProperty("input").GetString()!);
                    var result = loader.OpenResult(bytes, (uint)bytes.Length, new PictureOps(Ownership.Copy, null, null, false));
                    var actual = new StringBuilder(((int)result).ToString());
                    foreach (float value in new[] { loader.w, loader.h, loader.frameNo, loader.frameCnt, loader.frameRate, loader.segmentBegin, loader.segmentEnd })
                        actual.Append(',').Append(BitConverter.SingleToUInt32Bits(value).ToString("x8"));
                    string name = row.GetProperty("name").GetString()!;
                    Assert.Equal(name + ":" + row.GetProperty(arm ? "expectedLinuxArm64" : "expectedX86").GetString(), name + ":" + actual);
                }
                finally { LoaderMgr.Retrieve(loader); }
            }
        }
        finally
        {
            Assert.Equal(Result.Success, Initializer.Term());
            for (int i = 0; i < previous; ++i) Assert.Equal(Result.Success, Initializer.Init(previousThreads));
        }
    }
}
