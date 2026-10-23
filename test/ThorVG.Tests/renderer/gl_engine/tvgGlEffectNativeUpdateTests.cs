using System;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;
using ThorVG;

namespace ThorVG.Tests;
public unsafe class tvgGlEffectNativeUpdateTests
{
    [Fact]
    public void OrdinaryEffectPreparationMatchesPinnedNative()
    {
        string architecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        using var expected = new BinaryReader(File.OpenRead(System.IO.Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update", "gl-effect-" + architecture + "-native-golden.bin")));
        Matrix[] matrices =
        {
            new(1, 0, 0, 0, 1, 0, 0, 0, 1),
            new(.75f, .125f, 2.5f, -.25f, 1.25f, -1.75f, 0, 0, 1),
            new(1.3f, -.4f, -3.25f, .2f, .9f, 4.125f, 0, 0, 1)
        };
        int[] qualities =
        {
            0,
            1,
            33,
            34,
            50,
            66,
            67,
            99,
            100
        };
        float[] sigmas =
        {
            0,
            .125f,
            .75f,
            1,
            1.25f,
            2.25f,
            4.5f,
            8,
            12.5f
        }, angles =
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
        using var engine = new GlEffect(null !);
        for (int kind = 0; kind < 6; ++kind)
            for (int m = 0; m < 3; ++m)
                for (int j = 0; j < 9; ++j)
                {
                    RenderEffect effect = kind switch
                    {
                        0 => RenderEffectGaussianBlur.Gen(sigmas[j], j % 3, j % 2, qualities[j]),
                        1 => RenderEffectDropShadow.Gen(31, 113, 229, j == 0 ? 0 : 137, angles[j], j * 3, sigmas[j], qualities[j]),
                        2 => RenderEffectMotionBlur.Gen(j * 3, angles[j], qualities[j]),
                        3 => RenderEffectFill.Gen(31, 113, 229, j * 31),
                        4 => RenderEffectTint.Gen(11, 23, 47, 211, 193, 179, j * 12.5),
                        _ => RenderEffectTritone.Gen(11, 23, 47, 113, 151, 97, 211, 193, 179, j == 8 ? 255 : j * 31)};
                    for (int update = 0; update < 2; ++update)
                    {
                        using var memory = new MemoryStream();
                        using var row = new BinaryWriter(memory);
                        void Word(uint v) => row.Write(v);
                        void Number(float v) => Word(BitConverter.SingleToUInt32Bits(v));
                        engine.Update(effect, matrices[(m + update) % 3]);
                        Word(effect.valid ? 1u : 0u);
                        Word(effect.rd != null ? 1u : 0u);
                        bool region = effect.valid ? engine.Region(effect) : false;
                        Word(region ? 1u : 0u);
                        Word((uint)effect.extend.min.x);
                        Word((uint)effect.extend.min.y);
                        Word((uint)effect.extend.max.x);
                        Word((uint)effect.extend.max.y);
                        if (effect.valid)
                        {
                            if (kind == 0 || kind == 1)
                            {
                                var blur = (GlGaussianBlur)effect.rd!;
                                Number(blur.sigma);
                                Number(blur.scale);
                                Number(blur.extend);
                                Number(blur.quality);
                                if (kind == 1)
                                {
                                    var ds = (GlDropShadow)effect.rd!;
                                    for (int i = 0; i < 4; ++i)
                                        Number(ds.color[i]);
                                    for (int i = 0; i < 2; ++i)
                                        Number(ds.offset[i]);
                                }
                            }
                            else if (kind == 2)
                            {
                                var blur = (GlMotionBlur)effect.rd!;
                                Number(blur.offset.x);
                                Number(blur.offset.y);
                                Number(blur.samples);
                            }
                            else
                            {
                                var p = (GlEffectParams)effect.rd!;
                                for (int i = 0; i < (kind == 3 ? 4 : kind == 4 ? 9 : 12); ++i)
                                    Number(p.@params[i]);
                            }
                        }

                        row.Flush();
                        var actual = memory.ToArray();
                        uint expectedLength = expected.ReadUInt32();
                        Assert.Equal(expectedLength, (uint)actual.Length);
                        var native = expected.ReadBytes((int)expectedLength);
                        Assert.True(actual.AsSpan().SequenceEqual(native), $"Native effect {kind}/{m}/{j}/{update} ({architecture}) must match every defined parameter/region byte.");
                    }
                }

        Assert.Equal(expected.BaseStream.Length, expected.BaseStream.Position);
    }
}
