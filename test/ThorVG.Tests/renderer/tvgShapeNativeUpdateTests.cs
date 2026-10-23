using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace ThorVG.Tests;

public class tvgShapeNativeUpdateTests
{
    [Fact]
    public void SameCountDashesPreserveAliasesAndAllocateNoReplacementBuffers()
    {
        var shape = Shape.Gen();
        try
        {
            float[] pattern = { 3, 4 };
            Assert.Equal(Result.Success, shape.StrokeDash(pattern, 2, 2));
            Assert.Equal(2u, shape.GetStrokeDash(out var first, out _));
            pattern[0] = -1;
            Assert.Equal(Result.Success, shape.StrokeDash(pattern, 2, -0f));
            shape.GetStrokeDash(out var second, out var offset);
            Assert.Same(first, second);
            Assert.Equal(0f, first![0]);
            Assert.Equal(0x80000000u, BitConverter.SingleToUInt32Bits(offset));
            Assert.Equal(Result.Success, shape.StrokeDash(second, 2));
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; ++i) shape.StrokeDash(pattern, 2);
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
            Assert.Equal(Result.InvalidArguments, shape.StrokeDash(new[] { 1f }, 2));
            shape.GetStrokeDash(out second, out _);
            Assert.Same(first, second);
            Assert.Equal(Result.Success, shape.StrokeDash(null, 0, 7.5f));
            Assert.Equal(0u, shape.GetStrokeDash(out second, out offset));
            Assert.Null(second);
            Assert.Equal(7.5f, offset);
        }
        finally { Paint.Rel(shape); }
    }

    [Fact]
    public unsafe void InvalidManagedPathExtentsLeaveTheOwnedPathUntouched()
    {
        var shape = Shape.Gen();
        try
        {
            shape.AppendRect(0, 0, 8, 8);
            shape.GetPath(out var commands, out var commandCount, out var points, out var pointCount);
            Assert.Equal(Result.InvalidArguments, shape.AppendPath(new[] { PathCommand.MoveTo }, 2, new[] { new Point(1, 1) }, 1));
            Assert.Equal(Result.InvalidArguments, shape.AppendPath(new[] { PathCommand.MoveTo }, 1, new[] { new Point(1, 1) }, 2));
            shape.GetPath(out var afterCommands, out var afterCommandCount, out var afterPoints, out var afterPointCount);
            Assert.True(commands == afterCommands && points == afterPoints);
            Assert.Equal(commandCount, afterCommandCount);
            Assert.Equal(pointCount, afterPointCount);
        }
        finally { Paint.Rel(shape); }
    }

    [Fact]
    public void RepeatedParentCompositionPreservesPaintOpacityAndEveryNativePixel()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "shape-states-native-golden.json")));
        bool arm = OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        var expected = document.RootElement.GetProperty(arm ? "expectedLinuxArm64" : "expectedX86").GetProperty("pixelsSha256");
        Assert.Equal(Result.Success, Initializer.Init(0));
        try
        {
            using var canvas = SwCanvas.Gen(EngineOption.SmartRender)!;
            var pixels = new uint[64 * 64];
            canvas.Target(pixels, 64, 64, 64, ColorSpace.ABGR8888);
            var parent = Scene.Gen(); parent.Opacity(128);
            var shape = Shape.Gen(); shape.AppendRect(12, 12, 32, 28); shape.SetFill(200, 30, 60, 255);
            shape.StrokeWidth(8); shape.StrokeFill(20, 120, 220, 255); shape.Opacity(128);
            parent.Add(shape); canvas.Add(parent);
            for (int i = 0; i < 4; ++i)
            {
                shape.SetFill((byte)(200 - i), 30, 60, 255);
                Assert.Equal(Result.Success, canvas.Update());
                Assert.Equal(Result.Success, canvas.Draw(true));
                Assert.Equal(Result.Success, canvas.Sync());
                Assert.Equal(128, shape.Opacity()); Assert.Equal(128, parent.Opacity());
                Assert.Equal(expected[i].GetString(), Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(pixels.AsSpan()))).ToLowerInvariant());
            }
        }
        finally { Assert.Equal(Result.Success, Initializer.Term()); }
    }

    [Fact]
    public void PreparedStrokeBoundsUseTheNativeOutlineForMiterRotationAndRoundJoin()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "shape-states-native-golden.json")));
        bool arm = OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        var expected = document.RootElement.GetProperty(arm ? "expectedLinuxArm64" : "expectedX86").GetProperty("bounds");
        Assert.Equal(Result.Success, Initializer.Init(0));
        try
        {
            using var canvas = SwCanvas.Gen()!;
            canvas.Target(new uint[64 * 64], 64, 64, 64, ColorSpace.ABGR8888);
            var shape = Shape.Gen(); shape.MoveTo(12, 44); shape.LineTo(25, 12); shape.LineTo(38, 44);
            shape.StrokeWidth(10); shape.StrokeFill(255, 255, 255); shape.StrokeJoin(StrokeJoin.Miter); shape.StrokeCap(StrokeCap.Square);
            canvas.Add(shape);
            for (int i = 0; i < 3; ++i)
            {
                if (i == 1) shape.Rotate(17);
                if (i == 2) shape.StrokeJoin(StrokeJoin.Round);
                canvas.Update(); canvas.Draw(true); canvas.Sync();
                var result = shape.Bounds(out float x, out float y, out float width, out float height);
                string actual = (int)result + "," + string.Join(",", System.Array.ConvertAll(new[] { x, y, width, height }, n => BitConverter.SingleToUInt32Bits(n).ToString("x8"))) + ",";
                Assert.Equal(expected[i].GetString(), actual);
            }
        }
        finally { Assert.Equal(Result.Success, Initializer.Term()); }
    }
}
