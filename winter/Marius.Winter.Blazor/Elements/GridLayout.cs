using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;
using System.Linq;

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

    class Handler : LayoutHandler<Marius.Winter.GridLayout>
    {
        public Handler(NativeComponentRenderer renderer)
            : base(renderer) { }

        public override void ApplyAttribute(ulong attributeEventHandlerId, string attributeName, object attributeValue, string attributeEventUpdatesAttributeName)
        {
            switch (attributeName)
            {
                case "Columns":
                    var columns = AttributeHelper.GetTrackSizeArray(attributeValue) ?? [];
                    if (!Layout.Columns.SequenceEqual(columns))
                        SetLayoutValue(Layout.Columns, columns, value => Layout.Columns = value);
                    break;
                case "Rows":
                    var rows = AttributeHelper.GetTrackSizeArray(attributeValue) ?? [];
                    if (!Layout.Rows.SequenceEqual(rows))
                        SetLayoutValue(Layout.Rows, rows, value => Layout.Rows = value);
                    break;
                case "ColumnGap":
                    SetLayoutValue(Layout.ColumnGap, AttributeHelper.GetFloat(attributeValue, 4f), value => Layout.ColumnGap = value);
                    break;
                case "RowGap":
                    SetLayoutValue(Layout.RowGap, AttributeHelper.GetFloat(attributeValue, 4f), value => Layout.RowGap = value);
                    break;
                default:
                    base.ApplyAttribute(attributeEventHandlerId, attributeName, attributeValue, attributeEventUpdatesAttributeName);
                    break;
            }
        }
    }
}
