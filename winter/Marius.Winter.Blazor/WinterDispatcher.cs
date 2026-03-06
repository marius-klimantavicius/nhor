using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Marius.Winter.Blazor;

internal class WinterDispatcher : Dispatcher
{
    private readonly Window _window;
    private readonly int _mainThreadId;

    public WinterDispatcher(Window window)
    {
        _window = window;
        _mainThreadId = Environment.CurrentManagedThreadId;
    }

    public override bool CheckAccess()
    {
        return Environment.CurrentManagedThreadId == _mainThreadId;
    }

    public override Task InvokeAsync(Action workItem)
    {
        if (CheckAccess())
        {
            workItem();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource();
        _window.DispatcherQueue.Enqueue(() =>
        {
            try { workItem(); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    public override Task InvokeAsync(Func<Task> workItem)
    {
        if (CheckAccess())
        {
            return workItem();
        }

        var tcs = new TaskCompletionSource();
        _window.DispatcherQueue.Enqueue(async () =>
        {
            try { await workItem(); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    public override Task<TResult> InvokeAsync<TResult>(Func<TResult> workItem)
    {
        if (CheckAccess())
        {
            return Task.FromResult(workItem());
        }

        var tcs = new TaskCompletionSource<TResult>();
        _window.DispatcherQueue.Enqueue(() =>
        {
            try { tcs.SetResult(workItem()); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    public override Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> workItem)
    {
        if (CheckAccess())
        {
            return workItem();
        }

        var tcs = new TaskCompletionSource<TResult>();
        _window.DispatcherQueue.Enqueue(async () =>
        {
            try { tcs.SetResult(await workItem()); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }
}
