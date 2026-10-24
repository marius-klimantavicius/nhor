using System;
using System.Linq;
using System.Threading.Tasks;

namespace Marius.Winter.Playground.Scenarios;

/// <summary>A bounded rich note with a versioned snapshot and document undo/redo.</summary>
public sealed class NotesScenario : IDisposable
{
    private const string Note = "Review the blue horizon.\nKeep the trail open.";
    private readonly Window _window;
    private readonly Panel _root;
    private readonly RichTextBox _editor = new(Note, "Write a field note");
    private readonly Label _format = new() { TextWrapping = ThorVG.TextWrap.Word };
    private readonly Label _result = new() { TextWrapping = ThorVG.TextWrap.Word };
    private readonly LogView _snapshot = new();
    private readonly Button _undo, _redo;
    private string? _saved;
    private int _rerenders;
    private bool _large, _disposed;

    public NotesScenario(Window window)
    {
        _window = window;
        _root = new Panel { Layout = new FlexLayout { Direction = Orientation.Vertical, Gap = 8 }, Padding = new Thickness(12) };
        _root.AddChild(new Label("Notes") { FontSize = 22 });
        _root.AddChild(new Label("Select a phrase, style it, and keep a snapshot. Undo and redo restore text and formatting.") { TextWrapping = ThorVG.TextWrap.Word });
        var toolbar = Row();
        toolbar.AddChild(Action("Bold", _editor.ToggleBold));
        toolbar.AddChild(Action("Italic", _editor.ToggleItalic));
        toolbar.AddChild(Action("Underline", _editor.ToggleUnderline));
        toolbar.AddChild(Action("Strike", _editor.ToggleStrikethrough));
        toolbar.AddChild(Action("Blue", () => _editor.SetSelectionColor(Blue)));
        toolbar.AddChild(Action("Clear style", _editor.ClearFormatting));
        _undo = Action("Undo", () => _editor.Undo()); _redo = Action("Redo", () => _editor.Redo());
        toolbar.AddChild(_undo); toolbar.AddChild(_redo); _root.AddChild(toolbar);
        var actions = Row();
        actions.AddChild(Action("Select phrase", () => { _editor.Select(11, 12); _editor.Focus(); }));
        actions.AddChild(Action("Replace plain", () => { _editor.ResetText(Note); _editor.Focus(); Update(); }));
        actions.AddChild(Action("Save snapshot", Save));
        actions.AddChild(Action("Restore snapshot", Restore));
        actions.AddChild(Action("Rerender", () => { _rerenders++; _editor.Document = new RichDocument(_editor.Document.Text, _editor.Document.Runs); Update(); }));
        actions.AddChild(Action("Reset", Reset)); _root.AddChild(actions);
        _editor.LayoutData = new FlexItem { Grow = 1, Basis = 160 }; _editor.MinHeight = 130;
        _root.AddChild(_editor); _root.AddChild(_format); _root.AddChild(_result);
        _snapshot.MinHeight = 90; _snapshot.MaxHeight = 120;
        _snapshot.Text = "Save snapshot to inspect its versioned text, styles and colors."; _root.AddChild(_snapshot);
        var view = Row();
        view.AddChild(Action("Light", () => window.Theme = Theme.Light));
        view.AddChild(Action("Dark", () => window.Theme = Theme.Dark));
        view.AddChild(Action("Larger text", () => { _large = !_large; _editor.FontSize = _large ? 24 : 16; }));
        _root.AddChild(view);
        _editor.DocumentChanged += Changed;
        _editor.FormattingStateChanged += FormatChanged;
        window.Closing += Closing;
        Update();
    }
    public Element Content => _root;
    private static Color4 Blue => new(.12f, .45f, .88f);
    private static Panel Row() => new() { Layout = new FlexLayout { Wrap = FlexWrap.Wrap, Gap = 6 }, Padding = new Thickness(0) };
    private static Button Action(string text, Action action) => new(text) { Clicked = action };
    private void Changed(RichDocument document) => Update();
    private void FormatChanged(RichTextFormattingState state) => Update();
    private void Save() { _saved = _editor.Document.ToJson(); _snapshot.Text = _saved; Update(); }
    private void Restore()
    {
        if (_saved == null) { _snapshot.Text = "Save a snapshot first."; return; }
        _editor.ResetDocument(RichDocument.FromJson(_saved)); _editor.Focus(); Update();
    }
    private void Update()
    {
        var state = _editor.FormattingState;
        _undo.Enabled = state.CanUndo; _redo.Enabled = state.CanRedo;
        _format.Text = $"Format: {state.Format}{(state.MixedFormat ? " (mixed)" : "")} · {(state.Color.HasValue ? "custom color" : "theme color")} · Selection: {state.SelectionLength} · Undo: {state.CanUndo} · Redo: {state.CanRedo}";
        _result.Text = $"Document: {_editor.Text.Length} characters, {_editor.Document.Runs.Count(r => r.Style != default)} styled runs · Snapshot: {(_saved == null ? "none" : "saved")} · Rerenders: {_rerenders}";
    }
    private void Reset()
    {
        _editor.ResetText(Note); _saved = null; _rerenders = 0; _large = false; _editor.ResetFontSize();
        _snapshot.Text = "Save snapshot to inspect its versioned text, styles and colors."; Update();
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        _window.Closing -= Closing; _editor.DocumentChanged -= Changed; _editor.FormattingStateChanged -= FormatChanged;
        _root.Dispose();
    }
    private ValueTask Closing() { Dispose(); return ValueTask.CompletedTask; }
}
