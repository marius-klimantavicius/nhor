using System.Collections.Generic;
using Marius.Winter;
using Xunit;
using static Glfw.GLFW;

namespace Marius.Winter.Tests;

public class RichDocumentEditingTests
{
    [Fact]
    public void ReentrantLegacySynchronizationDoesNotPublishASupersededDocument()
    {
        using var editor = new RichTextBox("Note");
        var documents = new List<string>();
        editor.TextChanged = value => { if (value == "First replacement") editor.Text = "Accepted replacement"; };
        editor.DocumentChanged += document => documents.Add(document.Text);
        editor.Text = "First replacement";
        Assert.Equal("Accepted replacement", editor.Text);
        Assert.Equal(new[] { "Accepted replacement" }, documents);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void KeyboardUndoRestoresTextAndFormatting()
    {
        using var editor = new RichTextBox("Winter note");
        editor.SetFormat(0, 6, TextFormat.Bold);
        editor.OnTextInput("!");
        Assert.Equal("Winter note!", editor.Text);

        editor.OnKeyDown(GLFW_KEY_Z, GLFW_MOD_CONTROL, false);

        Assert.Equal("Winter note", editor.Text);
        editor.OnKeyDown(GLFW_KEY_HOME, GLFW_MOD_CONTROL, false);
        editor.OnKeyDown(GLFW_KEY_RIGHT, 0, false);
        Assert.Equal(TextFormat.Bold, editor.GetCurrentFormat());
        editor.OnKeyDown(GLFW_KEY_Y, GLFW_MOD_CONTROL, false);
        Assert.Equal("Winter note!", editor.Text);
    }

    [Fact]
    public void ToolbarRetainsSelectionAcrossBlurAndReturnsFormattingAsDocumentEdits()
    {
        using var editor = new RichTextBox("Winter note");
        int textEdits = 0, documentEdits = 0;
        editor.UserTextChanged += _ => textEdits++;
        editor.UserDocumentChanged += _ => documentEdits++;
        editor.Select(0, 6);
        editor.OnBlur();
        editor.ToggleBold();
        editor.SetSelectionColor(new Color4(.2f, .5f, .8f));
        Assert.Equal(6, editor.SelectionLength);
        Assert.Equal(TextFormat.Bold, editor.Document.StyleAt(0).Format);
        Assert.Equal(2, documentEdits); Assert.Equal(0, textEdits);
        Assert.True(editor.Undo());
        Assert.Null(editor.Document.StyleAt(0).Color);
        Assert.True(editor.Undo());
        Assert.Equal(TextFormat.None, editor.Document.StyleAt(0).Format);
        Assert.True(editor.Redo());
        Assert.Equal(TextFormat.Bold, editor.Document.StyleAt(0).Format);
    }

    [Fact]
    public void EqualSynchronizationPreservesSelectionAndPendingStyleWhileExplicitResetClearsBoth()
    {
        using var editor = new RichTextBox("Note");
        editor.Select(0, 4); editor.ToggleItalic();
        var document = editor.Document;
        editor.Document = new RichDocument(document.Text, document.Runs);
        editor.Text = document.Text;
        Assert.Equal(4, editor.SelectionLength);
        Assert.True(editor.CanUndo);
        editor.Select(4, 0); editor.ToggleBold();
        editor.SetSelectionColor(new Color4(.2f, .3f, .8f));
        editor.Document = document;
        editor.OnTextInput("!");
        Assert.Equal(TextFormat.Bold | TextFormat.Italic, editor.Document.StyleAt(4).Format);
        Assert.NotNull(editor.Document.StyleAt(4).Color);
        editor.ClearFormatting();
        editor.OnTextInput("?");
        Assert.Equal(default, editor.Document.StyleAt(5));
        editor.ResetText(editor.Text);
        Assert.All(editor.Document.Runs, run => Assert.Equal(default, run.Style));
        Assert.False(editor.CanUndo); Assert.False(editor.CanRedo); Assert.Equal(0, editor.SelectionLength);
    }

    [Fact]
    public void KeyboardUnicodeReplacementDeletionAndRedoInvalidationUseTheRichHistory()
    {
        using var editor = new RichTextBox("A👩🏽‍💻e\u0301Z");
        editor.Select(2, 1); editor.ToggleUnderline();
        var styled = editor.Document;
        editor.OnTextInput("日本語");
        Assert.Equal("A日本語e\u0301Z", editor.Text);
        Assert.True(editor.Undo()); Assert.Equal(styled, editor.Document);
        editor.Select(editor.Text.Length - 1, 0);
        editor.OnKeyDown(GLFW_KEY_BACKSPACE, 0, false);
        Assert.Equal("A👩🏽‍💻Z", editor.Text); Assert.False(editor.CanRedo);
        Assert.True(editor.Undo()); Assert.Equal(styled, editor.Document);
        editor.Select(1, 0); editor.OnKeyDown(GLFW_KEY_DELETE, 0, false);
        Assert.Equal("Ae\u0301Z", editor.Text);
    }
}
