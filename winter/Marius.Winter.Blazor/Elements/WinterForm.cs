using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace Marius.Winter.Blazor.Elements;

/// <summary>EditContext validation and native default-submit composition; no HTML elements.</summary>
public class WinterForm : ComponentBase, IDisposable
{
    [Parameter] public object? Model { get; set; }
    [Parameter] public EditContext? EditContext { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public EventCallback<EditContext> OnValidSubmit { get; set; }
    [Parameter] public EventCallback<EditContext> OnInvalidSubmit { get; set; }
    [Parameter] public EventCallback<EditContext> OnReset { get; set; }
    /// <summary>Synchronous application rules. This form owns the supplied message store.</summary>
    [Parameter] public Action<EditContext, ValidationMessageStore>? Validation { get; set; }
    [Parameter] public bool DataAnnotations { get; set; } = true;
    [Parameter] public bool Enabled { get; set; } = true;
    [Inject] private IServiceProvider Services { get; set; } = null!;
    public EditContext CurrentEditContext { get; private set; } = null!;
    private IDisposable? _annotations;
    private ValidationMessageStore? _messages;
    private bool _dataAnnotations, _disposed, _submitting;
    private long _generation, _resetVersion;
    internal long InputResetVersion => _resetVersion;
    private WinterFormScope _scope = null!;
    private readonly List<IWinterFormField> _fields = new();
    private EventCallback _submit;

    protected override void OnParametersSet()
    {
        if ((Model == null) == (EditContext == null)) throw new InvalidOperationException("WinterForm requires exactly one of Model or EditContext.");
        var next = EditContext ?? (ReferenceEquals(CurrentEditContext?.Model, Model) ? CurrentEditContext : new EditContext(Model!));
        if (!ReferenceEquals(next, CurrentEditContext))
        {
            DetachContext(); _generation++; _submitting = false;
            CurrentEditContext = next!;
            _messages = new ValidationMessageStore(CurrentEditContext);
            CurrentEditContext.OnValidationRequested += ValidationRequested;
            CurrentEditContext.OnFieldChanged += FieldChanged;
            AttachAnnotations(); NewScope();
        }
        else if (_dataAnnotations != DataAnnotations) { _annotations?.Dispose(); _annotations = null; AttachAnnotations(); }
    }
    private void AttachAnnotations()
    {
        _dataAnnotations = DataAnnotations;
        if (DataAnnotations) _annotations = CurrentEditContext.EnableDataAnnotationsValidation(Services);
    }
    private void NewScope()
    {
        _scope = new WinterFormScope(this);
        var generation = _generation;
        _submit = EventCallback.Factory.Create(this, () => generation == _generation ? SubmitAsync() : Task.CompletedTask);
    }
    private void ValidationRequested(object? sender, ValidationRequestedEventArgs args) => ValidateCustom(sender);
    private void FieldChanged(object? sender, FieldChangedEventArgs args) => ValidateCustom(sender);
    private void ValidateCustom(object? sender)
    {
        if (_disposed || !ReferenceEquals(sender, CurrentEditContext)) return;
        var context = CurrentEditContext; var messages = _messages!;
        messages.Clear(); Validation?.Invoke(context, messages);
        if (!_disposed && ReferenceEquals(context, CurrentEditContext)) context.NotifyValidationStateChanged();
    }
    internal void Register(IWinterFormField field) { if (!_fields.Contains(field)) _fields.Add(field); }
    internal void Unregister(IWinterFormField field) => _fields.Remove(field);

    public async Task SubmitAsync()
    {
        if (_disposed || _submitting || !Enabled) return;
        var context = CurrentEditContext; var generation = _generation;
        var validCallback = OnValidSubmit; var invalidCallback = OnInvalidSubmit;
        _submitting = true;
        try
        {
            var valid = context.Validate();
            if (_disposed || generation != _generation || !ReferenceEquals(context, CurrentEditContext)) return;
            if (valid) await validCallback.InvokeAsync(context);
            else
            {
                foreach (var field in _fields.ToArray())
                    if (ReferenceEquals(field.Context, context) && context.GetValidationMessages(field.Identifier).Any() && field.Focus()) break;
                await invalidCallback.InvokeAsync(context);
            }
        }
        finally { if (generation == _generation) _submitting = false; }
    }
    public async Task ResetAsync()
    {
        if (_disposed) return;
        var context = CurrentEditContext; var generation = ++_generation; _submitting = false;
        await OnReset.InvokeAsync(context);
        if (_disposed || generation != _generation || !ReferenceEquals(context, CurrentEditContext)) return;
        // Only this form's stores are cleared; caller-owned stores/context survive.
        _annotations?.Dispose(); _annotations = null; AttachAnnotations();
        _messages!.Clear(); context.MarkAsUnmodified();
        _resetVersion++; NewScope(); context.NotifyValidationStateChanged(); StateHasChanged();
    }
    private void DetachContext()
    {
        if (CurrentEditContext == null) return;
        CurrentEditContext.OnValidationRequested -= ValidationRequested;
        CurrentEditContext.OnFieldChanged -= FieldChanged;
        _annotations?.Dispose(); _annotations = null;
        _messages?.Clear(); CurrentEditContext.NotifyValidationStateChanged();
    }
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.OpenComponent<CascadingValue<EditContext>>(0);
        b.SetKey(CurrentEditContext);
        b.AddAttribute(1, "Value", CurrentEditContext);
        b.AddAttribute(2, "IsFixed", true);
        b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
        {
            c.OpenComponent<CascadingValue<WinterFormScope>>(0); c.AddAttribute(1, "Value", _scope);
            c.AddAttribute(2, "IsFixed", true);
            c.AddAttribute(3, "ChildContent", (RenderFragment)(s =>
            {
                s.OpenComponent<FormSurface>(0); s.AddAttribute(1, "Submit", _submit);
                s.AddAttribute(2, "Enabled", Enabled); s.AddAttribute(3, "ChildContent", ChildContent); s.CloseComponent();
            })); c.CloseComponent();
        })); b.CloseComponent();
    }
    public void Dispose() { if (_disposed) return; _disposed = true; _generation++; DetachContext(); _fields.Clear(); }
}

internal sealed record WinterFormScope(WinterForm Form);
internal interface IWinterFormField
{
    EditContext? Context { get; }
    FieldIdentifier Identifier { get; }
    bool Focus();
}
