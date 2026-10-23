using System;
using System.IO;
using System.Diagnostics;
using System.Text.Json;
using ThorVG;
using System.Runtime.InteropServices;
using Xunit;

namespace ThorVG.Tests;
public unsafe class tvgGpuCommonNativeUpdateTests
{
    [Fact]
    public void OrdinaryPathsDashesAndQueriesMatchNativeWithoutPerPathAllocation()
    {
        string architecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        using var expected = new BinaryReader(File.OpenRead(System.IO.Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "gpu-common-" + architecture + "-native-golden.bin")));
        int record = 0;
        Matrix[] matrices =
        {
            new(1, 0, 0, 0, 1, 0, 0, 0, 1),
            new(.75f, .125f, 2.5f, -.25f, 1.25f, -1.75f, 0, 0, 1),
            new(1.3f, -.4f, -3.25f, .2f, .9f, 4.125f, 0, 0, 1)
        };
        float[][] patterns =
        {
            new float[]
            {
                3,
                2
            },
            new float[]
            {
                2,
                4,
                1
            },
            new float[]
            {
                5,
                1,
                2,
                3
            }
        };
        float[] begins =
        {
            0,
            .15f,
            .75f
        }, ends =
        {
            1,
            .8f,
            1.25f
        }, offsets =
        {
            0,
            2.25f,
            -3.5f
        };
        StrokeCap[] caps =
        {
            StrokeCap.Butt,
            StrokeCap.Round,
            StrokeCap.Square
        };
        StrokeJoin[] joins =
        {
            StrokeJoin.Miter,
            StrokeJoin.Round,
            StrokeJoin.Bevel
        };
        Assert.Equal(Result.Success, Initializer.Init(0));
        try
        {
            void Emit(Action<Action<uint>, Action<float>, Action<RenderPath>> fn)
            {
                using var memory = new MemoryStream();
                using var row = new BinaryWriter(memory);
                void Word(uint v) => row.Write(v);
                void Number(float v) => Word(BitConverter.SingleToUInt32Bits(v));
                void Path(RenderPath p)
                {
                    Word(p.cmds.count);
                    for (uint i = 0; i < p.cmds.count; ++i)
                        Word((uint)p.cmds[i]);
                    Word(p.pts.count);
                    for (uint i = 0; i < p.pts.count; ++i)
                    {
                        Number(p.pts[i].x);
                        Number(p.pts[i].y);
                    }
                }

                fn(Word, Number, Path);
                row.Flush();
                var actual = memory.ToArray();
                uint expectedLength = expected.ReadUInt32();
                Assert.Equal(expectedLength, (uint)actual.Length);
                var native = expected.ReadBytes((int)expectedLength);
                Assert.True(actual.AsSpan().SequenceEqual(native), $"Native sharedGPU record {record} ({architecture}) must match every defined path/transform/query byte.");
                ++record;
            }

            for (uint n = 0; n < 132; ++n)
            {
                Emit((Word, Number, Path) =>
                {
                    if (n < 96)
                    {
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
                        if (pattern != 0)
                            shape.StrokeDash(patterns[pattern - 1], (uint)patterns[pattern - 1].Length, offsets[(n / 11) % 3]);
                        shape.Trimpath(begins[(n / 5) % 3], ends[(n / 5) % 3], true);
                        var rshape = shape.rs;
                        var matrix = matrices[(n / 9) % 3];
                        var transformed = new RenderPath();
                        var local = new RenderPath();
                        var dashed = new RenderPath();
                        try
                        {
                            GpuCommon.GpuOptimize(rshape.path, transformed, local, matrix, out bool thin, out bool skipFill);
                            Word(thin ? 1u : 0u);
                            Word(skipFill ? 1u : 0u);
                            Path(transformed);
                            Path(local);
                            Word(GpuCommon.GpuStrokeDash(rshape, dashed, n % 2 != 0 ? matrix : null) ? 1u : 0u);
                            Path(dashed);
                            var probe = GpuConvexProbe.Create();
                            Point first = default, previous = default;
                            var p = rshape.path.pts.data;
                            for (uint i = 0; i < rshape.path.cmds.count; ++i)
                            {
                                var c = rshape.path.cmds[i];
                                if (c == PathCommand.MoveTo)
                                {
                                    probe.NextContour();
                                    first = previous = *p++;
                                }
                                else if (c == PathCommand.LineTo)
                                {
                                    probe.AddEdge(TvgMath.PointSub(*p, previous));
                                    previous = *p++;
                                }
                                else if (c == PathCommand.CubicTo)
                                {
                                    if (GpuCommon.GpuEdgesCross(previous, p[0], p[1], p[2]))
                                        probe.convex = false;
                                    probe.AddEdge(TvgMath.PointSub(p[2], previous));
                                    previous = p[2];
                                    p += 3;
                                }
                                else if (c == PathCommand.Close)
                                {
                                    probe.AddContourClose(TvgMath.PointSub(first, previous));
                                    previous = first;
                                }

                                Word(probe.convex ? 1u : 0u);
                                Word((uint)probe.winding);
                                Word(probe.xDirChanges);
                                Word(probe.yDirChanges);
                                Word(probe.contourHasEdges ? 1u : 0u);
                            }
                        }
                        finally
                        {
                            transformed.cmds.Dispose();
                            transformed.pts.Dispose();
                            local.cmds.Dispose();
                            local.pts.Dispose();
                            dashed.cmds.Dispose();
                            dashed.pts.Dispose();
                            Paint.Rel(shape);
                        }
                    }
                    else
                    {
                        var shape = Shape.Gen();
                        switch ((n - 96) % 6)
                        {
                            case 0:
                                shape.MoveTo(4, 6);
                                shape.LineTo(48, 6);
                                shape.LineTo(48, 32);
                                shape.LineTo(4, 32);
                                shape.LineTo(4, 6);
                                shape.Close();
                                break;
                            case 1:
                                shape.MoveTo(4, 6);
                                shape.LineTo(4, 6);
                                shape.LineTo(48, 6);
                                shape.LineTo(26, 32);
                                shape.Close();
                                break;
                            case 2:
                                shape.MoveTo(4, 6);
                                shape.CubicTo(4, 6, 4, 6, 4, 6);
                                shape.LineTo(48, 6);
                                shape.CubicTo(48, 32, 4, 32, 4, 6);
                                shape.Close();
                                break;
                            case 3:
                                shape.MoveTo(4, 6);
                                shape.CubicTo(16, 6.125f, 32, 6.125f, 48, 6);
                                shape.LineTo(4, 6);
                                shape.Close();
                                break;
                            case 4:
                                shape.AppendCircle(26, 24, 22, 18);
                                break;
                            case 5:
                                shape.MoveTo(4, 6);
                                shape.LineTo(20, 6);
                                shape.LineTo(36, 6);
                                shape.LineTo(4, 6);
                                shape.Close();
                                shape.MoveTo(8, 18);
                                shape.LineTo(32, 18);
                                shape.LineTo(8, 18);
                                shape.Close();
                                break;
                        }

                        shape.StrokeWidth(3);
                        shape.StrokeCap(caps[(n / 6) % 3]);
                        uint pattern = (n / 6) % 3;
                        shape.StrokeDash(patterns[pattern], (uint)patterns[pattern].Length, offsets[(n / 6) % 3]);
                        var rshape = shape.rs;
                        var matrix = matrices[(n / 9) % 3];
                        var transformed = new RenderPath();
                        var local = new RenderPath();
                        var dashed = new RenderPath();
                        try
                        {
                            GpuCommon.GpuOptimize(rshape.path, transformed, local, matrix, out bool thin, out bool skipFill);
                            Word(thin ? 1u : 0u);
                            Word(skipFill ? 1u : 0u);
                            Path(transformed);
                            Path(local);
                            Word(GpuCommon.GpuStrokeDash(rshape, dashed, n % 2 != 0 ? matrix : null) ? 1u : 0u);
                            Path(dashed);
                            var probe = GpuConvexProbe.Create();
                            Point first = default, previous = default;
                            var p = rshape.path.pts.data;
                            for (uint i = 0; i < rshape.path.cmds.count; ++i)
                            {
                                var c = rshape.path.cmds[i];
                                if (c == PathCommand.MoveTo)
                                {
                                    probe.NextContour();
                                    first = previous = *p++;
                                }
                                else if (c == PathCommand.LineTo)
                                {
                                    probe.AddEdge(TvgMath.PointSub(*p, previous));
                                    previous = *p++;
                                }
                                else if (c == PathCommand.CubicTo)
                                {
                                    if (GpuCommon.GpuEdgesCross(previous, p[0], p[1], p[2]))
                                        probe.convex = false;
                                    probe.AddEdge(TvgMath.PointSub(p[2], previous));
                                    previous = p[2];
                                    p += 3;
                                }
                                else if (c == PathCommand.Close)
                                {
                                    probe.AddContourClose(TvgMath.PointSub(first, previous));
                                    previous = first;
                                }

                                Word(probe.convex ? 1u : 0u);
                                Word((uint)probe.winding);
                                Word(probe.xDirChanges);
                                Word(probe.yDirChanges);
                                Word(probe.contourHasEdges ? 1u : 0u);
                            }
                        }
                        finally
                        {
                            transformed.cmds.Dispose();
                            transformed.pts.Dispose();
                            local.cmds.Dispose();
                            local.pts.Dispose();
                            dashed.cmds.Dispose();
                            dashed.pts.Dispose();
                            Paint.Rel(shape);
                        }
                    }
                });
            }

            for (uint n = 0; n < 27; ++n)
                Emit((Word, Number, Path) =>
                {
                    Point[] centers =
                    {
                        new(0, 0),
                        new(17.25f, -9.5f),
                        new(-.125f, 41.75f)
                    };
                    float[] angles =
                    {
                        0,
                        37,
                        90,
                        143,
                        179,
                        270,
                        315,
                        -45,
                        360
                    };
                    var m = GpuCommon.GpuConicTransform(centers[n / 9], angles[n % 9]);
                    foreach (var f in new[]
                    {
                        m.e11,
                        m.e12,
                        m.e13,
                        m.e21,
                        m.e22,
                        m.e23,
                        m.e31,
                        m.e32,
                        m.e33
                    }

                    )
                        Number(f);
                    var r = GpuCommon.GpuTransformBounds(new(-3, 2, 47, 36), matrices[n % 3]);
                    Word((uint)r.min.x);
                    Word((uint)r.min.y);
                    Word((uint)r.max.x);
                    Word((uint)r.max.y);
                    foreach (var radius in new[]
                    {
                        0f,
                        .125f,
                        1f,
                        3.5f,
                        16f,
                        128f
                    }

                    )
                        Word(GpuCommon.GpuArcSegmentsCnt(TvgMath.Deg2Rad(angles[n % 9]), radius));
                });
            Emit((Word, Number, Path) =>
            {
                Point* vertices = stackalloc Point[]
                {
                    new(4, 6),
                    new(48, 6),
                    new(48, 32),
                    new(4, 32),
                    new(4, 6),
                    new(48, 32)
                };
                uint* indices = stackalloc uint[]
                {
                    0,
                    1,
                    2,
                    0,
                    2,
                    3
                };
                Point* quad = stackalloc Point[]
                {
                    vertices[0],
                    vertices[1],
                    vertices[2],
                    vertices[0],
                    vertices[2],
                    vertices[3]
                };
                for (int y = 0; y < 9; ++y)
                    for (int x = 0; x < 9; ++x)
                    {
                        var p = new Point(x * 7, y * 5);
                        Word(GpuCommon.GpuPointInTriangle(p, vertices[0], vertices[1], vertices[2]) ? 1u : 0u);
                        Word(GpuCommon.GpuPointInAnyMesh(p, vertices, indices, 6) ? 1u : 0u);
                        Word(GpuCommon.GpuPointInFillMesh(p, vertices, indices, 6, FillRule.NonZero) ? 1u : 0u);
                        Word(GpuCommon.GpuPointInFillMesh(p, vertices, indices, 6, FillRule.EvenOdd) ? 1u : 0u);
                        Word(GpuCommon.GpuPointInQuad(p, new ReadOnlySpan<Point>(quad, 6)) ? 1u : 0u);
                    }
            });
            var source = new RenderPath();
            var result = new RenderPath();
            var localOut = new RenderPath();
            source.MoveTo(new(4, 6));
            source.LineTo(new(48, 6));
            source.LineTo(new(48, 32));
            source.LineTo(new(4, 32));
            source.Close();
            for (int i = 0; i < 1000; ++i)
                GpuCommon.GpuOptimize(source, result, localOut, matrices[1], out _, out _);
            long before = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            for (int i = 0; i < 10000; ++i)
                GpuCommon.GpuOptimize(source, result, localOut, matrices[1], out _, out _);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            double elapsed = Stopwatch.GetElapsedTime(start).TotalSeconds;
            source.cmds.Dispose();
            source.pts.Dispose();
            result.cmds.Dispose();
            result.pts.Dispose();
            localOut.cmds.Dispose();
            localOut.pts.Dispose();
            Assert.Equal(0, allocated);
            Assert.Equal(160, record);
            Assert.Equal(expected.BaseStream.Length, expected.BaseStream.Position);
        }
        finally
        {
            Assert.Equal(Result.Success, Initializer.Term());
        }
    }
}
