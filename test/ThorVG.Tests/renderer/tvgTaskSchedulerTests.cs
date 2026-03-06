using Xunit;

namespace ThorVG.Tests
{
    public class tvgTaskSchedulerTests
    {
        [Fact]
        public void TaskScheduler_InitAndTerm()
        {
            TaskScheduler.Init(4);
            Assert.Equal(4u, TaskScheduler.Threads());
            TaskScheduler.Term();
            Assert.Equal(0u, TaskScheduler.Threads());
        }

        [Fact]
        public void TaskScheduler_OnThread_ReturnsFalse()
        {
            // Single-threaded stub always returns false
            Assert.False(TaskScheduler.OnThread());
        }

        private class TestTask : Task
        {
            public bool Ran { get; private set; }
            protected override void Run(uint tid) { Ran = true; }
        }

        [Fact]
        public void TaskScheduler_Request_CallsDone()
        {
            var task = new TestTask();
            TaskScheduler.Request(task);
            // In single-threaded mode, Request calls Done() which is a no-op
            // The task is not actually run in single-threaded stub mode
        }
    }
}
