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

    class Handler : LayoutHandler<Marius.Winter.StackLayout>
    {
        public Handler(NativeComponentRenderer renderer)
            : base(renderer) { }

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Orientation":
                    SetLayoutValue(Layout.Orientation, AttributeHelper.GetEnum<Orientation>(attributeValue), value => Layout.Orientation = value);
                    break;
                case "Spacing":
                    SetLayoutValue(Layout.Spacing, AttributeHelper.GetFloat(attributeValue, 4f), value => Layout.Spacing = value);
                    break;
                case "CrossAlignment":
                    SetLayoutValue(Layout.CrossAlignment, AttributeHelper.GetEnum<Alignment>(attributeValue), value => Layout.CrossAlignment = value);
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }
    }
}
