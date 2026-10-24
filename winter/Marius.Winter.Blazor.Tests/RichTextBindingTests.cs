using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Xunit;
using B = Marius.Winter.Blazor.Elements;

namespace Marius.Winter.Blazor.Tests;

[Collection("Native rendering")]
public class RichTextBindingTests
{
    [Fact]
    public async Task OwnedWindowQueuesEveryRichEditAndUnrelatedRendersPreserveNewerNativeState()
    {
        using var window = new Window(700, 450, "Rich document queue", Theme.Light, RenderBackend.SW);
        using var renderer = new TestRenderer(); var root = new Panel(); window.AddChild(root);
        var probe = await renderer.AddComponent<RichQueueProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var plain = (RichTextBox)root.Children[0]; var rich = (RichTextBox)root.Children[1];
        await renderer.Dispatcher.InvokeAsync(() =>
        {
            plain.Select(plain.Text.Length, 0); rich.Select(rich.Text.Length, 0);
            plain.OnTextInput("1"); plain.OnTextInput("2"); rich.OnTextInput("1"); rich.OnTextInput("2");
        });
        Assert.Equal(0, probe.DocumentEdits); Assert.Equal(0, probe.TextEdits);
        await renderer.Dispatcher.InvokeAsync(probe.RenderAgain);
        Assert.Equal("Winter note12", rich.Text); Assert.Equal("Winter note12", plain.Text);
        await renderer.Dispatcher.InvokeAsync(() => { while (window.DispatcherQueue.TryDequeue(out var action)) action(); });
        Assert.Equal("Winter note12", probe.Document.Text); Assert.Equal("Winter note12", probe.Text);
        Assert.Equal(2, probe.DocumentEdits); Assert.Equal(2, probe.TextEdits);
        Assert.True(rich.CanUndo);
        await renderer.Dispatcher.InvokeAsync(() => { rich.Select(0, 6); rich.ToggleBold(); rich.ToggleItalic(); });
        await renderer.Dispatcher.InvokeAsync(() => { while (window.DispatcherQueue.TryDequeue(out var action)) action(); });
        Assert.Equal(4, probe.DocumentEdits);
        Assert.Equal(TextFormat.Bold | TextFormat.Italic, probe.Document.StyleAt(0).Format);
        var count = probe.DocumentEdits;
        await renderer.Dispatcher.InvokeAsync(() => rich.OnTextInput("queued"));
        await renderer.Dispatcher.InvokeAsync(probe.Reset);
        await renderer.Dispatcher.InvokeAsync(() => { while (window.DispatcherQueue.TryDequeue(out var action)) action(); });
        Assert.Equal(count, probe.DocumentEdits);
        Assert.Equal(new RichDocument("Winter note"), rich.Document); Assert.False(rich.CanUndo);
    }

    [Fact]
    public async Task CompiledBindSetMethodsKeepQueuedIdentityAcrossRerendersAndParentRejection()
    {
        using var window = new Window(700, 450, "Rich binding setters", Theme.Light, RenderBackend.SW);
        using var renderer = new TestRenderer(); var root = new Panel(); window.AddChild(root);
        var probe = await renderer.AddComponent<RichBindingProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var plain = (RichTextBox)root.Children[0]; var rich = (RichTextBox)root.Children[1];
        await renderer.Dispatcher.InvokeAsync(() => { plain.Select(11, 0); rich.Select(11, 0); plain.OnTextInput("!"); rich.OnTextInput("!"); });
        await renderer.Dispatcher.InvokeAsync(probe.RenderAgain);
        Assert.Equal("Winter note!", plain.Text); Assert.Equal("Winter note!", rich.Text);
        await renderer.Dispatcher.InvokeAsync(() => { while (window.DispatcherQueue.TryDequeue(out var action)) action(); });
        Assert.Equal("Winter note!", probe.Text); Assert.Equal("Winter note!", probe.Document.Text);
        Assert.Equal(1, probe.TextEdits); Assert.Equal(1, probe.DocumentEdits);
        probe.Reject = true;
        await renderer.Dispatcher.InvokeAsync(() => { plain.OnTextInput("rejected"); rich.OnTextInput("rejected"); });
        await renderer.Dispatcher.InvokeAsync(() => { while (window.DispatcherQueue.TryDequeue(out var action)) action(); });
        Assert.Equal("Winter note!", plain.Text); Assert.Equal("Winter note!", rich.Text);
        Assert.False(rich.CanUndo); // Rejection is an authoritative external replacement baseline.
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ReplacingOrClearingCallbacksDropsOldQueuedSnapshots(bool richBinding, bool clear)
    {
        using var window = new Window(700, 450, "Rich subscription identity", Theme.Light, RenderBackend.SW);
        using var renderer = new TestRenderer(); var root = new Panel(); window.AddChild(root);
        var old = new List<string>(); var replacement = new List<string>();
        var firstText = EventCallback.Factory.Create<string>(this, v => old.Add(v));
        var nextText = clear ? default : EventCallback.Factory.Create<string>(this, v => replacement.Add(v));
        var firstDocument = EventCallback.Factory.Create<RichDocument>(this, v => old.Add(v.Text));
        var nextDocument = clear ? default : EventCallback.Factory.Create<RichDocument>(this, v => replacement.Add(v.Text));
        RenderFragment View(bool next) => b =>
        {
            b.OpenComponent<B.RichTextBox>(0); b.AddAttribute(1, "Text", "Note");
            b.AddAttribute(2, "TextChanged", next ? nextText : firstText);
            b.AddAttribute(3, "Document", richBinding ? new RichDocument("Note") : null);
            b.AddAttribute(4, "DocumentChanged", next ? nextDocument : firstDocument); b.CloseComponent();
        };
        var host = await renderer.AddComponent<InputBindingTests.Host>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Content"] = View(false) });
        var editor = (RichTextBox)root.Children[0];
        await renderer.Dispatcher.InvokeAsync(() => { editor.Select(4, 0); editor.OnTextInput(" old"); });
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(true)));
        await renderer.Dispatcher.InvokeAsync(() => { while (window.DispatcherQueue.TryDequeue(out var action)) action(); });
        Assert.Empty(old); Assert.Empty(replacement); Assert.Equal("Note", editor.Text);
        await renderer.Dispatcher.InvokeAsync(() => { editor.Select(4, 0); editor.OnTextInput(" new"); });
        await renderer.Dispatcher.InvokeAsync(() => { while (window.DispatcherQueue.TryDequeue(out var action)) action(); });
        if (clear) Assert.Empty(replacement); else Assert.Equal(new[] { "Note new" }, replacement);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InactiveBindingCallbacksDoNotInvalidateTheSelectedBindingMode(bool richBinding)
    {
        using var window = new Window(700, 450, "Rich binding precedence", Theme.Light, RenderBackend.SW);
        using var renderer = new TestRenderer(); var root = new Panel(); window.AddChild(root);
        var delivered = new List<string>();
        var activeText = EventCallback.Factory.Create<string>(this, v => delivered.Add(v));
        var activeDocument = EventCallback.Factory.Create<RichDocument>(this, v => delivered.Add(v.Text));
        RenderFragment View(bool replaceInactive) => b =>
        {
            b.OpenComponent<B.RichTextBox>(0); b.AddAttribute(1, "Text", "Note");
            b.AddAttribute(2, "TextChanged", richBinding && replaceInactive ? default : activeText);
            b.AddAttribute(3, "Document", richBinding ? new RichDocument("Note") : null);
            b.AddAttribute(4, "DocumentChanged", !richBinding && replaceInactive ? default : activeDocument);
            b.CloseComponent();
        };
        var host = await renderer.AddComponent<InputBindingTests.Host>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Content"] = View(false) });
        var editor = (RichTextBox)root.Children[0];
        await renderer.Dispatcher.InvokeAsync(() => { editor.Select(4, 0); editor.OnTextInput("!"); });
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(true)));
        Assert.Equal("Note!", editor.Text);
        await renderer.Dispatcher.InvokeAsync(() => { while (window.DispatcherQueue.TryDequeue(out var action)) action(); });
        Assert.Equal(new[] { "Note!" }, delivered);
    }

    [Fact]
    public async Task CompiledTextAndDocumentBindingsPreserveFormattingOnRerenderAndRejectAndResetThroughRealDiffs()
    {
        using var renderer = new TestRenderer(); var root = new Panel();
        var probe = await renderer.AddComponent<RichBindingProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var plain = Assert.IsType<RichTextBox>(root.Children[0]); var rich = Assert.IsType<RichTextBox>(root.Children[1]);
        await renderer.Dispatcher.InvokeAsync(() =>
        {
            plain.Select(0, 6); plain.ToggleBold();
            rich.Select(0, 6); rich.ToggleItalic(); rich.SetSelectionColor(new Color4(.2f, .4f, .8f));
        });
        Assert.Equal(0, probe.TextEdits); Assert.Equal(2, probe.DocumentEdits);
        var snapshot = rich.Document;
        renderer.AttributeUpdates.Clear();
        await renderer.Dispatcher.InvokeAsync(probe.RenderAgain);
        Assert.Equal(6, plain.SelectionLength); Assert.Equal(TextFormat.Bold, plain.Document.StyleAt(0).Format);
        Assert.Equal(snapshot, rich.Document); Assert.Equal(6, rich.SelectionLength);
        Assert.Contains(renderer.AttributeUpdates, a => a.Name == "Configuration");
        await renderer.Dispatcher.InvokeAsync(() => { plain.Select(plain.Text.Length, 0); plain.OnTextInput(" e\u0301"); });
        Assert.Equal(plain.Text, probe.Text); Assert.Equal(1, probe.TextEdits);
        await renderer.Dispatcher.InvokeAsync(() => { rich.Select(rich.Text.Length, 0); rich.ToggleUnderline(); });
        await renderer.Dispatcher.InvokeAsync(probe.RenderAgain);
        await renderer.Dispatcher.InvokeAsync(() => rich.OnTextInput("!"));
        Assert.Equal(TextFormat.Underline, rich.Document.StyleAt(rich.Text.Length - 1).Format);
        Assert.Equal(rich.Document, probe.Document);
        probe.Reject = true;
        await renderer.Dispatcher.InvokeAsync(() => { plain.OnTextInput("reject"); rich.OnTextInput("reject"); });
        Assert.DoesNotContain("reject", plain.Text); Assert.DoesNotContain("reject", rich.Text);
        Assert.Equal(probe.Document, rich.Document);
        await renderer.Dispatcher.InvokeAsync(probe.Reset);
        Assert.Equal("Winter note", plain.Text); Assert.Equal(new RichDocument("Winter note"), rich.Document);
        Assert.False(plain.CanUndo); Assert.False(rich.CanUndo);
        Assert.Equal(2, probe.TextEdits); Assert.Equal(4, probe.DocumentEdits);
    }

    [Fact]
    public async Task AwaitingRichEditsRetainSnapshotsAndCannotRestoreAfterParentResetOrRemoval()
    {
        using var renderer = new TestRenderer(); var root = new Panel();
        var probe = await renderer.AddComponent<RichBindingProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var rich = (RichTextBox)root.Children[1];
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var snapshots = new List<RichDocument>();
        probe.AfterDocument = async document => { await release.Task; snapshots.Add(document); if (snapshots.Count == 2) done.SetResult(); };
        await renderer.Dispatcher.InvokeAsync(() => { rich.Select(rich.Text.Length, 0); rich.OnTextInput("1"); rich.OnTextInput("2"); });
        Assert.Equal("Winter note12", probe.Document.Text);
        await renderer.Dispatcher.InvokeAsync(probe.Reset);
        await renderer.Dispatcher.InvokeAsync(probe.Remove);
        release.SetResult(); await done.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await renderer.Dispatcher.InvokeAsync(() => { });
        Assert.Contains(snapshots, d => d.Text == "Winter note1"); Assert.Contains(snapshots, d => d.Text == "Winter note12");
        Assert.Equal("Winter note", probe.Document.Text); Assert.Empty(root.Children);
        Assert.Null(probe.RichEditor!.CurrentDocument);
        rich.OnTextInput("detached"); Assert.Equal(2, probe.DocumentEdits);
    }

    [Fact]
    public async Task ConventionalTextCallbackWinsAndReplacementClearAndDocumentPrecedenceAreAuthoritative()
    {
        using var renderer = new TestRenderer(); var root = new Panel();
        int standard = 0, legacy = 0, replacement = 0, documents = 0;
        var standardCallback = EventCallback.Factory.Create<string>(this, _ => standard++);
        var legacyCallback = EventCallback.Factory.Create<string>(this, _ => legacy++);
        var replacementCallback = EventCallback.Factory.Create<string>(this, _ => replacement++);
        RenderFragment View(EventCallback<string> changed, EventCallback<string> fallback, RichDocument? document = null) => b =>
        {
            b.OpenComponent<B.RichTextBox>(0);
            b.AddAttribute(1, "Text", "Note"); b.AddAttribute(2, "TextChanged", changed); b.AddAttribute(3, "OnTextChanged", fallback);
            b.AddAttribute(4, "Document", document);
            b.AddAttribute(5, "DocumentChanged", EventCallback.Factory.Create<RichDocument>(this, _ => documents++));
            b.CloseComponent();
        };
        var host = await renderer.AddComponent<InputBindingTests.Host>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Content"] = View(standardCallback, legacyCallback) });
        var control = (RichTextBox)root.Children[0];
        await renderer.Dispatcher.InvokeAsync(() => control.OnTextInput("1"));
        Assert.Equal(1, standard); Assert.Equal(0, legacy);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(replacementCallback, legacyCallback)));
        await renderer.Dispatcher.InvokeAsync(() => control.OnTextInput("2"));
        Assert.Equal(1, replacement);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(default, legacyCallback)));
        await renderer.Dispatcher.InvokeAsync(() => control.OnTextInput("3")); Assert.Equal(1, legacy);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(default, default)));
        await renderer.Dispatcher.InvokeAsync(() => control.OnTextInput("4")); Assert.Equal(1, legacy);
        var rich = new RichDocument("Rich").WithStyle(0, 4, new(TextFormat.Bold));
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(standardCallback, legacyCallback, rich)));
        Assert.Equal(rich, control.Document); Assert.Equal(0, documents);
        await renderer.Dispatcher.InvokeAsync(() => control.OnTextInput("!"));
        Assert.Equal(1, documents); Assert.Equal(1, standard);
    }
}
