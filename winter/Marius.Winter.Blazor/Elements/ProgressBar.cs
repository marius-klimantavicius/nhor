using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class ProgressBar : WinterComponentBase
{
    static ProgressBar()
    {
        ElementHandlerRegistry.RegisterElementHandler<ProgressBar>(
            renderer => new Handler(renderer));
    }

    [Parameter] public float Value { get; set; }
    [Parameter] public bool IsIndeterminate { get; set; }
    [Parameter] public bool IsPaused { get; set; }

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);

        builder.AddAttribute("Value", Value);
        builder.AddAttribute(nameof(IsIndeterminate), IsIndeterminate);
        builder.AddAttribute(nameof(IsPaused), IsPaused);
    }

    class Handler : WinterElementHandler
    {
        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.ProgressBar()) { }

        Marius.Winter.ProgressBar ProgressBarControl => (Marius.Winter.ProgressBar)ElementControl;

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case nameof(IsIndeterminate): ProgressBarControl.IsIndeterminate = AttributeHelper.GetBool(attributeValue); break;
                case nameof(IsPaused): ProgressBarControl.IsPaused = AttributeHelper.GetBool(attributeValue); break;
                case "Value":
                    ProgressBarControl.Value = AttributeHelper.GetFloat(attributeValue);
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }
    }
}
