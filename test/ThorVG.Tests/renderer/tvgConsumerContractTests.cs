using System;
using System.Collections.Generic;
using Xunit;

namespace ThorVG.Tests
{
    public class tvgConsumerContractTests
    {
        [Fact]
        public void RetainedSceneSurvivesDetachAndUnretainedSceneReleasesItsPaints()
        {
            var parent = Scene.Gen();
            var retained = Scene.Gen(); retained.Ref();
            var paint = Shape.Gen(); paint.AppendRect(0, 0, 10, 10); paint.SetFill(255, 0, 0);
            retained.Add(paint);
            parent.Add(retained);
            Assert.Equal((ushort)2, retained.RefCnt());
            parent.Remove(retained);
            Assert.Null(retained.Parent());
            Assert.Single(retained.Paints());
            Assert.Equal(Result.Success, parent.Add(retained));
            Assert.Same(retained, paint.Parent());
            parent.Remove(retained); retained.Unref();
            Assert.Empty(retained.Paints());

            var unretained = Scene.Gen();
            unretained.Add(Shape.Gen()); parent.Add(unretained); parent.Remove(unretained);
            Assert.Empty(unretained.Paints()); // Winter F03 must fix its owner reference.
            Paint.Rel(parent);
        }

        [Fact]
        public void PureOrderMutationRequiresDamageOrClearForPartialRendering()
        {
            Initializer.Init();
            using var canvas = SwCanvas.Gen(EngineOption.SmartRender)!;
            try
            {
                var pixels = new uint[32 * 32]; canvas.Target(pixels, 32, 32, 32, ColorSpace.ABGR8888);
                var scene = Scene.Gen();
                var red = Shape.Gen(); red.AppendRect(0, 0, 24, 24); red.SetFill(255, 0, 0, 128);
                var blue = Shape.Gen(); blue.AppendRect(8, 8, 24, 24); blue.SetFill(0, 0, 255, 128);
                scene.Add(red); scene.Add(blue); canvas.Add(scene);
                Draw(true); var original = (uint[])pixels.Clone();
                ((List<Paint>)scene.Paints()).Reverse();
                Draw(false);
                Assert.Equal(original, pixels);
                Draw(true);
                Assert.NotEqual(original[12 * 32 + 12], pixels[12 * 32 + 12]);
                red.Ref(); scene.Remove(red); scene.Add(red, blue); red.Unref(false);
                Draw(false);
                Assert.Equal(original, pixels);

                void Draw(bool clear)
                {
                    Assert.Equal(Result.Success, canvas.Update());
                    Assert.Equal(Result.Success, canvas.Draw(clear));
                    Assert.Equal(Result.Success, canvas.Sync());
                }
            }
            finally { canvas.Dispose(); Initializer.Term(); }
        }

        [Theory]
        [InlineData(ColorSpace.ARGB8888, 0x80804020u)]
        [InlineData(ColorSpace.ABGR8888, 0x80204080u)]
        [InlineData(ColorSpace.ARGB8888S, 0x80ff7f3fu)]
        [InlineData(ColorSpace.ABGR8888S, 0x803f7fffu)]
        public void ManagedTargetRemainsPinnedAcrossGcAndRetargetsWithNativeAlpha(ColorSpace format, uint expected)
        {
            Initializer.Init();
            using var canvas = SwCanvas.Gen()!;
            try
            {
                var first = new uint[16 * 16]; canvas.Target(first, 16, 16, 16, format);
                var shape = Shape.Gen(); shape.AppendRect(0, 0, 32, 32); shape.SetFill(255, 128, 64, 128); canvas.Add(shape);
                GC.Collect(2, GCCollectionMode.Forced, true, true);
                Draw(); Assert.Equal(expected, first[8 * 16 + 8]);
                var previous = (uint[])first.Clone();
                var second = new uint[32 * 32]; Assert.Equal(Result.Success, canvas.Target(second, 32, 32, 32, format));
                GC.Collect(2, GCCollectionMode.Forced, true, true);
                Draw(); Assert.Equal(expected, second[20 * 32 + 20]); Assert.Equal(previous, first);

                void Draw()
                {
                    Assert.Equal(Result.Success, canvas.Update());
                    Assert.Equal(Result.Success, canvas.Draw(true));
                    Assert.Equal(Result.Success, canvas.Sync());
                }
            }
            finally { canvas.Dispose(); Initializer.Term(); }
        }

        [Theory]
        [InlineData(0u)]
        [InlineData(2u)]
        public void CpuTaskUsesConfiguredWorkersAndDoneWaits(uint threads)
        {
            TaskScheduler.Term(); TaskScheduler.Init(threads);
            try
            {
                var task = new CaptureTask();
                task.Request(); task.Done();
                Assert.True(task.ran);
                Assert.Equal(threads == 0, task.worker == 0);
            }
            finally { TaskScheduler.Term(); }
        }

        private sealed class CaptureTask : SwTask
        {
            public bool ran;
            public uint worker;
            public override void Run(uint tid) { worker = tid; ran = true; }
        }

        [Fact]
        public void NestedWorkerRequestsCompleteWithTheirOriginalPoolIndex()
        {
            TaskScheduler.Term(); TaskScheduler.Init(1);
            try
            {
                var task = new NestedTask();
                TaskScheduler.Request(task);
                task.Done();
                Assert.Equal(1u, task.outerWorker);
                Assert.Equal(task.outerWorker, task.inner.worker);
                Assert.True(task.inner.ran);
            }
            finally { TaskScheduler.Term(); }
        }

        private sealed class NestedTask : Task
        {
            public uint outerWorker;
            public CaptureTask inner = new();
            protected internal override void Run(uint tid)
            {
                outerWorker = tid;
                inner.Request();
                inner.Done();
            }
        }
    }
}
