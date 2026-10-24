using System;
using System.Collections.Concurrent;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Marius.Winter;

/// <summary>FIFO compatibility queue. Enqueue wakes the native pump and throws
/// after shutdown starts. Prefer Window.Dispatcher for observable completion.</summary>
public sealed class WindowDispatcherQueue : IReadOnlyCollection<Action>
{
    private readonly ConcurrentQueue<Action> _queue = new();
    private readonly object _gate = new();
    private readonly Action _wake;
    private bool _stopping, _closed;
    internal WindowDispatcherQueue(Action wake) => _wake = wake;
    public bool IsEmpty => _queue.IsEmpty;
    public int Count => _queue.Count;
    public IEnumerator<Action> GetEnumerator() => _queue.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public void Enqueue(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (!TryPost(action, false)) throw new ObjectDisposedException(nameof(Window));
    }
    internal bool TryPost(Action action, bool cleanup)
    {
        lock (_gate)
        {
            if (_closed || (_stopping && !cleanup)) return false;
            _queue.Enqueue(action);
            _wake();
            return true;
        }
    }
    public bool TryDequeue(out Action action) => _queue.TryDequeue(out action!);
    internal void StopAccepting() { lock (_gate) { _stopping = true; _queue.Clear(); } }
    internal void Close() { lock (_gate) { _closed = true; _queue.Clear(); } }
}

/// <summary>Executes Window work on its construction thread. Awaited work captures
/// a pump synchronization context; shutdown cancels outward application tasks
/// and drops their later UI continuations. External work must cancel cooperatively.</summary>
public sealed class WindowDispatcher
{
    private readonly int _owner = Environment.CurrentManagedThreadId;
    private readonly WindowDispatcherQueue _queue;
    private readonly object _gate = new();
    private readonly Dictionary<Task, Action> _pending = new();
    private readonly PumpContext _context, _cleanupContext;
    private bool _stopping, _closed;
    internal WindowDispatcher(WindowDispatcherQueue queue)
    {
        _queue = queue;
        _context = new PumpContext(this, false);
        _cleanupContext = new PumpContext(this, true);
    }
    public bool CheckAccess() => Environment.CurrentManagedThreadId == _owner;
    public void VerifyAccess()
    {
        if (!CheckAccess()) throw new InvalidOperationException("Window construction, Run, tree access and disposal require the owning UI thread. Use Window.Dispatcher.InvokeAsync.");
    }
    internal IDisposable EnterScope(bool cleanup = false)
    {
        VerifyAccess();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(cleanup ? _cleanupContext : _context);
        return new Scope(previous);
    }
    private sealed class Scope(SynchronizationContext? previous) : IDisposable
    {
        public void Dispose() => SynchronizationContext.SetSynchronizationContext(previous);
    }
    private sealed class PumpContext(WindowDispatcher owner, bool cleanup) : SynchronizationContext
    {
        public override SynchronizationContext CreateCopy() => this;
        public override void Post(SendOrPostCallback d, object? state)
        {
            lock (owner._gate)
            {
                if (owner._closed || (owner._stopping && !cleanup)) return;
                owner._queue.TryPost(() => { using var scope = owner.EnterScope(cleanup); d(state); }, cleanup);
            }
        }
        public override void Send(SendOrPostCallback d, object? state)
        {
            owner.VerifyAccess();
            using var scope = owner.EnterScope(cleanup);
            d(state);
        }
    }
    public Task InvokeAsync(Action work)
    {
        ArgumentNullException.ThrowIfNull(work);
        return InvokeAsync<object?>(() => { work(); return (object?)null; });
    }
    public Task<T> InvokeAsync<T>(Func<T> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        return InvokeAsync<T>(() => Task.FromResult(work()));
    }
    public Task InvokeAsync(Func<Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        return InvokeAsync<object?>(() => AwaitWork(work));
    }
    // This bridge has no UI work after the await. Observe a returned Task even
    // when its captured UI context has stopped accepting continuations.
    private static async Task<object?> AwaitWork(Func<Task> work) { await work().ConfigureAwait(false); return null; }
    public Task<T> InvokeAsync<T>(Func<Task<T>> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool cleanup = CheckAccess() && ReferenceEquals(SynchronizationContext.Current, _cleanupContext);
        lock (_gate)
        {
            if (_closed || (_stopping && !cleanup)) return Task.FromCanceled<T>(new CancellationToken(true));
            _pending.Add(completion.Task, () => completion.TrySetCanceled());
        }
        void Finish(Task<T> task)
        {
            try { completion.TrySetResult(task.GetAwaiter().GetResult()); }
            catch (OperationCanceledException) { completion.TrySetCanceled(); }
            catch (Exception ex) { completion.TrySetException(ex); }
            finally { lock (_gate) _pending.Remove(completion.Task); }
        }
        void Execute()
        {
            if (completion.Task.IsCompleted) return;
            try
            {
                using var scope = EnterScope(cleanup);
                var task = work() ?? throw new InvalidOperationException("Dispatcher delegate returned a null Task.");
                if (task.IsCompleted) Finish(task);
                else _ = task.ContinueWith(Finish, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
            catch (Exception ex) { Finish(Task.FromException<T>(ex)); }
        }
        if (CheckAccess()) Execute();
        else if (!_queue.TryPost(Execute, cleanup)) Finish(Task.FromCanceled<T>(new CancellationToken(true)));
        return completion.Task;
    }
    internal void BeginShutdown()
    {
        VerifyAccess();
        lock (_gate)
        {
            _stopping = true;
            foreach (var cancel in _pending.Values) cancel();
            _pending.Clear();
            _queue.StopAccepting();
        }
    }
    internal void Close()
    {
        lock (_gate)
        {
            _closed = true;
            foreach (var cancel in _pending.Values) cancel();
            _pending.Clear();
            _queue.Close();
        }
    }
}
