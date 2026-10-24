using System;
using System.Linq;
using Xunit;

namespace Marius.Winter.Tests;

public class RichDocumentTests
{
    private static readonly RichTextStyle Emphasis = new(TextFormat.Bold | TextFormat.Underline, new Color4(.1f, .4f, .8f));

    [Fact]
    public void SnapshotsCanonicalizeGapsAndNeighborsAndDefensivelyOwnRuns()
    {
        var source = new[] { new RichTextRun(1, 2, Emphasis), new RichTextRun(3, 2, Emphasis) };
        var document = new RichDocument("Winter note", source);
        source[0] = new(0, 11, default);
        Assert.Equal(new[] { new RichTextRun(0, 1, default), new RichTextRun(1, 4, Emphasis), new RichTextRun(5, 6, default) }, document.Runs);
        Assert.Equal(document, new RichDocument("Winter note").WithStyle(1, 4, Emphasis));
        Assert.Equal(document.GetHashCode(), new RichDocument(document.Text, document.Runs).GetHashCode());
        Assert.NotEqual(new RichDocument(document.Text), document);
        Assert.Empty(RichDocument.Empty.Runs);
        Assert.Equal(default, RichDocument.Empty.StyleAt(0));
    }

    [Fact]
    public void ReplacementInsertionAndDeletionUseWholeUnicodeGraphemesAndKeepSurvivingStyles()
    {
        const string emoji = "👩🏽‍💻";
        var original = new RichDocument("A" + emoji + "e\u0301Z").WithStyle(1, emoji.Length, Emphasis);
        var inserted = original.Replace(2, 0, "Hi "); // Inside the emoji floors to its start.
        Assert.Equal("AHi " + emoji + "e\u0301Z", inserted.Text);
        Assert.Equal(Emphasis, inserted.StyleAt(4));
        Assert.Equal(default, inserted.StyleAt(1)); // Inherits preceding A.
        var deleted = original.Replace(2, 1, "");
        Assert.Equal("Ae\u0301Z", deleted.Text);
        var replaced = original.Replace(1 + emoji.Length + 1, 1, "日本語", Emphasis);
        Assert.Equal("A" + emoji + "日本語Z", replaced.Text);
        Assert.Equal(Emphasis, replaced.StyleAt(1 + emoji.Length));
        Assert.Equal("A" + emoji + "e\u0301Z", original.Text);
    }

    [Fact]
    public void SerializationRestoresColorsFlagsAndScalarStylesAndSupportsFurtherEdits()
    {
        var rich = new RichDocument("Notes e\u0301 👩🏽‍💻\n日本語")
            .WithStyle(0, 5, Emphasis).WithStyle(7, 1, new(TextFormat.Italic | TextFormat.Strikethrough));
        var restored = RichDocument.FromJson(rich.ToJson());
        Assert.Equal(rich, restored);
        Assert.Equal(Emphasis, restored.StyleAt(2));
        Assert.Equal(TextFormat.Italic | TextFormat.Strikethrough, restored.StyleAt(7).Format);
        Assert.Equal(rich.Replace(rich.Text.Length, 0, "!"), restored.Replace(restored.Text.Length, 0, "!"));
        Assert.Equal(RichDocument.Empty, RichDocument.FromJson(RichDocument.Empty.ToJson()));
        Assert.Throws<NotSupportedException>(() => RichDocument.FromJson("{\"Version\":2,\"Text\":\"Notes\",\"Runs\":[]}"));
    }

    [Fact]
    public void HistoryIncludesTextAndFormatEditsWithBoundedUndoRedoAndExplicitReset()
    {
        var baseline = new RichDocument("Note");
        var history = new RichDocumentHistory(baseline, capacity: 2);
        var typed = baseline.Replace(4, 0, " e\u0301");
        Assert.True(history.Apply(typed));
        var formatted = typed.WithStyle(0, 4, Emphasis);
        Assert.True(history.Apply(formatted));
        Assert.True(history.Undo()); Assert.Equal(typed, history.Current);
        Assert.False(history.Apply(new RichDocument(typed.Text, typed.Runs))); // No-op keeps redo.
        Assert.True(history.CanRedo);
        Assert.True(history.Redo()); Assert.Equal(formatted, history.Current);
        Assert.True(history.Undo());
        var branch = typed.Replace(0, 4, "Memo");
        Assert.True(history.Apply(branch)); Assert.False(history.CanRedo);
        Assert.True(history.Apply(branch.Replace(0, 0, "Review ")));
        Assert.True(history.Undo()); Assert.Equal(branch, history.Current);
        Assert.True(history.Undo()); Assert.Equal(typed, history.Current);
        Assert.False(history.Undo()); // Oldest baseline was evicted.
        history.Reset(formatted);
        Assert.Equal(formatted, history.Current); Assert.False(history.CanUndo); Assert.False(history.CanRedo);
    }

    [Fact]
    public void OrdinaryValidationRejectsOverlappingRunsAndOversizedDocumentsBeforeMutation()
    {
        Assert.Throws<ArgumentException>(() => new RichDocument("Note", new[] { new RichTextRun(0, 3, default), new RichTextRun(2, 1, Emphasis) }));
        Assert.Throws<ArgumentException>(() => new RichDocument(new string('n', RichDocument.MaximumTextLength + 1)));
        var history = new RichDocumentHistory(new RichDocument("Note"));
        Assert.Throws<ArgumentOutOfRangeException>(() => history.Current.Replace(5, 0, "n"));
        Assert.Equal("Note", history.Current.Text); Assert.False(history.CanUndo);
    }
}
