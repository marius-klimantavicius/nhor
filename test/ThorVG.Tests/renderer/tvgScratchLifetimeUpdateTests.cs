using Xunit;

namespace ThorVG.Tests;

public class tvgScratchLifetimeUpdateTests
{
    private sealed class ScratchTask : Task
    {
        public RenderPath[] Paths = new RenderPath[3];
        protected internal override void Run(uint tid)
        {
            for (int i = 0; i < 3; ++i)
            {
                var path = RenderPath.Scratch();
                path.MoveTo(new Point(i, tid));
                Paths[i] = path;
            }
        }
    }

    [Fact]
    public unsafe void WorkerTerminationReleasesNativeThreadLocalScratchStorage()
    {
        var task = new ScratchTask();
        int previous = TvgCommon.engineInit;
        uint previousThreads = TaskScheduler.Threads();
        for (int i = 0; i < previous; ++i) Assert.Equal(Result.Success, Initializer.Term());
        // Direct scheduler fixtures can leave an instance without an initializer
        // reference. This test must own a fresh one-worker scheduler.
        TaskScheduler.Term();
        Assert.Equal(Result.Success, Initializer.Init(1));
        try
        {
            Assert.Equal(1u, TaskScheduler.Threads());
            TaskScheduler.Request(task); task.Done();
            Assert.NotSame(task.Paths[0], task.Paths[1]);
            Assert.NotSame(task.Paths[1], task.Paths[2]);
            foreach (var path in task.Paths)
                Assert.True(path.cmds.data != null && path.pts.data != null);
        }
        finally
        {
            try
            {
                Assert.Equal(Result.Success, Initializer.Term());
                // Keep managed references as instrumentation; native TLS ownership
                // ends with the actual joined worker, not collection timing.
                foreach (var path in task.Paths)
                {
                    Assert.True(path.cmds.data == null && path.pts.data == null);
                    Assert.Equal(0u, path.cmds.reserved); Assert.Equal(0u, path.pts.reserved);
                }
            }
            finally
            {
                for (int i = 0; i < previous; ++i) Assert.Equal(Result.Success, Initializer.Init(previousThreads));
            }
        }
    }
}
