using System;
using System.Linq;
using System.Threading.Tasks;

namespace Marius.Winter.Playground.Scenarios;

/// <summary>A project browser beside a preview, with a separately resizable preview/log split.</summary>
public sealed class ResizableWorkspaceScenario : IDisposable
{
    private readonly Window _window;
    private readonly WorkspacePanel _root;
    private readonly SplitPane _browserSplit, _previewSplit;
    private readonly ScrollPanel _browser;
    private readonly Panel _preview, _alternate;
    private readonly Label _selection = new("Preview: landscape.svg");
    private readonly Label _result = new();
    private readonly TextArea _note = new("Review the selected asset.\nKeep the horizon level.");
    private readonly LogView _log = new();
    private bool _replacement, _disposed;

    public ResizableWorkspaceScenario(Window window)
    {
        _window = window;
        _root = new WorkspacePanel { Layout = new FlexLayout { Direction = Orientation.Vertical, Gap = 8 }, Padding = new Thickness(8) };
        _root.AddChild(new Label("Resizable workspace") { FontSize = 22 });
        _root.AddChild(new Label("Choose an asset, edit its review note, and resize the browser or preview/log divider.") { TextWrapping = ThorVG.TextWrap.Word });
        var actions = new Panel { Layout = new FlexLayout { Wrap = FlexWrap.Wrap, Gap = 6 }, Padding = new Thickness(0) };
        actions.AddChild(Action("Reset", Reset));
        actions.AddChild(Action("Change orientation", Flip));
        actions.AddChild(Action("Replace preview", ReplacePreview));
        actions.AddChild(Action("Hide/show workspace", ToggleVisible));
        _root.AddChild(actions);

        _browser = new ScrollPanel { Layout = new StackLayout { Spacing = 6 }, Padding = new Thickness(8), HorizontalScroll = ScrollMode.Never };
        _browser.AddChild(new Label("Project browser"));
        foreach (string asset in Assets)
        {
            string selected = asset;
            _browser.AddChild(Action(asset, () => { _selection.Text = "Preview: " + selected; _log.Text = LogText("Opened " + selected); UpdateResult(); }));
        }
        _preview = new Panel { Layout = new FlexLayout { Direction = Orientation.Vertical, Gap = 6 }, Padding = new Thickness(8) };
        _preview.AddChild(_selection);
        _preview.AddChild(_note);
        _note.LayoutData = new FlexItem { Grow = 1, Basis = 0 };
        _alternate = new Panel { Layout = new StackLayout(), Padding = new Thickness(8) };
        _alternate.AddChild(new Label("Preview details"));
        _alternate.AddChild(new Label("The replacement keeps the browser and log in place." ) { TextWrapping = ThorVG.TextWrap.Word });
        _alternate.AddChild(new Button("Return to review") { Clicked = RestorePreview });
        _log.Text = LogText("Opened landscape.svg");
        _previewSplit = new SplitPane { Orientation = Orientation.Vertical, Position = 180, ResetPosition = 180, FirstMinimum = 80, SecondMinimum = 70, First = _preview, Second = _log };
        _browserSplit = new SplitPane { Position = 220, FirstMinimum = 130, SecondMinimum = 180, First = _browser, Second = _previewSplit, LayoutData = new FlexItem { Grow = 1, Basis = 0 } };
        _browserSplit.PositionChanged = _ => UpdateResult();
        _previewSplit.PositionChanged = _ => UpdateResult();
        _root.AddChild(_browserSplit);
        _root.AddChild(_result);
        _root.Arranged = UpdateResult;
        _window.Resized += UpdateResult;
        _window.Closing += Closing;
        UpdateResult();
    }

    public Element Content => _root;
    private static Button Action(string text, Action action) => new(text) { Clicked = action };
    private static readonly string[] Assets = new[] { "landscape.svg", "portrait.png", "notes é.txt", "日本語 preview.svg", "Family 👩‍💻.svg" }
        .Concat(Enumerable.Range(1, 24).Select(i => $"Sample {i:00} — landscape review.svg")).ToArray();
    private static string LogText(string action) => action + "\n" + string.Join("\n", Enumerable.Range(1, 28).Select(i => $"{i:00}  Preview ready · review the asset"));
    private void Flip() { _browserSplit.Orientation = _browserSplit.Orientation == Orientation.Horizontal ? Orientation.Vertical : Orientation.Horizontal; UpdateResult(); }
    private void ReplacePreview() { _replacement = !_replacement; _previewSplit.First = _replacement ? _alternate : _preview; UpdateResult(); }
    private void ToggleVisible() { _browserSplit.Visible = !_browserSplit.Visible; UpdateResult(); }
    private void RestorePreview() { _replacement = false; _previewSplit.First = _preview; }
    private void UpdateResult() => _result.Text = $"Browser: {_browserSplit.Position:0} requested / {_browserSplit.VisiblePosition:0} visible · Preview: {_previewSplit.Position:0} / {_previewSplit.VisiblePosition:0} · {_browserSplit.Orientation}";
    private void Reset()
    {
        _browserSplit.Orientation = Orientation.Horizontal;
        _browserSplit.Position = 220;
        _browserSplit.Visible = true;
        _previewSplit.Position = 180;
        _previewSplit.First = _preview;
        _replacement = false;
        _browser.Scroll = 0;
        _selection.Text = "Preview: landscape.svg";
        _note.Text = "Review the selected asset.\nKeep the horizon level.";
        _log.Text = LogText("Opened landscape.svg");
        UpdateResult();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _window.Resized -= UpdateResult;
        _window.Closing -= Closing;
        _root.Arranged = null;
        _root.Dispose();
        _preview.Dispose();
        _alternate.Dispose();
    }
    private ValueTask Closing() { Dispose(); return ValueTask.CompletedTask; }
    private sealed class WorkspacePanel : Panel
    {
        internal Action? Arranged;
        protected override void ArrangeCore(RectF finalBounds) { base.ArrangeCore(finalBounds); Arranged?.Invoke(); }
    }
}
