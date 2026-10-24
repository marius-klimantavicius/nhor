using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Marius.Winter.Blazor.Tests;
[Collection("Native rendering")]
public class MenuBindingAuthorityTests
{
    private static void Drain(Window w) { while (w.DispatcherQueue.TryDequeue(out var action)) action(); }
    [Theory]
    [InlineData("default")] [InlineData("getset")] [InlineData("model")]
    public async Task OrdinaryRenderPreservesTwoCheckedActions(string mode)
    {
        using var w = new Window(400, 250, "C09 independent compiled binding");
        using var renderer = new TestRenderer(); var root = new Panel(); w.AddChild(root);
        var p = await renderer.AddComponent<MenuBindingAuthorityProbe>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Mode"] = mode });
        var item = ((MenuBar)root.Children.Single()).Menus[0].Entries[0];
        await renderer.Dispatcher.InvokeAsync(() => { item.Action!(); item.Action!(); p.RenderAgain(); Drain(w); });
        var checks = mode == "model" ? p.Model.Checks : p.Checks;
        Assert.Equal(new[] { true, false }, checks);
        Assert.Equal(2, mode == "model" ? p.Model.Clicks : p.Clicks);
        Assert.False(item.IsChecked);
        if (mode == "model") Assert.Equal(new[] { "check:True", "click:True", "check:False", "click:False" }, p.Model.Trace);
    }

    [Theory]
    [InlineData("default")] [InlineData("getset")] [InlineData("model")]
    public async Task ChangedParentValueThenChangedBackDoesNotReviveObsoleteActions(string mode)
    {
        using var w = new Window(400, 250, "C09 changed parent and back");
        using var renderer = new TestRenderer(); var root = new Panel(); w.AddChild(root);
        var p = await renderer.AddComponent<MenuBindingAuthorityProbe>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Mode"] = mode });
        var item = ((MenuBar)root.Children.Single()).Menus[0].Entries[0];
        await renderer.Dispatcher.InvokeAsync(() => { item.Action!(); item.Action!(); p.ParentSet(true); Assert.True(item.IsChecked); p.ParentSet(false); Drain(w); });
        Assert.Empty(mode == "model" ? p.Model.Checks : p.Checks);
        Assert.Equal(0, mode == "model" ? p.Model.Clicks : p.Clicks);
        Assert.False(item.IsChecked);
    }
    [Theory]
    [InlineData("default")] [InlineData("getset")] [InlineData("model")]
    public async Task ChangedParentCheckedStateSupersedesPendingUserActions(string mode)
    {
        using var w = new Window(400, 250, "C09 independent parent state");
        using var renderer = new TestRenderer(); var root = new Panel(); w.AddChild(root);
        var p = await renderer.AddComponent<MenuBindingAuthorityProbe>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Mode"] = mode });
        var item = ((MenuBar)root.Children.Single()).Menus[0].Entries[0];
        await renderer.Dispatcher.InvokeAsync(() => { item.Action!(); item.Action!(); p.ParentSet(true); });
        Assert.True(item.IsChecked);
        await renderer.Dispatcher.InvokeAsync(() => Drain(w));
        Assert.Empty(mode == "model" ? p.Model.Checks : p.Checks);
        Assert.True(mode == "model" ? p.Model.Checked : p.Checked);
        Assert.True(item.IsChecked);
    }
    [Theory]
    [InlineData("default")] [InlineData("getset")] [InlineData("model")]
    public async Task ExplicitResetCancelsPendingActions(string mode)
    {
        using var w = new Window(400, 250, "C09 independent reset");
        using var renderer = new TestRenderer(); var root = new Panel(); w.AddChild(root);
        var p = await renderer.AddComponent<MenuBindingAuthorityProbe>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Mode"] = mode });
        var item = ((MenuBar)root.Children.Single()).Menus[0].Entries[0];
        await renderer.Dispatcher.InvokeAsync(() => { item.Action!(); p.Reset(); Drain(w); });
        Assert.Empty(mode == "model" ? p.Model.Checks : p.Checks);
        Assert.False(item.IsChecked);
    }
}
