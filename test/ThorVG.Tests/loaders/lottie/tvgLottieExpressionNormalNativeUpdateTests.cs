using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Xunit;

namespace ThorVG.Tests;
public class tvgLottieExpressionNormalNativeUpdateTests
{
    private static string Number(float value) => float.IsNaN(value) ? "nan" : BitConverter.SingleToUInt32Bits(value).ToString("x8");
    [Theory]
    [InlineData("bridge", 88)]
    [InlineData("text", 60)]
    [InlineData("keys", 5)]
    [InlineData("time", 3)]
    [InlineData("hidden", 13)]
    public void NormalExpressionAlgorithmsDescriptorsAndUnicodeMatchOriginalNative(string batch, int count)
    {
        // Native reference executables start each group with a fresh worker
        // context. The shared interpreter deliberately retains user properties;
        // each complete fixture runs on its own thread to preserve that scope.
        Exception? error = null;
        var worker = new System.Threading.Thread(() =>
        {
            try { Verify(batch, count); }
            catch (Exception caught) { error = caught; }
        });
        worker.Start(); worker.Join();
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }

    private static void Verify(string batch, int count)
    {
        string arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        string folder = Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "expression-normal-" + batch);
        var expected = new Dictionary<string, string>();
        foreach (var line in File.ReadLines(Path.Combine(folder, arch + "-native.tsv")))
        {
            int at = line.IndexOf('\t');
            expected.Add(line[..at], line[(at + 1)..]);
        }

        var root = new LottieRootLayer
        {
            inFrame = 0,
            outFrame = 90
        };
        var comp = new LottieComposition
        {
            root = root,
            w = 128,
            h = 64,
            frameRate = 30,
            name = "Fixture"
        };
        var layer = new LottieLayer
        {
            id = TvgCompressor.Djb2Encode("Layer"),
            ix = 7,
            inFrame = 3,
            outFrame = 90,
            w = 128,
            h = 64,
            comp = root,
            cacheMatrix = new Matrix
            {
                e11 = 1,
                e22 = 1,
                e33 = 1,
                e13 = 11,
                e23 = 13
            },
            transform = new LottieTransform()
        };
        layer.transform.position.value = new Point(20, 30);
        root.children.Add(layer);
        var group = new LottieGroup
        {
            type = LottieObject.ObjectType.Group,
            id = TvgCompressor.Djb2Encode("Group")
        };
        layer.children.Add(group);
        var rect = new LottieRect
        {
            id = TvgCompressor.Djb2Encode("Box")
        };
        rect.size.value = new Point(40, 20);
        rect.position.value = new Point(3, 4);
        rect.position.ix = 1;
        rect.radius.ix = 3;
        group.children.Add(rect);
        var first = rect.radius.NewFrame();
        first.no = 0;
        first.value = 2;
        var second = rect.radius.NewFrame();
        second.no = 30;
        second.value = 8.5f;
        var vf = rect.position.NewFrame();
        vf.no = 0;
        vf.value = new Point(3, 4);
        var vl = rect.position.NewFrame();
        vl.no = 30;
        vl.value = new Point(33, 64);
        var color = new LottieColor(new RGB32(60, 120, 180));
        var opacity = new LottieOpacity(128);
        var text = new LottieTextDoc();
        text.value.text = "Hi";
        var path = new LottiePath();
        path.pathset.value = new PathSet
        {
            ptsCnt = 4,
            cmdsCnt = 5,
            pts = new[]
            {
                new Point(0, 0),
                new Point(30, 0),
                new Point(30, 20),
                new Point(0, 20)
            },
            cmds = new[]
            {
                PathCommand.MoveTo,
                PathCommand.LineTo,
                PathCommand.LineTo,
                PathCommand.LineTo,
                PathCommand.Close
            }
        };
        var transform = new LottieTransform();
        group.children.Add(transform);
        foreach (var position in new[]
        {
            layer.transform.position,
            transform.position,
            rect.position
        }

        )
        {
            position.Release();
            for (int k = 0; k < 3; ++k)
            {
                var f = position.NewFrame();
                f.no = k * 30.0f;
                f.value = k == 0 ? new Point(27.3f, 33.7f) : k == 1 ? new Point(83.7f, 61.3f) : new Point(131.3f, 83.7f);
                f.hasTangent = k < 2;
                f.outTangent = k == 0 ? new Point(13.7f, 21.3f) : new Point(17.3f, -7.3f);
                f.inTangent = k == 0 ? new Point(-7.3f, -13.7f) : new Point(-13.7f, 3.7f);
            }

            position.Prepare();
        }

        var expressions = LottieExpressions.Instance()!;
        LottieExpressions.SeedRandom(1);
        try
        {
            int checkedCount = 0;
            foreach (var line in File.ReadLines(Path.Combine(folder, "inputs.txt")))
            {
                var fields = line.Split('\t', 4);
                var kind = fields[0];
                var frame = float.Parse(fields[2], System.Globalization.CultureInfo.InvariantCulture);
                LottieProperty property = kind switch
                {
                    "vector" => rect.position,
                    "color" => color,
                    "opacity" => opacity,
                    "text" => text,
                    "path-query" => path.pathset,
                    _ => rect.radius
                };
                var exp = new LottieExpression
                {
                    code = fields[3],
                    comp = comp,
                    layer = layer,
                    obj = kind == "path-query" ? path : rect,
                    property = property
                };
                property.exp = exp;
                if (kind != "peer") expressions.Update(comp.TimeAtFrame(frame));
                string result;
                if (kind == "vector")
                {
                    var p = rect.position.Evaluate(frame, expressions);
                    result = Number(p.x) + "," + Number(p.y);
                }
                else if (kind == "color")
                {
                    var c = color.Evaluate(frame, expressions);
                    result = $"{c.r},{c.g},{c.b}";
                }
                else if (kind == "opacity")
                    result = opacity.Evaluate(frame, expressions).ToString();
                else if (kind == "text")
                {
                    text.value.text = "Hi";
                    result = text.Evaluate(frame, expressions).text ?? "";
                }
                else if (kind == "path-query")
                {
                    expressions.ResultFloat(rect.radius, frame, out var value, exp);
                    result = Number(value);
                }
                else
                    result = Number(rect.radius.Evaluate(frame, expressions));
                var actual = result + "\t" + (exp.disabled ? 1 : 0);
                Assert.True(expected.TryGetValue(fields[1], out var native) && native == actual, $"{fields[1]} ({arch}): {actual} expected {native}");
                property.exp = null;
                checkedCount++;
            }

            Assert.Equal(count, checkedCount);
            Assert.Equal(count, expected.Count);
        }
        finally
        {
            LottieExpressions.Retrieve(expressions);
            comp.Release();
            LottieExpressions.SeedRandom(1);
        }
    }
}
