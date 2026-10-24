using System;
using System.Threading.Tasks;

namespace Marius.Winter.Playground.Scenarios;

/// <summary>Cooperative preview-pack installation with inspectable retained logs.</summary>
public sealed class OperationFeedbackScenario : IDisposable, IAsyncDisposable
{
    private readonly Window _window;
    private readonly OperationFeedbackData _data;
    private readonly Panel _root;
    private readonly StatusPanel _status = new();
    private readonly Button _start = new("Install preview pack"), _fail = new("Simulate failure");
    private readonly Button _motion = new("Pause motion"), _progressVisibility = new("Hide progress");
    private readonly Label _result = new() { TextWrapping = ThorVG.TextWrap.Word };
    private readonly Label _following = new();
    private readonly TextArea _notes = new(InitialNote) { MinHeight = 72, MaxHeight = 90 };
    private readonly LogView _log;
    private readonly GifImage _gif = new() { RequestedWidth = 48, RequestedHeight = 40 };
    private readonly LottieImage _lottie = new() { RequestedWidth = 80, RequestedHeight = 40 };
    private Task _shutdown = Task.CompletedTask;
    private bool _disposed;
    private bool _pausedMotion, _showProgress = true;
    public const string InitialNote = "Review the preview pack before installation.\nKeep the raster cover and animated samples.";

    public OperationFeedbackScenario(Window window)
    {
        _window = window;
        _data = new OperationFeedbackData(action => window.Dispatcher.InvokeAsync(action));
        _log = new LogView { Buffer = _data.Logs, LayoutData = new FlexItem { Grow = 1, Basis = 0 }, MinHeight = 100 };
        _root = new Panel { Layout = Column, Padding = new Thickness(8) };
        _root.AddChild(new Label("Operation feedback · Preview pack") { FontSize = 22 });
        _root.AddChild(new Label("Install 40 simulated media previews. Inspect earlier log lines during work, then jump to the latest output.") { TextWrapping = ThorVG.TextWrap.Word });
        var actions = new Panel { Layout = Row, Padding = new Thickness(0) };
        _start.Clicked = () => _ = window.Dispatcher.InvokeAsync(() => _data.StartAsync());
        _fail.Clicked = () => _ = window.Dispatcher.InvokeAsync(() => _data.StartAsync(true));
        actions.AddChild(_start); actions.AddChild(_fail);
        actions.AddChild(new Button("Reset") { Clicked = Reset });
        _motion.Clicked = () => { _pausedMotion = !_pausedMotion; Refresh(); };
        _progressVisibility.Clicked = () => { _showProgress = !_showProgress; Refresh(); };
        actions.AddChild(_motion);
        actions.AddChild(_progressVisibility);
        _root.AddChild(actions);
        _status.Retry = _data.RetryAsync;
        _status.Cancel = _data.CancelAsync;
        _root.AddChild(_status);
        var media = new Panel { Layout = Row, Padding = new Thickness(0) };
        media.AddChild(Sample("cover.png", new Image { Source = OperationFeedbackMedia.Raster, RequestedWidth = 48, RequestedHeight = 40 }));
        _gif.LoadFromBytes(OperationFeedbackMedia.Gif);
        media.AddChild(Sample("motion.gif", _gif));
        _lottie.Source = OperationFeedbackMedia.Lottie;
        media.AddChild(Sample("pulse.json", _lottie));
        media.AddChild(new Label("Samples animate while installing.") { TextWrapping = ThorVG.TextWrap.Word });
        _root.AddChild(media);
        _root.AddChild(new Label("Review note (editable; not written to disk)"));
        _root.AddChild(_notes);
        var logs = new Panel { Layout = Row, Padding = new Thickness(0) };
        logs.AddChild(new Label("Installation log"));
        logs.AddChild(new Button("Jump to latest") { Clicked = () => _log.JumpToLatest() });
        logs.AddChild(new Button("Clear log") { Clicked = () => _log.Clear() });
        logs.AddChild(_following);
        _root.AddChild(logs);
        _root.AddChild(_log);
        _root.AddChild(_result);
        _log.FollowTailChanged = _ => UpdateFollowing();
        _data.Changed += Refresh;
        _window.Closing += Closing;
        Refresh();
    }

    public Element Content => _root;
    private static readonly FlexLayout Column = new() { Direction = Orientation.Vertical, Gap = 6 };
    private static readonly FlexLayout Row = new() { Gap = 6, Wrap = FlexWrap.Wrap, AlignItems = Alignment.Center };
    private static Panel Sample(string name, Element image)
    {
        var panel = new Panel { Layout = new StackLayout { Spacing = 2 }, Padding = new Thickness(2) };
        panel.AddChild(new Label(name)); panel.AddChild(image);
        return panel;
    }
    private void UpdateFollowing() => _following.Text = _log.FollowTail ? "Following latest" : "Inspecting earlier output";
    private void Refresh()
    {
        if (_disposed) return;
        _status.Status = _data.Status;
        _status.IsProgressPaused = _pausedMotion;
        _status.ShowProgress = _showProgress;
        _motion.Text = _pausedMotion ? "Resume motion" : "Pause motion";
        _progressVisibility.Text = _showProgress ? "Hide progress" : "Show progress";
        _start.Enabled = _fail.Enabled = !_data.Status.IsBusy;
        _gif.IsPlaying = _lottie.IsPlaying = _data.Status.IsBusy && !_pausedMotion;
        _result.Text = _data.Result;
        UpdateFollowing();
    }
    private void Reset() { _pausedMotion = false; _showProgress = true; _data.Reset(); _notes.Text = InitialNote; _log.FollowTail = true; UpdateFollowing(); }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _data.Changed -= Refresh;
        _shutdown = _data.DisposeAsync().AsTask();
        _root.Dispose();
        if (_shutdown.IsCompleted) _window.Closing -= Closing;
    }
    public async ValueTask DisposeAsync()
    {
        Dispose();
        await _shutdown;
        _window.Closing -= Closing;
    }
    private ValueTask Closing() => DisposeAsync();
}
