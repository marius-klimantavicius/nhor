using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Marius.Winter.Taffy;

namespace Marius.Winter.Taffy.Tests.Native;

// Audit probe, invoked by the reproducible differential runner. It reports all mismatches;
// it does not change expected values or the regular regression suite's assertions.
public static class LayoutProbe
{
    private static float Number(JsonElement value) => value.GetSingle();
    private static Dimension DimensionValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => Dimension.AUTO,
        JsonValueKind.Number => Dimension.Length(Number(value)),
        JsonValueKind.String when value.GetString()!.EndsWith('%') => Dimension.Percent(
            float.Parse(value.GetString()![..^1], CultureInfo.InvariantCulture) / 100f),
        _ => throw new InvalidOperationException(value.ToString()),
    };
    private static Size<Dimension> SizeValue(JsonElement value) => new(DimensionValue(value[0]), DimensionValue(value[1]));
    private static Rect<LengthPercentage> Spacing(JsonElement value) => new(
        LengthPercentage.Length(Number(value[0])), LengthPercentage.Length(Number(value[1])),
        LengthPercentage.Length(Number(value[2])), LengthPercentage.Length(Number(value[3])));
    private static Rect<LengthPercentageAuto> AutoSpacing(JsonElement value) => new(
        LengthPercentageAuto.Length(Number(value[0])), LengthPercentageAuto.Length(Number(value[1])),
        LengthPercentageAuto.Length(Number(value[2])), LengthPercentageAuto.Length(Number(value[3])));

    private static Style ParseStyle(JsonElement value)
    {
        var style = new Style();
        foreach (var property in value.EnumerateObject())
        {
            var v = property.Value;
            switch (property.Name)
            {
                case "display": style.Display = Enum.Parse<Display>(v.GetString()!); break;
                case "size": style.SizeValue = SizeValue(v); break;
                case "min": style.MinSizeValue = SizeValue(v); break;
                case "max": style.MaxSizeValue = SizeValue(v); break;
                case "padding": style.PaddingValue = Spacing(v); break;
                case "border": style.BorderValue = Spacing(v); break;
                case "margin": style.MarginValue = AutoSpacing(v); break;
                case "inset": style.Inset = AutoSpacing(v); break;
                case "position": style.PositionValue = Enum.Parse<Position>(v.GetString()!); break;
                case "direction": style.DirectionValue = Enum.Parse<Direction>(v.GetString()!); break;
                case "flex-direction": style.FlexDirectionValue = Enum.Parse<FlexDirection>(v.GetString()!); break;
                case "wrap": style.FlexWrapValue = Enum.Parse<FlexWrap>(v.GetString()!); break;
                case "gap": style.GapValue = new(LengthPercentage.Length(Number(v[0])), LengthPercentage.Length(Number(v[1]))); break;
                case "grow": style.FlexGrowValue = Number(v); break;
                case "shrink": style.FlexShrinkValue = Number(v); break;
                case "basis": style.FlexBasisValue = DimensionValue(v); break;
                case "aspect": style.AspectRatioValue = Number(v); break;
                case "box": style.BoxSizingValue = Enum.Parse<BoxSizing>(v.GetString()!); break;
                case "align": style.AlignItemsValue = v.GetString() switch
                {
                    "Baseline" => AlignItems.Baseline, "Center" => AlignItems.Center,
                    "Start" => AlignItems.Start, "End" => AlignItems.End, "Stretch" => AlignItems.Stretch,
                    _ => throw new InvalidOperationException(v.ToString()),
                }; break;
                case "overflow": style.OverflowValue = new(Enum.Parse<Overflow>(v[0].GetString()!), Enum.Parse<Overflow>(v[1].GetString()!)); break;
                case "scrollbar": style.ScrollbarWidthValue = Number(v); break;
                case "columns": style.GridTemplateColumns = v.EnumerateArray().Select(x => GridTemplateComponent.FromFr(Number(x))).ToImmutableList(); break;
                default: throw new InvalidOperationException($"Untranslated probe style {property.Name}");
            }
        }
        return style;
    }

    private static AvailableSpace Available(JsonElement value) => value.ValueKind == JsonValueKind.Number
        ? AvailableSpace.Definite(Number(value)) : value.GetString() switch
        {
            "min" => AvailableSpace.MinContent, "max" => AvailableSpace.MaxContent,
            _ => throw new InvalidOperationException(value.ToString()),
        };

    public static string Evaluate(string json)
    {
        using var document = JsonDocument.Parse(json);
        var results = new List<List<uint[]>>();
        foreach (var scene in document.RootElement.EnumerateArray())
        {
            var tree = new TaffyTree<TestNodeContext>();
            if (!scene.GetProperty("round").GetBoolean()) tree.DisableRounding();
            var nodes = new List<NodeId>();
            foreach (var definition in scene.GetProperty("nodes").EnumerateArray())
            {
                var style = ParseStyle(definition.GetProperty("style"));
                var children = definition.GetProperty("children").EnumerateArray().Select(x => nodes[x.GetInt32()]).ToArray();
                if (children.Length != 0) nodes.Add(tree.NewWithChildren(style, children));
                else if (definition.TryGetProperty("text", out var text))
                    nodes.Add(tree.NewLeafWithContext(style, TestNodeContext.AhemText(text.GetString()!,
                        definition.GetProperty("vertical").GetBoolean() ? WritingMode.Vertical : WritingMode.Horizontal)));
                else
                {
                    var size = definition.GetProperty("fixed");
                    nodes.Add(tree.NewLeafWithContext(style, TestNodeContext.Fixed(Number(size[0]), Number(size[1]))));
                }
            }
            var viewport = scene.GetProperty("viewport");
            var available = new Size<AvailableSpace>(Available(viewport[0]), Available(viewport[1]));
            var phases = new List<uint[]>();
            for (int phase = 0; phase < 5; phase++)
            {
                if (phase == 2)
                {
                    var style = tree.GetStyle(nodes[0]);
                    style.SizeValue.Width = Dimension.Percent(0.333f);
                    tree.SetStyle(nodes[0], style);
                }
                if (phase == 3) tree.SetNodeContext(nodes[0], TestNodeContext.Fixed(43.125f, 19.75f));
                if (phase == 4) tree.Remove(nodes[0]);
                tree.ComputeLayoutWithMeasure(nodes[^1], available, TestHelpers.MeasureFunction);
                var values = new List<uint>();
                for (int i = phase == 4 ? 1 : 0; i < nodes.Count; i++)
                {
                    var layout = tree.GetLayout(nodes[i]);
                    foreach (float f in new[] { layout.Location.X, layout.Location.Y, layout.Size.Width, layout.Size.Height,
                        layout.ScrollWidth(), layout.ScrollHeight(), layout.Border.Left, layout.Border.Right,
                        layout.Border.Top, layout.Border.Bottom, layout.Padding.Left, layout.Padding.Right,
                        layout.Padding.Top, layout.Padding.Bottom, layout.Margin.Left, layout.Margin.Right, layout.Margin.Top, layout.Margin.Bottom })
                        values.Add(BitConverter.SingleToUInt32Bits(f));
                }
                phases.Add(values.ToArray());
            }
            results.Add(phases);
        }
        return JsonSerializer.Serialize(results);
    }
}
