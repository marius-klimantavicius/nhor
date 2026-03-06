// Ported from ThorVG/src/renderer/tvgTaskScheduler.h and tvgTaskScheduler.cpp

using System;
using System.Threading;

namespace ThorVG
{
    /// <summary>
    /// Abstract async task. Mirrors C++ tvg::Task.
    /// Supports both threaded and non-threaded execution.
    /// </summary>
    public abstract class Task : IInlistNode<Task>
    {
        // IInlistNode implementation
        public Task? Prev { get; set; }
        public Task? Next { get; set; }

        private readonly object _mtx = new object();
        private bool _ready = true;
        private bool _pending;

        public void Done()
        {
            if (!_pending) return;

            lock (_mtx)
            {
                while (!_ready) Monitor.Wait(_mtx);
            }
            _pending = false;
        }

        internal protected abstract void Run(uint tid);

        internal void Execute(uint tid)
        {
            Run(tid);

            lock (_mtx)
            {
                _ready = true;
                Monitor.Pulse(_mtx);
            }
        }

        internal void Prepare()
        {
            _ready = false;
            _pending = true;
        }
    }

    /// <summary>
    /// Thread-safe task queue. Mirrors C++ TaskQueue.
    /// </summary>
    internal class TaskQueue
    {
        private readonly Inlist<Task> _taskDeque = new Inlist<Task>();
        private readonly object _mtx = new object();
        private bool _done;

        public bool TryPop(out Task? task)
        {
            task = null;
            if (!Monitor.TryEnter(_mtx)) return false;
            try
            {
                if (_taskDeque.Empty()) return false;
                task = _taskDeque.PopFront();
                return task != null;
            }
            finally
            {
                Monitor.Exit(_mtx);
            }
        }

        public bool TryPush(Task task)
        {
            if (!Monitor.TryEnter(_mtx))
                return false;
            try
            {
                _taskDeque.Back(task);
            }
            finally
            {
                Monitor.Exit(_mtx);
            }
            lock (_mtx) { Monitor.Pulse(_mtx); }
            return true;
        }

        public void Complete()
        {
            lock (_mtx)
            {
                _done = true;
                Monitor.PulseAll(_mtx);
            }
        }

        public bool Pop(out Task? task)
        {
            task = null;
            lock (_mtx)
            {
                while (_taskDeque.Empty() && !_done)
                {
                    Monitor.Wait(_mtx);
                }
                if (_taskDeque.Empty()) return false;
                task = _taskDeque.PopFront();
                return task != null;
            }
        }

        public void Push(Task task)
        {
            lock (_mtx)
            {
                _taskDeque.Back(task);
                Monitor.Pulse(_mtx);
            }
        }
    }

    /// <summary>
    /// Internal task scheduler implementation. Mirrors C++ TaskSchedulerImpl.
    /// </summary>
    internal class TaskSchedulerImpl
    {
        private readonly Thread[] _threads;
        private readonly TaskQueue[] _taskQueues;
        private int _idx;

        public TaskSchedulerImpl(uint threadCnt)
        {
            _threads = new Thread[threadCnt];
            _taskQueues = new TaskQueue[threadCnt];

            for (int i = 0; i < (int)threadCnt; ++i)
            {
                _taskQueues[i] = new TaskQueue();
            }

            for (int i = 0; i < (int)threadCnt; ++i)
            {
                var index = i;
                _threads[i] = new Thread(() => RunLoop((uint)index))
                {
                    IsBackground = true,
                    Name = $"ThorVG-Worker-{index}"
                };
                _threads[i].Start();
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _taskQueues.Length; ++i)
            {
                _taskQueues[i].Complete();
            }
            for (int i = 0; i < _threads.Length; ++i)
            {
                _threads[i].Join();
            }
        }

        private void RunLoop(uint i)
        {
            // Thread Loop
            while (true)
            {
                Task? task = null;
                var success = false;

                for (uint x = 0; x < (uint)_threads.Length * 2; ++x)
                {
                    if (_taskQueues[(i + x) % (uint)_threads.Length].TryPop(out task))
                    {
                        success = true;
                        break;
                    }
                }

                if (!success && !_taskQueues[i].Pop(out task)) break;
                task!.Execute(i + 1);
            }
        }

        public void Request(Task task)
        {
            // Async
            if (_threads.Length > 0)
            {
                task.Prepare();
                var i = (uint)Interlocked.Increment(ref _idx) - 1;
                for (uint n = 0; n < (uint)_threads.Length; ++n)
                {
                    if (_taskQueues[(i + n) % (uint)_threads.Length].TryPush(task)) return;
                }
                _taskQueues[i % (uint)_threads.Length].Push(task);
            }
            // Sync
            else
            {
                task.Run(0);
            }
        }

        public uint ThreadCnt() => (uint)_threads.Length;
    }

    /// <summary>
    /// Task scheduler. Mirrors C++ tvg::TaskScheduler.
    /// </summary>
    public static class TaskScheduler
    {
        private static TaskSchedulerImpl? _inst;
        private static int _tid;  // dominant thread id

        public static uint Threads()
        {
            return _inst != null ? _inst.ThreadCnt() : 0;
        }

        public static void Init(uint threads)
        {
            if (_inst != null) return;
            _inst = new TaskSchedulerImpl(threads);
            _tid = Tid();

            // Enable threading in ScopedLock if threads > 0
            ScopedLock.ThreadingEnabled = threads > 0;
        }

        public static void Term()
        {
            _inst?.Dispose();
            _inst = null;
            ScopedLock.ThreadingEnabled = false;
        }

        public static void Request(Task task)
        {
            if (_inst != null) _inst.Request(task);
        }

        public static bool OnThread()
        {
            return _tid != Tid();
        }

        public static int Tid()
        {
            return Environment.CurrentManagedThreadId;
        }
    }
}
