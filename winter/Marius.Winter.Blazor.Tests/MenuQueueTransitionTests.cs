using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Marius.Winter.Blazor.Tests;

[Collection("Native rendering")]
public class MenuQueueTransitionTests
{
    private static void Drain(Window window) { while (window.DispatcherQueue.TryDequeue(out var action)) action(); }

    [Theory]
    [InlineData(false, false, "back")] [InlineData(true, false, "back")]
    [InlineData(false, true, "back")] [InlineData(true, true, "back")]
    [InlineData(false, false, "replace")] [InlineData(true, false, "replace")]
    [InlineData(false, true, "replace")] [InlineData(true, true, "replace")]
    [InlineData(false, false, "clear")] [InlineData(true, false, "clear")]
    [InlineData(false, true, "clear")] [InlineData(true, true, "clear")]
    public async Task PhysicalContextReplacementCannotReviveQueuedOrAwaitingDescendants(bool nested, bool awaiting, string transition)
    {
        using var window = new Window(400, 250, "C09 physical context lifetime");
        using var renderer = new TestRenderer(); var root = new Panel(); window.AddChild(root);
        var probe = await renderer.AddComponent<MenuQueuedTransitionProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var owner = root.Children.First(); var original = owner.ContextMenu!;
        var item = nested ? original.Entries[1].SubMenu!.Entries[0] : original.Entries[0];
        var gate = new TaskCompletionSource();
        if (awaiting) probe.Model.Gate = gate;
        await renderer.Dispatcher.InvokeAsync(() =>
        {
            item.Action!();
            if (awaiting) Drain(window);
            owner.ContextMenu = transition == "clear" ? null : new Menu().AddItem("Replacement");
            if (transition == "back") owner.ContextMenu = original;
            Drain(window);
            gate.SetResult();
        });
        Assert.Equal(awaiting ? new[] { true } : Array.Empty<bool>(), probe.Model.Checks);
        Assert.Equal(0, probe.Model.Clicks);
        // A fresh activation belongs to the newly restored attachment.
        probe.Model.Gate = null;
        await renderer.Dispatcher.InvokeAsync(() => { owner.ContextMenu = original; item.Action!(); Drain(window); });
        Assert.Equal(1, probe.Model.Clicks);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ChangedParentStateInvalidatesAwaitingActionEvenAfterChangingBack(bool context)
    {
        using var window = new Window(400, 250, "C09 awaiting parent authority");
        using var renderer = new TestRenderer(); var root = new Panel(); window.AddChild(root);
        var probe = await renderer.AddComponent<MenuQueuedTransitionProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var item = context ? root.Children.First().ContextMenu!.Entries[0] : ((MenuBar)root.Children.Last()).Menus[0].Entries[0];
        var gate = probe.Model.Gate = new TaskCompletionSource();
        await renderer.Dispatcher.InvokeAsync(() => { item.Action!(); Drain(window); });
        Assert.Equal(new[] { true }, probe.Model.Checks);
        await renderer.Dispatcher.InvokeAsync(() => { probe.ParentSet(false); Assert.False(item.IsChecked); probe.ParentSet(true); gate.SetResult(); });
        Assert.True(item.IsChecked); Assert.True(probe.Model.Checked); Assert.Equal(0, probe.Model.Clicks);
    }

    [Fact]
    public async Task RejectedCheckSupersedesLaterQueuedProposalsWithoutClickOrEcho()
    {
        using var window = new Window(400, 250, "C09 rejected proposals");
        using var renderer = new TestRenderer(); var root = new Panel(); window.AddChild(root);
        var probe = await renderer.AddComponent<MenuQueuedTransitionProbe>(TestRenderer.CreateRootHandler(renderer, root));
        probe.Model.Reject = true;
        var item = ((MenuBar)root.Children.Last()).Menus[0].Entries[0];
        await renderer.Dispatcher.InvokeAsync(() => { item.Action!(); item.Action!(); Drain(window); });
        Assert.Equal(new[] { true }, probe.Model.Checks); Assert.Equal(0, probe.Model.Clicks);
        Assert.False(probe.Model.Checked); Assert.False(item.IsChecked);
    }

    [Fact]
    public async Task SameContextAttachmentLiveUpdatesPreserveEveryCheckedAction()
    {
        using var window = new Window(400, 250, "C09 live context feedback");
        using var renderer = new TestRenderer(); var root = new Panel(); window.AddChild(root);
        var probe = await renderer.AddComponent<MenuQueuedTransitionProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var owner = root.Children.First(); var attachment = owner.ContextMenu!; var item = attachment.Entries[0];
        await renderer.Dispatcher.InvokeAsync(() => { item.Action!(); item.Action!(); probe.RenderAgain(); Drain(window); });
        Assert.Same(attachment, owner.ContextMenu); Assert.Equal("Detailed preview", item.Label);
        Assert.Equal(new[] { true, false }, probe.Model.Checks); Assert.Equal(2, probe.Model.Clicks);
    }

    [Fact]
    public async Task ChangingAMenuBarsOwnContextDoesNotInvalidateItsBarCommand()
    {
        using var window = new Window(400, 250, "C09 distinct bar context identity");
        using var renderer = new TestRenderer(); var root = new Panel(); window.AddChild(root);
        var probe = await renderer.AddComponent<MenuQueuedTransitionProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var bar = (MenuBar)root.Children.Last();
        await renderer.Dispatcher.InvokeAsync(() => { bar.Menus[0].Entries[0].Action!(); bar.ContextMenu = new Menu().AddItem("Help"); Drain(window); });
        Assert.Equal(new[] { true }, probe.Model.Checks); Assert.Equal(1, probe.Model.Clicks);
    }
}
