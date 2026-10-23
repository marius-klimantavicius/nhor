using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Xunit;

namespace ThorVG.Tests;

public class tvgGeometryNativeUpdateTests
{
    [Fact]
    public void SubdivisionMatchesNativeDegenerateAndNonfiniteCaps()
    {
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "fixtures", "thorvg-update", "math-segments-native-golden.json")));
        var root = data.RootElement;
        using var input = new BinaryReader(new MemoryStream(root.GetProperty("inputBase64").GetBytesFromBase64()));
        bool arm = OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        using var expected = new BinaryReader(new MemoryStream(root.GetProperty(arm ? "expectedLinuxArm64Base64" : "expectedX86Base64").GetBytesFromBase64()));
        for (int i = 0; i < root.GetProperty("cases").GetInt32(); ++i)
        {
            var curve = new Bezier(new Point(input.ReadSingle(), input.ReadSingle()),
                new Point(input.ReadSingle(), input.ReadSingle()), new Point(input.ReadSingle(), input.ReadSingle()),
                new Point(input.ReadSingle(), input.ReadSingle()));
            uint count = curve.Segments(input.ReadSingle());
            Assert.Equal(expected.ReadUInt32(), count);
            Assert.InRange(count, 1u, 1024u);
        }
        Assert.Equal(input.BaseStream.Length, input.BaseStream.Position);
        Assert.Equal(expected.BaseStream.Length, expected.BaseStream.Position);
    }

    [Theory]
    [InlineData("geometry")]
    [InlineData("operands")]
    [InlineData("core")]
    public void CommonGeometryMatchesAllPinnedNativeOutputBits(string group)
    {
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "common-math-" + group + "-native-golden.json")));
        var root = data.RootElement;
        using var input = new BinaryReader(new MemoryStream(root.GetProperty("inputBase64").GetBytesFromBase64()));
        bool arm = OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        using var expected = new BinaryReader(new MemoryStream(root.GetProperty(arm ? "expectedLinuxArm64Base64" : "expectedX86Base64").GetBytesFromBase64()));
        bool special = root.GetProperty("special").GetBoolean();
        int cases = root.GetProperty("cases").GetInt32();
        for (int index = 0; index < cases; index++)
        {
            var v = new float[24];
            for (int i = 0; i < v.Length; i++) v[i] = input.ReadSingle();
            var actual = group == "core" ? EvaluateCore(v) : Evaluate(v, special);
            Assert.Equal(root.GetProperty("wordsPerCase").GetInt32(), actual.Count);
            foreach (float value in actual)
            {
                float native = expected.ReadSingle();
                if (float.IsNaN(native)) Assert.True(float.IsNaN(value));
                else Assert.Equal(BitConverter.SingleToUInt32Bits(native), BitConverter.SingleToUInt32Bits(value));
            }
        }
        Assert.Equal(input.BaseStream.Length, input.BaseStream.Position);
        Assert.Equal(expected.BaseStream.Length, expected.BaseStream.Position);
    }

    private static unsafe List<float> EvaluateCore(float[] v)
    {
        var result = new List<float>();
        void Emit(float value) => result.Add(value);
        void PointOut(Point point) { Emit(point.x); Emit(point.y); }
        void MatrixOut(Matrix matrix) => result.AddRange(new[] { matrix.e11, matrix.e12, matrix.e13, matrix.e21, matrix.e22, matrix.e23, matrix.e31, matrix.e32, matrix.e33 });
        void CurveOut(Bezier curve) { PointOut(curve.start); PointOut(curve.ctrl1); PointOut(curve.ctrl2); PointOut(curve.end); }
        var m = new Matrix(v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8]);
        var a = new Point(v[9], v[10]); var b = new Point(v[11], v[12]);
        var c = new Point(v[13], v[14]); var d = new Point(v[15], v[16]);
        var curve = new Bezier(a, b, c, d); float t = v[20];
        Emit(TvgMath.Deg2Rad(v[18])); Emit(TvgMath.Rad2Deg(v[19])); Emit(TvgMath.Atan(a));
        Emit(TvgMath.Zero(v[18]) ? 1 : 0); Emit(TvgMath.Equal(v[18], v[19]) ? 1 : 0);
        Emit(TvgMath.Clamp(v[18], -1f, 1f)); Emit(TvgMath.Remap255(v[20]));
        Emit(TvgMath.Remap255<int>(v[20])); Emit(TvgMath.Remap255<float>(v[20]));
        Emit(TvgMath.IsIdentity(m) ? 1 : 0); Emit(TvgMath.MatrixEqual(m, m) ? 1 : 0);
        Emit(TvgMath.MatrixNotEqual(m, TvgMath.Identity()) ? 1 : 0);
        var changed = m; TvgMath.Rotate(ref changed, v[21]); MatrixOut(changed);
        changed = m; TvgMath.Scale(ref changed, a); MatrixOut(changed);
        changed = m; TvgMath.ScaleR(ref changed, a); MatrixOut(changed);
        changed = m; TvgMath.Translate(ref changed, a); MatrixOut(changed);
        float length = curve.Length(); Emit(curve.At(t * length, length));
        float approximate = curve.LengthApprox(); Emit(curve.AtApprox(t * approximate, approximate));
        curve.Split(t * length, out var left, out var right); CurveOut(left); CurveOut(right);
        PathCommand[] commands = { PathCommand.MoveTo, PathCommand.LineTo, PathCommand.CubicTo, PathCommand.Close, PathCommand.MoveTo, PathCommand.LineTo };
        Point[] points = { a, b, c, d, a, b, c };
        fixed (PathCommand* cp = commands) fixed (Point* pp = points) Emit(TvgMath.Length(cp, 6, pp, 7));
        Emit(TvgMath.Length(null, 0, null, 0));
        PointOut(TvgMath.PointAdd(a, v[18])); PointOut(TvgMath.PointDiv(a, b)); PointOut(TvgMath.PointNeg(a));
        Emit((int)TvgMath.GetOrientation(a, b, c));
        var box = new BBox { min = TvgMath.PointMin(a, b), max = TvgMath.PointMax(a, b) };
        Emit(box.Inside(c) ? 1 : 0); Emit(box.Zero() ? 1 : 0); Emit(box.W()); Emit(box.H());
        return result;
    }

    private static List<float> Evaluate(float[] v, bool special)
    {
        var result = new List<float>();
        void PointOut(Point p) { result.Add(p.x); result.Add(p.y); }
        var a = new Point(v[9], v[10]); var b = new Point(v[11], v[12]);
        if (special)
        {
            PointOut(TvgMath.PointMin(a, b)); PointOut(TvgMath.PointMax(a, b));
            result.Add(TvgMath.Atan2(v[18], v[19]));
            return result;
        }
        var m = new Matrix(v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8]);
        var c = new Point(v[13], v[14]); var d = new Point(v[15], v[16]);
        var bez = new Bezier(a, b, c, d); float t = v[20];
        result.Add(TvgMath.Scaling(m));
        var translated = m; TvgMath.TranslateR(ref translated, a);
        result.AddRange(new[] { translated.e11, translated.e12, translated.e13, translated.e21, translated.e22, translated.e23, translated.e31, translated.e32, translated.e33 });
        var normalized = a; TvgMath.Normalize(ref normalized); PointOut(normalized);
        PointOut(TvgMath.Normal(a, b));
        var line = new Line { pt1 = a, pt2 = b }; result.Add(line.Length());
        line.Split(t, out var left, out var right);
        PointOut(left.pt1); PointOut(left.pt2); PointOut(right.pt1); PointOut(right.pt2);
        PointOut(bez.Tangent(t)); result.Add(bez.Angle(t));
        foreach (float tolerance in new[] { .001f, .1f, 1f, 10f, 100f }) result.Add(bez.Flatten(tolerance) ? 1 : 0);
        result.Add(bez.Segments(v[22]));
        var quarter = new Bezier(a, b, v[17]);
        PointOut(quarter.start); PointOut(quarter.ctrl1); PointOut(quarter.ctrl2); PointOut(quarter.end);
        result.Add(TvgMath.Closed(a, b, v[18]) ? 1 : 0);
        PointOut(TvgMath.PointMin(a, b)); PointOut(TvgMath.PointMax(a, b));
        result.Add(TvgMath.Radian(m)); result.Add(TvgMath.RightAngle(m) ? 1 : 0);
        result.Add(TvgMath.Skewed(m) ? 1 : 0); result.Add(TvgMath.Atan2(v[18], v[19]));
        return result;
    }
}
