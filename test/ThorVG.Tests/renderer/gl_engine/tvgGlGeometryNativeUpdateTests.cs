using System;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;
using ThorVG;

namespace ThorVG.Tests;
public unsafe class tvgGlGeometryNativeUpdateTests
{
    [Fact]
    public void OrdinaryTessellationAndHitsMatchPinnedNative()
    {
        string architecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        using var expected = new BinaryReader(File.OpenRead(System.IO.Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "gl-geometry-" + architecture + "-native-golden.bin")));
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
        float* matrix = stackalloc float[12];
        Assert.Equal(Result.Success, Initializer.Init(0));
        try
        {
            for (uint n = 0; n < 108; ++n)
            {
                using var memory = new MemoryStream();
                using var row = new BinaryWriter(memory);
                void Word(uint v) => row.Write(v);
                void Number(float v) => Word(BitConverter.SingleToUInt32Bits(v));
                void Region(RenderRegion r)
                {
                    Word((uint)r.min.x);
                    Word((uint)r.min.y);
                    Word((uint)r.max.x);
                    Word((uint)r.max.y);
                }

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

                void Mesh(GlGeometryBuffer m)
                {
                    Word(m.vertex.count);
                    for (uint i = 0; i < m.vertex.count; ++i)
                        Number(m.vertex[i]);
                    Word(m.index.count);
                    for (uint i = 0; i < m.index.count; ++i)
                        Word(m.index[i]);
                }

                void Emit(GlGeometry g)
                {
                    Mesh(g.fill);
                    Mesh(g.stroke);
                    Region(g.fillBounds);
                    Region(g.strokeBounds);
                    Region(g.GetBounds());
                    Path(g.optPath);
                    Path(g.optStrokePath);
                    float* m = stackalloc float[12];
                    GlMatrixHelper.GetMatrix3Std140(g.InverseMatrix(), new Span<float>(m, 12));
                    for (int i = 0; i < 12; ++i)
                        Number(m[i]);
                    Number(g.strokeRenderWidth);
                }

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
                    var drawable = new GlShape
                    {
                        rshape = rshape,
                        opacity = 211
                    };
                    using var g = drawable.geometry;
                    g.SetMatrix(matrices[(n / 9) % 3]);
                    g.viewport = new(0, 0, 128, 96);
                    g.Prepare(rshape);
                    float multiplier = 1;
                    bool filled = g.TesselateShape(rshape, ref multiplier), stroked = g.TesselateStroke(rshape);
                    drawable.validFill = filled;
                    drawable.validStroke = stroked;
                    Word(filled ? 1u : 0u);
                    Word(stroked ? 1u : 0u);
                    Number(multiplier);
                    Word(g.fillWorld ? 1u : 0u);
                    Word(g.optPathThin ? 1u : 0u);
                    Word(g.optPathSkipFill ? 1u : 0u);
                    Word(g.convex ? 1u : 0u);
                    Word(stroked && g.strokeDirect ? 1u : 0u);
                    Word((uint)g.fillRule);
                    Emit(g);
                    foreach (var flag in new[]
                    {
                        RenderUpdateFlag.Color,
                        RenderUpdateFlag.Stroke,
                        RenderUpdateFlag.GradientStroke
                    }

                    )
                    {
                        Word(g.Drawable(flag) ? 1u : 0u);
                        Word((uint)g.GetStencilMode(flag));
                    }

                    var intersector = new GlIntersector();
                    for (int y = 0; y < 4; ++y)
                        for (int x = 0; x < 4; ++x)
                            Word(intersector.IntersectShape(new(x * 16, y * 16, x * 16 + 4, y * 16 + 4), drawable) ? 1u : 0u);
                    Paint.Rel(shape);
                }
                else
                {
                    uint[] dims =
                    {
                        1,
                        16,
                        37
                    };
                    uint w = dims[(n - 96) % 3];
                    var pixels = new uint[w * 24];
                    System.Array.Fill(pixels, 0xff358bcdu);
                    using var image = new RenderSurface();
                    image.Setup(pixels, w, w, 24, 4, ColorSpace.ABGR8888);
                    var drawable = new GlShape
                    {
                        opacity = 255
                    };
                    using var g = drawable.geometry;
                    g.SetMatrix(matrices[(n - 96) / 4]);
                    g.viewport = new(0, 0, 128, 96);
                    g.TesselateImage(image);
                    Mesh(g.fill);
                    Region(g.fillBounds);
                    Region(g.GetBounds());
                    GlMatrixHelper.GetMatrix3Std140(g.InverseMatrix(), new Span<float>(matrix, 12));
                    for (int i = 0; i < 12; ++i)
                        Number(matrix[i]);
                    Word(g.Drawable(RenderUpdateFlag.Image) ? 1u : 0u);
                    Word((uint)g.GetStencilMode(RenderUpdateFlag.Image));
                    var intersector = new GlIntersector();
                    for (int y = 0; y < 4; ++y)
                        for (int x = 0; x < 4; ++x)
                            Word(intersector.IntersectImage(new(x * 16, y * 16, x * 16 + 4, y * 16 + 4), drawable) ? 1u : 0u);
                }

                row.Flush();
                var actual = memory.ToArray();
                uint expectedLength = expected.ReadUInt32();
                Assert.Equal(expectedLength, (uint)actual.Length);
                var native = expected.ReadBytes((int)expectedLength);
                Assert.True(actual.AsSpan().SequenceEqual(native), $"Native geometry case {n} ({architecture}) must match every mesh/path/bounds/mode/hit byte.");
            }
        }
        finally
        {
            Assert.Equal(Result.Success, Initializer.Term());
        }

        Assert.Equal(expected.BaseStream.Length, expected.BaseStream.Position);
    }
}
