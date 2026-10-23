using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ThorVG.Tests;

public class tvgLottieLoaderStateNativeUpdateTests
{
    [Fact]
    public void ValidHeaderOperationsMatchNativeBeforeTheFirstParse()
    {
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "fixtures", "thorvg-update", "lottie-loader-valid-state-golden.json")));
        int previous = TvgCommon.engineInit;
        uint previousThreads = TaskScheduler.Threads();
        for (int i = 0; i < previous; ++i) Assert.Equal(Result.Success, Initializer.Term());
        // Match a fresh native scheduler even after direct scheduler fixtures.
        TaskScheduler.Term();
        Assert.Equal(Result.Success, Initializer.Init(2));
        try
        {
            foreach (var row in data.RootElement.GetProperty("cases").EnumerateArray())
            {
                int index = row.GetProperty("index").GetInt32();
                byte[] source = Encoding.UTF8.GetBytes(row.GetProperty("input").GetString()!);
                var loader = new LottieLoader { segmentEnd = 17.25f };
                try
                {
                    var output = new StringBuilder(((int)loader.OpenResult(source, (uint)source.Length,
                        new PictureOps(Ownership.Copy, null, null, false))).ToString());
                    output.Append(",comp=").Append(loader.comp != null ? 1 : 0);
                    if (index < 3)
                    {
                        output.Append(",segment=").Append((int)loader.Segment(index == 1 ? -5 : 10, index == 2 ? 5 : 50));
                        output.Append(',').Append(Num(loader.segmentBegin)).Append(',').Append(Num(loader.segmentEnd));
                    }
                    else if (index < 5) output.Append(",duration=").Append(Num(loader.Duration()));
                    else if (index < 8)
                    {
                        var paint = Shape.Gen();
                        try
                        {
                            output.Append(",resize=").Append(loader.Resize(paint, 96, 72) ? 1 : 0);
                            var m = paint.Transform();
                            foreach (float v in new[] { m.e11, m.e12, m.e13, m.e21, m.e22, m.e23, m.e31, m.e32, m.e33 })
                                output.Append(',').Append(Num(v));
                        }
                        finally { Paint.Rel(paint); }
                    }
                    else
                    {
                        output.Append(",to=").Append(loader.TweenTo(60) ? 1 : 0);
                        output.Append(",progress=").Append(loader.Tween(0.5f) ? 1 : 0);
                        loader.Sync();
                        output.Append(",active=").Append(loader.builder.tween.active ? 1 : 0);
                        output.Append(",value=").Append(Num(loader.builder.tween.progress));
                    }
                    Assert.Equal(row.GetProperty("name").GetString() + ":" + row.GetProperty("native").GetString(),
                        row.GetProperty("name").GetString() + ":" + output);
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

    private static string Num(float value) => BitConverter.SingleToUInt32Bits(value).ToString("x8");
}
