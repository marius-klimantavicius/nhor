using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace Marius.Winter.Blazor.Elements;

/// <summary>Typed binding and Microsoft EditContext field identity for Winter editors.</summary>
public abstract class FormFieldBase<TValue> : ComponentBase, IDisposable, IWinterFormField
{
    [CascadingParameter] private EditContext? CascadedContext { get; set; }
    [CascadingParameter] internal WinterFormScope? Scope { get; set; }
    [Parameter] public TValue Value { get; set; } = default!;
    [Parameter] public EventCallback<TValue> ValueChanged { get; set; }
    [Parameter] public EventCallback<TValue> OnValueChanged { get; set; }
    [Parameter] public Expression<Func<TValue>>? ValueExpression { get; set; }
    [Parameter] public string? Label { get; set; }
    [Parameter] public string? HelpText { get; set; }
    [Parameter] public bool Enabled { get; set; } = true;
    [Parameter] public object? LayoutData { get; set; }
    [Parameter] public long ResetVersion { get; set; }
    private EditContext? _context;
    private WinterFormScope? _scope;
    private FieldIdentifier _identifier;
    private EventCallback<TValue> _application, _changed;
    private bool _hasCallback, _disposed;
    private long _identity;
    private long _inputResetIdentity, _lastResetVersion, _lastFormResetVersion;
    private bool _resetInitialized;
    private Field? _field;
    private string[] _messages = Array.Empty<string>();
    EditContext? IWinterFormField.Context => _context;
    FieldIdentifier IWinterFormField.Identifier => _identifier;
    public bool Focus() => !_disposed && (_field?.FocusEditor() ?? false);
    protected long InputResetVersion
    {
        get
        {
            var formVersion = _scope?.Form.InputResetVersion ?? 0;
            if (!_resetInitialized || _lastResetVersion != ResetVersion || _lastFormResetVersion != formVersion)
            {
                _resetInitialized = true; _lastResetVersion = ResetVersion; _lastFormResetVersion = formVersion;
                _inputResetIdentity++;
            }
            return _inputResetIdentity;
        }
    }
    protected bool IsInvalid => _messages.Length > 0;
    protected virtual bool FieldReadOnly => false;
    protected EventCallback<TValue> Changed => _changed;

    protected override void OnParametersSet()
    {
        var context = CascadedContext;
        if (context != null && ValueExpression == null) throw new InvalidOperationException($"{GetType().Name} requires ValueExpression (normally supplied by @bind-Value) inside an EditContext.");
        var identifier = ValueExpression == null ? default : FieldIdentifier.Create(ValueExpression);
        var callback = ValueChanged.HasDelegate ? ValueChanged : OnValueChanged;
        if (!ReferenceEquals(context, _context) || !_identifier.Equals(identifier) || !ReferenceEquals(Scope, _scope) || !_hasCallback || !callback.Equals(_application))
        {
            Detach(); _identity++; _context = context; _identifier = identifier; _scope = Scope;
            if (_context != null) _context.OnValidationStateChanged += ValidationStateChanged;
            _scope?.Form.Register(this);
            _application = callback; _hasCallback = true;
            var identity = _identity; var capturedContext = _context; var capturedField = _identifier;
            _changed = EventCallback.Factory.Create<TValue>(this, value =>
            {
                if (_disposed || identity != _identity) return Task.CompletedTask;
                Value = value;
                // Assignments in binding handlers run before their first await. Never send a
                // late field notification after an awaiting callback replaces/unmounts us.
                var pending = callback.InvokeAsync(value);
                if (!_disposed && identity == _identity && ReferenceEquals(capturedContext, _context)) capturedContext?.NotifyFieldChanged(capturedField);
                return pending;
            });
        }
        ReadMessages();
    }
    private void ReadMessages() => _messages = _context?.GetValidationMessages(_identifier).ToArray() ?? Array.Empty<string>();
    private void ValidationStateChanged(object? sender, ValidationStateChangedEventArgs args)
    {
        if (_disposed || !ReferenceEquals(sender, _context)) return;
        var identity = _identity;
        _ = InvokeAsync(() => { if (!_disposed && identity == _identity) { ReadMessages(); StateHasChanged(); } });
    }
    private void Detach()
    {
        if (_context != null) _context.OnValidationStateChanged -= ValidationStateChanged;
        _scope?.Form.Unregister(this);
    }
    protected sealed override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.OpenComponent<Field>(0); b.AddAttribute(1, "Label", Label); b.AddAttribute(2, "HelpText", HelpText);
        b.AddAttribute(3, "Messages", _messages); b.AddAttribute(4, "ReadOnly", FieldReadOnly);
        b.AddAttribute(5, "Enabled", Enabled); b.AddAttribute(6, "LayoutData", LayoutData);
        b.AddAttribute(7, "ChildContent", (RenderFragment)RenderEditor);
        b.AddComponentReferenceCapture(8, value => _field = (Field)value); b.CloseComponent();
    }
    protected abstract void RenderEditor(RenderTreeBuilder b);
    public void Dispose() { if (_disposed) return; _disposed = true; _identity++; Detach(); _field = null; }
}
