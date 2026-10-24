using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Marius.Winter.Blazor.Tests;

[Collection("Native rendering")]
public class MenuLifetimeTests
{
    [Fact]
    public async Task DisabledThenReenabledAncestorInvalidatesItsQueuedChildCommand()
    {
        using var window = new Window(400, 240, "C09 branch identity", Theme.Light);
        using var renderer = new TestRenderer(); var root = new Panel(); window.AddChild(root); int calls = 0;
        await renderer.AddComponent<MenuProbe>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Activated"] = EventCallback.Factory.Create<string>(this, _ => calls++) });
        var zoom = ((MenuBar)root.Children.Single().Children.Last()).Menus[0].Entries[3];
        await renderer.Dispatcher.InvokeAsync(() =>
        {
            zoom.SubMenu!.Entries[0].Action!.Invoke(); Assert.Equal(0, calls);
            zoom.Enabled = false; zoom.Enabled = true;
            while (window.DispatcherQueue.TryDequeue(out var action)) action();
        });
        Assert.Equal(0, calls);
    }
    [Theory]
    [InlineData("same", 2)] [InlineData("replace", 0)] [InlineData("clear", 0)]
    [InlineData("reset", 0)] [InlineData("disable", 0)] [InlineData("detach", 0)] [InlineData("context", 0)] [InlineData("menu", 0)]
    public async Task QueuedCommandsKeepDiscreteIdentityAndRejectObsoleteOwnersAndCallbacks(string transition, int expected)
    {
        using var window = new Window(400, 240, "C09 queued identity", Theme.Light, RenderBackend.SW);
        using var renderer = new TestRenderer(); var root = new Panel(); window.AddChild(root);
        var calls = new List<string>(); var callback = EventCallback.Factory.Create(this, () => calls.Add("original"));
        var probe = await renderer.AddComponent<MenuActionProbe>(TestRenderer.CreateRootHandler(renderer, root), new()
        { ["SaveEnabled"] = true, ["Action"] = callback, ["ContextAction"] = callback });
        var panel = root.Children.Single(); var bar = (MenuBar)panel.Children.Last(); var owner = panel.Children.First();
        var item = transition == "context" ? owner.ContextMenu!.Entries[0] : bar.Menus[0].Entries[0];
        await renderer.Dispatcher.InvokeAsync(() =>
        {
            item.Action!.Invoke(); item.Action!.Invoke(); Assert.Empty(calls);
            switch (transition)
            {
                case "same": probe.Rerender(); break;
                case "replace": probe.ReplaceAction(EventCallback.Factory.Create(this, () => calls.Add("replacement"))); break;
                case "clear": probe.ReplaceAction(default); break;
                case "reset": probe.Update(true, false, ["100%", "125%", "150%"], 1); break;
                case "disable": probe.Update(false, false, ["100%", "125%", "150%"]); break;
                case "detach": root.RemoveChild(panel); root.AddChild(panel); break;
                case "context": probe.Context(false); probe.Context(true); break;
                case "menu": bar.RemoveMenu(bar.Menus[0]); break;
            }
            while (window.DispatcherQueue.TryDequeue(out var action)) action();
        });
        Assert.Equal(Enumerable.Repeat("original", expected), calls);
    }

    [Fact]
    public async Task AwaitingCheckCannotStartAnActionAfterResetAndNoCompletionMutatesNewState()
    {
        using var window = new Window(400, 240, "C09 awaiting identity", Theme.Light, RenderBackend.SW);
        using var renderer = new TestRenderer(); var root = new Panel(); window.AddChild(root);
        var gate = new TaskCompletionSource();
        int checks = 0, clicks = 0;
        InputBindingTests.Host host = null!;
        RenderFragment View(long reset, bool state) => b =>
        {
            b.OpenComponent<Elements.MenuBar>(0); b.AddAttribute(1, "ChildContent", (RenderFragment)(m =>
            {
                m.OpenComponent<Elements.Menu>(0); m.AddAttribute(1, "Title", "Preview"); m.AddAttribute(2, "ChildContent", (RenderFragment)(i =>
                {
                    i.OpenComponent<Elements.MenuItem>(0); i.AddAttribute(1, "IsChecked", state); i.AddAttribute(2, "ResetVersion", reset);
                    i.AddAttribute(3, "IsCheckedChanged", EventCallback.Factory.Create<bool>(this, async _ => { checks++; await gate.Task; }));
                    i.AddAttribute(4, "OnClick", EventCallback.Factory.Create(this, () => clicks++)); i.CloseComponent();
                })); m.CloseComponent();
            })); b.CloseComponent();
        };
        host = await renderer.AddComponent<InputBindingTests.Host>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Content"] = View(0, false) });
        var item = ((MenuBar)root.Children.Single()).Menus[0].Entries[0];
        await renderer.Dispatcher.InvokeAsync(() => { item.Action!.Invoke(); while (window.DispatcherQueue.TryDequeue(out var action)) action(); });
        Assert.Equal(1, checks); Assert.Equal(0, clicks);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(1, false))); Assert.False(item.IsChecked);
        await renderer.Dispatcher.InvokeAsync(() => gate.SetResult());
        Assert.Equal(0, clicks); Assert.False(item.IsChecked);
    }
}
