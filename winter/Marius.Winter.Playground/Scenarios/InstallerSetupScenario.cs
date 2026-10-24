using System;
using System.Linq;
using System.Threading.Tasks;
using Marius.Winter;

namespace Marius.Winter.Playground.Scenarios;

/// <summary>A simulated installer configuration using native, caller-owned editors.</summary>
public sealed class InstallerSetupScenario : IDisposable
{
    public record EnvironmentOption(string Id, string Name);
    private readonly EnvironmentOption[] _environments = [new("production", "Production"), new("staging", "Staging"), new("development", "Development")];
    private readonly Form _form = new();
    private readonly TextBox _name = new("MyApp"), _destination = new("/opt/myapp");
    private readonly TextArea _notes = new("Configuration notes") { MinHeight = 64, MaxHeight = 64 };
    private readonly Select<EnvironmentOption, string> _environment = new();
    private readonly Checkbox _shortcut = new("Create desktop shortcut", true);
    private readonly Checkbox _readOnly = new("Read-only destination"), _protect = new("Keep application name");
    private readonly Checkbox _disableNotes = new("Disable notes");
    private readonly Field _nameField, _destinationField, _notesField;
    private readonly Label _result = Wrap("No configuration submitted.");
    private int _submissions, _modelNumber = 1;
    private string _acceptedName = "MyApp";
    private bool _disposed;
    private readonly Window _window;
    public Element Content { get; }
    public InstallerSetupScenario(Window window)
    {
        _window = window;
        _window.Closing += Closing;
        var root = new ScrollPanel { Layout = Column(), Padding = new Thickness(8), HorizontalScroll = ScrollMode.Never };
        Content = root;
        root.AddChild(new Label("Installer setup") { FontSize = 22 });
        root.AddChild(Wrap("Prepare a simulated installation. Enter submits text fields; notes keep newlines. No files are created."));
        root.AddChild(_form);
        _nameField = new Field("Application name", _name) { HelpText = "Name shown in the launcher.", Validate = () => string.IsNullOrWhiteSpace(_name.Text) ? ["Enter an application name."] : [] };
        _destinationField = new Field("Destination", _destination)
        {
            HelpText = "Use an absolute destination beginning with /. This is configuration only; the destination is never created or changed.",
            Validate = () => string.IsNullOrWhiteSpace(_destination.Text) || !_destination.Text.StartsWith('/') ? ["Enter an absolute destination beginning with /, for example /opt/myapp. Choose a location that your team can recognize when reviewing this simulated configuration."] : []
        };
        _environment.Configure(_environments, e => e.Name, e => e.Id, "production");
        _form.AddChild(_nameField); _form.AddChild(_destinationField);
        _form.AddChild(new Field("Environment", _environment)); _form.AddChild(new Field("Shortcut", _shortcut));
        _notesField = new Field("Notes (optional)", _notes); _form.AddChild(_notesField);
        var options = Row(); _form.AddChild(options); options.AddChild(_readOnly); options.AddChild(_protect); options.AddChild(_disableNotes);
        _readOnly.UserChanged += SetReadOnly; _name.UserTextChanged += NameChanged;
        _disableNotes.UserChanged += SetNotesDisabled;
        _destination.UserTextChanged += DestinationChanged;
        _form.ValidSubmit += Submitted; _form.InvalidSubmit += Invalid; _form.ResetRequested += ResetModel;
        var actions = Row(); _form.AddChild(actions);
        Add(actions, "Submit", () => _form.Submit()); Add(actions, "Reset", () => _form.Reset());
        Add(actions, "Replace configuration", Replace); Add(actions, "Toggle notes", ToggleNotes);
        root.AddChild(_result);
    }
    private static FlexLayout Column() => new() { Direction = Orientation.Vertical, Gap = 8, AlignItems = Alignment.Stretch };
    private static Panel Row() => new() { Layout = new FlexLayout { Wrap = FlexWrap.Wrap, Gap = 10 } };
    private static Label Wrap(string text) => new(text) { TextWrapping = ThorVG.TextWrap.Word };
    private static void Add(Panel row, string text, Action action) => row.AddChild(new Button(text) { Clicked = action });
    private void NameChanged(string value)
    {
        if (_protect.IsChecked) _name.ResetText(_acceptedName); else _acceptedName = value;
        _nameField.SetMessages(_nameField.Validate!());
    }
    private void DestinationChanged(string _) => _destinationField.SetMessages(_destinationField.Validate!());
    private void SetReadOnly(bool value) => _destinationField.ReadOnly = value;
    private void SetNotesDisabled(bool value) => _notesField.Enabled = !value;
    private void Submitted() => _result.Text = $"Configuration {_modelNumber} submitted #{++_submissions}: {_name.Text} to {_destination.Text} · {_environment.Value} · shortcut {(_shortcut.IsChecked ? "on" : "off")}. Simulation only.";
    private void Invalid() => _result.Text = "Review the messages, correct the configuration, then Submit.";
    private void ResetModel()
    {
        _name.ResetText("MyApp"); _acceptedName = "MyApp"; _destination.ResetText("/opt/myapp"); _notes.ResetText("Configuration notes");
        _environment.Value = "production"; _shortcut.IsChecked = true; _readOnly.IsChecked = false; _protect.IsChecked = false;
        _disableNotes.IsChecked = false; SetNotesDisabled(false);
        SetReadOnly(false);
        if (_notesField.Parent == null) _form.InsertChild(_notesField, 4);
        _submissions = 0; _result.Text = "Configuration reset. No configuration submitted.";
    }
    private void Replace() { _form.Reset(); _modelNumber++; _name.ResetText("Replacement app"); _acceptedName = _name.Text; _result.Text = $"Configuration {_modelNumber} is a new draft."; }
    private void ToggleNotes()
    {
        if (_notesField.Parent == null) _form.InsertChild(_notesField, 4);
        else _form.RemoveChild(_notesField);
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        _window.Closing -= Closing;
        _readOnly.UserChanged -= SetReadOnly; _name.UserTextChanged -= NameChanged; _destination.UserTextChanged -= DestinationChanged;
        _disableNotes.UserChanged -= SetNotesDisabled;
        _form.ValidSubmit -= Submitted; _form.InvalidSubmit -= Invalid; _form.ResetRequested -= ResetModel;
        // Fields detach editors during disposal; these editors are owned by this scenario.
        Content.Dispose();
        _notesField.Dispose();
        foreach (var editor in new Element[] { _name, _destination, _notes, _environment, _shortcut }) editor.Dispose();
    }
    private ValueTask Closing() { Dispose(); return ValueTask.CompletedTask; }
}
