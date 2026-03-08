using System;
using Microsoft.Extensions.DependencyInjection;

namespace Marius.Winter.Blazor;

public static class WindowExtensions
{
    public static WinterBlazorHost UseBlazor(this Window window, Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(window);
        configureServices?.Invoke(services);
        var serviceProvider = services.BuildServiceProvider();

        var host = new WinterBlazorHost(window, window, serviceProvider);

        // Re-layout Blazor-managed children when window resizes
        window.Resized += () =>
        {
            var b = window.Bounds;
            foreach (var child in window.Children)
            {
                child.Measure(b.W, b.H);
                child.Arrange(new RectF(0, 0, b.W, b.H));
            }
        };

        return host;
    }
}
