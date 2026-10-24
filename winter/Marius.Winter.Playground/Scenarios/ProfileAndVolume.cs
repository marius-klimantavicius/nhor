using System;
using System.Threading;
using System.Threading.Tasks;
using Marius.Winter;

namespace Marius.Winter.Playground.Scenarios;

public sealed class ProfileAndVolume : IDisposable
{
    private readonly Window _window;
    private readonly TextBox _name = new("Milda");
    private readonly TextArea _notes = new("Studio profile");
    private readonly Checkbox _shortcut = new("Create desktop shortcut");
    private readonly Checkbox _protect = new("Protect name");
    private readonly Checkbox _limit = new("Limit volume to 60%");
    private readonly ComboBox _profile = new(["Personal", "Studio"]);
    private readonly Slider _volume = new(0, 100, 40) { Step = 1 };
    private readonly ProgressBar _progress = new(.4f);
    private readonly Label _summary = new();
    private readonly Label _commit = new("Committed edit: none");
    private readonly Label _saved = new("No saved profile yet");
    private CancellationTokenSource? _saveCancellation;
    private Task _saveTask = Task.CompletedTask;
    private long _generation;
    private string _acceptedName = "Milda";
    private bool _disposed;
    public Element Content { get; }

    public ProfileAndVolume(Window window)
    {
        _window = window;
        var root = new ScrollPanel { Layout = new FlexLayout { Direction = Orientation.Vertical, Gap = 8 }, Padding = new Thickness(8) };
        Content = root;
        root.AddChild(new Label("Profile and volume") { FontSize = 22 });
        root.AddChild(new Label("Enter commits names; Ctrl+Enter commits notes."));
        root.AddChild(new Label("Name")); root.AddChild(_name);
        var options = new Panel { Layout = new FlexLayout { Wrap = FlexWrap.Wrap, Gap = 20 } };
        options.AddChild(_shortcut); options.AddChild(_protect); root.AddChild(options);
        root.AddChild(new Label("Profile type")); root.AddChild(_profile);
        root.AddChild(new Label("Volume")); root.AddChild(_volume); root.AddChild(_progress); root.AddChild(_limit);
        root.AddChild(new Label("Notes")); _notes.MinHeight = 84; _notes.MaxHeight = 84; root.AddChild(_notes);
        var actions = new Panel { Layout = new FlexLayout { Wrap = FlexWrap.Wrap, Gap = 10 } };
        actions.AddChild(new Button("Reset") { Clicked = Reset });
        actions.AddChild(new Button("Clear name") { Clicked = ClearName });
        actions.AddChild(new Button("Save after delay") { Clicked = StartSave }); root.AddChild(actions);
        root.AddChild(_summary); root.AddChild(_commit); root.AddChild(_saved);
        _name.UserTextChanged += NameChanged; _notes.UserTextChanged += NotesChanged;
        _name.TextCommitted += Committed; _notes.TextCommitted += Committed;
        _shortcut.UserChanged += ShortcutChanged; _profile.UserSelectionChanged += ProfileChanged;
        _volume.UserValueChanged += VolumeChanged; _limit.UserChanged += LimitChanged;
        _window.Closing += Closing;
        Refresh();
    }

    private void NameChanged(string value)
    {
        if (_protect.IsChecked) _name.Text = _acceptedName;
        else _acceptedName = value;
        Refresh();
    }
    private void NotesChanged(string _) => Refresh();
    private void ShortcutChanged(bool _) => Refresh();
    private void ProfileChanged(int _) => Refresh();
    private void VolumeChanged(float _) => Refresh();
    private void LimitChanged(bool limited) { _volume.SetRange(0, limited ? 60 : 100, _volume.Value, 1); Refresh(); }
    private void Committed(TextCommitEventArgs args) => _commit.Text = $"Committed edit ({args.Reason}): {args.Text.Replace("\n", " / ")}";
    private void Refresh()
    {
        CancelSave();
        _progress.Value = _volume.Value / 100;
        _summary.Text = $"Profile: {_name.Text} · {_profile.SelectedItem} · shortcut {(_shortcut.IsChecked ? "on" : "off")} · volume {_volume.Value:0}%";
    }
    private void ClearName() { _acceptedName = ""; _name.ResetText(""); Refresh(); }
    private void Reset()
    {
        CancelSave(); _acceptedName = "Milda"; _name.ResetText("Milda"); _notes.ResetText("Studio profile");
        _shortcut.IsChecked = false; _protect.IsChecked = false; _limit.IsChecked = false;
        _profile.SelectedIndex = 0; _volume.SetRange(0, 100, 40, 1);
        _commit.Text = "Committed edit: none"; Refresh(); _saved.Text = "No saved profile yet";
    }
    private void CancelSave()
    {
        _generation++; _saveCancellation?.Cancel();
        if (!_saveTask.IsCompleted && !_disposed) _saved.Text = "Profile changed; save again";
    }
    private void StartSave()
    {
        CancelSave(); var generation = _generation; var summary = _summary.Text;
        _saveCancellation?.Dispose(); _saveCancellation = new CancellationTokenSource();
        _saved.Text = "Saving profile…";
        var save = SaveAsync(generation, summary, _saveCancellation.Token);
        _saveTask = _saveTask.IsCompleted ? save : Task.WhenAll(_saveTask, save);
    }
    private async Task SaveAsync(long generation, string summary, CancellationToken token)
    {
        try
        {
            // Cancellation must complete even after the ordinary UI context stops accepting work.
            await Task.Delay(650, token).ConfigureAwait(false);
            await _window.Dispatcher.InvokeAsync(() =>
            {
                if (!_disposed && generation == _generation && !token.IsCancellationRequested)
                    _saved.Text = "Saved: " + summary;
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
    }
    private ValueTask Closing() { CancelSave(); return new ValueTask(_saveTask); }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true; CancelSave(); _saveCancellation?.Dispose();
        _window.Closing -= Closing;
        _name.UserTextChanged -= NameChanged; _notes.UserTextChanged -= NotesChanged;
        _name.TextCommitted -= Committed; _notes.TextCommitted -= Committed;
        _shortcut.UserChanged -= ShortcutChanged; _profile.UserSelectionChanged -= ProfileChanged;
        _volume.UserValueChanged -= VolumeChanged; _limit.UserChanged -= LimitChanged;
        Content.Dispose();
    }
}
