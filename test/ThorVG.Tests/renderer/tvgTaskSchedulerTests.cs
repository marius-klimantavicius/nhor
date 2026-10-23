using Xunit;

namespace ThorVG.Tests
{
    public class tvgTaskSchedulerTests
    {
        [Fact]
        public void TaskScheduler_InitAndTerm()
        {
            TaskScheduler.Term();
            TaskScheduler.Init(4);
            Assert.Equal(4u, TaskScheduler.Threads());
            TaskScheduler.Term();
            Assert.Equal(0u, TaskScheduler.Threads());
        }

        [Fact]
        public void TaskScheduler_OnThread_ReturnsFalse()
        {
            TaskScheduler.Term();
            TaskScheduler.Init(0);
            try
            {
                Assert.False(TaskScheduler.OnThread());
                bool otherThread = false;
                var thread = new System.Threading.Thread(() => otherThread = TaskScheduler.OnThread());
                thread.Start();
                thread.Join();
                Assert.True(otherThread);
            }
            finally { TaskScheduler.Term(); }
        }

        private class TestTask : Task
        {
            public bool Ran { get; private set; }
            public uint Worker { get; private set; }
            protected internal override void Run(uint tid) { Worker = tid; Ran = true; }
        }

        [Fact]
        public void TaskScheduler_Request_CallsDone()
        {
            TaskScheduler.Term();
            var task = new TestTask();
            TaskScheduler.Request(task);
            task.Done();
            Assert.False(task.Ran); // No scheduler: native request is a no-op.
            TaskScheduler.Init(0);
            try
            {
                TaskScheduler.Request(task);
                Assert.True(task.Ran); // Zero workers: synchronous execution.
                Assert.Equal(0u, task.Worker);
                task.Done();
            }
            finally { TaskScheduler.Term(); }
        }

        [Theory]
        [InlineData(1u)]
        [InlineData(4u)]
        public void WorkerRequestsFinishAndCanReuseTheSameTask(uint workers)
        {
            TaskScheduler.Term();
            TaskScheduler.Init(workers);
            try
            {
                var task = new TestTask();
                for (int i = 0; i < 128; ++i)
                {
                    TaskScheduler.Request(task);
                    task.Done();
                    Assert.True(task.Ran);
                    Assert.InRange(task.Worker, 1u, workers);
                }
            }
            finally { TaskScheduler.Term(); }
        }
    }
}
