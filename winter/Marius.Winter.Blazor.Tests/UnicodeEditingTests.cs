using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Xunit;
using static Glfw.GLFW;

namespace Marius.Winter.Blazor.Tests;

[CollectionDefinition("Native editors", DisableParallelization = true)]
public sealed class NativeEditorCollection : ICollectionFixture<EditorWindow> { }

// Run this collection on an owned display (on Linux: xvfb-run -a dotnet test ...).
public sealed class EditorWindow : IDisposable
{
    public Window Window { get; } = new(900, 600, "Editor regression tests");

    public EditorWindow()
    {
        Assert.True((bool)typeof(Window).GetField("_initialized", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(Window)!, "The native test window must initialize successfully.");
    }

    public Element Attach(string kind, string text)
    {
        Element editor = kind switch
        {
            "TextBox" => new TextBox(text),
            "TextArea" => new TextArea(text),
            "RichTextBox" => new RichTextBox(text),
            _ => throw new ArgumentException(kind)
        };
        Window.AddChild(editor);
        editor.Bounds = new RectF(0, 0, 800, kind == "TextBox" ? 30 : 260);
        return editor;
    }

    public void Dispose() => Window.Dispose();
}

[Collection("Native editors")]
public sealed class UnicodeEditingTests(EditorWindow host)
{
    private static readonly string[] Kinds = ["TextBox", "TextArea", "RichTextBox"];
    private static readonly string[] Clusters = ["😀", "e\u0301", "👩🏽‍💻", "🇱🇹"];

    public static IEnumerable<object[]> EditorClusters =>
        from kind in Kinds from cluster in Clusters select new object[] { kind, cluster };

    public static IEnumerable<object[]> Editors => Kinds.Select(kind => new object[] { kind });

    internal static string TextOf(Element editor) => editor switch
    {
        TextBox box => box.Text,
        TextArea area => area.Text,
        RichTextBox rich => rich.Text,
        _ => throw new ArgumentException()
    };

    internal static int Caret(Element editor) => Field<int>(editor, "_cursorPos");
    internal static T Field<T>(object obj, string name) =>
        (T)obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(obj)!;
    internal static void Key(Element editor, int key, int mods = 0) => editor.OnKeyDown(key, mods, false);

    private static void AssertBoundary(Element editor)
    {
        var text = TextOf(editor);
        var boundaries = StringInfo.ParseCombiningCharacters(text).Append(text.Length).ToArray();
        Assert.Contains(Caret(editor), boundaries);
        int selection = Field<int>(editor, "_selectionStart");
        if (selection >= 0) Assert.Contains(selection, boundaries);
    }

    [Theory, MemberData(nameof(EditorClusters))]
    public void BackspaceDeletesOneGrapheme(string kind, string cluster)
    {
        var editor = host.Attach(kind, "A" + cluster);
        Key(editor, GLFW_KEY_BACKSPACE);
        Assert.Equal("A", TextOf(editor));
        Assert.Equal(1, Caret(editor));
    }

    [Theory, MemberData(nameof(EditorClusters))]
    public void DeleteDeletesOneGrapheme(string kind, string cluster)
    {
        var editor = host.Attach(kind, cluster + "B");
        Key(editor, GLFW_KEY_HOME, GLFW_MOD_CONTROL);
        Key(editor, GLFW_KEY_DELETE);
        Assert.Equal("B", TextOf(editor));
        Assert.Equal(0, Caret(editor));
    }

    [Theory, MemberData(nameof(EditorClusters))]
    public void ArrowsAndShiftReplacementUseWholeGraphemes(string kind, string cluster)
    {
        var editor = host.Attach(kind, "A" + cluster + "B");
        Key(editor, GLFW_KEY_LEFT);
        Assert.Equal(1 + cluster.Length, Caret(editor));
        Key(editor, GLFW_KEY_LEFT, GLFW_MOD_SHIFT);
        Assert.Equal(1, Caret(editor));
        AssertBoundary(editor);
        editor.OnTextInput("Ω");
        Assert.Equal("AΩB", TextOf(editor));
        Key(editor, GLFW_KEY_HOME, GLFW_MOD_CONTROL);
        Key(editor, GLFW_KEY_RIGHT);
        Key(editor, GLFW_KEY_RIGHT, GLFW_MOD_SHIFT);
        Key(editor, GLFW_KEY_LEFT); // Collapse to the start of the selection.
        Assert.Equal(1, Caret(editor));
        AssertBoundary(editor);
    }

    [Theory, MemberData(nameof(Editors))]
    public void TextReplacementAndInsertedJoinsLeaveCaretOnBoundary(string kind)
    {
        var editor = host.Attach(kind, "x");
        switch (editor)
        {
            case TextBox box: box.Text = "😀x"; break;
            case TextArea area: area.Text = "😀x"; break;
            case RichTextBox rich: rich.Text = "😀x"; break;
        }
        AssertBoundary(editor); // Old offset 1 is inside the replacement's surrogate pair.
        Key(editor, GLFW_KEY_END, GLFW_MOD_CONTROL);
        editor.OnTextInput("\u0301");
        AssertBoundary(editor);
        Key(editor, GLFW_KEY_BACKSPACE);
        Assert.Equal("😀", TextOf(editor));

        // Inserting a base before an existing combining mark joins the suffix.
        var joining = host.Attach(kind, "\u0301Z");
        Key(joining, GLFW_KEY_HOME, GLFW_MOD_CONTROL);
        joining.OnTextInput("a");
        Assert.Equal(2, Caret(joining));
        AssertBoundary(joining);
    }

    [Theory, MemberData(nameof(Editors))]
    public void WordCommandsAndClipboardRoundTripPreserveGraphemes(string kind)
    {
        var editor = host.Attach(kind, "start 👩🏽‍💻e\u0301");
        Key(editor, GLFW_KEY_LEFT, GLFW_MOD_CONTROL | GLFW_MOD_SHIFT);
        AssertBoundary(editor);
        Key(editor, GLFW_KEY_C, GLFW_MOD_CONTROL);
        Assert.Equal("👩🏽‍💻e\u0301", Glfw.Glfw.glfwGetClipboardString(null));
        Key(editor, GLFW_KEY_X, GLFW_MOD_CONTROL);
        Assert.Equal("start ", TextOf(editor));
        Key(editor, GLFW_KEY_V, GLFW_MOD_CONTROL);
        Assert.Equal("start 👩🏽‍💻e\u0301", TextOf(editor));
        Key(editor, GLFW_KEY_BACKSPACE);
        Assert.Equal("start 👩🏽‍💻", TextOf(editor));
        Key(editor, GLFW_KEY_BACKSPACE, GLFW_MOD_CONTROL);
        Assert.Equal("start ", TextOf(editor));
    }

    [Theory]
    [InlineData("TextArea")]
    [InlineData("RichTextBox")]
    public void VerticalNavigationMapsGraphemeColumns(string kind)
    {
        var editor = host.Attach(kind, "x\n👩🏽‍💻z\ne\u0301q");
        Key(editor, GLFW_KEY_HOME, GLFW_MOD_CONTROL);
        Key(editor, GLFW_KEY_RIGHT);
        Key(editor, GLFW_KEY_DOWN);
        Assert.Equal(2 + "👩🏽‍💻".Length, Caret(editor));
        AssertBoundary(editor);
        Key(editor, GLFW_KEY_DOWN, GLFW_MOD_SHIFT);
        Assert.Equal(TextOf(editor).Length - 1, Caret(editor));
        AssertBoundary(editor);
        Key(editor, GLFW_KEY_UP);
        Assert.Equal(2 + "👩🏽‍💻".Length, Caret(editor));
    }

    [Theory]
    [InlineData("TextArea", "\n")]
    [InlineData("RichTextBox", "\n")]
    [InlineData("TextArea", "\r\n")]
    [InlineData("RichTextBox", "\r\n")]
    public void HomeEndAndLineTransitionsPreserveNewlineGraphemes(string kind, string newline)
    {
        var editor = host.Attach(kind, "e\u0301" + newline + "😀B");
        Key(editor, GLFW_KEY_HOME, GLFW_MOD_CONTROL);
        Key(editor, GLFW_KEY_END);
        Assert.Equal(2, Caret(editor));
        AssertBoundary(editor);
        Key(editor, GLFW_KEY_RIGHT);
        Assert.Equal(2 + newline.Length, Caret(editor));
        Key(editor, GLFW_KEY_LEFT);
        Assert.Equal(2, Caret(editor));
        Key(editor, GLFW_KEY_DELETE);
        Assert.Equal("e\u0301😀B", TextOf(editor));
        Key(editor, GLFW_KEY_ENTER);
        Assert.Equal("e\u0301\n😀B", TextOf(editor));
        Key(editor, GLFW_KEY_BACKSPACE);
        Assert.Equal("e\u0301😀B", TextOf(editor));
    }

    [Theory]
    [InlineData("TextArea", false)]
    [InlineData("RichTextBox", false)]
    [InlineData("TextArea", true)]
    [InlineData("RichTextBox", true)]
    public void ReplacementKeepsOriginalInsertionOffsetWhenRemovingNewlineJoinsGraphemes(string kind, bool paste)
    {
        var editor = host.Attach(kind, "a\n\u0301B");
        Key(editor, GLFW_KEY_HOME, GLFW_MOD_CONTROL);
        Key(editor, GLFW_KEY_RIGHT);
        Key(editor, GLFW_KEY_RIGHT, GLFW_MOD_SHIFT); // Select only the newline.
        if (paste)
        {
            Glfw.Glfw.glfwSetClipboardString(null, "X");
            Key(editor, GLFW_KEY_V, GLFW_MOD_CONTROL);
        }
        else editor.OnTextInput("X");
        Assert.Equal("aX\u0301B", TextOf(editor));
        Assert.Equal(3, Caret(editor));
        AssertBoundary(editor);
        if (editor is RichTextBox rich) AssertRuns(rich);
    }

    [Theory]
    [InlineData("TextArea", "\n")]
    [InlineData("RichTextBox", "\n")]
    [InlineData("TextArea", "\r\n")]
    [InlineData("RichTextBox", "\r\n")]
    public void WordCommandsRetainMultilineStops(string kind, string newline)
    {
        var editor = host.Attach(kind, "one" + newline + "😀two");
        Key(editor, GLFW_KEY_HOME, GLFW_MOD_CONTROL);
        Key(editor, GLFW_KEY_RIGHT, GLFW_MOD_CONTROL);
        Assert.Equal(3, Caret(editor));
        Key(editor, GLFW_KEY_RIGHT, GLFW_MOD_CONTROL);
        Assert.Equal(3 + newline.Length, Caret(editor));
        Key(editor, GLFW_KEY_DELETE, GLFW_MOD_CONTROL);
        Assert.Equal("one" + newline, TextOf(editor));
        AssertBoundary(editor);
    }

    [Theory, MemberData(nameof(Editors))]
    public void JoinedInsertionAndSelectionAcrossClustersRemainValid(string kind)
    {
        var editor = host.Attach(kind, "👩💻!");
        Key(editor, GLFW_KEY_HOME, GLFW_MOD_CONTROL);
        Key(editor, GLFW_KEY_RIGHT);
        editor.OnTextInput("\u200d");
        Assert.Equal("👩‍💻!", TextOf(editor));
        Assert.Equal("👩‍💻".Length, Caret(editor));
        Key(editor, GLFW_KEY_HOME, GLFW_MOD_CONTROL | GLFW_MOD_SHIFT);
        Key(editor, GLFW_KEY_DELETE);
        Assert.Equal("!", TextOf(editor));
        editor.OnTextInput("e\u0301😀");
        Key(editor, GLFW_KEY_HOME, GLFW_MOD_CONTROL);
        Key(editor, GLFW_KEY_RIGHT, GLFW_MOD_SHIFT);
        Key(editor, GLFW_KEY_RIGHT, GLFW_MOD_SHIFT);
        editor.OnTextInput("🇱🇹");
        Assert.Equal("🇱🇹!", TextOf(editor));
        AssertBoundary(editor);
        if (editor is RichTextBox rich) AssertRuns(rich);
    }

    [Theory, MemberData(nameof(Editors))]
    public void PasteClearsAnEmptyShiftSelection(string kind)
    {
        var editor = host.Attach(kind, "A😀");
        Key(editor, GLFW_KEY_LEFT, GLFW_MOD_SHIFT);
        Key(editor, GLFW_KEY_RIGHT, GLFW_MOD_SHIFT);
        Glfw.Glfw.glfwSetClipboardString(null, "e\u0301");
        Key(editor, GLFW_KEY_V, GLFW_MOD_CONTROL);
        Assert.Equal("A😀e\u0301", TextOf(editor));
        Assert.Equal(-1, Field<int>(editor, "_selectionStart"));
        Key(editor, GLFW_KEY_BACKSPACE);
        Assert.Equal("A😀", TextOf(editor));
    }

    [Fact]
    public void ScrolledTextBoxUsesGraphemeSafeVisibleRangesAndRetainsItsPaint()
    {
        var box = (TextBox)host.Attach("TextBox", string.Concat(Enumerable.Repeat("Ae\u0301👩🏽‍💻🇱🇹", 8)));
        box.Bounds = new RectF(0, 0, 100, 30);
        var paint = Field<ThorVG.Text>(box, "_textPaint");
        var scene = Field<ThorVG.Scene>(box, "_textScene");
        var boundaries = StringInfo.ParseCombiningCharacters(box.Text).Append(box.Text.Length).ToArray();
        Key(box, GLFW_KEY_HOME);
        for (int i = 0; i < boundaries.Length; i++)
        {
            Key(box, GLFW_KEY_RIGHT);
            int start = Field<int>(box, "_visibleStart");
            int end = Field<int>(box, "_visibleEnd");
            Assert.Contains(start, boundaries);
            Assert.Contains(end, boundaries);
            Assert.Equal(box.Text[start..end], paint.GetText());
            Assert.Same(scene, paint.Parent());
            Assert.Same(paint, Field<ThorVG.Text>(box, "_textPaint"));
        }
    }

    [Theory, MemberData(nameof(EditorClusters))]
    public void MousePlacementAndDragOnlySelectGraphemeBoundaries(string kind, string cluster)
    {
        var editor = host.Attach(kind, "A" + cluster + "B");
        // Sweep actual mouse input through the text, including internal glyph advances.
        float y = kind == "TextBox" ? 15 : 10;
        for (float x = 0; x <= 160; x += 0.5f)
        {
            Assert.True(editor.OnMouseDown(0, x, y));
            AssertBoundary(editor);
            editor.OnMouseMove(x + 1, y);
            AssertBoundary(editor);
            editor.OnMouseUp(0, x + 1, y);
        }
    }

    [Fact]
    public void RichRunsTrackUtf16LengthsWhenDeletingAndReplacingGraphemesAcrossRuns()
    {
        var rich = (RichTextBox)host.Attach("RichTextBox", "Ae\u0301👩🏽‍💻B");
        rich.SetFormat(1, 1, TextFormat.Bold); // A grapheme can span styled scalar runs.
        rich.SetFormat(3, "👩🏽‍💻".Length, TextFormat.Italic);
        Key(rich, GLFW_KEY_LEFT);
        Key(rich, GLFW_KEY_BACKSPACE);
        Assert.Equal("Ae\u0301B", rich.Text);
        AssertRuns(rich);
        Key(rich, GLFW_KEY_BACKSPACE);
        Assert.Equal("AB", rich.Text);
        AssertRuns(rich);
        rich.ToggleUnderline();
        rich.OnTextInput("😀e\u0301");
        Assert.Equal("A😀e\u0301B", rich.Text);
        Assert.Equal(TextFormat.Underline, rich.GetCurrentFormat());
        AssertRuns(rich);
        Key(rich, GLFW_KEY_LEFT, GLFW_MOD_SHIFT);
        rich.OnTextInput("🇱🇹");
        Assert.Equal("A😀🇱🇹B", rich.Text);
        AssertRuns(rich);
    }

    [Fact]
    public void ProgrammaticRichFormattingDoesNotSplitSurrogatePairs()
    {
        var rich = (RichTextBox)host.Attach("RichTextBox", "A😀B");
        rich.SetFormat(2, 1, TextFormat.Bold);
        rich.SetColor(1, 1, Color4.White);
        AssertRuns(rich);
        Key(rich, GLFW_KEY_HOME, GLFW_MOD_CONTROL);
        Key(rich, GLFW_KEY_RIGHT);
        Key(rich, GLFW_KEY_DELETE);
        Assert.Equal("AB", rich.Text);
        AssertRuns(rich);
    }

    private static void AssertRuns(RichTextBox rich)
    {
        var runs = Field<IEnumerable>(rich, "_runs");
        int offset = 0;
        foreach (var run in runs)
        {
            int start = (int)run.GetType().GetField("Start")!.GetValue(run)!;
            int length = (int)run.GetType().GetField("Length")!.GetValue(run)!;
            Assert.Equal(offset, start);
            offset += length;
            Assert.InRange(offset, 0, rich.Text.Length);
            Assert.False(offset > 0 && offset < rich.Text.Length &&
                char.IsHighSurrogate(rich.Text[offset - 1]) && char.IsLowSurrogate(rich.Text[offset]),
                "Rich run boundaries must be valid UTF-16 offsets.");
        }
        Assert.Equal(rich.Text.Length, offset);
    }
}
