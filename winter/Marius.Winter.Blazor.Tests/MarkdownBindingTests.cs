using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Xunit;
using B = Marius.Winter.Blazor.Elements;
using static Glfw.GLFW;

namespace Marius.Winter.Blazor.Tests;

public class MarkdownBindingTests
{
    private static RenderFragment View(string source, EventCallback<string> callback, float? size = null, Func<string, MarkdownImage?>? resolver = null) => b =>
    {
        b.OpenComponent<B.MarkdownView>(0); b.AddAttribute(1, "Markdown", source);
        b.AddAttribute(2, "FontSize", size); b.AddAttribute(3, "ImageResolver", resolver); b.AddAttribute(4, "LinkClicked", callback); b.CloseComponent();
    };

    [Fact]
    public async Task RealDiffsUpdateSourceFontAndResolverAndClearRemovedParameters()
    {
        using var renderer = new TestRenderer(); using var root = new Panel();
        Func<string, MarkdownImage?> resolver = _ => null;
        var host = await renderer.AddComponent<InputBindingTests.Host>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Content"] = View("# First", default, 22, resolver) });
        var control = Assert.IsType<MarkdownView>(Assert.Single(root.Children));
        Assert.Equal("# First", control.Markdown); Assert.Equal(22, control.FontSize); Assert.Same(resolver, control.ImageResolver);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View("**Second**", default)));
        Assert.Equal("**Second**", control.Markdown); Assert.Equal(control.Style.FontSize, control.FontSize); Assert.Null(control.ImageResolver);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View("", default)));
        Assert.Equal("", control.Markdown);
    }

    [Fact]
    public async Task CallbackReplacementClearAndRemovalApplyThroughRealRendererDiffs()
    {
        using var renderer = new TestRenderer(); using var root = new Panel();
        var deliveries = new List<string>();
        EventCallback<string> Callback(string prefix) => EventCallback.Factory.Create<string>(this, url => deliveries.Add(prefix + url));
        var host = await renderer.AddComponent<InputBindingTests.Host>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Content"] = View("[link](action:one)", Callback("old:")) });
        var control = (MarkdownView)root.Children[0];
        await renderer.Dispatcher.InvokeAsync(() => { control.OnFocus(); control.OnKeyDown(GLFW_KEY_ENTER, 0, false); });
        Assert.Equal(new[] { "old:action:one" }, deliveries);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View("[link](action:two)", Callback("new:"))));
        await renderer.Dispatcher.InvokeAsync(() => { control.OnFocus(); control.OnKeyDown(GLFW_KEY_ENTER, 0, false); });
        Assert.Equal("new:action:two", deliveries[1]);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View("[link](action:two)", default)));
        await renderer.Dispatcher.InvokeAsync(() => control.OnKeyDown(GLFW_KEY_ENTER, 0, false)); Assert.Equal(2, deliveries.Count);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(_ => { }));
        Assert.Empty(root.Children); Assert.True(control.IsDisposed);
        control.OnKeyDown(GLFW_KEY_ENTER, 0, false); Assert.Equal(2, deliveries.Count);
    }

    [Fact]
    public async Task AwaitedCallbackRetainsDestinationWithoutRestoringReplacedSourceOrDisposedControl()
    {
        using var renderer = new TestRenderer(); using var root = new Panel();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var complete = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callback = EventCallback.Factory.Create<string>(this, async url => { await release.Task; complete.SetResult(url); });
        var host = await renderer.AddComponent<InputBindingTests.Host>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Content"] = View("[link](action:snapshot)", callback) });
        var control = (MarkdownView)root.Children[0];
        await renderer.Dispatcher.InvokeAsync(() => { control.OnFocus(); control.OnKeyDown(GLFW_KEY_ENTER, 0, false); });
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View("replacement", default)));
        Assert.Equal("replacement", control.Markdown);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(_ => { }));
        release.SetResult(); Assert.Equal("action:snapshot", await complete.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(control.IsDisposed); Assert.Empty(root.Children);
    }
}
