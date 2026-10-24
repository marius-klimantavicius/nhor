using System;
using System.Linq;
using System.Reflection;
using ThorVG;
using Xunit;

namespace Marius.Winter.Tests;

public class RichRunDetachTests
{
    // Run each failing case in its own test process: a failed scene removal can
    // leave the graphics session damaged and obscure subsequent assertions.
    [Theory]
    [InlineData("format", "remove")]
    [InlineData("restore", "remove")]
    [InlineData("format", "clear-parent")]
    [InlineData("restore", "clear-parent")]
    [InlineData("format", "close")]
    [InlineData("restore", "close")]
    public void RenderedRunGrowthCanDetachBeforeAnotherDraw(string edit, string transition) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(500, 300, "Rich same-turn detach", Theme.Light, RenderBackend.SW);
        using var parent = new Panel(); window.AddChild(parent);
        using var editor = new RichTextBox("Review the blue horizon."); parent.AddChild(editor);
        editor.Arrange(new RectF(10, 10, 480, 220));
        Draw(window);
        if (edit == "format") { editor.Select(11, 12); editor.ToggleBold(); }
        else editor.Document = RichDocument.FromJson(new RichDocument(editor.Text)
            .WithStyle(11, 12, new(TextFormat.Bold, new Color4(.1f, .2f, .8f))).ToJson());
        var document = editor.Document;
        var selection = (editor.SelectionStart, editor.SelectionLength);
        var history = (editor.CanUndo, editor.CanRedo);
        // Deliberately no canvas preparation between run growth and detachment.
        var error = Record.Exception(() =>
        {
            if (transition == "remove") parent.RemoveChild(editor);
            else if (transition == "clear-parent") parent.ClearChildren();
            else window.Dispose();
        });
        Console.WriteLine($"rich {edit} -> {transition}: {error?.ToString() ?? "ok"}");
        Assert.Null(error);
        Assert.Equal(document, editor.Document);
        Assert.Equal(selection, (editor.SelectionStart, editor.SelectionLength));
        Assert.Equal(history, (editor.CanUndo, editor.CanRedo));
        if (transition == "close") { Assert.True(editor.IsDisposed); return; }
        Assert.False(editor.IsDisposed); Assert.Null(editor.Parent);
        parent.AddChild(editor); editor.Arrange(new RectF(10, 10, 480, 220));
        Assert.Equal(document, editor.Document);
        Draw(window);
        AssertRenderedText(editor, document.Text);
        if (edit == "format")
        {
            Assert.True(editor.Undo()); Assert.Equal(TextFormat.None, editor.Document.StyleAt(11).Format);
            Assert.True(editor.Redo()); Assert.Equal(document, editor.Document);
        }
        else
        {
            editor.Select(11, 12); editor.ToggleUnderline();
            Assert.True(editor.Undo()); Assert.Equal(document, editor.Document);
            Assert.True(editor.Redo());
        }
        // Repeat after a prepared frame and more new slots.
        Draw(window); editor.Select(12, 3); editor.ToggleItalic();
        parent.RemoveChild(editor); parent.AddChild(editor); Draw(window);
        AssertRenderedText(editor, document.Text);
    });

    [Fact]
    public void DetachPreservesScrollCaretAndPendingTypingStyle() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(500, 300, "Rich retained editing state", Theme.Light, RenderBackend.SW);
        using var parent = new Panel(); window.AddChild(parent);
        using var editor = new RichTextBox(string.Join('\n', Enumerable.Repeat("Ágy e\u0301 long horizontal note", 8)));
        parent.AddChild(editor); editor.Arrange(new RectF(10, 10, 170, 100)); Draw(window);
        editor.Select(editor.Text.Length, 0); editor.ToggleBold();
        editor.SetSelectionColor(new Color4(.1f, .2f, .8f));
        var document = editor.Document; var state = editor.FormattingState;
        var scroll = (Field<float>(editor, "_scrollX"), Field<float>(editor, "_scrollY"));
        Assert.True(scroll.Item2 > 0);
        parent.RemoveChild(editor); parent.AddChild(editor); Draw(window);
        Assert.Equal(document, editor.Document); Assert.Equal(state, editor.FormattingState);
        Assert.Equal(scroll, (Field<float>(editor, "_scrollX"), Field<float>(editor, "_scrollY")));
        editor.OnTextInput("Ω");
        Assert.Equal(TextFormat.Bold, editor.Document.StyleAt(editor.Text.Length - 1).Format);
        Assert.Equal(state.Color, editor.Document.StyleAt(editor.Text.Length - 1).Color);
        Assert.True(editor.Undo()); Assert.Equal(document, editor.Document);
        Assert.True(editor.Redo()); Assert.EndsWith("Ω", editor.Text);
    });

    private static T Field<T>(object target, string name) => (T)target.GetType()
        .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;
    private static void AssertRenderedText(RichTextBox editor, string expected)
    {
        var scene = Field<Scene>(editor, "_contentScene");
        // Visible paints must be rebuilt, not just retained document state.
        Assert.Equal(expected, string.Concat(scene.Paints().OfType<Text>().Select(t => t.GetText())));
    }
    private static void Draw(Window window) => Contracts.Draw(Field<Canvas>(window, "_canvas"), true);
}
