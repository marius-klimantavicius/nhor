using System;
using Microsoft.Extensions.DependencyInjection;

namespace Marius.Winter.Blazor;

public static class WindowExtensions
{
    public static WinterBlazorHost UseBlazor(this Window window, Action<IServiceCollection>? configureServices = null)
    {
        window.Dispatcher.VerifyAccess();
        var services = new ServiceCollection();
        services.AddSingleton(window);
        configureServices?.Invoke(services);
        var serviceProvider = services.BuildServiceProvider();

        var host = new WinterBlazorHost(window, window, serviceProvider);

        return host;
    }
}
