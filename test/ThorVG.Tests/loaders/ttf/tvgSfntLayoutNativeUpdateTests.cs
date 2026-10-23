using System;
using System.IO;
using System.Text;
using ThorVG;
using System.Runtime.InteropServices;
using Xunit;

namespace ThorVG.Tests;
public unsafe class tvgSfntLayoutNativeUpdateTests
{
    [Fact]
    public void OrdinaryUnicodeLayoutsMetricsAndCopiesMatchNative()
    {
        string architecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        using var expected = new BinaryReader(File.OpenRead(System.IO.Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "sfnt-layout-" + architecture + "-native-golden.bin")));
        Assert.Equal(Result.Success, Initializer.Init(0));
        try
        {
            string[] fonts =
            {
                System.IO.Path.Combine(AppContext.BaseDirectory, "fixtures", "legacy-upstream", "resources", "PublicSans-Regular.ttf"),
                System.IO.Path.Combine(AppContext.BaseDirectory, "fixtures", "legacy-upstream", "resources", "DMSans.otf")
            };
            string[] strings =
            {
                "AVATAR WA To alphabet",
                "Žmogus Ąžuolas",
                "one\u00a0two three",
                "A\u2009B C\u3000D",
                "Hello\nworld",
                ""
            };
            int count = 0;
            foreach (var font in fonts)
            {
                var data = File.ReadAllBytes(font);
                var loader = new TtfLoader();
                try
                {
                    if (loader.OpenResult(data, (uint)data.Length, new LoaderOps(ThorVG.Type.Text)) != Result.Success)
                        throw new Exception("valid font");
                    for (int t = 0; t < 6; ++t)
                        for (int wrap = 0; wrap < 5; ++wrap)
                            for (int config = 0; config < 3; ++config)
                                for (int api = 0; api < 2; ++api)
                                {
                                    using var memory = new MemoryStream();
                                    using var row = new BinaryWriter(memory);
                                    void Word(uint v) => row.Write(v);
                                    void Number(float v) => Word(BitConverter.SingleToUInt32Bits(v));
                                    var fm = new FontMetrics
                                    {
                                        fontSize = config == 0 ? 12 : config == 1 ? 21.5f : 36,
                                        box = new(config == 0 ? 64 : config == 1 ? 93.75f : 214.5f, 80),
                                        align = new(config * .5f, config * .25f),
                                        spacing = new(config == 0 ? 1 : config == 1 ? 1.13f : 1.25f, config == 1 ? 1.3f : 1),
                                        wrap = (TextWrap)wrap,
                                        hinting = false
                                    };
                                    var path = new RenderPath();
                                    var text = strings[t];
                                    var shape = Shape.Gen();
                                    try
                                    {
                                        bool result = api == 0 ? loader.Get(fm, text, (uint)Encoding.UTF8.GetByteCount(text), path) : loader.Get(fm, text, path);
                                        Word(result ? 1u : 0u);
                                        void Metrics(FontMetrics m)
                                        {
                                            Word(m.lines);
                                            Number(m.scale);
                                            Number(m.size.x);
                                            Number(m.size.y);
                                            Word(m.engine != null ? 1u : 0u);
                                        }

                                        Metrics(fm);
                                        Word(path.cmds.count);
                                        for (uint i = 0; i < path.cmds.count; ++i)
                                            Word((uint)path.cmds[i]);
                                        Word(path.pts.count);
                                        for (uint i = 0; i < path.pts.count; ++i)
                                        {
                                            Number(path.pts[i].x);
                                            Number(path.pts[i].y);
                                        }

                                        void Transform()
                                        {
                                            loader.Transform(shape, fm, .2f);
                                            var m = shape.Transform();
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
                                        }

                                        Transform();
                                        loader.Metrics(fm, out var tm);
                                        foreach (var v in new[]
                                        {
                                            tm.ascent,
                                            tm.descent,
                                            tm.linegap,
                                            tm.advance
                                        }

                                        )
                                            Number(v);
                                        bool glyph = loader.GlyphMetrics(fm, text, out var gm, out int next);
                                        Word(glyph ? 1u : 0u);
                                        if (glyph)
                                        {
                                            foreach (var v in new[]
                                            {
                                                gm.advance,
                                                gm.bearing,
                                                gm.min.x,
                                                gm.min.y,
                                                gm.max.x,
                                                gm.max.y
                                            }

                                            )
                                                Number(v);
                                            Word((uint)Encoding.UTF8.GetByteCount(text.AsSpan(0, next)));
                                        }

                                        Transform();
                                        var copy = new FontMetrics();
                                        loader.Copy(fm, copy);
                                        Metrics(copy);
                                        Word(!ReferenceEquals(copy.engine, fm.engine) ? 1u : 0u);
                                        loader.Release(copy);
                                        Word(copy.engine == null ? 1u : 0u);
                                        loader.Release(fm);
                                        Word(fm.engine == null ? 1u : 0u);
                                    }
                                    finally
                                    {
                                        path.cmds.Dispose();
                                        path.pts.Dispose();
                                        Paint.Rel(shape);
                                        loader.Release(fm);
                                    }

                                    row.Flush();
                                    var actual = memory.ToArray();
                                    uint expectedLength = expected.ReadUInt32();
                                    Assert.Equal(expectedLength, (uint)actual.Length);
                                    var native = expected.ReadBytes((int)expectedLength);
                                    Assert.True(actual.AsSpan().SequenceEqual(native), $"Native normal font layout record {count}:text{t}/wrap{wrap}/configuration{config}/API{api} ({architecture}) must match every defined path/metric/transform/copy-release word.");
                                    ++count;
                                }
                }
                finally
                {
                    loader.ReleaseResources();
                }
            }

            Assert.Equal(360, count);
            Assert.Equal(expected.BaseStream.Length, expected.BaseStream.Position);
        }
        finally
        {
            Assert.Equal(Result.Success, Initializer.Term());
        }
    }
}
