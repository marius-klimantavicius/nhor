using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;

namespace Marius.Winter.Blazor.Tests;

public sealed record TreeBindingItem(string Key, string Name, TreeBindingItem[]? Children = null);

[Collection("Native rendering")]
public class DataTreeBindingTests
{
    [Fact]
    public async Task ConventionalCallbacksWinClearToLegacyAndSynchronizationHasNoEcho()
    {
        using var renderer = new TestRenderer(); var root = new Panel();
        int standard = 0, legacy = 0;
        var paired = EventCallback.Factory.Create<string?>(this, _ => standard++);
        var fallback = EventCallback.Factory.Create<string?>(this, _ => legacy++);
        RenderFragment View(bool changed, string? selected) => b =>
        {
            b.OpenComponent<Elements.DataTree<TreeBindingItem>>(0);
            b.AddAttribute(1, "Items", new TreeBindingItem[] { new("a", "same"), new("b", "same") });
            b.AddAttribute(2, "KeySelector", TreeBindingProbe.Key); b.AddAttribute(3, "TextSelector", TreeBindingProbe.Text);
            b.AddAttribute(4, "SelectedKey", selected); b.AddAttribute(5, "SelectedKeyChanged", changed ? paired : default);
            b.AddAttribute(6, "OnSelectionChanged", fallback); b.CloseComponent();
        };
        var host = await renderer.AddComponent<InputBindingTests.Host>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Content"] = View(true, "a") });
        var tree = (DataTree<TreeBindingItem>)root.Children[0];
        Assert.Equal(0, standard + legacy);
        await renderer.Dispatcher.InvokeAsync(() => tree.Controller!.Select("b"));
        Assert.Equal(1, standard); Assert.Equal(0, legacy);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(false, "a")));
        Assert.Equal("a", tree.SelectedKey); Assert.Equal(1, standard + legacy);
        await renderer.Dispatcher.InvokeAsync(() => tree.Controller!.Select("b"));
        Assert.Equal(1, standard); Assert.Equal(1, legacy);
    }

    [Fact]
    public async Task CompiledBindingsAcceptRejectResetAndRetainDataIdentityThroughRealDiffs()
    {
        using var renderer = new TestRenderer(); var root = new Panel();
        var probe = await renderer.AddComponent<TreeBindingProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var tree = Assert.IsType<DataTree<TreeBindingItem>>(Assert.Single(root.Children));
        var original = tree.Nodes[0];
        await renderer.Dispatcher.InvokeAsync(() => tree.ExpandAsync("p"));
        Assert.Contains("p", probe.Expanded);
        await renderer.Dispatcher.InvokeAsync(() => tree.Controller!.Select("b"));
        Assert.Equal("b", probe.Selected);
        var row = tree.Nodes[0].Children[1];
        renderer.AttributeUpdates.Clear();
        await renderer.Dispatcher.InvokeAsync(probe.Replace);
        Assert.Same(row, tree.Nodes[0].Children[0]);
        Assert.Equal("renamed 👩🏽‍💻é", tree.SelectedNode!.Text);
        Assert.Contains(renderer.AttributeUpdates, a => a.Name == "Configuration");
        probe.RejectSelection = true;
        await renderer.Dispatcher.InvokeAsync(() => tree.Controller!.Select("a"));
        Assert.Equal("b", tree.SelectedKey);
        probe.RejectExpansion = true;
        await renderer.Dispatcher.InvokeAsync(() => tree.Collapse("q"));
        Assert.Contains("q", tree.ExpandedKeys);
        Assert.Equal(0, probe.LegacyCalls);
        await renderer.Dispatcher.InvokeAsync(probe.Reset);
        Assert.Null(tree.SelectedKey); Assert.Empty(tree.ExpandedKeys);
    }

    [Fact]
    public async Task AwaitingCallbacksDoNotOverwriteResetOrReviveRemovedView()
    {
        using var renderer = new TestRenderer(); var root = new Panel();
        var probe = await renderer.AddComponent<TreeBindingProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var tree = (DataTree<TreeBindingItem>)root.Children[0];
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        probe.WaitSelection = () => release.Task;
        await renderer.Dispatcher.InvokeAsync(() => tree.Controller!.Select("p"));
        Assert.Equal("p", probe.Selected);
        await renderer.Dispatcher.InvokeAsync(probe.Reset);
        await renderer.Dispatcher.InvokeAsync(probe.Remove);
        release.SetResult();
        await renderer.Dispatcher.InvokeAsync(() => { });
        Assert.Null(probe.Selected); Assert.Empty(root.Children); Assert.True(tree.IsDisposed);
    }

    [Fact]
    public async Task DeclarativeStringCallbacksPreserveEveryExpansionIdentityAndEscapeHatch()
    {
        using var renderer = new TestRenderer(); var window = new Window(300, 180, "Tree discrete events");
        try
        {
            var root = new Panel(); window.AddChild(root);
            var events = new List<string?>(); TreeView? native = null;
            var callback = EventCallback.Factory.Create<string?>(this, events.Add);
            await renderer.AddComponent<DeclarativeProbe>(TestRenderer.CreateRootHandler(renderer, root), new()
            {
                [nameof(DeclarativeProbe.Callback)] = callback,
                [nameof(DeclarativeProbe.Capture)] = (Action<TreeView>)(t => native = t)
            });
            Assert.NotNull(native); Assert.Equal(2, native.Nodes.Count);
            await renderer.Dispatcher.InvokeAsync(() =>
            {
                native.NodeExpanded!(native.Nodes[0]); native.NodeExpanded!(native.Nodes[1]);
                native.Nodes[0].Tag = "changed after request";
                while (window.DispatcherQueue.TryDequeue(out var work)) work();
            });
            Assert.Equal(new[] { "a", "b" }, events);
        }
        finally { renderer.Dispose(); window.Dispose(); }
    }

    public sealed class DeclarativeProbe : ComponentBase
    {
        [Parameter] public EventCallback<string?> Callback { get; set; }
        [Parameter] public Action<TreeView>? Capture { get; set; }
        protected override void BuildRenderTree(RenderTreeBuilder b)
        {
            b.OpenComponent<Elements.TreeView>(0); b.AddAttribute(1, "OnNodeExpanded", Callback); b.AddAttribute(2, "OnNativeTreeView", Capture);
            b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
            {
                c.OpenComponent<Elements.TreeViewNode>(0); c.AddAttribute(1, "Text", "same"); c.AddAttribute(2, "Tag", "a"); c.CloseComponent();
                c.OpenComponent<Elements.TreeViewNode>(3); c.AddAttribute(4, "Text", "same"); c.AddAttribute(5, "Tag", "b"); c.CloseComponent();
            })); b.CloseComponent();
        }
    }
}
