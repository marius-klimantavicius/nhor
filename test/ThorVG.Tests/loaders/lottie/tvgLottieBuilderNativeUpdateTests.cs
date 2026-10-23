using System;
using System.IO;
using ThorVG;
using System.Runtime.InteropServices;
using Xunit;

namespace ThorVG.Tests;
public unsafe class tvgLottieBuilderNativeUpdateTests
{
    [Fact]
    public void NormalBuilderSceneTransformsGeometryStylesAndMasksMatchPinnedNative()
    {
        string arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        string folder = Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update");
        using var expected = new BinaryReader(File.OpenRead(Path.Combine(folder, "lottie-builder-" + arch + "-native-golden.bin")));
        Initializer.Init(0);
        try
        {
            int count = 0;
            foreach (var line in File.ReadLines(Path.Combine(folder, "lottie-builder-normal", "inputs.txt")))
            {
                var tab = line.IndexOf('\t');
                var kind = line[..tab];
                int i = int.Parse(line[(tab + 1)..]);
                string text = File.ReadAllText(Path.Combine(Path.Combine(folder, "lottie-builder-normal"), "case-" + (i / 4) + ".json"));
                using var memory = new MemoryStream();
                using var row = new BinaryWriter(memory);
                void Word(uint v) => row.Write(v);
                void Number(float v) => Word(BitConverter.SingleToUInt32Bits(v));
                void MatrixWords(Matrix m)
                {
                    foreach (float v in new[]
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

                void Gradient(Fill f)
                {
                    Word(f != null ? 1u : 0u);
                    if (f == null)
                        return;
                    Word((uint)f.GetFillType());
                    MatrixWords(f.GetTransform());
                    Word((uint)f.GetSpread());
                    uint n = f.GetColorStops(out var stops);
                    Word(n);
                    for (int j = 0; j < n; ++j)
                    {
                        Number(stops[j].offset);
                        Word(stops[j].r);
                        Word(stops[j].g);
                        Word(stops[j].b);
                        Word(stops[j].a);
                    }

                    if (f is LinearGradient linear)
                    {
                        linear.Linear(out var a, out var b, out var c, out var d);
                        foreach (float v in new[]
                        {
                            a,
                            b,
                            c,
                            d
                        }

                        )
                            Number(v);
                    }
                    else if (f is RadialGradient radial)
                    {
                        radial.Radial(out var a, out var b, out var c, out var d, out var e, out var g);
                        foreach (float v in new[]
                        {
                            a,
                            b,
                            c,
                            d,
                            e,
                            g
                        }

                        )
                            Number(v);
                    }
                }

                void PaintWords(Paint p)
                {
                    Word(p != null ? 1u : 0u);
                    if (p == null)
                        return;
                    Word((uint)p.PaintType());
                    MatrixWords(p.Transform());
                    Word(p.Opacity());
                    Word((uint)p.pImpl.blendMethod);
                    PaintWords(p.GetClipper());
                    Word((uint)p.GetMask(out var mask));
                    PaintWords(mask);
                    if (p is Scene scene)
                    {
                        var paints = scene.Paints();
                        Word((uint)paints.Count);
                        foreach (var q in paints)
                            PaintWords(q);
                    }
                    else if (p is Shape shape)
                    {
                        var path = shape.rs.path;
                        Word(path.cmds.count);
                        for (uint j = 0; j < path.cmds.count; ++j)
                            Word((uint)path.cmds[j]);
                        Word(path.pts.count);
                        for (uint j = 0; j < path.pts.count; ++j)
                        {
                            Number(path.pts[j].x);
                            Number(path.pts[j].y);
                        }

                        Word((uint)shape.GetFillColor(out var r, out var g, out var b, out var a));
                        Word(r);
                        Word(g);
                        Word(b);
                        Word(a);
                        Word((uint)shape.GetFillRule());
                        Gradient(shape.GetFill());
                        Number(shape.GetStrokeWidth());
                        Word((uint)shape.GetStrokeFill(out r, out g, out b, out a));
                        Word(r);
                        Word(g);
                        Word(b);
                        Word(a);
                        Gradient(shape.GetStrokeFillGradient());
                        Word((uint)shape.GetStrokeJoin());
                        Word((uint)shape.GetStrokeCap());
                        Number(shape.GetStrokeMiterlimit());
                    }
                }

                LottieExpressions.SeedRandom((uint)(17 + i));
                var parser = new LottieParser(text, "", false);
                Word(parser.Parse() ? 1u : 0u);
                var comp = parser.comp!;
                using (var builder = new LottieBuilder())
                {
                    builder.Build(comp);
                    Word(builder.Update(comp, (i % 4) * 6.25f + (i % 4 == 2 ? 1f : i % 4 == 3 ? 6f : 0f)) ? 1u : 0u);
                    PaintWords(comp.root.scene);
                }

                comp.Release();
                row.Flush();
                var actual = memory.ToArray();
                uint length = expected.ReadUInt32();
                Assert.Equal(length, (uint)actual.Length);
                var native = expected.ReadBytes((int)length);
                Assert.True(actual.AsSpan().SequenceEqual(native), $"Original native Builder {kind} record {i} ({arch}) must match every defined graph byte");
                ++count;
            }

            Assert.Equal(528, count);
            Assert.Equal(expected.BaseStream.Length, expected.BaseStream.Position);
        }
        finally
        {
            Initializer.Term();
            LottieExpressions.SeedRandom(1);
        }
    }
}
