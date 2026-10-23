using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using Xunit;

namespace ThorVG.Tests;

public class tvgLottieExpressionUpdateTests
{
    private sealed class Model : IDisposable
    {
        public readonly LottieComposition Composition;
        public readonly LottieLayer Layer;
        public readonly LottieRect Rect = new();
        public readonly LottieColor Color = new(new RGB32(60, 120, 180));
        public readonly LottieOpacity Opacity = new(128);
        public readonly LottieTextDoc Text = new();
        public readonly LottiePath Path = new();
        public readonly LottieExpressions Expressions = LottieExpressions.Instance()!;

        public Model()
        {
            var root = new LottieRootLayer { inFrame = 0, outFrame = 90 };
            Composition = new LottieComposition { root = root, w = 128, h = 64, frameRate = 30, name = "Fixture" };
            Layer = new LottieLayer
            {
                id = TvgCompressor.Djb2EncodeNative("Layer"), ix = 7, inFrame = 3, outFrame = 90, w = 128, h = 64, comp = root,
                cacheMatrix = new Matrix { e11 = 1, e22 = 1, e33 = 1, e13 = 11, e23 = 13 }, transform = new LottieTransform()
            };
            Layer.transform.position.value = new Point(20, 30);
            root.children.Add(Layer);
            var group = new LottieGroup { type = LottieObject.ObjectType.Group, id = TvgCompressor.Djb2EncodeNative("Group") };
            Layer.children.Add(group);
            Rect.id = TvgCompressor.Djb2EncodeNative("Box"); Rect.size.value = new Point(40, 20); Rect.position.value = new Point(3, 4);
            Rect.position.ix = 1; Rect.radius.ix = 3; group.children.Add(Rect);
            var first = Rect.radius.NewFrame(); first.no = 0; first.value = 2;
            var second = Rect.radius.NewFrame(); second.no = 30; second.value = 8.5f;
            var vf = Rect.position.NewFrame(); vf.no = 0; vf.value = new Point(3, 4);
            var vl = Rect.position.NewFrame(); vl.no = 30; vl.value = new Point(33, 64);
            Text.value.text = "Hi";
            Path.pathset.value = new PathSet
            {
                ptsCnt = 4, cmdsCnt = 5,
                pts = new[] { new Point(0, 0), new Point(30, 0), new Point(30, 20), new Point(0, 20) },
                cmds = new[] { PathCommand.MoveTo, PathCommand.LineTo, PathCommand.LineTo, PathCommand.LineTo, PathCommand.Close }
            };
        }

        public LottieExpression Expression(string code, LottieProperty property) => new()
            { code = code, comp = Composition, layer = Layer, obj = property == Path.pathset ? Path : Rect, property = property };

        public string Evaluate(string kind, string code, float frame)
        {
            LottieProperty property = kind switch
            { "vector" => Rect.position, "color" => Color, "opacity" => Opacity, "text" => Text, "path-query" => Path.pathset, _ => Rect.radius };
            var expression = Expression(code, property); property.exp = expression;
            Expressions.Update(Composition.TimeAtFrame(frame));
            string value;
            if (kind == "vector") { var p = Rect.position.Evaluate(frame, Expressions); value = Number(p.x) + "," + Number(p.y); }
            else if (kind == "color") { var c = Color.Evaluate(frame, Expressions); value = $"{c.r},{c.g},{c.b}"; }
            else if (kind == "opacity") value = Opacity.Evaluate(frame, Expressions).ToString();
            else if (kind == "text") { Text.value.text = "Hi"; value = Text.Evaluate(frame, Expressions).text ?? ""; }
            else if (kind == "path-query") { Expressions.ResultFloat(Rect.radius, frame, out var f, expression); value = Number(f); }
            else value = Number(Rect.radius.Evaluate(frame, Expressions));
            property.exp = null;
            return value + "\t" + (expression.disabled ? "1" : "0");
        }

        public void Dispose() { Composition.Release(); LottieExpressions.Retrieve(Expressions); }
    }

    private static string Number(float value) => float.IsNaN(value) ? "nan" : BitConverter.SingleToUInt32Bits(value).ToString("x8");

    [Fact]
    public void DefinedExpressionResultsMatchPinnedNativeBitsAndPersistentContext()
    {
        using var model = new Model();
        using var document = JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "expressions-native-golden.json")));
        foreach (var row in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var result = model.Evaluate(row.GetProperty("kind").GetString()!, row.GetProperty("code").GetString()!, row.GetProperty("frame").GetSingle());
            string expected = row.GetProperty("expected").GetString()!;
            if (OperatingSystem.IsLinux() && System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture ==
                System.Runtime.InteropServices.Architecture.Arm64 && row.TryGetProperty("expectedLinuxArm64", out var arm))
                expected = arm.GetString()!;
            Assert.True(result == expected, row.GetProperty("name").GetString() + ": " + result + " expected " + expected);
        }
    }

    [Theory]
    [InlineData("random()")]
    [InlineData("Math.random()")]
    public void RandomSeedAndGeneratorStreamAreSharedAcrossExpressionOwners(string call)
    {
        using var first = new Model(); using var second = new Model();
        Assert.Same(first.Expressions, second.Expressions);
        LottieExpressions.SeedRandom(123);
        var a = first.Evaluate("float", "$bm_rt=" + call + ";", 10);
        var b = second.Evaluate("float", "$bm_rt=" + call + ";", 10);
        LottieExpressions.SeedRandom(123);
        Assert.Equal(a, second.Evaluate("float", "$bm_rt=" + call + ";", 10));
        Assert.Equal(b, first.Evaluate("float", "$bm_rt=" + call + ";", 10));
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void SharedExpressionOwnerKeepsSeparateWorkerGlobalsAndTimes()
    {
        using var a = new Model(); using var b = new Model();
        string? one = null, two = null; Exception? error = null;
        using var barrier = new Barrier(2);
        var t1 = new Thread(() => Run(a, 12, 7, ref one));
        var t2 = new Thread(() => Run(b, 30, 9, ref two));
        t1.Start(); t2.Start(); t1.Join(); t2.Join();
        Assert.Null(error); Assert.Equal(Number(7.4f) + "\t0", one); Assert.Equal(Number(10) + "\t0", two);
        void Run(Model model, float frame, int offset, ref string? output)
        {
            try
            {
                model.Evaluate("float", "var workerValue=" + offset + ";$bm_rt=workerValue;", frame);
                if (!barrier.SignalAndWait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Second expression worker did not reach the barrier");
                output = model.Evaluate("float", "$bm_rt=workerValue+time;", frame);
            }
            catch (Exception e) { Interlocked.CompareExchange(ref error, e, null); }
        }
    }

    [Fact]
    public void ChangedExpressionCodeInvalidatesThePreparedScript()
    {
        using var model = new Model();
        var expression = model.Expression("$bm_rt=2;", model.Rect.radius);
        Assert.True(model.Expressions.ResultFloat(model.Rect.radius, 0, out var value, expression)); Assert.Equal(2, value);
        expression.code = "$bm_rt=3;";
        Assert.True(model.Expressions.ResultFloat(model.Rect.radius, 0, out value, expression)); Assert.Equal(3, value);
    }
}
