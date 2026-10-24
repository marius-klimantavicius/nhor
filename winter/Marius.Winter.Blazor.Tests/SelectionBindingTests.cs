using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Xunit;
using B = Marius.Winter.Blazor.Elements;

namespace Marius.Winter.Blazor.Tests;

[Collection("Native rendering")]
public class SelectionBindingTests
{
    static RenderFragment Control<T>(params (string Name, object? Value)[] attributes) where T : IComponent => b =>
    {
        b.OpenComponent<T>(0);
        foreach (var attribute in attributes) b.AddAttribute(1, attribute.Name, attribute.Value);
        b.CloseComponent();
    };

    [Fact]
    public async Task CompiledRazor_ValueAndTabBindingsResetRejectClearAndPreserveKeyedIdentity()
    {
        using var renderer = new TestRenderer(); using var root = new Panel();
        var probe = await renderer.AddComponent<SelectionBindingProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var select = Assert.IsType<Select<SelectionBindingProbe.Choice, string>>(root.Children[0]);
        var tabs = Assert.IsType<TabWidget>(root.Children[1]); var details = tabs.ActiveContent;
        Assert.Equal(1, select.SelectedIndex); Assert.Equal(1, tabs.ActiveTab);
        await renderer.Dispatcher.InvokeAsync(() => { select.SelectFromUser(2); tabs.SelectFromUser(2); });
        Assert.Equal("c", probe.Selected); Assert.Equal(2, probe.Active);
        Assert.Single(probe.Values); Assert.Single(probe.Activations);
        await renderer.Dispatcher.InvokeAsync(probe.Reset);
        Assert.Equal("b", select.Value); Assert.Equal(1, tabs.ActiveTab);
        await renderer.Dispatcher.InvokeAsync(probe.Reorder);
        Assert.Same(details, tabs.ActiveContent); Assert.Equal(2, tabs.ActiveTab);
        Assert.Equal("b", select.Value); Assert.Equal(1, select.SelectedIndex);
        Assert.Single(probe.Values); Assert.Single(probe.Activations);
        probe.Reject = true; renderer.AttributeUpdates.Clear();
        await renderer.Dispatcher.InvokeAsync(() => { select.SelectFromUser(0); tabs.SelectFromUser(0); });
        Assert.Equal("b", select.Value); Assert.Equal(1, tabs.ActiveTab);
        Assert.Contains(renderer.AttributeUpdates, a => a.Name == "SelectedIndex" && Equals(a.Value, "1"));
        Assert.Contains(renderer.AttributeUpdates, a => a.Name == "ActiveTab" && Equals(a.Value, "1"));
        await renderer.Dispatcher.InvokeAsync(probe.Clear);
        Assert.Equal(-1, select.SelectedIndex); Assert.Equal(-1, tabs.ActiveTab);
        Assert.All(tabs.Children, child => Assert.False(child.Visible));
        await renderer.Dispatcher.InvokeAsync(() => probe.ReplaceOptions());
        Assert.Empty(select.Items!); Assert.Equal(-1, select.SelectedIndex);
        Assert.Equal(2, probe.Values.Count); Assert.Equal(2, probe.Activations.Count);
    }

    [Fact]
    public async Task AwaitedBindings_PreserveSnapshotsAndDoNotReviveResetOrUnmountedControls()
    {
        using var renderer = new TestRenderer(); using var root = new Panel();
        var probe = await renderer.AddComponent<SelectionBindingProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var select = (Select<SelectionBindingProbe.Choice, string>)root.Children[0]; var tabs = (TabWidget)root.Children[1];
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var snapshots = new List<string>();
        probe.AfterValue = async value => { await gate.Task; snapshots.Add("value:" + value); if (snapshots.Count == 2) done.SetResult(); };
        probe.AfterTab = async value => { await gate.Task; snapshots.Add("tab:" + value); if (snapshots.Count == 2) done.SetResult(); };
        await renderer.Dispatcher.InvokeAsync(() => { select.SelectFromUser(0); tabs.SelectFromUser(2); });
        await renderer.Dispatcher.InvokeAsync(probe.RenderAgain);
        Assert.Equal("a", select.Value); Assert.Equal(2, tabs.ActiveTab);
        await renderer.Dispatcher.InvokeAsync(probe.Reset);
        await renderer.Dispatcher.InvokeAsync(probe.Remove);
        gate.SetResult(); await done.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { "tab:2", "value:a" }, snapshots.Order());
        Assert.Empty(root.Children); Assert.Equal("b", probe.Selected); Assert.Equal(1, probe.Active);
        select.SelectFromUser(1); tabs.SelectFromUser(0);
        Assert.Single(probe.Values); Assert.Single(probe.Activations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UncontrolledTabs_KeepSelectionOnOrdinaryRendersAndKeyedMutation(bool removeActive)
    {
        using var renderer = new TestRenderer(); using var root = new Panel();
        RenderFragment View(string[] keys) => b =>
        {
            b.OpenComponent<B.TabWidget>(0); b.AddAttribute(1, "ChildContent", (RenderFragment)(c =>
            {
                foreach (var key in keys)
                { c.OpenComponent<B.Tab>(0); c.SetKey(key); c.AddAttribute(1, "Label", key); c.CloseComponent(); }
            })); b.CloseComponent();
        };
        var host = await renderer.AddComponent<InputBindingTests.Host>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Content"] = View(["a", "b", "c"]) });
        var native = (TabWidget)Assert.Single(root.Children); native.ActiveTab = 1; var selected = native.ActiveContent;
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(["a", "b", "c"])));
        Assert.Same(selected, native.ActiveContent);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(removeActive ? ["c", "a"] : ["c", "a", "b"])));
        Assert.Equal(removeActive ? 0 : 2, native.ActiveTab);
        if (!removeActive) Assert.Same(selected, native.ActiveContent);
    }

    [Fact]
    public async Task DeclarativeDisabledAndHiddenTabs_FallBackAndStayExcludedFromNavigation()
    {
        using var renderer = new TestRenderer(); using var root = new Panel();
        var probe = await renderer.AddComponent<SelectionBindingProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var tabs = (TabWidget)root.Children[1]; var first = tabs.Children[0];
        await renderer.Dispatcher.InvokeAsync(() => probe.ReplaceTabs(new("general", "General"), new("details", "Details", false), new("advanced", "Advanced", true, false)));
        Assert.Same(first, tabs.ActiveContent);
        tabs.OnKeyDown(269, 0, false); Assert.Same(first, tabs.ActiveContent);
        await renderer.Dispatcher.InvokeAsync(() => probe.ReplaceTabs(new SelectionBindingProbe.Page("general", "General", false)));
        Assert.Equal(-1, tabs.ActiveTab); Assert.False(first.Visible);
        await renderer.Dispatcher.InvokeAsync(() => probe.ReplaceTabs(new SelectionBindingProbe.Page("general", "General")));
        Assert.Equal(0, tabs.ActiveTab); Assert.True(first.Visible); Assert.Empty(probe.Activations);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CallbackReplacementAndClear_InvalidateQueuedIdentityAndLegacyIsFallbackOnly(bool tabs, bool clear)
    {
        using var renderer = new TestRenderer();
        using var window = new Window(300, 200, "C02 queued subscription");
        var root = new Panel(); window.AddChild(root);
        var old = 0; var replacement = 0; var legacy = 0;
        object Callback(Action action) => tabs ? EventCallback.Factory.Create<int>(this, _ => action()) : EventCallback.Factory.Create<string>(this, _ => action());
        object Empty() => tabs ? default(EventCallback<int>) : default(EventCallback<string>);
        RenderFragment View(object changed) => tabs
            ? Control<B.TabWidget>(("ActiveTab", 0), ("ActiveTabChanged", changed), ("OnActiveTabChanged", Callback(() => legacy++)), ("ChildContent", (RenderFragment)(b =>
            { for (int i = 0; i < 3; i++) b.AddContent(0, Control<B.Tab>(("Label", "Page " + i))); })))
            : Control<B.Select<string, string>>(("Items", new[] { "a", "b", "c" }), ("TextSelector", (Func<string, string>)(s => s)), ("ValueSelector", (Func<string, string>)(s => s)), ("Value", "a"), ("ValueChanged", changed), ("OnValueChanged", Callback(() => legacy++)));
        var host = await renderer.AddComponent<InputBindingTests.Host>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Content"] = View(Callback(() => old++)) });
        var native = Assert.Single(root.Children);
        void Change(int index) { if (tabs) ((TabWidget)native).SelectFromUser(index); else ((ComboBox)native).SelectFromUser(index); }
        void Drain() { while (window.DispatcherQueue.TryDequeue(out var action)) action(); }
        await renderer.Dispatcher.InvokeAsync(() => Change(1));
        Assert.Equal(0, old);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(clear ? Empty() : Callback(() => replacement++))));
        await renderer.Dispatcher.InvokeAsync(Drain); Assert.Equal(0, old); Assert.Equal(0, replacement); Assert.Equal(0, legacy);
        await renderer.Dispatcher.InvokeAsync(() => { Change(2); Drain(); });
        Assert.Equal(clear ? 0 : 1, replacement); Assert.Equal(clear ? 1 : 0, legacy); Assert.Equal(0, old);
    }

    [Fact]
    public async Task QueuedTypedValueAndTabIndex_AreSupersededByExplicitParentReset()
    {
        using var renderer = new TestRenderer(); using var window = new Window(300, 200, "C02 queued reset");
        var root = new Panel(); window.AddChild(root);
        var probe = await renderer.AddComponent<SelectionBindingProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var select = (Select<SelectionBindingProbe.Choice, string>)root.Children[0]; var tabs = (TabWidget)root.Children[1];
        await renderer.Dispatcher.InvokeAsync(() => { select.SelectFromUser(2); tabs.SelectFromUser(2); });
        Assert.Empty(probe.Values); Assert.Empty(probe.Activations);
        await renderer.Dispatcher.InvokeAsync(probe.Reset);
        await renderer.Dispatcher.InvokeAsync(() => { while (window.DispatcherQueue.TryDequeue(out var action)) action(); });
        Assert.Equal("b", select.Value); Assert.Equal(1, tabs.ActiveTab); Assert.Empty(probe.Values); Assert.Empty(probe.Activations);
    }

    [Fact]
    public async Task UnrelatedRerender_DoesNotDropAnUndeliveredTypedChoice()
    {
        using var renderer = new TestRenderer(); using var window = new Window(300, 200, "C02 queued ordinary render");
        var root = new Panel(); window.AddChild(root);
        var probe = await renderer.AddComponent<SimpleSelectionProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var select = (Select<string, string>)root.Children[0];
        await renderer.Dispatcher.InvokeAsync(() => select.SelectFromUser(2));
        renderer.AttributeUpdates.Clear();
        await renderer.Dispatcher.InvokeAsync(probe.RenderAgain);
        Assert.DoesNotContain(renderer.AttributeUpdates, a => a.Name == "Selection");
        Assert.DoesNotContain(renderer.AttributeUpdates, a => a.Name == "onvaluechanged");
        await renderer.Dispatcher.InvokeAsync(() => { while (window.DispatcherQueue.TryDequeue(out var action)) action(); });
        Assert.Equal(new[] { "c" }, probe.Values); Assert.Equal("c", probe.Selected); Assert.Equal("c", select.Value);
        var simple = (Select<string, string>)root.Children[1];
        await renderer.Dispatcher.InvokeAsync(() =>
        {
            simple.SelectFromUser(2);
            while (window.DispatcherQueue.TryDequeue(out var action)) action();
        });
        Assert.Equal("c", probe.SimpleSelected); Assert.Equal("c", simple.Value);
    }
}
