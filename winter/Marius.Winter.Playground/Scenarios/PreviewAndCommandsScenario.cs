using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Marius.Winter.Playground.Scenarios;

/// <summary>Preview a real SVG; conversion/save are explicitly simulated command results.</summary>
public sealed class PreviewAndCommandsScenario : IDisposable
{
    public const string Harbor = "<svg xmlns='http://www.w3.org/2000/svg' width='240' height='110'><rect width='240' height='110' fill='#79c3e3'/><circle cx='191' cy='25' r='14' fill='#ffdc78'/><path d='M0 75L65 30L119 75L167 44L240 82V110H0Z' fill='#286a72'/><path d='M0 87Q60 76 120 90T240 85V110H0Z' fill='#2066a0'/><path d='M95 78V43L122 75Z' fill='#f8f5d7'/></svg>";
    private readonly Window _window;
    private readonly Panel _body;
    private readonly MenuBar _bar = new();
    private readonly Button _owner = new("Harbor.svg preview");
    private readonly Button _saveButton = new("Save preview");
    private readonly Label _status = new(), _result = new(), _details = new();
    private readonly SvgImage _image = new(Harbor);
    private readonly MenuItemDef _save, _view, _contextSave;
    private readonly Menu _zoom;
    private readonly Dictionary<string, MenuItemDef> _zooms = [];
    private CancellationTokenSource? _conversion;
    private Task _work = Task.CompletedTask;
    private long _generation;
    private bool _ready, _detailed = true, _reverse, _include150 = true, _disposed;
    private string _zoomValue = "100%";
    public Element Content { get; }

    public PreviewAndCommandsScenario(Window window)
    {
        _window = window;
        var scroll = new ScrollPanel { Padding = new Thickness(8), Layout = new StackLayout { Spacing = 8, CrossAlignment = Alignment.Stretch } };
        _body = new Panel { Layout = new StackLayout { Spacing = 8, CrossAlignment = Alignment.Stretch } }; scroll.AddChild(_body); Content = scroll;
        _body.AddChild(new Label("Preview and commands") { FontSize = 22 });
        _body.AddChild(new Label("Convert Harbor.svg, then save the simulated result. F10 enters Preview; arrows, Enter and Escape navigate. Menu or Shift+F10 opens commands at the preview button.") { TextWrapping = ThorVG.TextWrap.Word });
        _body.AddChild(_bar);
        var preview = _bar.AddMenu("Preview");
        _save = preview.InsertItem(0, "Save preview", Save); _save.Shortcut = "Ctrl+S";
        _view = preview.InsertItem(1, "Detailed view", () => { _detailed = !_detailed; Refresh(); _result.Text = _detailed ? "View: detailed." : "View: compact."; });
        preview.AddSeparator(); _zoom = preview.AddSubMenu("Zoom");
        var context = new Menu(); context.AddItem("Inspect source", Inspect); context.AddSeparator();
        _contextSave = context.InsertItem(2, "Save preview", Save); _contextSave.Shortcut = "Ctrl+S"; _owner.ContextMenu = context;
        _owner.Clicked = Inspect; _saveButton.Clicked = Save;
        var actions = Row(); Add(actions, "Convert (simulated)", () => _work = ConvertAsync()); actions.AddChild(_saveButton); Add(actions, "Reset", Reset); _body.AddChild(actions);
        var edits = Row(); Add(edits, "Reverse zoom", () => { _reverse = !_reverse; ReconcileZoom(); }); Add(edits, "Toggle 150%", () => { _include150 = !_include150; ReconcileZoom(); });
        Add(edits, "Toggle preview", TogglePreview); Add(edits, "Toggle commands", ToggleCommands); _body.AddChild(edits);
        _body.AddChild(_owner); _body.AddChild(_image); _body.AddChild(_details); _body.AddChild(_status); _body.AddChild(_result);
        foreach (var label in new[] { _status, _result, _details }) label.TextWrapping = ThorVG.TextWrap.Word;
        Reset(); _window.Closing += Closing;
    }
    private static Panel Row() => new() { Layout = new FlexLayout { Gap = 6, Wrap = FlexWrap.Wrap } };
    private static void Add(Panel row, string label, Action action) => row.AddChild(new Button(label) { Clicked = action });
    private void Inspect() => _result.Text = "Source: Harbor.svg · 240 × 110 · vector preview.";
    private void Save() { if (_ready) _result.Text = "Saved: Harbor.png (simulated; no file written)."; }
    private async Task ConvertAsync()
    {
        _conversion?.Cancel(); _conversion?.Dispose(); var source = new CancellationTokenSource(); _conversion = source;
        long generation = ++_generation; _ready = false; _status.Text = "Converting Harbor.svg (simulated)…"; Refresh();
        try
        {
            await Task.Delay(450, source.Token).ConfigureAwait(false);
            await _window.Dispatcher.InvokeAsync(() =>
            {
                if (_disposed || source.IsCancellationRequested || generation != _generation) return;
                _ready = true; _status.Text = "Converted: Harbor.svg to Harbor.png (simulated)."; Refresh();
            });
        }
        catch (OperationCanceledException) { }
    }
    private void ReconcileZoom()
    {
        string[] keys = _include150 ? ["100%", "125%", "150%"] : ["100%", "125%"];
        if (_reverse) Array.Reverse(keys);
        foreach (var key in _zooms.Keys.Except(keys).ToArray()) { _zoom.RemoveItem(_zooms[key]); _zooms.Remove(key); }
        foreach (var key in keys)
            if (!_zooms.ContainsKey(key)) _zooms.Add(key, _zoom.InsertItem(_zoom.Entries.Count, key, () => SetZoom(key)));
        for (int i = 0; i < keys.Length; i++) _zoom.MoveItem(_zooms[keys[i]], i);
        Refresh();
    }
    private void SetZoom(string key) { _zoomValue = key; Refresh(); _result.Text = "Zoom: " + key + "."; }
    private void Refresh()
    {
        _save.Enabled = _contextSave.Enabled = _saveButton.Enabled = _ready; _view.IsChecked = _detailed;
        foreach (var (key, item) in _zooms) item.IsChecked = key == _zoomValue;
        int zoom = int.Parse(_zoomValue.TrimEnd('%')); _image.RequestedWidth = 240 * zoom / 100; _image.RequestedHeight = 110 * zoom / 100;
        _details.Text = _detailed ? $"Harbor.svg · vector source · {_zoomValue} · detailed view" : $"Harbor.svg · {_zoomValue}";
    }
    private void TogglePreview() { if (_owner.Parent == null) _body.InsertChild(_owner, _body.Children.ToList().IndexOf(_image)); else _body.RemoveChild(_owner); }
    private void ToggleCommands() { if (_bar.Parent == null) _body.InsertChild(_bar, 2); else _body.RemoveChild(_bar); }
    private void Reset()
    {
        _generation++; _conversion?.Cancel(); _ready = false; _detailed = true; _reverse = false; _include150 = true; _zoomValue = "100%";
        if (_bar.Parent == null) _body.InsertChild(_bar, 2); if (_owner.Parent == null) _body.InsertChild(_owner, _body.Children.ToList().IndexOf(_image));
        ReconcileZoom(); _status.Text = "Ready: convert Harbor.svg to enable Save."; _result.Text = "No saved preview.";
    }
    private async ValueTask Closing() { Dispose(); await _work.ConfigureAwait(false); }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true; _generation++; _conversion?.Cancel(); _conversion?.Dispose(); _window.Closing -= Closing;
        // Detached controls retain native paints and belong to this module.
        _owner.Dispose(); _bar.Dispose(); Content.Dispose();
    }
}
