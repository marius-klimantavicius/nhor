using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Marius.Winter.Blazor;

internal sealed class WinterDispatcher(Window window) : Dispatcher
{
    public override bool CheckAccess() => window.Dispatcher.CheckAccess();
    public override Task InvokeAsync(Action workItem) => window.Dispatcher.InvokeAsync(workItem);
    public override Task InvokeAsync(Func<Task> workItem) => window.Dispatcher.InvokeAsync(workItem);
    public override Task<T> InvokeAsync<T>(Func<T> workItem) => window.Dispatcher.InvokeAsync(workItem);
    public override Task<T> InvokeAsync<T>(Func<Task<T>> workItem) => window.Dispatcher.InvokeAsync(workItem);
}
