using System;
using System.Threading;
using System.Threading.Tasks;

namespace Marius.Winter.Playground.Scenarios;

public sealed class FilesScenario : IDisposable
{
    private readonly Window _window;
    private readonly Panel _root;
    private readonly ScrollPanel _scroll;
    private readonly FilePickerController _controller;
    private readonly FilePicker _picker;
    private readonly PathPicker _destination;
    private FilesFixtureProvider _provider = new();
    private readonly Label _result = new("No file selected. Use the fixture; no files are written.") { TextWrapping = ThorVG.TextWrap.Word };
    private bool _disposed, _large;
    private CancellationTokenSource? _switchCancellation;
    private Task _switch = Task.CompletedTask;
    private long _generation;
    private readonly object _switchGate = new();
    private static readonly FilePickerFilter[] Filters = [new("SVG and Lottie", ".svg", ".json"), new("SVG", ".svg"), new("All files")];
    public FilesScenario(Window window)
    {
        _window = window;
        _root = new Panel { Layout = new FlexLayout { Direction = Orientation.Vertical, Gap = 10 }, Padding = new Thickness(12) };
        _scroll = new ScrollPanel { Layout = new StackLayout(), HorizontalScroll = ScrollMode.Never }; _scroll.AddChild(_root);
        _controller = new(_provider); _picker = new(_controller) { Filters = Filters };
        _destination = new(_provider) { Label = "Export folder", InitialDirectory = "/Files", Value = new PickerPath("/Files"), HelpText = "Edit the path or Browse. Cancel keeps the previous folder." };
        _destination.ValueChanged += DestinationChanged;
        _picker.Completed += Completed;
        _root.AddChild(new Label("Files") { FontSize = 22 });
        _root.AddChild(new Label("Prepare an export: open cover.svg, save an existing name to confirm overwrite, or choose a folder. This deterministic fixture never writes file contents.") { TextWrapping = ThorVG.TextWrap.Word });
        var actions = Row();
        actions.AddChild(Action("Open file", () => Open(FilePickerMode.OpenFile)));
        actions.AddChild(Action("Save file", () => Open(FilePickerMode.SaveFile)));
        actions.AddChild(Action("Choose folder", () => Open(FilePickerMode.SelectFolder)));
        actions.AddChild(Action("Retryable listing", () => { _provider.FailNext = true; Open(FilePickerMode.OpenFile); }));
        actions.AddChild(Action("Pending listing", () => { _provider.SlowNext = true; Open(FilePickerMode.OpenFile); }));
        actions.AddChild(Action("Replace pending provider", () => _switch = ReplacePendingAsync()));
        actions.AddChild(Action("Reset", Reset)); _root.AddChild(actions);
        _root.AddChild(_destination);
        var options = Row();
        var readOnly = new Checkbox("Read-only destination"); readOnly.Changed = value => _destination.ReadOnly = value; options.AddChild(readOnly);
        var disabled = new Checkbox("Disable destination"); disabled.Changed = value => _destination.Enabled = !value; options.AddChild(disabled);
        options.AddChild(Action("Replace provider", () => { _provider = new(); _ = _controller.ReplaceProviderAsync(_provider); _destination.Provider = _provider; _result.Text = "Provider replaced. Previous pending work cannot change this fixture."; }));
        _root.AddChild(options); _root.AddChild(_result);
        var theme = Row(); theme.AddChild(Action("Light", () => window.Theme = Theme.Light)); theme.AddChild(Action("Dark", () => window.Theme = Theme.Dark));
        theme.AddChild(Action("Larger text", () => { _large = !_large; foreach (var style in new[] { window.Theme.Label, window.Theme.Panel, window.Theme.Button, window.Theme.TextBox, window.Theme.Checkbox }) style.FontSize = _large ? 22 : 14; }));
        _root.AddChild(theme); _root.AddChild(_picker);
        window.Closing += Closing;
    }
    public Element Content => _scroll;
    private static Panel Row() => new() { Layout = new FlexLayout { Gap = 8, Wrap = FlexWrap.Wrap }, Padding = new Thickness(0) };
    private static Button Action(string text, Action action) => new(text) { Clicked = action };
    private void Open(FilePickerMode mode)
    { CancelSwitch(); _ = _controller.OpenAsync(mode, "/Files", Filters[0], mode == FilePickerMode.SaveFile ? "cover.svg" : ""); }
    private void CancelSwitch() { _generation++; lock (_switchGate) _switchCancellation?.Cancel(); }
    private async Task ReplacePendingAsync()
    {
        _provider.SlowNext = true; Open(FilePickerMode.OpenFile);
        using var source = new CancellationTokenSource(); lock (_switchGate) _switchCancellation = source; var generation = _generation;
        try
        {
            await Task.Delay(250, source.Token).ConfigureAwait(false);
            await _window.Dispatcher.InvokeAsync(() =>
            {
                if (_disposed || source.IsCancellationRequested || generation != _generation) return;
                _provider = new(); _ = _controller.ReplaceProviderAsync(_provider); _destination.Provider = _provider;
                _result.Text = "Pending provider replaced; the new fixture is ready.";
            });
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested) { }
        finally { lock (_switchGate) if (ReferenceEquals(_switchCancellation, source)) _switchCancellation = null; }
    }
    private void DestinationChanged(PickerPath? path) => _result.Text = "Export folder: " + (path?.Value ?? "(empty)");
    private void Completed(FilePickerResult result) => _result.Text = result.Accepted ? $"{result.Mode}: {result.Path} · overwrite {(result.Overwrite ? "confirmed" : "not needed")}. No file contents written." : "Canceled; previous export folder retained.";
    private void Reset()
    {
        CancelSwitch();
        _controller.Reset(); _destination.ResetValue(new("/Files")); _destination.ReadOnly = false; _destination.Enabled = true;
        foreach (var box in _root.Children[4].Children) if (box is Checkbox c) c.IsChecked = false;
        _provider.FailNext = _provider.SlowNext = false; _result.Text = "No file selected. Use the fixture; no files are written.";
    }
    private async ValueTask Closing() { Dispose(); await _switch.ConfigureAwait(false); }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true; CancelSwitch(); _window.Closing -= Closing;
        _picker.Completed -= Completed; _destination.ValueChanged -= DestinationChanged;
        _scroll.Dispose(); _controller.Dispose();
    }
}
