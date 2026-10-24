using System;
using System.Collections.Generic;
using Marius.Winter.Blazor;
using Microsoft.AspNetCore.Components;
using Xunit;
using B = Marius.Winter.Blazor.Elements;

namespace Marius.Winter.Tests;

public class SplitPaneQueuedBindingTests
{
    [Fact]
    public void QueuedScalarEditsCoalesceAndCannotOverwriteParentResetOrDisposedIdentity()
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(600, 350, "SplitPane queued binding", Theme.Light, RenderBackend.SW);
            var host = window.UseBlazor();
            int calls = 0;
            float last = 0;
            var mounted = host.AddComponent<B.SplitPane>(new()
            {
                ["Position"] = 180f,
                ["PositionChanged"] = EventCallback.Factory.Create<float>(this, value => { calls++; last = value; })
            });
            Assert.True(mounted.IsCompletedSuccessfully);
            WindowRegressionOwner.Flush(window);
            var split = Assert.IsType<SplitPane>(Assert.Single(window.Children));
            var divider = Assert.Single(split.Children, e => e.Focusable);
            divider.OnKeyDown(262, 0, false);
            divider.OnKeyDown(262, 0, false);
            Assert.Equal(0, calls);
            Assert.True(mounted.Result.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?> { ["Position"] = 250f })).IsCompletedSuccessfully);
            Drain(window);
            Assert.Equal(0, calls);
            Assert.Equal(250, split.Position);
            divider.OnKeyDown(262, 0, false);
            divider.OnKeyDown(262, 0, false);
            Drain(window);
            Assert.Equal(1, calls);
            Assert.Equal(270, last);
            divider.OnKeyDown(262, 0, false);
            // Renderer disposal unregisters event identities before a queued UI
            // scalar notification can target an obsolete component.
            var disposal = host.DisposeAsync();
            Drain(window);
            Assert.True(disposal.IsCompletedSuccessfully);
            Assert.Equal(1, calls);
            Assert.True(split.IsDisposed);
        });
    }

    private static void Drain(Window window)
    {
        int actions = 0;
        while (window.DispatcherQueue.TryDequeue(out var action))
        {
            Assert.True(++actions < 100, "Unexpected binding render loop.");
            action();
        }
    }
}
