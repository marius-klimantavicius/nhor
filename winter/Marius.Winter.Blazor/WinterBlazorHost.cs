using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Marius.Winter.Blazor;

public class WinterBlazorHost
{
    private readonly WinterRenderer _renderer;
    private readonly Window _window;
    private readonly Element _rootContainer;

    internal WinterBlazorHost(Window window, Element rootContainer, IServiceProvider serviceProvider)
    {
        _window = window;
        _rootContainer = rootContainer;
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>() ?? LoggerFactory.Create(_ => { });
        _renderer = new WinterRenderer(window, serviceProvider, loggerFactory);
    }

    public WinterRenderer Renderer => _renderer;

    public Task<TComponent> AddComponent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent>() where TComponent : IComponent
    {
        return _renderer.AddComponent<TComponent>(_rootContainer);
    }

    public Task<TComponent> AddComponent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent>(Dictionary<string, object> parameters) where TComponent : IComponent
    {
        return _renderer.AddComponent<TComponent>(_rootContainer, parameters);
    }
}
