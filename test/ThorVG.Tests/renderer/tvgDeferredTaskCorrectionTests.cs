using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Xunit;

namespace ThorVG.Tests;

public class tvgDeferredTaskCorrectionTests
{
    private sealed class SchedulerScope : IDisposable
    {
        private readonly int previous = TvgCommon.engineInit;
        private readonly uint threads = TaskScheduler.Threads();
        public SchedulerScope(uint workers)
        {
            for (int i = 0; i < previous; ++i) Assert.Equal(Result.Success, Initializer.Term());
            TaskScheduler.Term();
            Assert.Equal(Result.Success, Initializer.Init(workers));
            Assert.Equal(workers, TaskScheduler.Threads());
        }
        public void Dispose()
        {
            Assert.Equal(Result.Success, Initializer.Term());
            for (int i = 0; i < previous; ++i) Assert.Equal(Result.Success, Initializer.Init(threads));
        }
    }

    [Theory]
    [InlineData(8, false)]
    [InlineData(9, false)]
    [InlineData(17, false)]
    [InlineData(8, true)]
    [InlineData(9, true)]
    [InlineData(17, true)]
    public void NestedPublicClipsOwnShapeAndImageStorageThroughUpdateReuseAndDisposal(int depth, bool image)
    {
        var serial = RenderClips(depth, image, 0);
        var threaded = RenderClips(depth, image, 2);
        Assert.Equal(serial, threaded);
    }

    private static uint[] RenderClips(int depth, bool image, uint workers)
    {
        using var scheduler = new SchedulerScope(workers);
        var pixels = new uint[128 * 128];
        using var canvas = SwCanvas.Gen()!;
        Assert.Equal(Result.Success, canvas.Target(pixels, 128, 128, 128, ColorSpace.ABGR8888));
        var root = Scene.Gen();
        var inner = root;
        var clippers = new List<Shape>();
        for (int i = 0; i < depth; ++i)
        {
            var clip = Shape.Gen();
            Assert.Equal(Result.Success, clip.AppendCircle(64, 64, 60 - i * 2, 60 - i * 2));
            clip.SetFill(255, 255, 255);
            Assert.Equal(Result.Success, inner.Clip(clip));
            clippers.Add(clip);
            var child = Scene.Gen();
            Assert.Equal(Result.Success, inner.Add(child));
            inner = child;
        }
        var paints = new List<Paint>();
        for (int i = 0; i < 128; ++i)
        {
            Paint paint;
            if (image)
            {
                var picture = Picture.Gen();
                var data = new uint[128 * 128];
                Array.Fill(data, 0xff965014u + (uint)(i % 200));
                Assert.Equal(Result.Success, picture.Load(data, 128, 128, ColorSpace.ABGR8888, true));
                paint = picture;
            }
            else
            {
                var shape = Shape.Gen();
                shape.AppendRect(0, 0, 128, 128);
                shape.SetFill((byte)(20 + i % 200), 80, 150);
                paint = shape;
            }
            inner.Add(paint);
            paints.Add(paint);
        }
        canvas.Add(root);
        SwTask? retained = null;
        var frames = new uint[pixels.Length * 3];
        for (int frame = 0; frame < 3; ++frame)
        {
            if (frame != 0) clippers[^1].Translate(2, 0);
            Assert.Equal(Result.Success, canvas.Update());
            var task = Assert.IsAssignableFrom<SwTask>(paints[^1].pImpl.rd);
            if (retained != null) Assert.Same(retained, task);
            retained = task;
            // Traversal has already popped every clip, before Draw waits for tasks.
            foreach (var paint in paints)
            {
                var prepared = Assert.IsAssignableFrom<SwTask>(paint.pImpl.rd);
                Assert.Equal(depth, prepared.clips.Count);
                for (int i = 0; i < depth; ++i) Assert.Same(clippers[i].pImpl.rd, prepared.clips[i]);
            }
            Assert.Equal(Result.Success, canvas.Draw(true));
            Assert.Equal(Result.Success, canvas.Sync());
            Assert.Equal(0u, pixels[0]);
            Assert.NotEqual(0u, pixels[64 * 128 + 64]);
            pixels.CopyTo(frames, frame * pixels.Length);
        }
        // Keep the task as a lifetime witness while removing its owning paints.
        Assert.Equal(Result.Success, canvas.Remove(root));
        Assert.Equal(0, retained!.clips.Count);
        return frames;
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(2u)]
    public void LcdOptionsRefreshRetainedTextAndNeverAffectOrdinaryShapes(uint workers)
    {
        var expected = RenderLcd(0);
        Assert.Equal(expected, RenderLcd(workers));
    }

    private static uint[] RenderLcd(uint workers)
    {
        using var scheduler = new SchedulerScope(workers);
        bool previous = SwLcdSubpixel.Enabled;
        try
        {
            SwLcdSubpixel.Enabled = false;
            var font = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "legacy-upstream", "resources", "PublicSans-Regular.ttf"));
            Assert.Equal(Result.Success, Text.LoadFont("correction-lcd", font, (uint)font.Length, "ttf", true));
            using var canvas = SwCanvas.Gen()!;
            var pixels = new uint[192 * 64];
            canvas.Target(pixels, 192, 192, 64, ColorSpace.ABGR8888);
            var background = Shape.Gen();
            background.AppendRect(0, 0, 192, 64); background.SetFill(15, 15, 15);
            canvas.Add(background);
            var text = Text.Gen();
            text.SetFont("correction-lcd"); text.SetFontSize(19.5f); text.SetHinting(false);
            text.SetText("ThorVG 1.2"); text.SetFill(245, 245, 245); text.Translate(2.25f, 3.5f);
            canvas.Add(text);
            var normal = Shape.Gen(); normal.AppendCircle(170, 30, 12.5f, 12.5f); normal.SetFill(180, 90, 70);
            canvas.Add(normal);
            SwShapeTask? retained = null;
            var frames = new uint[pixels.Length * 6];
            for (int frame = 0; frame < 6; ++frame)
            {
                // off, per-text, off, global, both (global off), off.
                if (frame == 1 || frame == 4) text.SetSubpixelRendering(true);
                if (frame == 2 || frame == 5) text.SetSubpixelRendering(false);
                SwLcdSubpixel.Enabled = frame == 3;
                // Reuse/update ordinary shapes beside text in every option state.
                normal.SetFill(180, 90, 70);
                Assert.Equal(Result.Success, canvas.Update());
                var task = Assert.IsType<SwShapeTask>(text.shape.pImpl.rd);
                if (retained != null) Assert.Same(retained, task);
                retained = task;
                canvas.Draw(true); canvas.Sync();
                Assert.Equal(frame is 1 or 3 or 4, task.shape.hasLcdRle);
                Assert.False(Assert.IsType<SwShapeTask>(normal.pImpl.rd).shape.hasLcdRle);
                Assert.False(Assert.IsType<SwShapeTask>(background.pImpl.rd).shape.hasLcdRle);
                pixels.CopyTo(frames, frame * pixels.Length);
            }
            var size = pixels.Length;
            Assert.Equal(frames.AsSpan(0, size).ToArray(), frames.AsSpan(size * 2, size).ToArray());
            Assert.Equal(frames.AsSpan(0, size).ToArray(), frames.AsSpan(size * 5, size).ToArray());
            Assert.Equal(frames.AsSpan(size, size).ToArray(), frames.AsSpan(size * 3, size).ToArray());
            Assert.Equal(frames.AsSpan(size, size).ToArray(), frames.AsSpan(size * 4, size).ToArray());
            Assert.False(frames.AsSpan(0, size).SequenceEqual(frames.AsSpan(size, size)));
            return frames;
        }
        finally { SwLcdSubpixel.Enabled = previous; }
    }

    private sealed class BlockingTask : Task
    {
        public readonly ManualResetEventSlim Started = new(false);
        public readonly ManualResetEventSlim Release = new(false);
        protected internal override void Run(uint tid)
        {
            Assert.NotEqual(0u, tid);
            Started.Set();
            Assert.True(Release.Wait(TimeSpan.FromSeconds(20)));
        }
    }

    [Fact]
    public void WorkerDispatchRemainsParallel()
    {
        using var scheduler = new SchedulerScope(2);
        var first = new BlockingTask(); var second = new BlockingTask();
        try
        {
            TaskScheduler.Request(first); TaskScheduler.Request(second);
            Assert.True(first.Started.Wait(TimeSpan.FromSeconds(10)));
            Assert.True(second.Started.Wait(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            first.Release.Set(); second.Release.Set(); first.Done(); second.Done();
            first.Started.Dispose(); first.Release.Dispose(); second.Started.Dispose(); second.Release.Dispose();
        }
    }

    [Fact]
    public void ValueListCopyFromDetachesPreexistingAliasesAndSupportsSelfCopy()
    {
        var source = new ValueList<object?>();
        for (int i = 0; i < 17; ++i) source.Add(new object());
        var alias = source;
        alias.CopyFrom(ref source);
        Assert.Equal(17, source.Count);
        for (int i = 0; i < source.Count; ++i) Assert.Same(source[i], alias[i]);
        source.CopyFrom(ref source);
        source.Clear();
        foreach (var item in alias) Assert.NotNull(item);
        alias.Clear();
    }
}
