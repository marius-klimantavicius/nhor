using System;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;

namespace ThorVG.Tests;

public class tvgRendererHitNativeUpdateTests
{
    [Fact]
    public void ReusedCurvedPathThenRectangleKeepsNativeHitTestingAndPixels()
    {
        string architecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        string fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", $"renderer-hit-{architecture}-native-golden.bin");
        using var expected = new BinaryReader(File.OpenRead(fixture));
        void Word(uint value) => Assert.Equal(expected.ReadUInt32(), value);
        Word((uint)Initializer.Init(0));
        try
        {
            for (int kind = 0; kind < 3; ++kind)
            {
                using var canvas = SwCanvas.Gen()!;
                var pixels = new uint[64 * 64];
                Word((uint)canvas.Target(pixels, 64, 64, 64, ColorSpace.ABGR8888));
                var shape = Shape.Gen();
                if (kind == 0) shape.AppendCircle(27.25f, 26.5f, 19.75f, 18.25f);
                if (kind == 1)
                {
                    shape.MoveTo(4, 5); shape.LineTo(53, 19); shape.LineTo(13, 51); shape.Close();
                }
                if (kind == 2) shape.AppendRect(4, 5, 49, 43, 7, 9);
                shape.SetFill(81, 163, 227, 211);
                Word((uint)canvas.Add(shape));
                for (int stage = 0; stage < 3; ++stage)
                {
                    if (stage > 0)
                    {
                        Word((uint)shape.ResetShape());
                        if (stage == 1) Word((uint)shape.AppendRect(7, 9, 39, 31));
                        else Word((uint)shape.AppendCircle(27.25f, 26.5f, 19.75f, 18.25f));
                        shape.SetFill(81, 163, 227, 211);
                    }
                    Word((uint)canvas.Update());
                    Word((uint)canvas.Draw(true));
                    Word((uint)canvas.Sync());
                    for (int y = 0; y < 64; y += 4)
                    for (int x = 0; x < 64; x += 4)
                        Word(shape.Intersects(x, y, 2, 2) ? 1u : 0u);
                    foreach (uint pixel in pixels) Word(pixel);
                }
            }
        }
        finally { Word((uint)Initializer.Term()); }
        Assert.Equal(expected.BaseStream.Length, expected.BaseStream.Position);
    }
}
