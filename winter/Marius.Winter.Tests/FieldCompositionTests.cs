using System;
using System.Linq;
using Xunit;
using Marius.Winter.Blazor;

namespace Marius.Winter.Tests;

public class FieldCompositionTests
{
    [Fact]
    public void RazorResetInvalidatesQueuedTextAndPreservesEditorIdentity() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(800, 1000, "Form queued reset");
        Assert.True(window.UseBlazor().AddComponent<Blazor.Playground.Scenarios.InstallerSetup>().IsCompletedSuccessfully);
        using var scope = window.Dispatcher.EnterScope();
        var name = (TextBox)InstallerConfigurationTests.Walk(window).OfType<Field>().Single(f => f.Label == "Application name").Editor!;
        name.OnTextInput("queued edit");
        InstallerConfigurationTests.Walk(window).OfType<Button>().Single(b => b.Text == "Reset").OnClick();
        while (window.DispatcherQueue.TryDequeue(out var action)) action();
        Assert.Equal("MyApp", name.Text); Assert.False(name.IsDisposed);
        Assert.DoesNotContain(InstallerConfigurationTests.Walk(window).OfType<Label>(), l => l.Text.Contains("submitted #"));
    });

    [Fact]
    public void RazorPathCorrectionKeepsCaretAcrossValidationRelayout() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(800, 1000, "Form validation caret");
        Assert.True(window.UseBlazor().AddComponent<Blazor.Playground.Scenarios.InstallerSetup>().IsCompletedSuccessfully);
        using var scope = window.Dispatcher.EnterScope();
        WindowRegressionOwner.Flush(window);
        var destination = (TextBox)InstallerConfigurationTests.Walk(window).OfType<Field>().Single(f => f.Label == "Destination").Editor!;
        void Drain() { while (window.DispatcherQueue.TryDequeue(out var action)) action(); WindowRegressionOwner.Flush(window); }
        destination.OnKeyDown(65, 2, false); destination.OnTextInput("relative/path"); destination.OnKeyDown(257, 0, false); Drain();
        destination.OnKeyDown(65, 2, false);
        string expected = "";
        foreach (var character in "/opt/myapp")
        {
            expected += character; destination.OnTextInput(character.ToString()); Drain();
            Assert.Equal(expected, destination.Text);
            Assert.Equal(expected.Length, (int)typeof(TextBox).GetField("_cursorPos", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(destination)!);
        }
    });

    [Fact]
    public void FieldLabelsFocusMessagesWrapAndEditorRemainsCallerOwned() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(500, 400, "Field composition");
        using var editor = new TextBox("Select and copy me");
        var field = new Field("Application name", editor) { HelpText = "Name shown in the launcher.", ReadOnly = true };
        window.AddChild(field);
        field.SetMessages(new[] { "Choose a meaningful application name so colleagues can identify this installation in the launcher and in configuration summaries." });
        Assert.True(field.Invalid); Assert.True(editor.Invalid); Assert.True(editor.ReadOnly);
        var wide = field.Measure(450, 400); var narrow = field.Measure(160, 600);
        Assert.True(narrow.Y > wide.Y, $"Long messages must wrap: wide={wide}, narrow={narrow}");
        field.Arrange(new RectF(0, 0, 160, narrow.Y));
        field.LabelElement.OnClick(); Assert.Same(editor, window.FocusedElement);
        field.Enabled = false; window.SetFocus(null); field.LabelElement.OnClick(); Assert.Null(window.FocusedElement);
        Assert.Contains("disabled", field.LabelElement.Text);
        field.Enabled = true; field.SetMessages(Array.Empty<string>()); Assert.False(field.Invalid); Assert.False(editor.Invalid);
        field.Dispose(); Assert.False(editor.IsDisposed); Assert.Null(editor.Parent);
        Assert.True(field.LabelElement.IsDisposed); Assert.True(field.MessageElement.IsDisposed);
    });

    [Fact]
    public void FormValidatesFocusesFirstInvalidSubmitsOnceAndResets() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(500, 400, "Form submit");
        using var name = new TextBox(""); using var notes = new TextArea("");
        var field = new Field("Application name", name) { Validate = () => string.IsNullOrWhiteSpace(name.Text) ? new[] { "Enter an application name." } : Array.Empty<string>() };
        var notesField = new Field("Notes", notes);
        using var form = new Form(); form.AddChild(field); form.AddChild(notesField); window.AddChild(form);
        var valid = 0; var invalid = 0; var resets = 0;
        form.ValidSubmit += () => valid++; form.InvalidSubmit += () => invalid++;
        form.ResetRequested += () => { resets++; name.ResetText(""); notes.ResetText(""); };
        window.SetFocus(name); name.OnKeyDown(257, 0, false);
        Assert.Equal(1, invalid); Assert.Equal(0, valid); Assert.True(field.Invalid); Assert.Same(name, window.FocusedElement);
        name.OnTextInput("Žąsis"); name.OnKeyDown(257, 0, false); name.OnKeyDown(257, 0, true);
        Assert.Equal(1, valid); Assert.False(field.Invalid);
        window.SetFocus(notes); notes.OnKeyDown(257, 0, false); notes.OnKeyDown(257, 2, false);
        Assert.Equal(1, valid); Assert.Contains("\n", notes.Text);
        form.Reset(); Assert.Equal(1, resets); Assert.Empty(name.Text); Assert.Empty(field.Messages);
        form.RemoveChild(field); field.Dispose(); name.OnKeyDown(257, 0, false); Assert.Equal(1, valid);
    });
}
