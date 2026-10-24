using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Marius.Winter.Playground.Scenarios;

/// <summary>A transactional profile editor alongside the existing modeless dialog API.</summary>
public sealed class WindowsAndDialogsScenario : IDisposable
{
    private readonly Window _window;
    private readonly Panel _root;
    private readonly Button _opener;
    private readonly Label _result = new() { TextWrapping = ThorVG.TextWrap.Word };
    private readonly Label _profile = new();
    private readonly Checkbox _backdrop = new("Close on backdrop");
    private ModalDialog? _modal;
    private Panel? _body, _footer;
    private TextBox _name = null!;
    private TextArea _biography = null!;
    private ComboBox _environment = null!;
    private Checkbox _protect = null!;
    private Label _error = null!;
    private DialogWindow? _settings;
    private string _savedName = "Marius", _savedBio = "Designing a quiet workspace.";
    private int _savedEnvironment, _counter;
    private bool _disposed, _large;

    public WindowsAndDialogsScenario(Window window)
    {
        _window = window;
        _root = new Panel { Layout = new FlexLayout { Direction = Orientation.Vertical, Gap = 10 }, Padding = new Thickness(12) };
        _root.AddChild(new Label("Windows and dialogs") { FontSize = 22 });
        _root.AddChild(new Label("Edit your profile, save it, or cancel to keep the original. The background counter waits while the editor is open.") { TextWrapping = ThorVG.TextWrap.Word });
        var actions = Row();
        _opener = Action("Edit profile", Open); actions.AddChild(_opener);
        actions.AddChild(Action("Background counter", () => { _counter++; Update(); }));
        actions.AddChild(Action("Modeless Settings", ShowSettings));
        actions.AddChild(Action("Recreate dialog", () => { RemoveModal(); CreateModal(); _result.Text = "Editor recreated. Open it to edit your profile."; }));
        actions.AddChild(Action("Reset", Reset)); _root.AddChild(actions);
        _root.AddChild(_backdrop); _root.AddChild(_profile); _root.AddChild(_result);
        var view = Row();
        view.AddChild(Action("Light", () => window.Theme = Theme.Light));
        view.AddChild(Action("Dark", () => window.Theme = Theme.Dark));
        view.AddChild(Action("Larger text", () => { _large = !_large; SetSize(); })); _root.AddChild(view);
        _backdrop.Changed = value => { if (_modal != null) _modal.CloseOnBackdrop = value; };
        window.Closing += Closing;
        CreateModal(); Reset();
    }
    public Element Content => _root;
    private static Panel Row() => new() { Layout = new FlexLayout { Wrap = FlexWrap.Wrap, Gap = 8 }, Padding = new Thickness(0) };
    private static Button Action(string text, Action action) => new(text) { Clicked = action };
    private void CreateModal()
    {
        _name = new TextBox(); _biography = new TextArea() { MinHeight = 76 };
        _environment = new ComboBox(Enumerable.Range(0, 24).Select(i => i == 0 ? "Personal studio" : $"Shared studio {i:00} · Žąsis").ToArray());
        _protect = new Checkbox("Protect unsaved edits (checks before cancel)");
        _error = new Label() { TextWrapping = ThorVG.TextWrap.Word };
        _body = new Panel { Layout = new FlexLayout { Direction = Orientation.Vertical, Gap = 6 }, Padding = new Thickness(0) };
        _body.AddChild(new Label("Display name (required)")); _body.AddChild(_name);
        _body.AddChild(new Label("Workspace")); _body.AddChild(_environment);
        _body.AddChild(new Label("Biography (Enter adds a line)")); _body.AddChild(_biography);
        _body.AddChild(_protect); _body.AddChild(_error);
        _footer = Row();
        _footer.AddChild(Action("Save", () => _ = _modal?.RequestCloseAsync(DialogCloseReason.Accepted)));
        _footer.AddChild(Action("Cancel", () => _ = _modal?.RequestCloseAsync(DialogCloseReason.Cancel)));
        _footer.AddChild(Action("Remove opener", () => { _root.Children[2].RemoveChild(_opener); _result.Text = "Opener removed; closing will choose another available action."; }));
        _footer.AddChild(Action("Remove dialog", () => { RemoveModal(); _result.Text = "Editor removed; profile kept. Recreate it to continue."; }));
        _modal = new ModalDialog { Title = "Edit profile", Width = 560, Height = 440, Body = _body, Footer = _footer, InitialFocus = () => _name,
            DefaultAction = Validate, CloseGuard = Guard, CloseOnBackdrop = _backdrop.IsChecked };
        _modal.Closed += Closed;
        _root.AddChild(_modal); if (_large) SetSize();
    }
    private ValueTask<bool> Validate(CancellationToken token)
    {
        bool valid = !string.IsNullOrWhiteSpace(_name.Text);
        _error.Text = valid ? "" : "Enter a display name before saving.";
        if (!valid) _window.SetFocus(_name);
        return ValueTask.FromResult(valid);
    }
    private async ValueTask<bool> Guard(DialogResult result, CancellationToken token)
    {
        if (!_protect.IsChecked || result.Accepted) return true;
        _error.Text = "Checking unsaved edits…";
        await Task.Delay(350, token);
        bool unchanged = _name.Text == _savedName && _biography.Text == _savedBio && _environment.SelectedIndex == _savedEnvironment;
        _error.Text = unchanged ? "" : "Unsaved edits: Save, or turn off protection to discard them.";
        return unchanged;
    }
    private void Open()
    {
        if (_modal == null) CreateModal();
        _name.ResetText(_savedName); _biography.ResetText(_savedBio); _environment.SelectedIndex = _savedEnvironment;
        _protect.IsChecked = false; _error.Text = ""; _modal!.IsOpen = true;
    }
    private void Closed(DialogResult result)
    {
        if (result.Accepted) { _savedName = _name.Text; _savedBio = _biography.Text; _savedEnvironment = _environment.SelectedIndex; }
        _result.Text = result.Accepted ? $"Saved profile: {_savedName}." : $"Canceled ({result.Reason}); kept {_savedName}.";
        Update();
    }
    private void Update() => _profile.Text = $"Profile: {_savedName} · Background counter: {_counter}";
    private void SetSize()
    {
        if (_modal == null) return;
        var size = _large ? 22 : 16;
        foreach (var style in new[] { _window.Theme.Panel, _window.Theme.TextBox, _window.Theme.Button, _window.Theme.Label, _window.Theme.Checkbox }) style.FontSize = size;
    }
    private void ShowSettings()
    {
        if (_settings != null) { _settings.Visible = true; return; }
        _settings = new DialogWindow("Modeless Settings") { Bounds = new RectF(60, 100, 280, 170), Layout = new StackLayout { Spacing = 8 } };
        _settings.AddChild(new Label("Background actions remain available."));
        _settings.AddChild(new Checkbox("Show profile hints") { IsChecked = true });
        _settings.CloseClicked = () => _settings.Visible = false; _root.AddChild(_settings);
    }
    private void RemoveModal()
    {
        _modal?.Dispose(); _modal = null;
        _body?.Dispose(); _body = null; _footer?.Dispose(); _footer = null;
    }
    private void Reset()
    {
        if (_modal != null) _modal.IsOpen = false;
        if (_opener.Parent == null) ((Panel)_root.Children[2]).InsertChild(_opener, 0);
        _savedName = "Marius"; _savedBio = "Designing a quiet workspace."; _savedEnvironment = _counter = 0;
        _backdrop.IsChecked = false; bool wasLarge = _large; _large = false; if (wasLarge) SetSize();
        if (_settings != null) _settings.Visible = false;
        _result.Text = "Ready to edit. Save accepts changes; Escape or Cancel keeps the original."; Update();
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        _window.Closing -= Closing;
        RemoveModal(); if (_opener.Parent == null) _opener.Dispose();
        _root.Dispose();
    }
    private ValueTask Closing() { Dispose(); return ValueTask.CompletedTask; }
}
