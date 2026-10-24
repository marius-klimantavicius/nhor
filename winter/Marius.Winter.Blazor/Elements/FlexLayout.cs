using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class FlexLayout : WinterComponentBase
{
    static FlexLayout()
    {
        ElementHandlerRegistry.RegisterElementHandler<FlexLayout>(
            renderer => new Handler(renderer));
    }

    [Parameter] public Orientation Direction { get; set; } = Orientation.Horizontal;
    [Parameter] public FlexWrap Wrap { get; set; } = FlexWrap.NoWrap;
    [Parameter] public JustifyContent JustifyContent { get; set; } = JustifyContent.Start;
    [Parameter] public Alignment AlignItems { get; set; } = Alignment.Stretch;
    [Parameter] public float Gap { get; set; } = 4f;

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);

        builder.AddAttribute(nameof(Direction), Direction);
        builder.AddAttribute(nameof(Wrap), Wrap);
        builder.AddAttribute(nameof(JustifyContent), JustifyContent);
        builder.AddAttribute(nameof(AlignItems), AlignItems);
        builder.AddAttribute(nameof(Gap), Gap);
    }

    class Handler : LayoutHandler<Marius.Winter.FlexLayout>
    {
        public Handler(NativeComponentRenderer renderer)
            : base(renderer) { }

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Direction":
                    SetLayoutValue(Layout.Direction, AttributeHelper.GetEnum<Orientation>(attributeValue), value => Layout.Direction = value);
                    break;
                case "Wrap":
                    SetLayoutValue(Layout.Wrap, AttributeHelper.GetEnum<FlexWrap>(attributeValue), value => Layout.Wrap = value);
                    break;
                case "JustifyContent":
                    SetLayoutValue(Layout.JustifyContent, AttributeHelper.GetEnum<JustifyContent>(attributeValue), value => Layout.JustifyContent = value);
                    break;
                case "AlignItems":
                    SetLayoutValue(Layout.AlignItems, AttributeHelper.GetEnum<Alignment>(attributeValue), value => Layout.AlignItems = value);
                    break;
                case "Gap":
                    SetLayoutValue(Layout.Gap, AttributeHelper.GetFloat(attributeValue, 4f), value => Layout.Gap = value);
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }
    }
}
