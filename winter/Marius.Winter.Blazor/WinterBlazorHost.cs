using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Marius.Winter.Blazor;

public class WinterBlazorHost : IAsyncDisposable
{
    private readonly WinterRenderer _renderer;
    private readonly Window _window;
    private readonly Element _rootContainer;
    private readonly IServiceProvider _services;
    private Task? _disposeTask;

    internal WinterBlazorHost(Window window, Element rootContainer, IServiceProvider serviceProvider)
    {
        _window = window;
        _rootContainer = rootContainer;
        _services = serviceProvider;
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
        _renderer = new WinterRenderer(window, serviceProvider, loggerFactory);
        _window.Closing += DisposeAsync;
    }

    public WinterRenderer Renderer => _renderer;

    public Task<TComponent> AddComponent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent>() where TComponent : IComponent
    {
        ObjectDisposedException.ThrowIf(_disposeTask != null, this);
        return _renderer.AddComponent<TComponent>(_rootContainer);
    }

    public Task<TComponent> AddComponent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent>(Dictionary<string, object> parameters) where TComponent : IComponent
    {
        ObjectDisposedException.ThrowIf(_disposeTask != null, this);
        return _renderer.AddComponent<TComponent>(_rootContainer, parameters);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposeTask != null) return new ValueTask(_disposeTask);
        if (!_window.Dispatcher.CheckAccess())
            // Marshal only the start. The disposal task itself belongs to
            // teardown and must not be canceled with application invocations.
            return new ValueTask(_window.Dispatcher.InvokeAsync<Task>(StartDisposal).Unwrap());
        return new ValueTask(StartDisposal());
    }

    private Task StartDisposal()
    {
        if (_disposeTask != null) return _disposeTask;
        // Capture the cleanup context even when explicitly disposed before
        // Window shutdown. Closing retains ownership until this task ends.
        using var scope = _window.Dispatcher.EnterScope(cleanup: true);
        return _disposeTask = DisposeCoreAsync();
    }

    private async Task DisposeCoreAsync()
    {
        try
        {
            try { await _renderer.DisposeAsync(); }
            finally
            {
                if (_services is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync();
                else if (_services is IDisposable disposable) disposable.Dispose();
            }
        }
        finally
        {
            _window.Closing -= DisposeAsync;
        }
    }
}
