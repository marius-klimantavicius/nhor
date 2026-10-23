
using System;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text.Json;
using ThorVG;
using Xunit;

namespace ThorVG.Tests;

public unsafe class tvgShapeStrokeNativeUpdateTests
{
    [Fact]
    public void OrdinaryDashTrimCapsAndJoinsMatchEveryNativeBorderAndSpan()
    {
        using var stream = new MemoryStream();
        using var output = new BinaryWriter(stream);
        Matrix[] matrices =
        {
            new(1, 0, 0, 0, 1, 0, 0, 0, 1),
            new(.75f, .125f, 2.5f, -.25f, 1.25f, -1.75f, 0, 0, 1),
            new(1.3f, -.4f, -3.25f, .2f, .9f, 4.125f, 0, 0, 1)
        };
        float[][] patterns = { new float[] { 3, 2 }, new float[] { 2, 4, 1 }, new float[] { 5, 1, 2, 3 } };
        float[] begins = { 0, .15f, .75f }, ends = { 1, .8f, 1.25f }, offsets = { 0, 2.25f, -3.5f };
        StrokeCap[] caps = { StrokeCap.Butt, StrokeCap.Round, StrokeCap.Square };
        StrokeJoin[] joins = { StrokeJoin.Miter, StrokeJoin.Round, StrokeJoin.Bevel };
        Assert.Equal(Result.Success, Initializer.Init(0));
        SwMemPool.mpoolInit(2);
        var pool = SwMemPool.mpoolReq();

        for (uint n = 0; n < 96; ++n)
        {
            using var memory = new MemoryStream();
            using var row = new BinaryWriter(memory);
            void Word(uint v) => row.Write(v);
            void Number(float f) => Word(BitConverter.SingleToUInt32Bits(f));
            var shape = Shape.Gen();
            switch (n % 4)
            {
                case 0:
                    shape.AppendRect(4.25f, 5.5f, 43.75f, 37.25f);
                    break;
                case 1:
                    shape.AppendCircle(29.25f, 28.5f, 21.75f, 18.25f);
                    break;
                case 2:
                    shape.MoveTo(3.25f, 7.5f);
                    shape.CubicTo(19.75f, -2.25f, 51.5f, 27.25f, 43.75f, 47.5f);
                    shape.LineTo(13.5f, 39.25f);
                    break;
                case 3:
                    shape.MoveTo(4.25f, 8.5f);
                    shape.LineTo(49.5f, 11.25f);
                    shape.LineTo(17.75f, 47.5f);
                    shape.Close();
                    shape.MoveTo(16, 18);
                    shape.LineTo(38, 24);
                    shape.LineTo(22, 35);
                    shape.Close();
                    break;
            }
            shape.SetFill(97, 173, 229, 211);
            shape.StrokeWidth((n / 7) % 2 != 0 ? 7.25f : 2.5f);
            shape.StrokeFill(229, 71, 113, 191);
            shape.StrokeCap(caps[(n / 4) % 3]);
            shape.StrokeJoin(joins[(n / 12) % 3]);
            shape.StrokeMiterlimit(4);
            uint pattern = (n / 3) % 4;
            if (pattern != 0) shape.StrokeDash(patterns[pattern - 1], (uint)patterns[pattern - 1].Length, offsets[(n / 11) % 3]);
            shape.Trimpath(begins[(n / 5) % 3], ends[(n / 5) % 3], true);
            var rshape = shape.rs;
            var result = new SwShape();
            var clip = new RenderRegion(-16, -16, 96, 96);
            RenderRegion box = default;
            var matrix = matrices[(n / 9) % 3];
            bool prepared = SwShapeOps.shapePrepare(result, rshape, matrix, clip, ref box, pool, n % 3, false);
            bool filled = prepared && SwShapeOps.shapeGenRle(result, box, pool, n % 3, true);
            Word(filled ? 1u : 0u);
            Word(result.fastTrack ? 1u : 0u);
            Word((uint)box.min.x);
            Word((uint)box.min.y);
            Word((uint)box.max.x);
            Word((uint)box.max.y);
            void Spans(in SwRle rle)
            {
                Word(rle.spans.count);
                for (uint i = 0; i < rle.spans.count; ++i)
                {
                    var s = rle.spans[i];
                    Word((uint)s.x);
                    Word((uint)s.y);
                    Word((uint)s.len);
                    Word(s.coverage);
                }
            }
            Spans(result.rle);
            Word(SwShapeOps.shapeGenStrokeRle(result, rshape, matrix, clip, ref box, pool, n % 3, true) ? 1u : 0u);
            Word((uint)box.min.x);
            Word((uint)box.min.y);
            Word((uint)box.max.x);
            Word((uint)box.max.y);
            Spans(result.strokeRle);
            for (int side = 0; side < 2; ++side)
            {
                var b = result.stroke!.borders[side];
                Word(b.pts.count);
                for (uint i = 0; i < b.pts.count; ++i)
                {
                    Number(b.pts[i].x);
                    Number(b.pts[i].y);
                    Word(b.tags[i]);
                }
            }
            SwShapeOps.shapeFree(result);
            Paint.Rel(shape);
            row.Flush();
            output.Write((uint)memory.Length);
            output.Write(memory.ToArray());
        }
        SwMemPool.mpoolTerm();
        Assert.Equal(Result.Success, Initializer.Term());
        output.Flush();
        stream.Position = 0;
        using var reader = new BinaryReader(stream);
        using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "shape-stroke-native-golden.json")));
        string arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        int count = 0;
        foreach (var entry in expected.RootElement.GetProperty("architectures").GetProperty(arch).GetProperty("cases").EnumerateArray())
        {
            Assert.Equal(count, entry.GetProperty("index").GetInt32());
            int length = checked((int)reader.ReadUInt32());
            Assert.Equal(entry.GetProperty("bytes").GetInt32(), length);
            byte[] bytes = reader.ReadBytes(length);
            Assert.Equal(length, bytes.Length);
            Assert.Equal(entry.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
            ++count;
        }
        Assert.Equal(96, count);
        Assert.Equal(stream.Length, stream.Position);
    }
}

