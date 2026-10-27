using System;
using System.IO;
using ThorVG;
using System.Runtime.InteropServices;
using Xunit;

namespace ThorVG.Tests;
public unsafe class tvgLottieModelNativeUpdateTests
{
    [Fact]
    public void NormalModelPropertyAndAutoOrientTweenRecordsMatchPinnedNative()
    {
        string architecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        string folder = Path.Combine(AppContext.BaseDirectory, "fixtures", "thorvg-update");
        using var expected = new BinaryReader(File.OpenRead(Path.Combine(folder, "lottie-model-" + architecture + "-native-golden.bin")));
        int count = 0;
        foreach (var line in File.ReadLines(Path.Combine(folder, "lottie-model-normal", "inputs.txt")))
        {
            var tab = line.IndexOf('\t');
            var kind = line[..tab];
            int i = int.Parse(line[(tab + 1)..]);
            using var memory = new MemoryStream();
            using var row = new BinaryWriter(memory);
            void Word(uint v) => row.Write(v);
            void Number(float v) => Word(BitConverter.SingleToUInt32Bits(v));
            void PointWords(Point p)
            {
                Number(p.x);
                Number(p.y);
            }

            using var tween = new LottieTween();
            if (kind == "range")
            {
                var r = new LottieTextRange();
                r.shape = (LottieTextRange.RangeShape)(1 + i % 6);
                r.rangeUnit = (i / 6) % 2 != 0 ? LottieTextRange.Unit.Index : LottieTextRange.Unit.Percent;
                r.start.value = r.rangeUnit == LottieTextRange.Unit.Percent ? 13.7f : 1.3f;
                r.end.value = r.rangeUnit == LottieTextRange.Unit.Percent ? 81.3f : 9.7f;
                r.offset.value = (i / 12) % 2 != 0 ? 3.7f : 0f;
                r.maxAmount.value = 83.7f;
                r.smoothness.value = (i / 24) % 3 == 0 ? 0f : ((i / 24) % 3 == 1 ? 17.3f : 100f);
                if ((i / 72) % 2 != 0)
                {
                    r.minEase.value = -23.7f;
                    r.maxEase.value = 31.3f;
                }

                Number(r.Factor(0, 13.7f, (i / 144) * 2.3f));
            }
            else if (kind == "trim")
            {
                var t = new LottieTrimpath();
                t.start.value = (i % 4) * 13.7f;
                t.end.value = 100 - (i % 3) * 17.3f;
                t.offset.value = (i / 4 - 5) * 41.3f;
                t.Segment(0, out var s, out var e, tween, null);
                Number(s);
                Number(e);
                Word(t.Mergeable() ? 1u : 0u);
            }
            else if (kind == "stops")
            {
                var g = new LottieGradientFill();
                var c = new ColorStop
                {
                    input = new ThorVG.Array<float>(0)
                };
                var input = c.input.Value;
                void Push(float v) => input.Push(v);
                for (int k = 0; k < 3; ++k)
                {
                    Push(k * .5f);
                    Push(.1f + k * .3f);
                    Push(.9f - k * .3f);
                    Push(.3f + k * .1f);
                }

                if (i % 4 != 0)
                {
                    for (int k = 0; k <= i % 4; ++k)
                    {
                        Push((float)k / (i % 4));
                        Push(.2f + k * .2f);
                    }
                }

                c.input = input;
                var n = g.Populate(c, 3);
                Word(n);
                Word(g.opaque ? 1u : 0u);
                for (int k = 0; k < n; ++k)
                {
                    Assert.NotNull(c.data);
                    var s = c.data[k];
                    Number(s.offset);
                    Word(s.r);
                    Word(s.g);
                    Word(s.b);
                    Word(s.a);
                }
            }
            else if (kind == "fill")
            {
                var g = new LottieGradientFill();
                g.gradientId = (byte)(i % 2 != 0 ? 1 : 2);
                g.start.value = new(13.7f, -8.3f);
                g.end.value = new(123.7f, 81.3f);
                g.height.value = (i / 2 % 4) * 17.3f;
                g.angle.value = (i / 8 % 3) * 43.7f;
                g.colorStops.value.data = new[]
                {
                    new Fill.ColorStop(0, 31, 73, 119, 219),
                    new Fill.ColorStop(1, 211, 113, 47, 173)
                };
                g.colorStops.count = 2;
                g.colorStops.populated = true;
                var f = g.CreateFill(0, 173, tween, null);
                Assert.NotNull(f);
                if (g.gradientId == 1)
                {
                    Word((uint)((LinearGradient)f).Linear(out var x1, out var y1, out var x2, out var y2));
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
                else
                {
                    Word((uint)((RadialGradient)f).Radial(out var cx, out var cy, out var r, out var fx, out var fy, out var fr));
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

                var n = f.GetColorStops(out var s);
                Word(n);
                for (int j = 0; j < n; ++j)
                {
                    Assert.NotNull(s);
                    Number(s[j].offset);
                    Word(s[j].r);
                    Word(s[j].g);
                    Word(s[j].b);
                    Word(s[j].a);
                }

                GC.KeepAlive(f);
            }
            else if (kind == "path" || kind == "follow")
            {
                var mask = new LottieMask();
                var prop = mask.pathset;
                for (int k = 0; k < 3; ++k)
                {
                    var f = prop.NewFrame();
                    f.no = k * 12.3f;
                    f.value.cmdsCnt = 3;
                    f.value.cmds = new[]
                    {
                        PathCommand.MoveTo,
                        PathCommand.CubicTo,
                        PathCommand.LineTo
                    };
                    f.value.ptsCnt = 5;
                    f.value.pts = new Point[5];
                    for (int j = 0; j < 5; ++j)
                        f.value.pts[j] = new(13.7f + j * 8.0f + k * 4.0f, -8.3f + j * 16.0f - k * 2.0f);
                }

                float frame = (i % 12) * 2.3f;
                var m = new Matrix(.87f, -.13f, 7.3f, .27f, 1.13f, -3.7f, 0, 0, 1);
                if (kind == "path")
                {
                    var path = new RenderPath();
                    prop.Evaluate(frame, path, i % 2 != 0 ? &m : null, tween, null);
                    Word(path.cmds.count);
                    for (uint j = 0; j < path.cmds.count; ++j)
                        Word((uint)path.cmds[j]);
                    Word(path.pts.count);
                    for (uint j = 0; j < path.pts.count; ++j)
                        PointWords(path.pts[j]);
                    path.cmds.Dispose();
                    path.pts.Dispose();
                }
                else
                {
                    var follow = new LottieTextFollowPath();
                    follow.firstMargin.value = 13.7f;
                    Number(follow.Prepare(mask, frame, 1.3f, tween, null));
                    float angle = 0;
                    PointWords(follow.Position((i / 12 - 1) * 130.7f, ref angle));
                    Number(angle);
                    follow.Release();
                }
            }
            else if (kind == "scalar")
            {
                var p = new LottieScalar();
                for (int k = 0; k < 3; ++k)
                {
                    var f = p.NewFrame();
                    f.no = k * 12.3f;
                    f.value = new(13.7f + k * 3.7f, -8.3f - k * 2.3f);
                    f.hold = (i / 12) % 2 != 0;
                }

                float frame = (i % 12) * 2.3f;
                PointWords(p.Evaluate(frame));
                Word(p.Nearest(frame));
                Number(p.FrameNo(i % 5 - 1));
                foreach (var mode in new[]
                {
                    LottieProperty.Loop.InCycle,
                    LottieProperty.Loop.InPingPong,
                    LottieProperty.Loop.OutCycle,
                    LottieProperty.Loop.OutPingPong
                }

                )
                    Number(p.DoLoop(frame, 0, mode, 24.6f));
            }
            else if (kind == "vector")
            {
                var p = new LottieVector();
                for (int k = 0; k < 3; ++k)
                {
                    var f = p.NewFrame();
                    f.no = k * 12.3f;
                    f.value = new(13.7f + k * 17.3f, -8.3f + k * 13.7f);
                    f.hasTangent = (i / 12) % 2 != 0;
                    f.outTangent = new(3.7f, 7.3f);
                    f.inTangent = new(-2.3f, -4.7f);
                }

                p.Prepare();
                float frame = (i % 12) * 2.3f;
                PointWords(p.Evaluate(frame));
                Number(p.GetAngle(frame));
            }
            else if (kind == "staging")
            {
                switch (i)
                {
                    case 0:
                    {
                        var p = new LottieFloat();
                        p.NewFrame().no = 0;
                        Word(p.FrameCnt());
                        p.NextFrame().no = 12.3f;
                        Word(p.FrameCnt());
                        p.NextFrame().no = 24.6f;
                        Word(p.FrameCnt());
                        Number(p.NewFrame().no);
                        Word(p.FrameCnt());
                        break;
                    }

                    case 1:
                    {
                        var p = new LottieInteger();
                        p.NewFrame().no = 0;
                        Word(p.FrameCnt());
                        p.NextFrame().no = 12.3f;
                        Word(p.FrameCnt());
                        p.NextFrame().no = 24.6f;
                        Word(p.FrameCnt());
                        Number(p.NewFrame().no);
                        Word(p.FrameCnt());
                        break;
                    }

                    case 2:
                    {
                        var p = new LottieScalar();
                        p.NewFrame().no = 0;
                        Word(p.FrameCnt());
                        p.NextFrame().no = 12.3f;
                        Word(p.FrameCnt());
                        p.NextFrame().no = 24.6f;
                        Word(p.FrameCnt());
                        Number(p.NewFrame().no);
                        Word(p.FrameCnt());
                        break;
                    }

                    case 3:
                    {
                        var p = new LottieVector();
                        p.NewFrame().no = 0;
                        Word(p.FrameCnt());
                        p.NextFrame().no = 12.3f;
                        Word(p.FrameCnt());
                        p.NextFrame().no = 24.6f;
                        Word(p.FrameCnt());
                        Number(p.NewFrame().no);
                        Word(p.FrameCnt());
                        break;
                    }

                    case 4:
                    {
                        var p = new LottieScalar3();
                        p.NewFrame().no = 0;
                        Word(p.FrameCnt());
                        p.NextFrame().no = 12.3f;
                        Word(p.FrameCnt());
                        p.NextFrame().no = 24.6f;
                        Word(p.FrameCnt());
                        Number(p.NewFrame().no);
                        Word(p.FrameCnt());
                        break;
                    }

                    case 5:
                    {
                        var p = new LottieColor();
                        p.NewFrame().no = 0;
                        Word(p.FrameCnt());
                        p.NextFrame().no = 12.3f;
                        Word(p.FrameCnt());
                        p.NextFrame().no = 24.6f;
                        Word(p.FrameCnt());
                        Number(p.NewFrame().no);
                        Word(p.FrameCnt());
                        break;
                    }

                    case 6:
                    {
                        var p = new LottieOpacity();
                        p.NewFrame().no = 0;
                        Word(p.FrameCnt());
                        p.NextFrame().no = 12.3f;
                        Word(p.FrameCnt());
                        p.NextFrame().no = 24.6f;
                        Word(p.FrameCnt());
                        Number(p.NewFrame().no);
                        Word(p.FrameCnt());
                        break;
                    }

                    case 7:
                    {
                        var p = new LottiePathSet();
                        p.NewFrame().no = 0;
                        Word(p.FrameCnt());
                        p.NextFrame().no = 12.3f;
                        Word(p.FrameCnt());
                        p.NextFrame().no = 24.6f;
                        Word(p.FrameCnt());
                        Number(p.NewFrame().no);
                        Word(p.FrameCnt());
                        break;
                    }

                    case 8:
                    {
                        var p = new LottieColorStop();
                        p.NewFrame().no = 0;
                        Word(p.FrameCnt());
                        p.NextFrame().no = 12.3f;
                        Word(p.FrameCnt());
                        p.NextFrame().no = 24.6f;
                        Word(p.FrameCnt());
                        Number(p.NewFrame().no);
                        Word(p.FrameCnt());
                        break;
                    }

                    case 9:
                    {
                        var p = new LottieTextDoc();
                        p.NewFrame().no = 0;
                        Word(p.FrameCnt());
                        p.NextFrame().no = 12.3f;
                        Word(p.FrameCnt());
                        p.NextFrame().no = 24.6f;
                        Word(p.FrameCnt());
                        Number(p.NewFrame().no);
                        Word(p.FrameCnt());
                        break;
                    }

                    case 10:
                    {
                        var p = new LottieFloat();
                        p.NewFrame().no = 0;
                        Word(p.FrameCnt());
                        p.NextFrame().no = 12.3f;
                        Word(p.FrameCnt());
                        p.NextFrame().no = 24.6f;
                        Word(p.FrameCnt());
                        Number(p.NewFrame().no);
                        Word(p.FrameCnt());
                        break;
                    }
                }
            }
            else if (kind == "vectorTween")
            {
                var p = new LottieVector();
                for (int k = 0; k < 3; ++k)
                {
                    var f = p.NewFrame();
                    f.no = k * 12.3f;
                    f.value = new(13.7f + k * 17.3f, -8.3f + k * 13.7f);
                    f.hasTangent = i % 2 != 0;
                    f.outTangent = new(3.7f, 7.3f);
                    f.inTangent = new(-2.3f, -4.7f);
                }

                p.Prepare();
                if (i / 2 % 2 != 0)
                    tween.On(21.3f, .37f);
                else
                    tween.On(21.3f);
                for (int j = 0; j < 4; ++j)
                {
                    tween.progress = (j + 1) * .25f;
                    float frame = (i / 4) * 2.3f;
                    PointWords(p.Evaluate(frame, tween, null));
                    Number(p.GetAngle(frame, tween));
                }

                tween.Off();
            }

            row.Flush();
            var actual = memory.ToArray();
            uint length = expected.ReadUInt32();
            Assert.Equal(length, (uint)actual.Length);
            var native = expected.ReadBytes((int)length);
            Assert.True(actual.AsSpan().SequenceEqual(native), $"Original native normal Lottie {kind} record {i} ({architecture}) must match every defined byte");
            ++count;
        }

        Assert.Equal(1227, count);
        Assert.Equal(expected.BaseStream.Length, expected.BaseStream.Position);
    }
}
