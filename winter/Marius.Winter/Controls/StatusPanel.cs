using System;
using System.Threading.Tasks;

namespace Marius.Winter;

/// <summary>Operation presentation with observable Task-returning actions. The application owns work and cancellation.</summary>
public sealed class StatusPanel : Panel
{
    private readonly Label _message = new() { TextWrapping = ThorVG.TextWrap.Word };
    private readonly ProgressBar _progress = new() { MinHeight = 12 };
    private readonly Button _retry = new("Retry"), _cancel = new("Cancel");
    private OperationStatus _status = new();
    private Func<Task>? _retryAction, _cancelAction;
    private bool _retryPending, _cancelPending;
    private bool _showProgress = true;
    private long _generation;
    private long _statusRevision, _retryIdentity, _cancelIdentity;

    public StatusPanel()
    {
        Layout = new FlexLayout { Direction = Orientation.Vertical, Gap = 6 };
        Padding = new Thickness(8);
        AddChild(_message);
        AddChild(_progress);
        var actions = new Panel { Layout = new FlexLayout { Gap = 6, Wrap = FlexWrap.Wrap }, Padding = new Thickness(0) };
        actions.AddChild(_retry);
        actions.AddChild(_cancel);
        AddChild(actions);
        _retry.Clicked = () => _ = ObserveAsync(true);
        _cancel.Clicked = () => _ = ObserveAsync(false);
        Refresh();
    }

    public OperationStatus Status { get => _status; set { _status = value ?? new(); _statusRevision++; ActionError = null; Refresh(); } }
    public Func<Task>? Retry
    {
        get => _retryAction;
        set { if (_retryAction == value) return; _retryAction = value; _retryIdentity++; _retryPending = false; Refresh(); }
    }
    public Func<Task>? Cancel
    {
        get => _cancelAction;
        set { if (_cancelAction == value) return; _cancelAction = value; _cancelIdentity++; _cancelPending = false; Refresh(); }
    }
    public bool IsInvokingAction => _retryPending || _cancelPending;
    public bool IsProgressPaused { get => _progress.IsPaused; set => _progress.IsPaused = value; }
    public bool ShowProgress { get => _showProgress; set { _showProgress = value; Refresh(); } }
    public Exception? ActionError { get; private set; }
    public Task RetryAsync() => InvokeAsync(true);
    public Task CancelAsync() => InvokeAsync(false);

    private void Refresh()
    {
        if (IsDisposed) return;
        _message.Text = $"{_status.Severity}: {_status.Message}";
        _progress.Visible = _showProgress && (_status.IsBusy || _status.Progress.HasValue);
        _progress.IsIndeterminate = _status.IsBusy && !_status.Progress.HasValue;
        _progress.Value = _status.Progress ?? 0;
        // Always visible, including before an action becomes available.
        _retry.Enabled = !_retryPending && _status.CanRetry && _retryAction != null;
        _cancel.Enabled = !_cancelPending && _status.IsBusy && _cancelAction != null;
    }

    private Task InvokeAsync(bool retry)
    {
        // Enter the owning scope even on the construction thread: renderer
        // callbacks may await and require its synchronization context.
        return OwnerWindow is { } owner ? owner.Dispatcher.InvokeAsync(() => InvokeCoreAsync(retry)) : InvokeCoreAsync(retry);
    }

    private Task InvokeCoreAsync(bool retry)
    {
        if (IsDisposed || (retry ? _retryPending || !_status.CanRetry : _cancelPending || !_status.IsBusy)) return Task.CompletedTask;
        var action = retry ? _retryAction : _cancelAction;
        if (action == null) return Task.CompletedTask;
        if (retry) _retryPending = true; else _cancelPending = true;
        ActionError = null;
        long generation = _generation;
        long identity = retry ? _retryIdentity : _cancelIdentity;
        long statusRevision = _statusRevision;
        int callerThread = Environment.CurrentManagedThreadId;
        var window = OwnerWindow;
        Refresh();
        return CompleteAsync();

        async Task CompleteAsync()
        {
            Exception? failure = null;
            try { await action().ConfigureAwait(false); }
            catch (Exception ex) { failure = ex; }
            void Finish()
            {
                if (IsDisposed || generation != _generation || identity != (retry ? _retryIdentity : _cancelIdentity)) return;
                if (retry) _retryPending = false; else _cancelPending = false;
                if (_statusRevision == statusRevision) ActionError = failure;
                Refresh();
                if (failure != null && _statusRevision == statusRevision) _message.Text = "Error: " + failure.Message;
            }
            if (window != null) await window.Dispatcher.InvokeAsync(Finish).ConfigureAwait(false);
            else if (callerThread == Environment.CurrentManagedThreadId) Finish();
            else if (generation == _generation && !IsDisposed && identity == (retry ? _retryIdentity : _cancelIdentity))
            {
                // Detached asynchronous completion updates no paints. Attachment
                // refreshes presentation on the next owning UI thread.
                if (retry) _retryPending = false; else _cancelPending = false;
                if (_statusRevision == statusRevision) ActionError = failure;
            }
            if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private async Task ObserveAsync(bool retry)
    {
        try { await InvokeAsync(retry).ConfigureAwait(false); }
        catch (Exception) { /* ActionError and visible message retain the failure. */ }
    }

    protected override void OnDetaching() { _generation++; _retryPending = _cancelPending = false; base.OnDetaching(); }
    protected override void OnAttached() { base.OnAttached(); Refresh(); }
    protected override void OnDisposed()
    {
        _generation++;
        _retryAction = _cancelAction = null;
        _retry.Clicked = _cancel.Clicked = null;
        base.OnDisposed();
    }
}
