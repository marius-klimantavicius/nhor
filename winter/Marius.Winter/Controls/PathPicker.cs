using System;
using System.Threading.Tasks;

namespace Marius.Winter;

/// <summary>A typed editable path field with Browse. Owns its picker/controller and editor;
/// borrows the provider. Cancel preserves Value. Value assignment is silent and authoritative,
/// including assignment of the previous value after a rejected user edit.</summary>
public sealed class PathPicker : Field
{
    private readonly TextBox _text;
    private readonly Button _browse;
    private readonly FilePickerController _controller;
    private readonly FilePicker _picker;
    private PickerPath? _value;
    private bool _readOnly;
    private FilePickerMode _mode = FilePickerMode.SelectFolder;
    private string _initialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private FilePickerFilter[] _filters = [];
    public PathPicker(IFilePickerProvider? provider = null)
    {
        _text = new TextBox(); Editor = _text;
        _controller = new(provider ?? new LocalFilePickerProvider());
        _picker = new(_controller);
        _browse = new Button("Browse…") { Clicked = () => _ = BrowseAsync() };
        AddChild(_browse); AddChild(_picker);
        _text.UserTextChanged += Edited; _picker.Completed += Completed;
    }
    public PickerPath? Value
    {
        get => _value;
        set { if (_value != value && _controller.State.IsOpen) _controller.Reset(); _value = value; _text.Text = value?.Value ?? ""; }
    }
    public new bool ReadOnly
    {
        get => _readOnly;
        set { _readOnly = value; base.ReadOnly = value; _browse.Enabled = !value; if (value && _controller.State.IsOpen) _controller.Reset(); }
    }
    public IFilePickerProvider Provider
    {
        get => _controller.Provider;
        set { _ = _controller.ReplaceProviderAsync(value); }
    }
    public FilePickerMode Mode
    { get => _mode; set { if (_mode == value) return; _controller.Reset(); _mode = value; } }
    public string InitialDirectory
    { get => _initialDirectory; set { if (_initialDirectory == value) return; _controller.Reset(); _initialDirectory = value; } }
    public FilePickerFilter[] Filters
    {
        get => _filters;
        set
        {
            value ??= [];
            if (ReferenceEquals(_filters, value)) return;
            _filters = value; _picker.Filters = value;
            if (_controller.State.IsOpen) _ = _controller.SetFilterAsync(value.Length > 0 ? value[0] : null);
        }
    }
    public FilePicker Picker => _picker;
    public TextBox TextEditor => _text;
    public event Action<PickerPath?>? ValueChanged;
    public event Action<PickerPath?>? ValueEdited;
    public event Action<FilePickerResult>? Picked;
    public Task BrowseAsync()
    {
        if (!IsEffectivelyEnabled || ReadOnly || IsDisposed) return Task.CompletedTask;
        string directory = InitialDirectory, name = "";
        if (_value is { } path)
        {
            if (Mode == FilePickerMode.SelectFolder) directory = path.Value;
            else
            {
                try { directory = Provider.Parent(path)?.Value ?? InitialDirectory; name = System.IO.Path.GetFileName(path.Value); }
                catch (Exception error) { SetMessages([error.Message]); return Task.CompletedTask; }
            }
        }
        _picker.Filters = Filters;
        return _controller.OpenAsync(Mode, directory, Filters.Length > 0 ? Filters[0] : null, name);
    }
    public void ResetValue(PickerPath? value)
    { _controller.Reset(); Value = value; _text.ResetText(value?.Value ?? ""); SetMessages(null); }
    private void Edited(string text)
    {
        if (ReadOnly) return;
        try
        {
            if (text.Contains('\0')) throw new ArgumentException("A path cannot contain a null character.");
            // Preserve incomplete/relative text for application validation.
            // The provider normalizes accepted picker results.
            var value = string.IsNullOrWhiteSpace(text) ? (PickerPath?)null : new PickerPath(text);
            _value = value; SetMessages(null); ValueChanged?.Invoke(value); ValueEdited?.Invoke(value);
        }
        catch (Exception error) { SetMessages([error.Message]); }
    }
    private void Completed(FilePickerResult result)
    {
        if (result.Accepted && !ReadOnly && IsEffectivelyEnabled)
        { Value = result.Path; SetMessages(null); ValueChanged?.Invoke(_value); }
        Picked?.Invoke(result);
    }
    protected override void OnActivityChanged(bool active)
    { if (!active && _controller.State.IsOpen) _controller.Reset(); base.OnActivityChanged(active); }
    public override void Dispose()
    {
        if (IsDisposed) return;
        _text.UserTextChanged -= Edited; _picker.Completed -= Completed;
        _picker.Dispose(); _controller.Dispose(); _text.Dispose();
        ValueChanged = null; ValueEdited = null; Picked = null;
        base.Dispose();
    }
}
