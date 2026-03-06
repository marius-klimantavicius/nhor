using Xunit;

namespace ThorVG.Tests
{
    public class tvgLockTests
    {
        [Fact]
        public void ScopedLock_NoThreading_NoOp()
        {
            ScopedLock.ThreadingEnabled = false;
            var key = new Key();
            using (var lk = new ScopedLock(key))
            {
                // Should not throw
            }
        }

        [Fact]
        public void ScopedLock_WithThreading_LocksAndUnlocks()
        {
            ScopedLock.ThreadingEnabled = true;
            try
            {
                var key = new Key();
                using (var lk = new ScopedLock(key))
                {
                    // Lock held — should not deadlock as this is the only thread.
                }
                // After dispose the lock should be released;
                // acquiring again should succeed.
                using (var lk2 = new ScopedLock(key))
                {
                }
            }
            finally
            {
                ScopedLock.ThreadingEnabled = false;
            }
        }

        [Fact]
        public void Key_CanBeCreated()
        {
            var key = new Key();
            Assert.NotNull(key);
        }

        [Fact]
        public void ScopedLock_ThreadSafety()
        {
            ScopedLock.ThreadingEnabled = true;
            try
            {
                var key = new Key();
                int counter = 0;
                const int iterations = 1000;
                var threads = new System.Threading.Thread[4];

                for (int i = 0; i < threads.Length; i++)
                {
                    threads[i] = new System.Threading.Thread(() =>
                    {
                        for (int j = 0; j < iterations; j++)
                        {
                            using var lk = new ScopedLock(key);
                            counter++;
                        }
                    });
                }

                foreach (var t in threads) t.Start();
                foreach (var t in threads) t.Join();

                Assert.Equal(threads.Length * iterations, counter);
            }
            finally
            {
                ScopedLock.ThreadingEnabled = false;
            }
        }
    }
}
