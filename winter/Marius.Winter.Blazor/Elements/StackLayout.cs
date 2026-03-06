using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class StackLayout : WinterComponentBase
{
    static StackLayout()
    {
        ElementHandlerRegistry.RegisterElementHandler<StackLayout>(
            renderer => new Handler(renderer));
    }

    [Parameter] public Orientation Orientation { get; set; } = Orientation.Vertical;
    [Parameter] public float Spacing { get; set; } = 4f;
    [Parameter] public Alignment CrossAlignment { get; set; } = Alignment.Stretch;

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);

        builder.AddAttribute(nameof(Orientation), Orientation);
        builder.AddAttribute(nameof(Spacing), Spacing);
        builder.AddAttribute(nameof(CrossAlignment), CrossAlignment);
    }

    class Handler : WinterElementHandler, INonPhysicalChild
    {
        private readonly Marius.Winter.StackLayout _layout = new();

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.Panel()) { }

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Orientation":
                    _layout.Orientation = AttributeHelper.GetEnum<Orientation>(attributeValue);
                    break;
                case "Spacing":
                    _layout.Spacing = AttributeHelper.GetFloat(attributeValue, 4f);
                    break;
                case "CrossAlignment":
                    _layout.CrossAlignment = AttributeHelper.GetEnum<Alignment>(attributeValue);
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }

        public void SetParent(object parentElement)
        {
            SetLayoutOn(parentElement, _layout);
        }

        public void Remove() { }
    }
}
