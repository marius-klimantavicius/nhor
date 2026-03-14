using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

public class GridLayout : WinterComponentBase
{
    static GridLayout()
    {
        ElementHandlerRegistry.RegisterElementHandler<GridLayout>(
            renderer => new Handler(renderer));
    }

    [Parameter] public TrackSize[]? Columns { get; set; }
    [Parameter] public TrackSize[]? Rows { get; set; }
    [Parameter] public float ColumnGap { get; set; } = 4f;
    [Parameter] public float RowGap { get; set; } = 4f;

    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);

        if (Columns != null)
            builder.AddAttribute(nameof(Columns), Columns);
        if (Rows != null)
            builder.AddAttribute(nameof(Rows), Rows);
        builder.AddAttribute(nameof(ColumnGap), ColumnGap);
        builder.AddAttribute(nameof(RowGap), RowGap);
    }

    class Handler : WinterElementHandler, INonPhysicalChild
    {
        private readonly Marius.Winter.GridLayout _layout = new();
        private ILayoutContainer? _parentContainer;

        public Handler(NativeComponentRenderer renderer)
            : base(renderer, new Marius.Winter.Panel()) { }

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Columns":
                    _layout.Columns = AttributeHelper.GetTrackSizeArray(attributeValue)!;
                    break;
                case "Rows":
                    _layout.Rows = AttributeHelper.GetTrackSizeArray(attributeValue)!;
                    break;
                case "ColumnGap":
                    _layout.ColumnGap = AttributeHelper.GetFloat(attributeValue, 4f);
                    break;
                case "RowGap":
                    _layout.RowGap = AttributeHelper.GetFloat(attributeValue, 4f);
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
