using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;
using B = Marius.Winter.Blazor.Elements;

namespace Marius.Winter.Blazor.Tests;

[Collection("Native rendering")]
public class InputBindingTests
{
    public sealed class Host : ComponentBase
    {
        [Parameter] public RenderFragment? Content { get; set; }
        protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, Content);
        public void Render(RenderFragment content) { Content = content; StateHasChanged(); }
    }

    static RenderFragment Control<T>(params (string name, object? value)[] attributes) where T : IComponent => b =>
    {
        b.OpenComponent<T>(0);
        foreach (var (name, value) in attributes) b.AddAttribute(1, name, value);
        b.CloseComponent();
    };

    [Fact]
    public async Task ProgrammaticSynchronization_DoesNotInvokeLegacyCallbacks()
    {
        using var renderer = new TestRenderer();
        var root = new Panel();
        var changes = new List<string>();
        RenderFragment View(string text, float volume, int selection) => b =>
        {
            b.AddContent(0, Control<B.TextBox>(("Text", text), ("OnTextChanged", EventCallback.Factory.Create<string>(this, v => changes.Add("text:" + v)))));
            b.AddContent(1, Control<B.TextArea>(("Text", text), ("OnTextChanged", EventCallback.Factory.Create<string>(this, v => changes.Add("area:" + v)))));
            b.AddContent(2, Control<B.Slider>(("Max", 100f), ("Value", volume), ("OnValueChanged", EventCallback.Factory.Create<float>(this, v => changes.Add("volume:" + v)))));
            b.AddContent(3, Control<B.ComboBox>(("Items", new[] { "Personal", "Studio" }), ("SelectedIndex", selection), ("OnSelectionChanged", EventCallback.Factory.Create<int>(this, v => changes.Add("selection:" + v)))));
        };
        var host = await renderer.AddComponent<Host>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Content"] = View("Milda", 40, 1) });
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View("Reset", 75, 0)));
        Assert.Equal("Reset", ((TextBox)root.Children[0]).Text);
        Assert.Equal("Reset", ((TextArea)root.Children[1]).Text);
        Assert.Equal(75, ((Slider)root.Children[2]).Value);
        Assert.Equal(0, ((ComboBox)root.Children[3]).SelectedIndex);
        Assert.Empty(changes);
    }

    [Fact]
    public async Task ParentCanRejectCheckboxChange_WithoutChangingItsValue()
    {
        using var renderer = new TestRenderer();
        var root = new Panel();
        Host host = null!;
        var calls = 0;
        RenderFragment View() => Control<B.Checkbox>(("IsChecked", false), ("OnChanged", EventCallback.Factory.Create<bool>(this, _ => { calls++; host.Render(View()); })));
        host = await renderer.AddComponent<Host>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Content"] = View() });
        var native = Assert.IsType<Checkbox>(Assert.Single(root.Children));
        await renderer.Dispatcher.InvokeAsync(native.OnClick);
        Assert.Equal(1, calls);
        Assert.False(native.IsChecked);
    }

    [Fact]
    public async Task CompiledRazorBindings_UpdateResetRejectAndClampThroughRealDiffs()
    {
        using var renderer = new TestRenderer();
        var root = new Panel();
        var probe = await renderer.AddComponent<BindingProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var text = (TextBox)root.Children[0]; var area = (TextArea)root.Children[1];
        var check = (Checkbox)root.Children[2]; var slider = (Slider)root.Children[3]; var combo = (ComboBox)root.Children[4];
        await renderer.Dispatcher.InvokeAsync(() =>
        {
            text.OnTextInput(" 👩🏽‍💻e\u0301"); area.OnTextInput("\nNotes"); check.OnClick();
            slider.SetValueFromUser(73.4f); combo.SelectFromUser(1);
        });
        Assert.Equal(text.Text, probe.Name); Assert.Equal(area.Text, probe.Notes);
        Assert.True(probe.Shortcut); Assert.Equal(73, probe.Volume); Assert.Equal(1, probe.Choice);
        await renderer.Dispatcher.InvokeAsync(probe.Reset);
        Assert.Equal("Milda", text.Text); Assert.Equal("Studio profile", area.Text);
        Assert.False(check.IsChecked); Assert.Equal(40, slider.Value); Assert.Equal(0, combo.SelectedIndex);
        Assert.Single(probe.Edits); Assert.Empty(probe.Commits);

        probe.RejectName = true;
        renderer.AttributeUpdates.Clear();
        await renderer.Dispatcher.InvokeAsync(() => text.OnTextInput(" rejected"));
        Assert.Equal("Milda", text.Text);
        Assert.Contains(renderer.AttributeUpdates, a => a.Name == "Text" && Equals(a.Value, "Milda"));
        Assert.Equal(2, probe.Edits.Count);

        await renderer.Dispatcher.InvokeAsync(probe.Clamp);
        Assert.Equal(60, slider.Value); Assert.Equal(40, probe.Volume); // Clamping is synchronization, not a user edit.
        await renderer.Dispatcher.InvokeAsync(() => slider.SetValueFromUser(75));
        Assert.Equal(75, probe.Volume);
        await renderer.Dispatcher.InvokeAsync(() => combo.SelectFromUser(1));
        await renderer.Dispatcher.InvokeAsync(probe.Expand);
        Assert.Equal(75, slider.Value); Assert.Equal(1, combo.SelectedIndex);
        await renderer.Dispatcher.InvokeAsync(probe.ClearChoices);
        Assert.Empty(combo.Items); Assert.Equal(-1, combo.SelectedIndex);
    }

    [Fact]
    public async Task AwaitingCallbacks_DoNotRestoreOlderValuesOrReviveRemovedInputs()
    {
        using var renderer = new TestRenderer();
        var root = new Panel();
        var probe = await renderer.AddComponent<BindingProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var text = (TextBox)root.Children[0];
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var snapshots = new List<string>();
        probe.AfterNameSet = async value =>
        {
            await release.Task;
            snapshots.Add(value);
            if (snapshots.Count == 2) completed.SetResult();
        };
        await renderer.Dispatcher.InvokeAsync(() => { text.OnKeyDown(269, 0, false); text.OnTextInput("1"); text.OnTextInput("2"); });
        Assert.Equal("Milda12", probe.Name);
        await renderer.Dispatcher.InvokeAsync(probe.RenderAgain);
        Assert.Equal("Milda12", text.Text);
        await renderer.Dispatcher.InvokeAsync(probe.Reset);
        Assert.Equal("Milda", text.Text);
        await renderer.Dispatcher.InvokeAsync(probe.Remove);
        Assert.Empty(root.Children);
        release.SetResult();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await renderer.Dispatcher.InvokeAsync(() => { });
        Assert.Equal(new[] { "Milda1", "Milda12" }, snapshots.Order());
        Assert.Equal("Milda", probe.Name); Assert.Empty(root.Children);
        text.OnTextInput("detached");
        Assert.Equal(2, probe.Edits.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TextCallbacks_PrecedenceReplacementClearAndNullValues(bool multiline)
    {
        using var renderer = new TestRenderer();
        var root = new Panel(); var legacy = new List<string>(); var standard = new List<string>(); var replacement = new List<string>();
        RenderFragment View(string? value, EventCallback<string> changed, EventCallback<string> fallback)
        {
            var attributes = new (string, object?)[] { ("Text", value), ("TextChanged", changed), ("OnTextChanged", fallback), ("Placeholder", value) };
            return multiline ? Control<B.TextArea>(attributes) : Control<B.TextBox>(attributes);
        }
        var fallback = EventCallback.Factory.Create<string>(this, legacy.Add);
        var first = EventCallback.Factory.Create<string>(this, standard.Add);
        var second = EventCallback.Factory.Create<string>(this, replacement.Add);
        var host = await renderer.AddComponent<Host>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Content"] = View("", first, fallback) });
        var native = Assert.Single(root.Children);
        await renderer.Dispatcher.InvokeAsync(() => native.OnTextInput("first"));
        Assert.Equal(new[] { "first" }, standard); Assert.Empty(legacy);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View("", second, fallback)));
        await renderer.Dispatcher.InvokeAsync(() => native.OnTextInput("second"));
        Assert.Equal(new[] { "second" }, replacement);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View("", default, fallback)));
        await renderer.Dispatcher.InvokeAsync(() => native.OnTextInput("legacy"));
        Assert.Equal(new[] { "legacy" }, legacy);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(null, default, default)));
        Assert.Equal("", multiline ? ((TextArea)native).Text : ((TextBox)native).Text);
        Assert.Equal("", multiline ? ((TextArea)native).Placeholder : ((TextBox)native).Placeholder);
        await renderer.Dispatcher.InvokeAsync(() => native.OnTextInput("quiet"));
        Assert.Single(legacy); Assert.Single(standard); Assert.Single(replacement);
    }

    [Fact]
    public async Task CommitsAreDiscreteSnapshots_EnterBlurAndMultilineRules()
    {
        using var renderer = new TestRenderer();
        var root = new Panel();
        var probe = await renderer.AddComponent<BindingProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var box = (TextBox)root.Children[0]; var area = (TextArea)root.Children[1];
        await renderer.Dispatcher.InvokeAsync(() =>
        {
            box.OnFocus(); box.OnKeyDown(269, 0, false); box.OnTextInput("1"); box.OnKeyDown(257, 0, false);
            box.OnKeyDown(257, 0, true); box.OnBlur();
            box.OnFocus(); box.OnTextInput("2"); box.OnBlur();
            area.OnFocus(); area.OnKeyDown(269, 2, false); area.OnTextInput("!"); area.OnKeyDown(257, 0, false);
        });
        Assert.Equal(2, probe.Commits.Count);
        Assert.Equal("Milda1", probe.Commits[0].Text); Assert.Equal(TextCommitReason.Enter, probe.Commits[0].Reason);
        Assert.Equal("Milda12", probe.Commits[1].Text); Assert.Equal(TextCommitReason.Blur, probe.Commits[1].Reason);
        Assert.EndsWith("!\n", area.Text);
        await renderer.Dispatcher.InvokeAsync(() => { area.OnKeyDown(257, 2, false); area.OnBlur(); });
        Assert.Equal(3, probe.Commits.Count); Assert.Equal(area.Text, probe.Commits[2].Text);
    }

    [Theory]
    [InlineData("Checkbox", "IsCheckedChanged", "OnChanged")]
    [InlineData("Slider", "ValueChanged", "OnValueChanged")]
    [InlineData("ComboBox", "SelectedIndexChanged", "OnSelectionChanged")]
    public async Task ScalarCallbacks_ConventionalWinsAndClearFallsBack(string kind, string standardName, string legacyName)
    {
        using var renderer = new TestRenderer(); var root = new Panel();
        var first = 0; var fallback = 0; var replacement = 0;
        object Callback(Action action) => kind switch
        {
            "Checkbox" => EventCallback.Factory.Create<bool>(this, _ => action()),
            "Slider" => EventCallback.Factory.Create<float>(this, _ => action()),
            _ => EventCallback.Factory.Create<int>(this, _ => action())
        };
        object Empty() => kind switch { "Checkbox" => default(EventCallback<bool>), "Slider" => default(EventCallback<float>), _ => default(EventCallback<int>) };
        RenderFragment View(object standard, object legacy) => kind switch
        {
            "Checkbox" => Control<B.Checkbox>(("IsChecked", false), (standardName, standard), (legacyName, legacy)),
            "Slider" => Control<B.Slider>(("Value", 0f), (standardName, standard), (legacyName, legacy)),
            _ => Control<B.ComboBox>(("Items", new[] { "Personal", "Studio" }), ("SelectedIndex", 0), (standardName, standard), (legacyName, legacy))
        };
        var legacy = Callback(() => fallback++);
        var host = await renderer.AddComponent<Host>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Content"] = View(Callback(() => first++), legacy) });
        var native = Assert.Single(root.Children);
        void Change()
        {
            if (native is Checkbox checkbox) checkbox.OnClick();
            else if (native is Slider slider) slider.SetValueFromUser(.75f);
            else ((ComboBox)native).SelectFromUser(1);
        }
        await renderer.Dispatcher.InvokeAsync(Change);
        Assert.Equal(1, first); Assert.Equal(0, fallback);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(Callback(() => replacement++), legacy)));
        await renderer.Dispatcher.InvokeAsync(Change);
        Assert.Equal(1, replacement); Assert.Equal(0, fallback);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(Empty(), legacy)));
        await renderer.Dispatcher.InvokeAsync(Change);
        Assert.Equal(1, fallback);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(Empty(), Empty())));
        await renderer.Dispatcher.InvokeAsync(Change);
        Assert.Equal(1, first); Assert.Equal(1, replacement); Assert.Equal(1, fallback);
    }

    [Theory]
    [InlineData("TextBox")]
    [InlineData("TextArea")]
    [InlineData("Checkbox")]
    [InlineData("Slider")]
    [InlineData("ComboBox")]
    public async Task ExplicitResetVersion_SupersedesAnUndeliveredNativeValue(string kind)
    {
        // A real Window causes the scalar slot to queue until the next UI drain.
        using var renderer = new TestRenderer();
        var window = new Window(240, 120, "Input reset identity");
        try
        {
            var root = new Panel(); window.AddChild(root);
            var edits = 0;
            RenderFragment View(long version) => kind switch
            {
                "TextBox" => Control<B.TextBox>(("Text", "Milda"), ("TextChanged", EventCallback.Factory.Create<string>(this, _ => edits++)), ("ResetVersion", version)),
                "TextArea" => Control<B.TextArea>(("Text", "Milda"), ("TextChanged", EventCallback.Factory.Create<string>(this, _ => edits++)), ("ResetVersion", version)),
                "Checkbox" => Control<B.Checkbox>(("IsChecked", false), ("IsCheckedChanged", EventCallback.Factory.Create<bool>(this, _ => edits++)), ("ResetVersion", version)),
                "Slider" => Control<B.Slider>(("Value", 0f), ("ValueChanged", EventCallback.Factory.Create<float>(this, _ => edits++)), ("ResetVersion", version)),
                _ => Control<B.ComboBox>(("Items", new[] { "Personal", "Studio" }), ("SelectedIndex", 0), ("SelectedIndexChanged", EventCallback.Factory.Create<int>(this, _ => edits++)), ("ResetVersion", version))
            };
            var host = await renderer.AddComponent<Host>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Content"] = View(0) });
            var native = Assert.Single(root.Children);
            await renderer.Dispatcher.InvokeAsync(() =>
            {
                if (native is Slider slider) slider.SetValueFromUser(.75f);
                else if (native is ComboBox combo) combo.SelectFromUser(1);
                else if (native is Checkbox checkbox) checkbox.OnClick();
                else native.OnTextInput("queued");
            });
            Assert.Single(window.DispatcherQueue); Assert.Equal(0, edits);
            await renderer.Dispatcher.InvokeAsync(() => host.Render(View(1)));
            await renderer.Dispatcher.InvokeAsync(() => { while (window.DispatcherQueue.TryDequeue(out var action)) action(); });
            if (native is TextBox text) Assert.Equal("Milda", text.Text);
            else if (native is TextArea area) Assert.Equal("Milda", area.Text);
            else if (native is Checkbox checkbox) Assert.False(checkbox.IsChecked);
            else if (native is Slider slider) Assert.Equal(0, slider.Value);
            else Assert.Equal(0, ((ComboBox)native).SelectedIndex);
            Assert.Equal(0, edits);
        }
        finally { renderer.Dispose(); window.Dispose(); }
    }

    [Fact]
    public async Task AwaitingCommits_PreserveEveryActionAndSnapshot()
    {
        using var renderer = new TestRenderer(); var root = new Panel();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var before = new List<TextCommitEventArgs>(); var after = new List<TextCommitEventArgs>();
        var callback = EventCallback.Factory.Create<TextCommitEventArgs>(this, async args =>
        {
            before.Add(args); await release.Task; after.Add(args);
            if (after.Count == 2) completed.SetResult();
        });
        var host = await renderer.AddComponent<Host>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Content"] = Control<B.TextBox>(("TextCommitted", callback)) });
        var text = (TextBox)Assert.Single(root.Children);
        await renderer.Dispatcher.InvokeAsync(() =>
        {
            text.OnTextInput("1"); text.OnKeyDown(257, 0, false);
            text.OnTextInput("2"); text.OnKeyDown(257, 0, false);
        });
        Assert.Equal(new[] { "1", "12" }, before.Select(a => a.Text));
        await renderer.Dispatcher.InvokeAsync(() => host.Render(b => { }));
        release.SetResult(); await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { "1", "12" }, after.Select(a => a.Text).Order());
        Assert.Empty(root.Children);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WrapperCallbackReplacement_InvalidatesQueuedSubscription(bool clear)
    {
        using var renderer = new TestRenderer();
        var window = new Window(240, 120, "Input callback identity");
        try
        {
            var root = new Panel(); window.AddChild(root);
            var oldCalls = 0; var newCalls = 0;
            var first = EventCallback.Factory.Create<string>(this, _ => oldCalls++);
            var second = EventCallback.Factory.Create<string>(this, _ => newCalls++);
            RenderFragment View(EventCallback<string> callback) => Control<B.TextBox>(("TextChanged", callback));
            var host = await renderer.AddComponent<Host>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Content"] = View(first) });
            var text = (TextBox)Assert.Single(root.Children);
            await renderer.Dispatcher.InvokeAsync(() => text.OnTextInput("old"));
            await renderer.Dispatcher.InvokeAsync(() => host.Render(View(clear ? default : second)));
            await renderer.Dispatcher.InvokeAsync(() => { while (window.DispatcherQueue.TryDequeue(out var action)) action(); });
            Assert.Equal(0, oldCalls); Assert.Equal(0, newCalls);
            await renderer.Dispatcher.InvokeAsync(() => text.OnTextInput("new"));
            await renderer.Dispatcher.InvokeAsync(() => { while (window.DispatcherQueue.TryDequeue(out var action)) action(); });
            Assert.Equal(0, oldCalls); Assert.Equal(clear ? 0 : 1, newCalls);
        }
        finally { renderer.Dispose(); window.Dispose(); }
    }

    [Fact]
    public async Task QueuedTextEdit_IsDeliveredBeforeACommitRerendersTheParent()
    {
        using var renderer = new TestRenderer();
        var window = new Window(240, 120, "Text commit ordering");
        try
        {
            var root = new Panel(); window.AddChild(root);
            var probe = await renderer.AddComponent<BindingProbe>(TestRenderer.CreateRootHandler(renderer, root));
            var text = (TextBox)root.Children[0]; var area = (TextArea)root.Children[1];
            await renderer.Dispatcher.InvokeAsync(() =>
            {
                text.OnKeyDown(269, 0, false); text.OnTextInput(" Studio");
                Assert.Equal("Milda", probe.Name); // Slot has not drained yet.
                text.OnKeyDown(257, 0, false);
                area.OnKeyDown(269, 2, false); area.OnTextInput("!"); area.OnBlur();
                while (window.DispatcherQueue.TryDequeue(out var action)) action();
            });
            Assert.Equal("Milda Studio", probe.Name); Assert.Equal(probe.Name, text.Text);
            Assert.Equal("Studio profile!", probe.Notes); Assert.Equal(probe.Notes, area.Text);
            Assert.Single(probe.Edits); Assert.Equal(2, probe.Commits.Count);
            Assert.Equal(probe.Name, probe.Commits[0].Text); Assert.Equal(probe.Notes, probe.Commits[1].Text);
        }
        finally { renderer.Dispose(); window.Dispose(); }
    }

    [Fact]
    public async Task ComboBox_DefaultSelectionAndEmptyUnicodeLabelsSurviveObjectTransport()
    {
        using var renderer = new TestRenderer(); var root = new Panel();
        string[] items = ["", "Žąsis 👩🏽‍💻 e\u0301", "Studio archive / International production workstation"];
        var host = await renderer.AddComponent<Host>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Content"] = Control<B.ComboBox>(("Items", items)) });
        var combo = (ComboBox)Assert.Single(root.Children);
        Assert.Equal(items, combo.Items); Assert.Equal(0, combo.SelectedIndex); Assert.Equal("", combo.SelectedItem);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(Control<B.ComboBox>(("Items", items), ("SelectedIndex", -1))));
        Assert.Equal(-1, combo.SelectedIndex); Assert.Null(combo.SelectedItem);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(Control<B.ComboBox>(("Items", null))));
        Assert.Empty(combo.Items); Assert.Equal(-1, combo.SelectedIndex);
    }
}
