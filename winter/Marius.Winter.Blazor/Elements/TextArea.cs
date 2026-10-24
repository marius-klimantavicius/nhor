using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class TextArea : InputComponentBase<string>
{
    static TextArea()
    {
        ElementHandlerRegistry.RegisterElementHandler<TextArea>(
            renderer => new Handler(renderer));
    }

    [Parameter] public string? Text { get; set; }
    [Parameter] public string? Placeholder { get; set; }
    [Parameter] public EventCallback<string> TextChanged { get; set; }
    [Parameter] public EventCallback<string> OnTextChanged { get; set; }

    [Parameter] public EventCallback<TextCommitEventArgs> TextCommitted { get; set; }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);
        RenderReset(builder, Text ?? "");

        builder.AddAttribute("Text", Text ?? "");
        builder.AddAttribute("Placeholder", Placeholder ?? "");

        builder.AddAttribute("ontextchanged", BindChange(TextChanged, OnTextChanged, value => Text = value), "Text");
        builder.AddAttribute("ontextcommitted", TextCommitted);
    }

    class Handler : WinterElementHandler
    {
        readonly CoalescedEvent _textChangedEvent;

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.TextArea())
        {
            _textChangedEvent = new CoalescedEvent(this);
            TextAreaControl.UserTextChanged += OnUserChanged;
            TextAreaControl.TextCommitted += OnTextCommitted;
        }

        private ulong _commitHandlerId;
        private void OnTextCommitted(TextCommitEventArgs args)
        {
            _textChangedEvent.Flush();
            if (_commitHandlerId != 0 && !IsDisposed)
                Renderer.DispatchNativeEventAsync(_commitHandlerId, null, args);
        }

        private void OnUserChanged(string value) => _textChangedEvent.Fire(value);

        public override void Dispose()
        {
            TextAreaControl.UserTextChanged -= OnUserChanged;
            TextAreaControl.TextCommitted -= OnTextCommitted;
            _commitHandlerId = 0;
            base.Dispose();
        }

        Marius.Winter.TextArea TextAreaControl => (Marius.Winter.TextArea)ElementControl;

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "InputReset":
                    var reset = AttributeHelper.GetObject<InputReset<string>>(attributeValue);
                    if (reset == null) break;
                    _textChangedEvent.CancelPending();
                    TextAreaControl.ResetText(reset.Value);
                    break;
                case "Text":
                    TextAreaControl.Text = AttributeHelper.GetString(attributeValue) ?? "";
                    break;
                case "Placeholder":
                    TextAreaControl.Placeholder = AttributeHelper.GetString(attributeValue) ?? "";
                    break;
                case "ontextcommitted":
                    RegisterEvent(attributeEventHandlerId, id => { if (_commitHandlerId == id) _commitHandlerId = 0; });
                    _commitHandlerId = attributeEventHandlerId;
                    break;
                case "ontextchanged":
                    RegisterEvent(attributeEventHandlerId, _textChangedEvent.Unregister);
                    _textChangedEvent.HandlerId = attributeEventHandlerId;
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }
    }
}
