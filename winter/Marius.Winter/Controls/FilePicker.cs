using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Marius.Winter;

/// <summary>A reusable single in-window modal over a caller-owned picker controller.
/// Owns its composed controls, never the supplied controller/provider. Detaching silently
/// resets the active session and cancels its work. Dispose even when already detached.</summary>
public sealed class FilePicker : Panel
{
    private readonly ModalDialog _modal;
    private readonly Panel _body, _footer, _navigation, _confirmation;
    private readonly TextBox _location = new(), _name = new();
    private readonly Field _nameField;
    private readonly ComboBox _filter = new();
    private readonly Button _back, _up, _go, _accept, _yes, _no;
    private readonly Label _message = new() { TextWrapping = ThorVG.TextWrap.Word };
    private readonly Label _overwrite = new() { TextWrapping = ThorVG.TextWrap.Word };
    private readonly TreeController<FilePickerEntry> _tree;
    private readonly DataTree<FilePickerEntry> _entries;
    private FilePickerController? _controller;
    private IReadOnlyList<FilePickerEntry>? _items;
    private IReadOnlyList<FilePickerFilter> _filters = Array.Empty<FilePickerFilter>();
    private bool _synchronizing, _observing;
    private long _identity;
    private long _confirmationId;
    private Window? _owner;
    public event Action<FilePickerResult>? Completed;

    public FilePicker()
    {
        Padding = new Thickness(0);
        _body = new Panel { Layout = new FlexLayout { Direction = Orientation.Vertical, Gap = 8 }, Padding = new Thickness(0) };
        _navigation = Row();
        _back = Action("Back", () => Run(c => c.BackAsync()));
        _up = Action("Up", () => Run(c => c.UpAsync()));
        _go = Action("Go", () => Run(c => c.NavigateAsync(new(_location.Text))));
        _location.LayoutData = new FlexItem { Grow = 1, Shrink = 1, Basis = 0 }; _location.MinWidth = 90;
        _navigation.AddChild(_back); _navigation.AddChild(_up); _navigation.AddChild(_location); _navigation.AddChild(_go);
        _tree = new(i => i.Path.Value, i => i.Name, iconSelector: i => i.IsDirectory ? FolderIcon : FileIcon);
        _entries = new DataTree<FilePickerEntry>(_tree) { LayoutData = new FlexItem { Grow = 1, Shrink = 1, Basis = 150 }, MinHeight = 90 };
        _entries.Requested += TreeRequested;
        _filter.UserSelectionChanged += FilterChanged;
        _name.UserTextChanged += NameChanged;
        _nameField = new Field("File name", _name) { MinWidth = 120 };
        _body.AddChild(_navigation); _body.AddChild(_filter); _body.AddChild(_entries); _body.AddChild(_nameField); _body.AddChild(_message);
        var retry = Action("Retry listing", () => Run(c => c.RetryAsync())); _body.AddChild(retry);
        _confirmation = new Panel { Layout = new FlexLayout { Direction = Orientation.Vertical, Gap = 8 }, Padding = new Thickness(0) };
        _confirmation.AddChild(_overwrite);
        _footer = Row();
        _accept = Action("Open", () => Run(c => c.AcceptAsync()));
        _yes = Action("Overwrite", () => _controller?.ConfirmOverwrite(_confirmationId, true));
        _no = Action("Keep existing", () => _controller?.ConfirmOverwrite(_confirmationId, false));
        _footer.AddChild(_accept); _footer.AddChild(_yes); _footer.AddChild(_no);
        _footer.AddChild(Action("Cancel", () => _controller?.Cancel()));
        _modal = new ModalDialog { Width = 660, Height = 570, Body = _body, Footer = _footer,
            InitialFocus = () => _controller?.State.Mode == FilePickerMode.SaveFile ? _name : _entries,
            DefaultAction = async _ => { if (_controller != null) await _controller.AcceptAsync(); return false; } };
        _modal.Closed += ModalClosed;
        AddChild(_modal);
        retry.Visible = false;
    }
    internal const string FolderIcon = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"16\" height=\"16\"><path d=\"M1 3h6l2 2h6v9H1z\" fill=\"#e8a838\"/></svg>";
    internal const string FileIcon = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"16\" height=\"16\"><path d=\"M3 1h7l3 3v11H3z\" fill=\"#72a7d6\"/></svg>";
    public FilePicker(FilePickerController controller) : this() => SetController(controller);
    public FilePickerController? Controller => _controller;
    public ModalDialog Dialog => _modal;
    public float DialogWidth { get => _modal.Width; set => _modal.Width = value; }
    public float DialogHeight { get => _modal.Height; set => _modal.Height = value; }
    public IReadOnlyList<FilePickerFilter> Filters
    {
        get => _filters;
        set { if (ReferenceEquals(_filters, value)) return; _filters = value ?? Array.Empty<FilePickerFilter>(); _filter.SetItems(_filters.Select(f => f.Name).ToArray()); Refresh(); }
    }
    public void SetController(FilePickerController controller)
    {
        ArgumentNullException.ThrowIfNull(controller); ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (ReferenceEquals(controller, _controller)) return;
        StopObserving(); _identity++;
        _modal.IsOpen = false;
        if (_controller?.State.IsOpen == true) _controller.Reset();
        _controller = controller; _items = null;
        if (OwnerWindow != null) Observe();
        Refresh();
    }
    private static Panel Row() => new() { Layout = new FlexLayout { Gap = 8, Wrap = FlexWrap.Wrap, AlignItems = Alignment.Center }, Padding = new Thickness(0) };
    private static Button Action(string text, Action action) => new(text) { Clicked = action };
    private void Run(Func<FilePickerController, Task> action) { if (_controller != null) _ = action(_controller); }
    private void NameChanged(string text) { if (!_synchronizing) _controller?.SetFileName(text); }
    private void FilterChanged(int index) { if (!_synchronizing && index >= 0 && index < _filters.Count) Run(c => c.SetFilterAsync(_filters[index])); }
    private void TreeRequested(TreeRequest<FilePickerEntry> request)
    {
        if (_synchronizing) return;
        if (request.Kind == TreeRequestKind.Select) _controller?.Select(request.SelectedKey == null ? null : request.Item.Path);
        if (request.Kind == TreeRequestKind.Activate) Run(c => c.ActivateAsync(request.Item.Path));
    }
    private void ModalClosed(DialogResult result) { if (!result.Accepted && _controller?.State.IsOpen == true) _controller.Cancel(); }
    private void Observe()
    {
        if (_observing || _controller == null) return;
        _observing = true; _controller.Changed += Changed; _controller.Completed += Result;
    }
    private void StopObserving()
    {
        if (!_observing || _controller == null) return;
        _observing = false; _controller.Changed -= Changed; _controller.Completed -= Result;
    }
    private void Dispatch(Action action)
    {
        var window = OwnerWindow; var controller = _controller; var identity = _identity;
        if (window == null) return;
        void Apply() { if (!IsDisposed && _observing && identity == _identity && ReferenceEquals(window, OwnerWindow) && ReferenceEquals(controller, _controller)) action(); }
        if (window.Dispatcher.CheckAccess()) Apply(); else _ = window.Dispatcher.InvokeAsync(Apply);
    }
    private void Changed() => Dispatch(Refresh);
    private void Result(FilePickerResult result)
    {
        var controller = _controller; var revision = controller?.Revision;
        Dispatch(() => { if (controller?.Revision == revision && controller?.State.SessionId == result.SessionId) Completed?.Invoke(result); });
    }
    private void Refresh()
    {
        if (IsDisposed || _controller == null) return;
        var state = _controller.State;
        _synchronizing = true;
        try
        {
            _modal.Title = state.Mode switch { FilePickerMode.SaveFile => "Save file", FilePickerMode.SelectFolder => "Select folder", _ => "Open file" };
            _accept.Text = state.Mode switch { FilePickerMode.SaveFile => "Save", FilePickerMode.SelectFolder => "Select folder", _ => "Open" };
            _location.Text = state.CurrentDirectory.Value ?? ""; _name.Text = state.FileName;
            _back.Enabled = state.CanGoBack; _up.Enabled = true;
            // Folder mode has no filename editor. Retain it detached so it does
            // not participate in layout, theme or focus for this mode.
            if (state.Mode == FilePickerMode.SelectFolder && _nameField.Parent != null) _body.RemoveChild(_nameField);
            else if (state.Mode != FilePickerMode.SelectFolder && _nameField.Parent == null) _body.InsertChild(_nameField, 3);
            _filter.Visible = state.Mode != FilePickerMode.SelectFolder && _filters.Count > 0;
            _filter.SelectedIndex = _filters.ToList().FindIndex(f => ReferenceEquals(f, state.Filter) || f == state.Filter);
            if (!ReferenceEquals(_items, state.Items)) { _items = state.Items; _tree.SetItems(state.Items, state.SelectedPath?.Value, null); }
            else _tree.Synchronize(state.SelectedPath?.Value, null);
            // Keep keyboard entry stable while a directory loads. Empty loading
            // rows have no selectable items; disabling the tree would move modal
            // focus to its close button and strand the first keyboard interaction.
            _entries.Enabled = !state.IsChecking;
            _accept.Enabled = state.LoadState == FilePickerLoadState.Ready && !state.IsChecking;
            _message.Text = state.Error ?? (state.IsChecking ? "Checking selection…" : state.LoadState switch
            { FilePickerLoadState.Loading => "Loading folder…", FilePickerLoadState.Ready when state.Items.Count == 0 => "This folder has no matching items.", _ => "Select an item. Enter opens a folder or file; Up and Back navigate." });
            _nameField.SetMessages(state.Error == null ? null : [state.Error]);
            _body.Children[^1].Visible = state.LoadState == FilePickerLoadState.Error;
            bool confirm = state.Overwrite != null;
            _accept.Visible = !confirm; _yes.Visible = _no.Visible = confirm;
            if (state.Overwrite is { } overwrite)
            {
                _confirmationId = overwrite.Id; _overwrite.Text = $"{overwrite.Path.Value} already exists. Replace this file?";
            }
            var nextBody = confirm ? _confirmation : _body;
            if (!ReferenceEquals(_modal.Body, nextBody)) { _modal.Body = nextBody; if (_modal.IsOpen) OwnerWindow?.SetFocus(confirm ? _no : _name); }
            _modal.IsOpen = state.IsOpen;
        }
        finally { _synchronizing = false; }
    }
    protected override void OnAttached()
    {
        base.OnAttached(); _owner = OwnerWindow; if (_owner != null) _owner.Closing += Closing;
        Observe(); Refresh();
    }
    protected override void OnDetaching()
    {
        StopObserving(); _identity++;
        if (_owner != null) _owner.Closing -= Closing; _owner = null;
        if (!_modal.IsDisposed) _modal.IsOpen = false;
        if (_controller?.State.IsOpen == true) _controller.Reset();
        base.OnDetaching();
    }
    private ValueTask Closing() { _controller?.Reset(); return ValueTask.CompletedTask; }
    public override void Dispose()
    {
        if (IsDisposed) return;
        StopObserving(); _identity++;
        if (_owner != null) _owner.Closing -= Closing; _owner = null;
        if (_controller?.State.IsOpen == true) _controller.Reset();
        _modal.Closed -= ModalClosed; _entries.Requested -= TreeRequested;
        _modal.Dispose(); _body.Dispose(); _nameField.Dispose(); _confirmation.Dispose(); _footer.Dispose(); _name.Dispose(); _tree.Dispose();
        Completed = null; _controller = null;
        base.Dispose();
    }
}
