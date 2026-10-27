using System;
using System.IO;
using ThorVG;
using System.Runtime.InteropServices;
using Xunit;

namespace ThorVG.Tests;
public class tvgLottieParserNativeUpdateTests
{
    [Fact]
    public void NormalEffectsSelectorsGlyphsAssetsAndPropertiesMatchPinnedNative()
    {
        string architecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        string folder = Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update");
        using var expected = new BinaryReader(File.OpenRead(Path.Combine(folder, "lottie-parser-" + architecture + "-native-golden.bin")));
        int count = 0;
        Initializer.Init(0);
        try
        {
            foreach (var line in File.ReadLines(Path.Combine(folder, "lottie-parser-normal", "inputs.txt")))
            {
                var tab = line.IndexOf('\t');
                var kind = line[..tab];
                int i = int.Parse(line[(tab + 1)..]);
                string text = File.ReadAllText(Path.Combine(Path.Combine(folder, "lottie-parser-normal"), "case-" + i + ".json"));
                using var memory = new MemoryStream();
                using var row = new BinaryWriter(memory);
                void Word(uint v) => row.Write(v);
                void Number(float v) => Word(BitConverter.SingleToUInt32Bits(v));
                void StringWords(string? s)
                {
                    if (s == null)
                    {
                        Word(0xffffffff);
                        return;
                    }

                    var b = System.Text.Encoding.UTF8.GetBytes(s);
                    Word((uint)b.Length);
                    row.Write(b);
                }

                void PointWords(Point p)
                {
                    Number(p.x);
                    Number(p.y);
                }

                LottieExpressions.SeedRandom((uint)(17 + i));
                var parser = new LottieParser(text, "normal-base", false);
                Word(parser.Parse() ? 1u : 0u);
                var comp = parser.comp;
                Assert.NotNull(comp);
                Assert.NotNull(comp.root);
                if (kind == "effect")
                {
                    var layer = (LottieLayer)comp.root.children[0];
                    Word((uint)layer.effects.Count);
                    var fx = (LottieFxCustom)layer.effects[0];
                    Word((uint)fx.props.Count);
                    foreach (var p in fx.props)
                    {
                        Word((uint)p.nm);
                        Word((uint)p.mn);
                        Number(Assert.IsType<LottieFloat>(p.property).Evaluate(0));
                    }
                }
                else if (kind == "random")
                {
                    var layer = (LottieLayer)comp.root.children[0];
                    var t = (LottieText)layer.children[0];
                    Word((uint)t.ranges.Count);
                    foreach (var r in t.ranges)
                    {
                        Word(r.random);
                        for (int j = 0; j < 16; ++j)
                            Number(r.Factor(0, 16, j));
                    }
                }
                else if (kind == "glyph")
                {
                    Word((uint)comp.fonts.Count);
                    foreach (var font in comp.fonts)
                    {
                        StringWords(font.name);
                        Word((uint)font.chars.Count);
                        foreach (var g in font.chars)
                        {
                            StringWords(g.code);
                            StringWords(g.family);
                            StringWords(g.style);
                            Number(g.width);
                            Word(g.size);
                            Word(g.len);
                        }
                    }
                }
                else if (kind == "assets")
                {
                    Word((uint)comp.assets.Count);
                    foreach (var asset in comp.assets)
                    {
                        Word((uint)asset.type);
                        if (asset is LottieImage image)
                        {
                            StringWords(image.asset.path);
                            Word(image.asset.external ? 1u : 0u);
                            Number(image.asset.width);
                            Number(image.asset.height);
                        }
                        else
                        {
                            var audio = (LottieAudio)asset;
                            StringWords((string?)audio.data);
                            Word(audio.external ? 1u : 0u);
                        }
                    }
                }
                else if (kind == "properties")
                {
                    var layer = (LottieLayer)comp.root.children[0];
                    Word((uint)layer.children.Count);
                    var rect = (LottieRect)layer.children[0];
                    Word(rect.position.FrameCnt());
                    foreach (float f in new[]
                    {
                        0f,
                        6.3f,
                        13.7f,
                        24.6f
                    }

                    )
                        PointWords(rect.position.Evaluate(f));
                    var fill = (LottieSolidFill)layer.children[1];
                    var color = fill.color.Evaluate(0);
                    Word((uint)color.r);
                    Word((uint)color.g);
                    Word((uint)color.b);
                    Word(fill.opacity.Evaluate(0));
                    var transform = (LottieTransform)layer.children[2];
                    PointWords(transform.position.Evaluate(0));
                    PointWords(transform.anchor.Evaluate(0));
                    PointWords(transform.scale.Evaluate(0));
                    Number(transform.rotation.Evaluate(0));
                    Word(transform.opacity.Evaluate(0));
                }

                comp.Release();
                row.Flush();
                var actual = memory.ToArray();
                uint length = expected.ReadUInt32();
                Assert.Equal(length, (uint)actual.Length);
                var native = expected.ReadBytes((int)length);
                Assert.True(actual.AsSpan().SequenceEqual(native), $"Original native normal Lottie {kind} record {i} ({architecture}) must match every defined byte");
                ++count;
            }

            Assert.Equal(28, count);
            Assert.Equal(expected.BaseStream.Length, expected.BaseStream.Position);
        }
        finally
        {
            Initializer.Term();
            LottieExpressions.SeedRandom(1);
        }
    }
}
