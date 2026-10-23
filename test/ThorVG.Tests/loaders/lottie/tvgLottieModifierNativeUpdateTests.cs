using System;
using System.IO;
using ThorVG;
using System.Runtime.InteropServices;
using Xunit;

namespace ThorVG.Tests;
unsafe class LottieNativePathViewWitness : LottieModifier
{
    public PathCommand* commands;
    public Point* points;
    public bool sameCommands, samePoints;
    public LottieNativePathViewWitness(PathCommand* c, Point* p) : base(ModifierType.Roundness)
    {
        commands = c;
        points = p;
    }

    public override void Path(RenderPath input, RenderPath output, Matrix* m)
    {
        sameCommands = input.cmds.data == commands;
        samePoints = input.pts.data == points;
        output.cmds.Push(input.cmds);
        for (uint i = 0; i < input.pts.count; ++i)
            output.pts.Push(m != null ? TvgMath.Transform(input.pts[i], *m) : input.pts[i]);
    }
}

public unsafe class tvgLottieModifierNativeUpdateTests
{
    static void ShapeInput(RenderPath p, int kind)
    {
        void Polygon(bool reversed)
        {
            Point[] v =
            {
                new(13.7f, 7.3f),
                new(61.3f, 11.7f),
                new(67.3f, 57.7f),
                new(17.3f, 63.7f),
                new(13.7f, 7.3f)
            };
            p.MoveTo(v[0]);
            for (int j = 1; j < 5; ++j)
            {
                int a = reversed ? (5 - j) % 4 : j - 1;
                int b = reversed ? (4 - j) % 4 : j;
                p.CubicTo(v[a], v[b], v[b]);
            }

            p.Close();
        }

        switch (kind)
        {
            case 0:
                Polygon(false);
                break;
            case 1:
                p.MoveTo(new(13.7f, 7.3f));
                p.CubicTo(new(17.3f, -13.7f), new(61.3f, 11.7f), new(67.3f, 37.7f));
                p.CubicTo(new(43.7f, 73.7f), new(13.7f, 63.7f), new(13.7f, 7.3f));
                p.Close();
                break;
            case 2:
                p.MoveTo(new(13.7f, 7.3f));
                p.LineTo(new(61.3f, 11.7f));
                p.CubicTo(new(73.7f, 27.3f), new(61.3f, 53.7f), new(43.7f, 61.3f));
                p.LineTo(new(17.3f, 63.7f));
                p.Close();
                break;
            case 3:
                Polygon(false);
                p.MoveTo(new(83.7f, 11.3f));
                p.CubicTo(new(91.3f, 23.7f), new(101.7f, 43.7f), new(117.3f, 57.7f));
                break;
            case 4:
                p.MoveTo(new(13.7f, 7.3f));
                p.LineTo(new(43.7f, 27.3f));
                p.LineTo(new(73.7f, 17.3f));
                break;
            case 5:
                p.AddCircle(43.7f, 31.3f, 23.7f, 17.3f, true);
                break;
            case 6:
                p.AddRect(13.7f, 7.3f, 61.3f, 43.7f, 13.7f, 9.3f, false);
                break;
            case 7:
                Polygon(true);
                break;
        }
    }

    [Fact]
    public void NormalModifierGeometryBorrowedViewsAndPolystarsMatchPinnedNative()
    {
        string arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        string folder = Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update");
        using var expected = new BinaryReader(File.OpenRead(Path.Combine(folder, "lottie-modifier-" + arch + "-native-golden.bin")));
        var starPaths = new RenderPath[12];
        using (var stars = new BinaryReader(File.OpenRead(Path.Combine(folder, "lottie-modifier-" + arch + "-native-star-inputs.bin"))))
        {
            for (int k = 0; k < 12; ++k)
            {
                var p = starPaths[k] = new RenderPath();
                uint n = stars.ReadUInt32();
                for (uint j = 0; j < n; ++j)
                    p.cmds.Push((PathCommand)stars.ReadUInt32());
                n = stars.ReadUInt32();
                for (uint j = 0; j < n; ++j)
                    p.pts.Push(new Point(stars.ReadSingle(), stars.ReadSingle()));
            }
        }

        int count = 0;
        foreach (var line in File.ReadLines(Path.Combine(folder, "lottie-modifier-normal-inputs.txt")))
        {
            var tab = line.IndexOf('\t');
            var kind = line[..tab];
            int i = int.Parse(line[(tab + 1)..]);
            using var memory = new MemoryStream();
            using var row = new BinaryWriter(memory);
            void Word(uint v) => row.Write(v);
            void Number(float v) => Word(BitConverter.SingleToUInt32Bits(v));
            void PathWords(RenderPath p)
            {
                Word(p.cmds.count);
                for (uint j = 0; j < p.cmds.count; ++j)
                    Word((uint)p.cmds[j]);
                Word(p.pts.count);
                for (uint j = 0; j < p.pts.count; ++j)
                {
                    Number(p.pts[j].x);
                    Number(p.pts[j].y);
                }
            }

            var input = new RenderPath();
            var path = new RenderPath();
            if (kind == "polystar")
            {
                input.cmds.Push(starPaths[i % 12].cmds);
                input.pts.Push(starPaths[i % 12].pts);
            }
            else
                ShapeInput(input, i % 8);
            PathWords(input);
            if ((i / 32) % 2 != 0)
            {
                path.MoveTo(new(3.7f, -4.3f));
                path.LineTo(new(7.3f, -2.7f));
            }

            var m = new Matrix(.87f, -.13f, 7.3f, .27f, 1.13f, -3.7f, 0, 0, 1);
            LottieModifier mod;
            if (kind == "round")
                mod = new LottieRoundnessModifier(1.3f + (i / 16) * 2.0f);
            else if (kind == "offset")
                mod = new LottieOffsetModifier(i / 16 % 2 != 0 ? 3.7f : -2.3f, 4.0f, (StrokeJoin)(i / 16 % 3));
            else if (kind == "pucker")
                mod = new LottiePuckerBloatModifier(-17.3f + (i / 16) * 8.0f);
            else if (kind == "zigzag")
                mod = new LottieZigZagModifier(3.7f + (i / 64) * .5f, i / 16 % 3, (LottieZigZagModifier.PointType)(1 + i / 48 % 2));
            else if (kind == "chain")
            {
                mod = new LottieRoundnessModifier(3.7f);
                if (i / 16 % 2 != 0)
                {
                    mod.next = new LottieOffsetModifier(2.3f);
                    mod.next.next = new LottiePuckerBloatModifier(17.3f);
                }
                else
                {
                    mod.next = new LottiePuckerBloatModifier(17.3f);
                    mod.next.next = new LottieZigZagModifier(3.7f, 2, LottieZigZagModifier.PointType.Smooth);
                }
            }
            else
                mod = new LottieRoundnessModifier(3.7f);
            if (kind == "polystar")
            {
                int k = i % 12;
                if (i / 24 % 2 != 0)
                {
                    mod.next = new LottiePuckerBloatModifier(17.3f);
                    mod.next.next = new LottieZigZagModifier(3.7f, 2, LottieZigZagModifier.PointType.Smooth);
                }

                mod.Polystar(input, path, k / 2 % 3 == 2 ? .237f : 0f, k / 2 % 3 != 0);
            }
            else if (kind == "propertyView")
            {
                var prop = new LottiePathSet();
                PathSet Copy() => new()
                {
                    cmds = input.cmds.ToArray(),
                    cmdsCnt = (ushort)input.cmds.count,
                    pts = input.pts.ToArray(),
                    ptsCnt = (ushort)input.pts.count
                };
                if (i % 2 != 0)
                {
                    for (int k = 0; k < 3; ++k)
                    {
                        var f = prop.NewFrame();
                        f.no = k * 12.3f;
                        f.value = Copy();
                        for (int j = 0; j < f.value.ptsCnt; ++j)
                            f.value.pts[j].x += k * 4.0f;
                    }
                }
                else
                    prop.value = Copy();
                var b = i % 2 != 0 ? prop.frames[0].value : prop.value;
                fixed (PathCommand* c = b.cmds)
                fixed (Point* p = b.pts)
                {
                    var witness = new LottieNativePathViewWitness(c, p);
                    prop.Evaluate(i % 2 != 0 ? 6.3f : 0f, path, i / 2 != 0 ? &m : null, null, witness);
                    Word(witness.sameCommands ? 1u : 0u);
                    Word(witness.samePoints ? 1u : 0u);
                }
            }
            else if (kind == "rect")
                mod.Rect(input, path, new(13.7f, 7.3f), new(61.3f, 43.7f), i / 16 % 2 != 0 ? 9.3f : 0f, i % 2 != 0);
            else if (kind == "ellipse")
                mod.Ellipse(input, path, new(43.7f, 31.3f), new(23.7f, 17.3f), i % 2 != 0);
            else
                mod.Path(input, path, i / 8 % 2 != 0 ? &m : null);
            PathWords(path);
            input.cmds.Dispose();
            input.pts.Dispose();
            path.cmds.Dispose();
            path.pts.Dispose();
            row.Flush();
            var actual = memory.ToArray();
            uint length = expected.ReadUInt32();
            Assert.Equal(length, (uint)actual.Length);
            var native = expected.ReadBytes((int)length);
            Assert.True(actual.AsSpan().SequenceEqual(native), $"Original native modifier {kind} record {i} ({arch}) must match every defined byte");
            ++count;
        }

        Assert.Equal(820, count);
        Assert.Equal(expected.BaseStream.Length, expected.BaseStream.Position);
        foreach (var p in starPaths)
        {
            p.cmds.Dispose();
            p.pts.Dispose();
        }
    }
}
