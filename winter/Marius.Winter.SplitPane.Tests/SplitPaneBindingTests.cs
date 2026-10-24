using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;
using Xunit;
using B = Marius.Winter.Blazor.Elements;

namespace Marius.Winter.Blazor.Tests;

public class SplitPaneBindingTests
{
    [Fact]
    public async Task CompiledRazorBindingResetOrientationAndSlotReplacementUseRealRenderDiffs()
    {
        using var renderer = new TestRenderer();
        using var root = new Panel();
        var host = await renderer.AddComponent<BoundSplitHost>(TestRenderer.CreateRootHandler(renderer, root));
        var split = Assert.IsType<Marius.Winter.SplitPane>(Assert.Single(root.Children));
        split.Arrange(new RectF(0, 0, 600, 400));
        Assert.Equal(180, split.Position);
        var original = split.First!.Children[0];
        var originalSecond = Assert.Single(split.Second!.Children);
        await renderer.Dispatcher.InvokeAsync(() => Divider(split).OnKeyDown(262, 0, false));
        Assert.Equal(190, host.Position);
        Assert.Same(original, split.First.Children[0]);
        int beforeReset = renderer.ConfigurationDiffs;
        host.Position = 180;
        await renderer.Dispatcher.InvokeAsync(host.Render);
        Assert.Equal(180, split.Position);
        Assert.True(renderer.ConfigurationDiffs > beforeReset);
        host.Direction = Orientation.Vertical;
        host.Replacement = true;
        await renderer.Dispatcher.InvokeAsync(host.Render);
        Assert.Same(split, Assert.Single(root.Children));
        Assert.Equal(Orientation.Vertical, split.Orientation);
        Assert.Equal(180, split.First.Bounds.H);
        Assert.IsType<Button>(Assert.Single(split.First.Children));
        Assert.True(original.IsDisposed);
        Assert.Same(originalSecond, Assert.Single(split.Second.Children));
        host.Mounted = false;
        await renderer.Dispatcher.InvokeAsync(host.Render);
        Assert.Empty(root.Children);
        Assert.True(split.IsDisposed);
    }

    [Fact]
    public async Task ChangedWinsLegacyFallbackAndSameValueRejectionAreAuthoritative()
    {
        using var renderer = new TestRenderer();
        using var root = new Panel();
        var parent = TestRenderer.CreateRootHandler(renderer, root);
        int changed = 0, legacy = 0;
        var control = await renderer.AddComponent<B.SplitPane>(parent, new()
        {
            ["Position"] = 180f,
            ["PositionChanged"] = EventCallback.Factory.Create<float>(this, _ => changed++),
            ["OnPositionChanged"] = EventCallback.Factory.Create<float>(this, _ => legacy++)
        });
        var split = Assert.IsType<Marius.Winter.SplitPane>(Assert.Single(root.Children));
        split.Arrange(new RectF(0, 0, 600, 350));
        await renderer.Dispatcher.InvokeAsync(() => Divider(split).OnKeyDown(262, 0, false));
        Assert.Equal(1, changed); Assert.Equal(0, legacy);
        await renderer.Dispatcher.InvokeAsync(() => control.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            ["Position"] = 180f // Explicit rejection equals the previous parent value.
        })));
        Assert.Equal(180, split.Position);
        Assert.Equal(1, changed);
        await renderer.Dispatcher.InvokeAsync(() => control.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            ["PositionChanged"] = default(EventCallback<float>)
        })));
        await renderer.Dispatcher.InvokeAsync(() => Divider(split).OnKeyDown(262, 0, false));
        Assert.Equal(1, changed); Assert.Equal(1, legacy);
        await renderer.Dispatcher.InvokeAsync(() => control.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            ["OnPositionChanged"] = default(EventCallback<float>)
        })));
        await renderer.Dispatcher.InvokeAsync(() => Divider(split).OnKeyDown(262, 0, false));
        Assert.Equal(1, changed); Assert.Equal(1, legacy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AwaitingCallbackCannotRestoreAnObsoletePositionAfterParentResetOrRemoval(bool removeBeforeCompletion)
    {
        using var renderer = new TestRenderer();
        using var root = new Panel();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var control = await renderer.AddComponent<B.SplitPane>(TestRenderer.CreateRootHandler(renderer, root), new()
        {
            ["Position"] = 180f,
            ["PositionChanged"] = EventCallback.Factory.Create<float>(this, (Func<float, Task>)(_ => completion.Task))
        });
        var split = Assert.IsType<Marius.Winter.SplitPane>(Assert.Single(root.Children));
        split.Arrange(new RectF(0, 0, 600, 350));
        await renderer.Dispatcher.InvokeAsync(() => Divider(split).OnKeyDown(262, 0, false));
        await renderer.Dispatcher.InvokeAsync(() => control.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?> { ["Position"] = 250f })));
        if (removeBeforeCompletion) renderer.Dispose();
        completion.SetResult();
        await renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);
        Assert.Equal(250, split.Position);
        Assert.Equal(250, control.Position);
        renderer.Dispose();
        Assert.True(split.IsDisposed);
        Assert.Null(split.PositionChanged);
    }

    private static Element Divider(Marius.Winter.SplitPane split) => Assert.Single(split.Children, c => c.Focusable);
}
