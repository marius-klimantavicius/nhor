// Ported from ThorVG/test/testAnimation.cpp

using System;
using System.IO;
using Xunit;

namespace ThorVG.Tests
{
    public class testAnimation
    {
        private static readonly string TEST_DIR = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "ref", "ThorVG", "test", "resources"));

        [Fact]
        public void AnimationBasic()
        {
            var animation = Animation.Gen();
            Assert.NotNull(animation);

            var picture = animation.GetPicture();
            Assert.Equal(Type.Picture, picture.PaintType());

            // Negative cases
            Assert.Equal(Result.InsufficientCondition, animation.Frame(0.0f));
            Assert.Equal(0.0f, animation.CurFrame());
            Assert.Equal(0.0f, animation.TotalFrame());
            Assert.Equal(0.0f, animation.Duration());
        }

        [Fact]
        public void LottieRawData()
        {
            Assert.Equal(Result.Success, Initializer.Init());
            {
                var animation = Animation.Gen();
                Assert.NotNull(animation);

                var picture = animation.GetPicture();

                var data = File.ReadAllBytes(Path.Combine(TEST_DIR, "test.lot"));
                Assert.NotEmpty(data);
                Assert.Equal(Result.Success, picture.Load(data, (uint)data.Length, "lot", "", true));
            }
            Assert.Equal(Result.Success, Initializer.Term());
        }

        [Fact]
        public void LottieFramesCounting()
        {
            Assert.Equal(Result.Success, Initializer.Init(1));
            {
                var animation = Animation.Gen();
                Assert.NotNull(animation);

                var picture = animation.GetPicture();

                Assert.Equal(Result.InvalidArguments, picture.Load(Path.Combine(TEST_DIR, "invalid.lot")));
                Assert.Equal(Result.Success, picture.Load(Path.Combine(TEST_DIR, "test.lot")));

                Assert.InRange(animation.TotalFrame(), 120.0f - 0.001f, 120.0f + 0.001f);
                Assert.Equal(0.0f, animation.CurFrame());
                Assert.InRange(animation.Duration(), 4.004f - 0.001f, 4.004f + 0.001f); // 120/29.97
                Assert.Equal(Result.Success, animation.Frame(20.0f));

                for (float i = 1.0f; i < 120.0f; i += 10.0f)
                {
                    Assert.Equal(Result.Success, animation.Frame(i));
                    Assert.Equal(i, animation.CurFrame());
                }

                // Frame() now passes through Shorten() which introduces float precision loss
                Assert.Equal(Result.Success, animation.Frame(102.8f));
                Assert.InRange(animation.CurFrame(), 102.8f - 0.001f, 102.8f + 0.001f);

                Assert.Equal(Result.Success, animation.Frame(13.32f));
                Assert.InRange(animation.CurFrame(), 13.32f - 0.001f, 13.32f + 0.001f);

                Assert.Equal(Result.Success, animation.Frame(27.1232f));
                Assert.InRange(animation.CurFrame(), 27.1232f - 0.001f, 27.1232f + 0.001f);

                Assert.Equal(Result.Success, animation.Frame(87.0004f));
                Assert.InRange(animation.CurFrame(), 87.0004f - 0.001f, 87.0004f + 0.001f);

                Assert.Equal(Result.Success, animation.Frame(88.0005f));
                Assert.InRange(animation.CurFrame(), 88.0005f - 0.001f, 88.0005f + 0.001f);

                Assert.Equal(Result.Success, animation.Frame(89.0009f));
                Assert.InRange(animation.CurFrame(), 89.0009f - 0.001f, 89.0009f + 0.001f);
            }
            Assert.Equal(Result.Success, Initializer.Term());
        }

        [Fact]
        public void LottieAccessor()
        {
            Assert.Equal(Result.Success, Initializer.Init());
            {
                var animation = Animation.Gen();
                Assert.NotNull(animation);

                var picture = animation.GetPicture();

                Assert.Equal(Result.Success, picture.Load(Path.Combine(TEST_DIR, "test2.lot")));

                // Specify the lottie scene first
                Assert.Equal(Result.Success, animation.Frame(20.0f));

                // Find specific paint nodes
                Assert.Null(picture.FindPaint(Accessor.Id("test1")));
                Assert.Null(picture.FindPaint(Accessor.Id("abcd")));
                Assert.Null(picture.FindPaint(Accessor.Id("abcd")));
                Assert.NotNull(picture.FindPaint(Accessor.Id("bar")));
                Assert.NotNull(picture.FindPaint(Accessor.Id("pad1")));
            }
            Assert.Equal(Result.Success, Initializer.Term());
        }

        [Fact]
        public void LottieSegment()
        {
            Assert.Equal(Result.Success, Initializer.Init());
            {
                var animation = Animation.Gen();
                Assert.NotNull(animation);

                var picture = animation.GetPicture();

                float begin, end;

                // Segment by range before loaded
                Assert.Equal(Result.InsufficientCondition, animation.Segment(0, 0.5f));

                // Get current segment before loaded
                Assert.Equal(Result.InsufficientCondition, animation.Segment(out begin, out end));

                // Animation load
                Assert.Equal(Result.Success, picture.Load(Path.Combine(TEST_DIR, "segment.lot")));

                // Get current segment before segment
                Assert.Equal(Result.Success, animation.Segment(out begin, out end));
                Assert.Equal(0.0f, begin);
                Assert.Equal(animation.TotalFrame(), end);

                // Segment by range
                Assert.Equal(Result.Success, animation.Segment(0.25f, 0.5f));

                // Get current segment
                Assert.Equal(Result.Success, animation.Segment(out begin, out end));
                Assert.Equal(0.25f, begin);
                Assert.Equal(0.5f, end);

                // Get only segment begin
                Assert.Equal(Result.Success, animation.Segment(out begin, out _));
                Assert.Equal(0.25f, begin);

                // Get only segment end
                Assert.Equal(Result.Success, animation.Segment(out _, out end));
                Assert.Equal(0.5f, end);

                // Segment by invalid range
                Assert.Equal(Result.InvalidArguments, animation.Segment(1.5f, -0.5f));
            }
            Assert.Equal(Result.Success, Initializer.Term());
        }
        [Fact]
        public unsafe void LottieRenderToCanvas()
        {
            Assert.Equal(Result.Success, Initializer.Init());
            {
                var animation = Animation.Gen();
                var picture = animation.GetPicture();

                Assert.Equal(Result.Success, picture.Load(Path.Combine(TEST_DIR, "test.lot")));
                picture.GetSize(out float pw, out float ph);
                Assert.True(pw > 0 && ph > 0);

                uint w = 200, h = (uint)(ph * 200f / pw);
                picture.SetSize(w, h);

                var canvas = SwCanvas.Gen();
                var buffer = new uint[w * h];
                Assert.Equal(Result.Success, canvas.Target(buffer, w, w, h, ColorSpace.ABGR8888S));
                Assert.Equal(Result.Success, canvas.Add(picture));

                // Advance to a mid-animation frame
                Assert.Equal(Result.Success, animation.Frame(30));
                Assert.Equal(Result.Success, canvas.Update());
                Assert.Equal(Result.Success, canvas.Draw(true));
                Assert.Equal(Result.Success, canvas.Sync());

                int nonZero = 0;
                for (int i = 0; i < buffer.Length; i++)
                    if (buffer[i] != 0) nonZero++;

                Assert.True(nonZero > 0, $"Expected non-zero pixels after rendering Lottie frame 30, but buffer was all zeros ({w}x{h} = {buffer.Length} pixels)");
            }
            Assert.Equal(Result.Success, Initializer.Term());
        }

        [Fact]
        public unsafe void LottieRenderViaSaverFlow()
        {
            // Mimics the exact GifSaver flow to find what breaks rendering
            Assert.Equal(Result.Success, Initializer.Init());
            {
                var animation = Animation.Gen();
                var picture = animation.GetPicture();

                Assert.Equal(Result.Success, picture.Load(Path.Combine(TEST_DIR, "test.lot")));
                picture.GetSize(out float pw, out float ph);

                float scale = 200f / pw;
                picture.SetSize(pw * scale, ph * scale);

                // This is what GifSaver.Save does before Run():
                float x, y, bw, bh;
                picture.Bounds(out x, out y, out bw, out bh);

                uint w = (uint)bw, h = (uint)bh;
                Assert.True(w > 0 && h > 0, $"Bounds returned zero size: {bw}x{bh}");

                // This is what GifSaver.Run does:
                var canvas = SwCanvas.Gen();
                var buffer = new uint[w * h];
                Assert.Equal(Result.Success, canvas.Target(buffer, w, w, h, ColorSpace.ABGR8888S));
                Assert.Equal(Result.Success, canvas.Add(picture));

                Assert.Equal(Result.Success, animation.Frame(30));
                Assert.Equal(Result.Success, canvas.Update());
                Assert.Equal(Result.Success, canvas.Draw(true));
                Assert.Equal(Result.Success, canvas.Sync());

                int nonZero = 0;
                for (int i = 0; i < buffer.Length; i++)
                    if (buffer[i] != 0) nonZero++;

                Assert.True(nonZero > 0, $"Saver flow: expected non-zero pixels at frame 30, got 0/{buffer.Length} (canvas {w}x{h})");
            }
            Assert.Equal(Result.Success, Initializer.Term());
        }

        [Fact]
        public unsafe void LottieMultiFrameRendering()
        {
            // Verifies that advancing to a new frame produces correct (non-stale) output.
            // Regression test for the RenderContext propagator reset bug.
            Assert.Equal(Result.Success, Initializer.Init());
            {
                uint w = 200, h = 160;

                int CountNonZero(uint[] buf)
                {
                    int nz = 0;
                    for (int i = 0; i < buf.Length; i++) if (buf[i] != 0) nz++;
                    return nz;
                }

                // Render frame 1 then frame 30 on the same canvas (the bug scenario)
                int nzFrame1, nzFrame30;
                {
                    var a = Animation.Gen(); var p = a.GetPicture();
                    Assert.Equal(Result.Success, p.Load(Path.Combine(TEST_DIR, "test.lot")));
                    p.SetSize(w, h);
                    var c = SwCanvas.Gen(); var buf = new uint[w * h];
                    c.Target(buf, w, w, h, ColorSpace.ABGR8888S); c.Add(p);

                    a.Frame(1);
                    c.Update(); c.Draw(true); c.Sync();
                    nzFrame1 = CountNonZero(buf);

                    a.Frame(30);
                    c.Update(); c.Draw(true); c.Sync();
                    nzFrame30 = CountNonZero(buf);
                }

                // Render frame 30 on a fresh canvas (baseline)
                int nzBaseline;
                {
                    var a = Animation.Gen(); var p = a.GetPicture();
                    Assert.Equal(Result.Success, p.Load(Path.Combine(TEST_DIR, "test.lot")));
                    p.SetSize(w, h);
                    var c = SwCanvas.Gen(); var buf = new uint[w * h];
                    c.Target(buf, w, w, h, ColorSpace.ABGR8888S); c.Add(p);

                    a.Frame(30);
                    c.Update(); c.Draw(true); c.Sync();
                    nzBaseline = CountNonZero(buf);
                }

                Assert.True(nzFrame1 > 0, "Frame 1 should have non-zero pixels");
                Assert.True(nzBaseline > 0, "Baseline frame 30 should have non-zero pixels");
                // Frame 30 after frame 1 must match fresh frame 30 (no stale data)
                Assert.Equal(nzBaseline, nzFrame30);
                // Frame 30 has more visible content than frame 1 in test.lot
                Assert.True(nzFrame30 > nzFrame1,
                    $"Frame 30 ({nzFrame30} pixels) should have more content than frame 1 ({nzFrame1} pixels)");
            }
            Assert.Equal(Result.Success, Initializer.Term());
        }

        [Fact]
        public unsafe void LottieRenderViaActualSaver()
        {
            // Uses the actual Saver → GifSaver pipeline, then validates output
            Assert.Equal(Result.Success, Initializer.Init());
            {
                var animation = Animation.Gen();
                var picture = animation.GetPicture();

                Assert.Equal(Result.Success, picture.Load(Path.Combine(TEST_DIR, "test.lot")));
                picture.GetSize(out float pw, out float ph);

                float scale = 200f / pw;
                picture.SetSize(pw * scale, ph * scale);

                var gifPath = Path.Combine(Path.GetTempPath(), "thorvg_test_" + Guid.NewGuid().ToString("N") + ".gif");
                try
                {
                    var saver = Saver.Gen();
                    Assert.Equal(Result.Success, saver.Save(animation, gifPath, 100, 30));
                    Assert.Equal(Result.Success, saver.Sync());

                    // Check GIF file was created and has varied content
                    Assert.True(File.Exists(gifPath), "GIF file not created");
                    var data = File.ReadAllBytes(gifPath);
                    Assert.True(data.Length > 1000, $"GIF file too small: {data.Length} bytes");

                    // Parse first 3 frames' LZW sizes to check for variation
                    int pos = 13 + 2 * 3; // after header + GCT
                    // Skip NETSCAPE extension
                    if (pos < data.Length && data[pos] == 0x21 && data[pos + 1] == 0xff)
                    {
                        pos += 3 + 11; // ext header + app id
                        while (pos < data.Length && data[pos] != 0) pos += 1 + data[pos];
                        pos++; // terminator
                    }

                    var frameSizes = new System.Collections.Generic.List<int>();
                    while (pos < data.Length && data[pos] != 0x3b && frameSizes.Count < 10)
                    {
                        if (data[pos] == 0x21 && data[pos + 1] == 0xf9) { pos += 8; continue; }
                        if (data[pos] != 0x2c) break;
                        pos += 10;
                        int packed = data[pos - 1];
                        if (((packed >> 7) & 1) != 0) pos += 3 * (1 << ((packed & 7) + 1));
                        pos++; // min code size
                        int lzw = 0;
                        while (pos < data.Length && data[pos] != 0) { lzw += data[pos]; pos += 1 + data[pos]; }
                        pos++;
                        frameSizes.Add(lzw);
                    }

                    Assert.True(frameSizes.Count > 3, $"Too few frames: {frameSizes.Count}");

                    // Not all frames should be the same size (animation has movement)
                    var uniqueSizes = new System.Collections.Generic.HashSet<int>(frameSizes);
                    Assert.True(uniqueSizes.Count > 1,
                        $"All {frameSizes.Count} frames have identical LZW size ({frameSizes[0]}), rendering likely broken");
                }
                finally
                {
                    if (File.Exists(gifPath)) File.Delete(gifPath);
                }
            }
            Assert.Equal(Result.Success, Initializer.Term());
        }
    }
}
