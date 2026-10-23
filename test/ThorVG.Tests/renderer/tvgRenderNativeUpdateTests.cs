using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Xunit;

namespace ThorVG.Tests;

public class tvgRenderNativeUpdateTests
{
    private static string Number(float n) => float.IsNaN(n) ? "nan," : BitConverter.SingleToUInt32Bits(n).ToString("x8") + ",";
    private static string PointOutput(Point p) => Number(p.x) + Number(p.y);

    private static unsafe string PathOutput(RenderPath path)
    {
        string text = path.cmds.count + "," + path.pts.count + ":";
        for (uint i = 0; i < path.cmds.count; ++i) text += (int)path.cmds[i] + ",";
        text += ":";
        for (uint i = 0; i < path.pts.count; ++i) text += PointOutput(path.pts[i]);
        return text;
    }

    private static JsonDocument Fixture(string group) => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", group + "-native-golden.json")));

    private static void Compare(JsonElement expected, string name, string actual)
    {
        string native = expected.GetProperty(name).GetString()!;
        Assert.True(native == actual, name + ": expected " + native + "; actual " + actual);
    }

    [Fact]
    public unsafe void PathsEffectsAndSurfaceReuseMatchEveryPinnedNativeOutput()
    {
        using var data = Fixture("render-types");
        var root = data.RootElement;
        bool arm = OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        var expected = root.GetProperty(arm ? "expectedLinuxArm64" : "expectedX86");
        using var file = new BinaryReader(new MemoryStream(root.GetProperty("inputBase64").GetBytesFromBase64()));
        int checkedRows = 0;
        void Check(string name, string actual) { Compare(expected, name, actual); ++checkedRows; }

        uint[] pixels = new uint[4];
        using var surface = new RenderSurface();
        int j = 0;
        foreach (var cs in new[] { ColorSpace.ABGR8888S, ColorSpace.XBGR8888, ColorSpace.ARGB8888, ColorSpace.Grayscale8, ColorSpace.XRGB8888, ColorSpace.ARGB8888S })
        {
            surface.Setup(pixels, 2, 2, 2, RenderHelper.ChannelSize(cs), cs);
            using var copy = new RenderSurface(surface);
            Check("surface-" + j++, (int)surface.cs + "," + (surface.opaque ? 1 : 0) + "," + (surface.premultiplied ? 1 : 0) + "," + surface.channelSize + "," + (copy.buf32 == surface.buf32 ? 1 : 0) + "," + (copy.opaque ? 1 : 0));
        }
        foreach (float n in new[] { -0f, float.NaN, -1f, 1f, float.PositiveInfinity })
        {
            var blur = RenderEffectGaussianBlur.Gen(n, -9, -1, -1);
            Check("blur-" + j, Number(blur.sigma) + blur.direction + "," + blur.border + "," + blur.quality);
            var motion = RenderEffectMotionBlur.Gen(n, -0f, -1);
            Check("motion-gen-" + j, Number(motion.distance) + Number(motion.angle) + motion.quality);
            var shadow = RenderEffectDropShadow.Gen(-1, 256, 257, 511, 1, 2, n, -1);
            Check("shadow-gen-" + j++, Number(shadow.sigma) + shadow.color[0] + "," + shadow.color[1] + "," + shadow.color[2] + "," + shadow.color[3] + "," + shadow.quality);
        }
        for (int index = 0; index < root.GetProperty("cases").GetInt32(); ++index)
        {
            var v = new float[32];
            for (int i = 0; i < v.Length; ++i) v[i] = file.ReadSingle();
            var input = new RenderPath();
            var output = new RenderPath();
            try
            {
                int kind = index % 4;
                bool cw = index % 2 != 0;
                if (kind == 0) input.AddRect(v[0], v[1], v[2], v[3], 0, 0, cw);
                if (kind == 1) input.AddRect(v[0], v[1], v[2], v[3], v[4], v[5], cw);
                if (kind == 2) input.AddCircle(v[0], v[1], v[2], v[3], cw);
                if (kind == 3)
                {
                    input.MoveTo(new(v[0], v[1])); input.LineTo(new(v[2], v[3]));
                    input.CubicTo(new(v[4], v[5]), new(v[6], v[7]), new(v[8], v[9])); input.Close();
                    input.MoveTo(new(v[10], v[11])); input.LineTo(new(v[12], v[13])); input.LineTo(new(v[14], v[15])); input.Close();
                }
                Check("path-" + index, PathOutput(input));
                string points = "";
                foreach (float progress in new[] { 0f, .1f, v[26], .9f, 1f }) points += PointOutput(input.PointAt(progress));
                Check("point-" + index, points);
                var matrix = new Matrix(v[16], v[17], v[18], v[19], v[20], v[21], 0, 0, 1);
                var bounds = new BBox();
                bool ok = input.Bounds(index % 2 != 0 ? &matrix : null, ref bounds);
                Check("bounds-" + index, (ok ? 1 : 0) + "," + PointOutput(bounds.min) + PointOutput(bounds.max));
                var trim = new RenderTrimPath { begin = v[27], end = v[28], simultaneous = index % 3 != 0 };
                ok = trim.Trim(input, output);
                Check("trim-" + index, (ok ? 1 : 0) + "," + PathOutput(output));
                var motion = new RenderEffectMotionBlur { distance = v[2], angle = v[22] * 30, quality = (byte)(index % 101) };
                ok = motion.Update(matrix, out var offset, out int samples);
                Check("motion-" + index, (ok ? 1 : 0) + "," + (motion.valid ? 1 : 0) + "," + samples + "," + PointOutput(offset));
                var shadow = new RenderEffectDropShadow { angle = v[23] * 30, distance = v[3] };
                shadow.color[3] = (byte)(index % 2 != 0 ? 255 : 0);
                Check("shadow-parameters-" + index, Number(shadow.angle) + Number(shadow.distance) + Number(matrix.e11) + Number(matrix.e12) + Number(matrix.e21) + Number(matrix.e22));
                ok = shadow.Update(matrix, out offset);
                Check("shadow-" + index, (ok ? 1 : 0) + "," + (shadow.valid ? 1 : 0) + "," + PointOutput(offset));
            }
            finally
            {
                input.cmds.Dispose(); input.pts.Dispose();
                output.cmds.Dispose(); output.pts.Dispose();
            }
        }
        Assert.Equal(2709, checkedRows);
        Assert.Equal(file.BaseStream.Length, file.BaseStream.Position);
    }

    private static unsafe string Regions(RenderDirtyRegion dirty)
    {
        string text = dirty.Count() + ":";
        for (int p = 0; p < 16; ++p)
        {
            text += p + "=";
            ref var list = ref dirty.Get(p);
            for (uint i = 0; i < list.count; ++i)
            {
                var r = list[i];
                text += r.min.x + "," + r.min.y + "," + r.max.x + "," + r.max.y + ";";
            }
            text += "|";
        }
        return text;
    }

    private static unsafe ulong RegionHash(RenderDirtyRegion dirty)
    {
        ulong hash = 14695981039346656037ul;
        for (int p = 0; p < 16; ++p)
        {
            ref var list = ref dirty.Get(p);
            for (uint i = 0; i < list.count; ++i)
            {
                var r = list[i];
                hash = (hash ^ (uint)r.min.x) * 1099511628211ul;
                hash = (hash ^ (uint)r.min.y) * 1099511628211ul;
                hash = (hash ^ (uint)r.max.x) * 1099511628211ul;
                hash = (hash ^ (uint)r.max.y) * 1099511628211ul;
            }
        }
        return hash;
    }

    [Fact]
    public void DirtyRegionsMatchEveryNativePartitionAndScalingCase()
    {
        using var data = Fixture("dirty-regions");
        var root = data.RootElement;
        var expected = root.GetProperty("expectedX86");
        using var file = new BinaryReader(new MemoryStream(root.GetProperty("inputBase64").GetBytesFromBase64()));
        int checkedRows = 0;
        void Check(string name, string actual) { Compare(expected, name, actual); ++checkedRows; }
        for (int index = 0; index < root.GetProperty("cases").GetInt32(); ++index)
        {
            uint w = file.ReadUInt32(), h = file.ReadUInt32(), count = file.ReadUInt32();
            using var dirty = new RenderDirtyRegion();
            dirty.Init(w, h);
            for (uint n = 0; n < count; ++n)
            {
                var r = new int[8];
                for (int i = 0; i < r.Length; ++i) r[i] = file.ReadInt32();
                dirty.Add(new(r[0], r[1], r[2], r[3]), new(r[4], r[5], r[6], r[7]));
            }
            dirty.Commit(); Check("commit-" + index, Regions(dirty));
            dirty.Clear(); dirty.Deactivate(true); dirty.Add(new(0, 0, (int)w, (int)h)); dirty.Commit();
            Check("disabled-" + index, (dirty.Deactivated() ? 1 : 0) + "," + dirty.Count() + "," + (dirty.Deactivate(false) ? 1 : 0));
            dirty.Commit(); Check("reenabled-" + index, Regions(dirty));
        }
        foreach (int count in new[] { 1024, 4096, 16384, 65536 })
        {
            using var dirty = new RenderDirtyRegion();
            dirty.Init((uint)count * 4, 4);
            for (int n = count - 1; n >= 0; --n) dirty.Add(new(n * 4, 3, n * 4 + 1, 4));
            dirty.Commit();
            Check("benchmark-" + count, dirty.Count() + "," + RegionHash(dirty).ToString("x"));
        }
        Assert.Equal(580, checkedRows);
        Assert.Equal(file.BaseStream.Length, file.BaseStream.Position);
    }
}
