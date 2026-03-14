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

    class Handler : WinterElementHandler, INonPhysicalChild
    {
        private readonly Marius.Winter.FlexLayout _layout = new();
        private ILayoutContainer? _parentContainer;

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.Panel()) { }

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Direction":
                    _layout.Direction = AttributeHelper.GetEnum<Orientation>(attributeValue);
                    break;
                case "Wrap":
                    _layout.Wrap = AttributeHelper.GetEnum<FlexWrap>(attributeValue);
                    break;
                case "JustifyContent":
                    _layout.JustifyContent = AttributeHelper.GetEnum<JustifyContent>(attributeValue);
                    break;
                case "AlignItems":
                    _layout.AlignItems = AttributeHelper.GetEnum<Alignment>(attributeValue);
                    break;
                case "Gap":
                    _layout.Gap = AttributeHelper.GetFloat(attributeValue, 4f);
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }

        public void SetParent(object parentElement)
        {
            _parentContainer = parentElement as ILayoutContainer;
            SetLayoutOn(parentElement, _layout);
        }

        public void Remove()
        {
            if (_parentContainer != null)
            {
                _parentContainer.Layout = null;
                _parentContainer = null;
            }
        }
    }
}
