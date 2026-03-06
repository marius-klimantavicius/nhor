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

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);

        builder.AddAttribute("Value", Value);
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
