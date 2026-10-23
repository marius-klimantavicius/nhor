using System;
using System.IO;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using ThorVG;
using System.Runtime.InteropServices;
using Xunit;

namespace ThorVG.Tests;
public unsafe class tvgSvgBuilderNativeUpdateTests
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicMethods, typeof(SvgBuilder))]
    static object Call(string name, params object[] args) => typeof(SvgBuilder).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args)!;
    [Fact]
    public void OrdinaryBuilderRecordsMatchNative()
    {
        string architecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        string folder = System.IO.Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update");
        using var expected = new BinaryReader(File.OpenRead(System.IO.Path.Combine(folder, "svg-builder-" + architecture + "-native-golden.bin")));
        int count = 0;
        foreach (var line in File.ReadLines(System.IO.Path.Combine(folder, "svg-builder-normal-inputs.txt")))
        {
            var tab = line.IndexOf('\t');
            var kind = line[..tab];
            int i = int.Parse(line[(tab + 1)..]);
            using var memory = new MemoryStream();
            using var row = new BinaryWriter(memory);
            void Word(uint v) => row.Write(v);
            void Number(float v) => Word(BitConverter.SingleToUInt32Bits(v));
            void MatrixWords(Matrix m)
            {
                foreach (var v in new[]
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
                    Number(v);
            }

            var bounds = new Box(-11.3f + i * .5f, 3.7f - i * .25f, 123.7f + i * 1.25f, 81.3f + i * .75f);
            var ratio = new Box(.13f, .27f, .83f, 1.13f);
            var transform = new Matrix(.87f, -.13f, 7.3f, .27f, 1.13f, -3.7f, 0, 0, 1);
            if (kind == "bbox")
            {
                var b = (Box)Call("_objectBoundingBox", ratio, bounds);
                foreach (var v in new[]
                {
                    b.x,
                    b.y,
                    b.w,
                    b.h
                }

                )
                    Number(v);
            }
            else if (kind == "transform")
            {
                var bbox = new Matrix(bounds.w, 0, bounds.x, 0, bounds.h, bounds.y, 0, 0, 1);
                object[] p =
                {
                    bbox,
                    transform
                };
                Call("_transformMultiply", p);
                MatrixWords((Matrix)p[1]);
            }
            else if (kind == "aspect")
            {
                MatrixWords((Matrix)Call("_calculateAspectRatioMatrix", (AspectRatioAlign)(i % 10), (AspectRatioMeetOrSlice)((i / 10) % 2), 187.3f + (i / 20) * 13.7f, 143.7f - (i / 20) * 8.3f, new Box(13.7f, -8.3f, 123.7f, 81.3f)));
            }
            else if (kind == "grid")
            {
                var cell = new Box(5.3f, 7.1f, 13.7f, 9.3f);
                object[] p =
                {
                    cell,
                    bounds,
                    i % 2 != 0 ? transform : null !,
                    0f,
                    0f,
                    0,
                    0
                };
                Call("_patternTileGrid", p);
                Number((float)p[3]);
                Number((float)p[4]);
                Word((uint)(int)p[5]);
                Word((uint)(int)p[6]);
            }
            else if (kind == "rectangle" || kind == "ellipse")
            {
                var shape = Shape.Gen();
                try
                {
                    if (kind == "rectangle")
                        Call("_appendRect", shape, bounds.x, bounds.y, bounds.w, bounds.h, i == 0 ? 0f : 13.7f + i, i == 0 ? 0f : 9.3f + i);
                    else
                        Call("_appendCircle", shape, bounds.x, bounds.y, i == 4 ? -1f : 13.7f + i, i == 5 ? -1f : 9.3f + i);
                    var path = shape.rs.path;
                    Word(path.cmds.count);
                    for (uint n = 0; n < path.cmds.count; ++n)
                        Word((uint)path.cmds[n]);
                    Word(path.pts.count);
                    for (uint n = 0; n < path.pts.count; ++n)
                    {
                        Number(path.pts[n].x);
                        Number(path.pts[n].y);
                    }
                }
                finally
                {
                    Paint.Rel(shape);
                }
            }
            else
            {
                bool radial = i % 2 != 0, user = (i / 2) % 2 != 0, trans = (i / 4) % 2 != 0;
                int stops = i / 8;
                var g = new SvgStyleGradient
                {
                    type = radial ? SvgGradientType.Radial : SvgGradientType.Linear,
                    userSpace = user,
                    spread = FillSpread.Reflect
                };
                if (radial)
                    g.radial = new()
                    {
                        cx = .473f,
                        cy = .317f,
                        fx = .381f,
                        fy = .279f,
                        r = .293f,
                        fr = .037f
                    };
                else
                    g.linear = new()
                    {
                        x1 = .127f,
                        y1 = .083f,
                        x2 = .913f,
                        y2 = .647f
                    };
                if (trans)
                    g.transform = transform;
                if (stops != 0)
                {
                    g.stops.Add(new() { offset = 0, r = 21, g = 47, b = 93, a = 255 });
                    if (stops == 2)
                    {
                        g.stops.Add(new() { offset = .37f, r = 117, g = 191, b = 43, a = 128 });
                        g.stops.Add(new() { offset = 1, r = 231, g = 73, b = 119, a = 219 });
                    }
                }

                var fill = (Fill)Call(radial ? "_applyRadialGradientProperty" : "_applyLinearGradientProperty", g, bounds, new Box(0, 0, 157.3f, 113.7f), 173);
                try
                {
                    if (radial)
                    {
                        Word((uint)((RadialGradient)fill).Radial(out var cx, out var cy, out var r, out var fx, out var fy, out var fr));
                        foreach (var v in new[]
                        {
                            cx,
                            cy,
                            r,
                            fx,
                            fy,
                            fr
                        }

                        )
                            Number(v);
                    }
                    else
                    {
                        Word((uint)((LinearGradient)fill).Linear(out var x1, out var y1, out var x2, out var y2));
                        foreach (var v in new[]
                        {
                            x1,
                            y1,
                            x2,
                            y2
                        }

                        )
                            Number(v);
                    }

                    MatrixWords(fill.GetTransform());
                    Word((uint)fill.GetSpread());
                    Word(fill.GetColorStops(out var cs));
                    if (cs != null)
                        foreach (var v in cs)
                        {
                            Number(v.offset);
                            Word(v.r);
                            Word(v.g);
                            Word(v.b);
                            Word(v.a);
                        }
                }
                finally
                {
                    GC.KeepAlive(fill);
                }
            }

            row.Flush();
            var actual = memory.ToArray();
            uint length = expected.ReadUInt32();
            Assert.Equal(length, (uint)actual.Length);
            var native = expected.ReadBytes((int)length);
            Assert.True(actual.AsSpan().SequenceEqual(native), $"Native ordinary SVG builder {kind} record{count} ({architecture}) must match complete defined bytes: {i}");
            ++count;
        }

        Assert.Equal(126, count);
        Assert.Equal(expected.BaseStream.Length, expected.BaseStream.Position);
    }
}
