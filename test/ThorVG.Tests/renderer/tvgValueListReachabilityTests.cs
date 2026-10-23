using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Xunit;

namespace ThorVG.Tests;

public class tvgValueListReachabilityTests
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
    [InlineData(8, false, false)]
    [InlineData(9, false, false)]
    [InlineData(17, false, false)]
    [InlineData(8, true, false)]
    [InlineData(9, true, false)]
    [InlineData(17, true, false)]
    [InlineData(8, false, true)]
    [InlineData(9, false, true)]
    [InlineData(17, false, true)]
    [InlineData(8, true, true)]
    [InlineData(9, true, true)]
    [InlineData(17, true, true)]
    public void RetainedDisposedTasksReleaseClips(int depth, bool image, bool popFirst)
    {
        var (task, references) = CreateDisposedTask(depth, image, popFirst);
        try
        {
            Assert.Equal(0, task.clips.Count);
            AssertCollected(references);
        }
        finally { GC.KeepAlive(task); }
    }

    // NoInlining keeps all former strong clip locals out of the collecting frame.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (SwTask, WeakReference[]) CreateDisposedTask(int depth, bool image, bool popFirst)
    {
        SwTask task = image ? new SwImageTask() : new SwShapeTask();
        var references = new WeakReference[depth];
        for (int i = 0; i < depth; ++i)
        {
            var clip = new SwShapeTask();
            references[i] = new WeakReference(clip);
            task.clips.Add(clip);
        }
        if (popFirst)
            while (!task.clips.IsEmpty) task.clips.RemoveAt(task.clips.Count - 1);
        task.DisposeTask();
        return (task, references);
    }

    [Theory]
    [InlineData(8, 0u, false)]
    [InlineData(9, 0u, false)]
    [InlineData(17, 0u, false)]
    [InlineData(8, 2u, false)]
    [InlineData(9, 2u, false)]
    [InlineData(17, 2u, false)]
    [InlineData(8, 0u, true)]
    [InlineData(9, 0u, true)]
    [InlineData(17, 0u, true)]
    [InlineData(8, 2u, true)]
    [InlineData(9, 2u, true)]
    [InlineData(17, 2u, true)]
    public void RetainedPublicCanvasReleasesRemovedClipTasks(int depth, uint workers, bool image)
    {
        using var scheduler = new SchedulerScope(workers);
        var (canvas, references) = CreateRemovedScene(depth, image);
        try
        {
            Assert.Equal(0, canvas.GetPaints().Count);
            AssertCollected(references);
        }
        finally { canvas.Dispose(); GC.KeepAlive(canvas); }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (SwCanvas, WeakReference[]) CreateRemovedScene(int depth, bool image)
    {
        var canvas = SwCanvas.Gen()!;
        try
        {
            var pixels = new uint[128 * 128];
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
            if (image)
            {
                var picture = Picture.Gen();
                var data = new uint[128 * 128];
                Array.Fill(data, 0xff965014u);
                Assert.Equal(Result.Success, picture.Load(data, 128, 128, ColorSpace.ABGR8888, true));
                Assert.Equal(Result.Success, inner.Add(picture));
            }
            else
            {
                var shape = Shape.Gen();
                shape.AppendRect(0, 0, 128, 128);
                shape.SetFill(20, 80, 150);
                Assert.Equal(Result.Success, inner.Add(shape));
            }
            Assert.Equal(Result.Success, canvas.Add(root));
            Assert.Equal(Result.Success, canvas.Draw(true));
            Assert.Equal(Result.Success, canvas.Sync());
            Assert.NotEqual(0u, pixels[64 * 128 + 64]);
            var references = new WeakReference[depth];
            for (int i = 0; i < depth; ++i)
            {
                Assert.NotNull(clippers[i].pImpl.rd);
                references[i] = new WeakReference(clippers[i].pImpl.rd);
            }
            Assert.Equal(Result.Success, canvas.Remove(root));
            Assert.Equal(Result.Success, canvas.Update());
            Assert.Equal(Result.Success, canvas.Draw(true));
            Assert.Equal(Result.Success, canvas.Sync());
            return (canvas, references);
        }
        catch { canvas.Dispose(); throw; }
    }

    private struct ReferenceValue
    {
        public object Item;
    }

    private sealed class Holder
    {
        public ValueList<ReferenceValue> Items;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ClearReleasesReferenceContainingValuesAfterSpillAndReuse(bool insert, bool popFirst)
    {
        var holder = new Holder();
        try
        {
            // Reuse after overflow must also release later inline-only contents.
            foreach (int count in new[] { 17, 8, 9 })
            {
                var references = FillAndClear(holder, count, insert, popFirst);
                Assert.Equal(0, holder.Items.Count);
                AssertCollected(references);
            }
        }
        finally { GC.KeepAlive(holder); }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] FillAndClear(Holder holder, int count, bool insert, bool popFirst)
    {
        var references = new WeakReference[count];
        for (int i = 0; i < count; ++i)
        {
            var item = new object();
            references[i] = new WeakReference(item);
            var value = new ReferenceValue { Item = item };
            if (insert) holder.Items.Insert(i, value);
            else holder.Items.Add(value);
        }
        if (popFirst)
            while (!holder.Items.IsEmpty) holder.Items.RemoveAt(holder.Items.Count - 1);
        holder.Items.Clear();
        return references;
    }

    [Fact]
    public void UnmanagedValuesPreserveSpillCopyPopClearAndReuseBehavior()
    {
        var values = new ValueList<int>();
        foreach (int count in new[] { 8, 9, 17, 8 })
        {
            for (int i = 0; i < count; ++i) values.Add(i);
            values.Insert(0, -1);
            values.RemoveAt(0);
            var copy = values;
            copy.CopyFrom(ref values);
            values.CopyFrom(ref values);
            for (int i = count - 1; i >= 0; --i)
            {
                Assert.Equal(i, values[i]);
                values.RemoveAt(i);
            }
            values.Clear();
            Assert.Equal(0, values.Count);
            for (int i = 0; i < count; ++i) Assert.Equal(i, copy[i]);
            copy.Clear();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AssertCollected(WeakReference[] references)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        foreach (var reference in references) Assert.False(reference.IsAlive);
    }
}
