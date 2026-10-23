// Managed adapter for the pinned upstream serde value format. Calc handles remain process-local.
using System;
using System.Collections;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Marius.Winter.Taffy;

public static class TaffyStyleJson
{
    public static string Serialize<T>(T value) => Write(value)?.ToJsonString() ?? "null";
    public static T Deserialize<T>(string json)
    {
        try { return (T)Read(JsonNode.Parse(json), typeof(T))!; }
        catch (Exception error) when (error is InvalidOperationException or FormatException or OverflowException or ArgumentException or IndexOutOfRangeException)
        { throw new JsonException("Invalid Taffy style value", error); }
    }
    public static T Deserialize<T>(JsonElement value) => Deserialize<T>(value.GetRawText());

    private static JsonNode? Write(object? value)
    {
        if (value == null) return null;
        if (value is CompactLength compact)
        {
            if (compact.HasCalc()) throw new JsonException("Calc values cannot be serialized");
            return JsonValue.Create(compact.SerializedValue());
        }
        if (value is LengthPercentage lp) return Write(lp.IntoRaw());
        if (value is LengthPercentageAuto lpa) return Write(lpa.IntoRaw());
        if (value is Dimension dimension) return Write(dimension.IntoRaw());
        if (value is MinTrackSizingFunction min) return Write(min.IntoRaw());
        if (value is MaxTrackSizingFunction max) return Write(max.IntoRaw());
        if (value is Contain contain) return JsonValue.Create((byte)contain);
        if (value is AlignItems items) return JsonValue.Create(AlignmentName(items.Keyword.ToString(), items.Safety, items.Keyword is AlignItemsKeyword.Normal or AlignItemsKeyword.Baseline or AlignItemsKeyword.Stretch));
        if (value is AlignContent content) return JsonValue.Create(AlignmentName(content.Keyword.ToString(), content.Safety, content.Keyword is AlignContentKeyword.Normal or AlignContentKeyword.Stretch or AlignContentKeyword.SpaceAround or AlignContentKeyword.SpaceBetween or AlignContentKeyword.SpaceEvenly));
        if (value is AvailableSpace available) return available.IsDefinite() ? new JsonObject { ["Definite"] = Write(available.Unwrap()) } : JsonValue.Create(available == AvailableSpace.MinContent ? "MinContent" : "MaxContent");
        if (value is GridPlacement placement) return placement.Kind switch
        {
            GridPlacement.GridPlacementKind.Auto => JsonValue.Create("Auto"),
            GridPlacement.GridPlacementKind.Line => new JsonObject { ["Line"] = placement.Value },
            GridPlacement.GridPlacementKind.Span => new JsonObject { ["Span"] = (ushort)placement.Value },
            GridPlacement.GridPlacementKind.NamedLine => new JsonObject { ["NamedLine"] = new JsonArray(JsonValue.Create(placement.Name), JsonValue.Create(placement.Value)) },
            GridPlacement.GridPlacementKind.NamedSpan => new JsonObject { ["NamedSpan"] = new JsonArray(JsonValue.Create(placement.Name), JsonValue.Create((ushort)placement.Value)) },
            _ => throw new JsonException("Invalid placement"),
        };
        if (value is RepetitionCount repetition) return repetition.IsCount ? new JsonObject { ["Count"] = repetition.CountValue } : JsonValue.Create(repetition.IsAutoFit ? "AutoFit" : "AutoFill");
        if (value is GridTemplateComponent component) return component.Kind == GridTemplateComponent.GridTemplateComponentKind.Single
            ? new JsonObject { ["Single"] = Write(component.Single) } : new JsonObject { ["Repeat"] = Write(component.Repetition) };
        if (value is GridAutoTracks auto) return Write(auto.Tracks);
        if (value is Enum) return JsonValue.Create(value.ToString());
        if (value is string text) return JsonValue.Create(text);
        if (value is float number) return float.IsFinite(number) ? JsonValue.Create(number) : null;
        if (value is bool boolean) return JsonValue.Create(boolean);
        if (value is byte u8) return JsonValue.Create(u8);
        if (value is short i16) return JsonValue.Create(i16);
        if (value is ushort u16) return JsonValue.Create(u16);
        if (value is int i32) return JsonValue.Create(i32);
        if (value is uint u32) return JsonValue.Create(u32);
        if (value is long i64) return JsonValue.Create(i64);
        if (value is ulong u64) return JsonValue.Create(u64);
        if (value is IEnumerable sequence) { var array = new JsonArray(); foreach (var item in sequence) array.Add(Write(item)); return array; }
        if (value is Style style) return new JsonObject
        {
            ["dummy"] = null,
            ["display"] = Write(style.Display),
            ["item_is_table"] = Write(style.ItemIsTable),
            ["item_is_replaced"] = Write(style.ItemIsReplaced),
            ["item_is_compressible_replaced"] = Write(style.ItemIsCompressibleReplaced),
            ["box_sizing"] = Write(style.BoxSizingValue),
            ["overflow"] = Write(style.OverflowValue),
            ["scrollbar_width"] = Write(style.ScrollbarWidthValue),
            ["float"] = Write(style.FloatValue),
            ["clear"] = Write(style.ClearValue),
            ["direction"] = Write(style.DirectionValue),
            ["position"] = Write(style.PositionValue),
            ["contain"] = Write(style.ContainValue),
            ["flex_line_count"] = Write(style.FlexLineCountValue),
            ["inset"] = Write(style.Inset),
            ["size"] = Write(style.SizeValue),
            ["min_size"] = Write(style.MinSizeValue),
            ["max_size"] = Write(style.MaxSizeValue),
            ["aspect_ratio"] = Write(style.AspectRatioValue),
            ["margin"] = Write(style.MarginValue),
            ["padding"] = Write(style.PaddingValue),
            ["border"] = Write(style.BorderValue),
            ["align_items"] = Write(style.AlignItemsValue ?? AlignItems.Normal),
            ["align_self"] = Write(style.AlignSelfValue),
            ["justify_items"] = Write(style.JustifyItemsValue ?? AlignItems.Normal),
            ["justify_self"] = Write(style.JustifySelfValue),
            ["align_content"] = Write(style.AlignContentValue ?? AlignContent.Normal),
            ["justify_content"] = Write(style.JustifyContentValue ?? AlignContent.Normal),
            ["gap"] = Write(style.GapValue),
            ["text_align"] = Write(style.TextAlignValue),
            ["flex_direction"] = Write(style.FlexDirectionValue),
            ["flex_wrap"] = Write(style.FlexWrapValue),
            ["flex_basis"] = Write(style.FlexBasisValue),
            ["flex_grow"] = Write(style.FlexGrowValue),
            ["flex_shrink"] = Write(style.FlexShrinkValue),
            ["grid_template_rows"] = Write(style.GridTemplateRows),
            ["grid_template_columns"] = Write(style.GridTemplateColumns),
            ["grid_auto_rows"] = Write(style.GridAutoRows),
            ["grid_auto_columns"] = Write(style.GridAutoColumns),
            ["grid_auto_flow"] = Write(style.GridAutoFlowValue),
            ["grid_template_areas"] = !style.GridTemplateAreasSpecified && style.GridTemplateAreas.Count == 0 && style.GridTemplateAreaRowCount == 0 && style.GridTemplateAreaColumnCount == 0 ? null
                : new JsonObject { ["row_count"] = style.GridTemplateAreaRowCount, ["column_count"] = style.GridTemplateAreaColumnCount, ["areas"] = Write(style.GridTemplateAreas) },
            ["grid_template_column_names"] = Write(style.GridTemplateColumnNames),
            ["grid_template_row_names"] = Write(style.GridTemplateRowNames),
            ["grid_row"] = Write(style.GridRow),
            ["grid_column"] = Write(style.GridColumn),
        };
        if (value is GridTemplateRepetition repeat) return new JsonObject { ["count"] = Write(repeat.Count), ["tracks"] = Write(repeat.Tracks), ["line_names"] = Write(repeat.LineNames) };
        if (value is NamedGridLine line) return new JsonObject { ["name"] = line.Name, ["index"] = line.Index };
        if (value is GridTemplateArea area) return new JsonObject { ["name"] = area.Name, ["row_start"] = area.RowStart, ["row_end"] = area.RowEnd, ["column_start"] = area.ColumnStart, ["column_end"] = area.ColumnEnd };
        if (value is Size<Dimension> g1) return new JsonObject { ["width"] = Write(g1.Width), ["height"] = Write(g1.Height) };
        if (value is Size<LengthPercentage> g2) return new JsonObject { ["width"] = Write(g2.Width), ["height"] = Write(g2.Height) };
        if (value is Size<LengthPercentageAuto> lpaSize) return new JsonObject { ["width"] = Write(lpaSize.Width), ["height"] = Write(lpaSize.Height) };
        if (value is Size<float> g3) return new JsonObject { ["width"] = Write(g3.Width), ["height"] = Write(g3.Height) };
        if (value is Point<Overflow> g4) return new JsonObject { ["x"] = Write(g4.X), ["y"] = Write(g4.Y) };
        if (value is Point<float> g5) return new JsonObject { ["x"] = Write(g5.X), ["y"] = Write(g5.Y) };
        if (value is Rect<LengthPercentage> g6) return new JsonObject { ["left"] = Write(g6.Left), ["right"] = Write(g6.Right), ["top"] = Write(g6.Top), ["bottom"] = Write(g6.Bottom) };
        if (value is Rect<LengthPercentageAuto> g7) return new JsonObject { ["left"] = Write(g7.Left), ["right"] = Write(g7.Right), ["top"] = Write(g7.Top), ["bottom"] = Write(g7.Bottom) };
        if (value is Rect<float> g8) return new JsonObject { ["left"] = Write(g8.Left), ["right"] = Write(g8.Right), ["top"] = Write(g8.Top), ["bottom"] = Write(g8.Bottom) };
        if (value is Line<GridPlacement> g9) return new JsonObject { ["start"] = Write(g9.Start), ["end"] = Write(g9.End) };
        if (value is Line<float> g10) return new JsonObject { ["start"] = Write(g10.Start), ["end"] = Write(g10.End) };
        if (value is MinMax<MinTrackSizingFunction, MaxTrackSizingFunction> g11) return new JsonObject { ["min"] = Write(g11.Min), ["max"] = Write(g11.Max) };
        if (value is GridTemplateTracks<GridTemplateComponent> tracks0) return new JsonObject { ["tracks"] = Write(tracks0.Tracks), ["line_names"] = Write(tracks0.LineNames) };
        if (value is GridTemplateTracks<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>> tracks1) return new JsonObject { ["tracks"] = Write(tracks1.Tracks), ["line_names"] = Write(tracks1.LineNames) };
        throw new NotSupportedException($"No native style JSON mapping for {value.GetType()}");
    }
    private static string AlignmentName(string keyword, AlignmentSafety safety, bool ignoresSafety) => ignoresSafety || safety == AlignmentSafety.Default ? keyword : safety + keyword;
    private static CompactLength Compact(ulong raw, Type? type = null)
    {
        byte tag = (byte)(raw >> 32);
        if (IntPtr.Size == 4 && (uint)(raw >> 32) != tag) throw new JsonException("Invalid compact length tag");
        bool common = tag is CompactLength.LENGTH_TAG or CompactLength.PERCENT_TAG;
        bool intrinsic = tag is CompactLength.AUTO_TAG or CompactLength.MIN_CONTENT_TAG or CompactLength.MAX_CONTENT_TAG or CompactLength.FIT_CONTENT_PX_TAG or CompactLength.FIT_CONTENT_PERCENT_TAG;
        bool keyword = tag is CompactLength.FIT_CONTENT_KEYWORD_TAG or CompactLength.STRETCH_TAG or CompactLength.CONTENT_TAG;
        bool valid = type == typeof(LengthPercentage) ? common
            : type == typeof(LengthPercentageAuto) ? common || tag == CompactLength.AUTO_TAG
            : type == typeof(MinTrackSizingFunction) ? common || intrinsic
            : type == typeof(MaxTrackSizingFunction) ? common || intrinsic || tag == CompactLength.FR_TAG
            : type == typeof(Dimension) ? common || intrinsic || keyword
            : common || intrinsic || keyword || tag == CompactLength.FR_TAG;
        // Native serde deliberately rejects calc handles, including fit-content(calc()).
        if (!valid) throw new JsonException("Invalid compact length tag; calc values cannot be deserialized");
        return CompactLength.FromSerializedNumeric(raw);
    }
    private static (string, AlignmentSafety) Alignment(JsonNode value)
    {
        var name = value.GetValue<string>(); var safety = AlignmentSafety.Default;
        if (name.StartsWith("Safe", StringComparison.Ordinal)) { safety = AlignmentSafety.Safe; name = name[4..]; }
        else if (name.StartsWith("Unsafe", StringComparison.Ordinal)) { safety = AlignmentSafety.Unsafe; name = name[6..]; }
        return (name, safety);
    }
    private static object? Read(JsonNode? node, Type type)
    {
        if (type == typeof(float?)) return node == null ? null : Read(node, typeof(float));
        if (type == typeof(AlignItems?)) return node == null ? null : Read(node, typeof(AlignItems));
        if (type == typeof(AlignContent?)) return node == null ? null : Read(node, typeof(AlignContent));
        if (node == null) throw new JsonException($"Null {type.Name}");
        // serde's struct visitor accepts both named objects and positional sequences.
        if (node is JsonArray)
        {
            if (type == typeof(Style)) node = StructSequence(node, true, "dummy", "display", "item_is_table", "item_is_replaced", "item_is_compressible_replaced", "box_sizing", "direction", "overflow", "scrollbar_width", "contain", "float", "clear", "position", "inset", "size", "min_size", "max_size", "aspect_ratio", "margin", "padding", "border", "align_items", "align_self", "justify_items", "justify_self", "align_content", "justify_content", "gap", "text_align", "flex_direction", "flex_wrap", "flex_line_count", "flex_basis", "flex_grow", "flex_shrink", "grid_template_rows", "grid_template_columns", "grid_auto_rows", "grid_auto_columns", "grid_auto_flow", "grid_template_areas", "grid_template_column_names", "grid_template_row_names", "grid_row", "grid_column");
            else if (type == typeof(GridTemplateRepetition)) node = StructSequence(node, false, "count", "tracks", "line_names");
            else if (type == typeof(NamedGridLine)) node = StructSequence(node, false, "name", "index");
            else if (type == typeof(GridTemplateArea)) node = StructSequence(node, false, "name", "row_start", "row_end", "column_start", "column_end");
            else if (type.IsGenericType)
            {
                var generic = type.GetGenericTypeDefinition();
                if (generic == typeof(Size<>)) node = StructSequence(node, false, "width", "height");
                else if (generic == typeof(Point<>)) node = StructSequence(node, false, "x", "y");
                else if (generic == typeof(Rect<>)) node = StructSequence(node, false, "left", "right", "top", "bottom");
                else if (generic == typeof(Line<>)) node = StructSequence(node, true, "start", "end");
                else if (generic == typeof(MinMax<,>)) node = StructSequence(node, false, "min", "max");
                else if (generic == typeof(GridTemplateTracks<>)) node = StructSequence(node, false, "tracks", "line_names");
            }
        }
        if (type == typeof(CompactLength)) return Compact(node.GetValue<ulong>());
        if (type == typeof(LengthPercentage)) return LengthPercentage.FromRaw(Compact(node.GetValue<ulong>(), type));
        if (type == typeof(LengthPercentageAuto)) return LengthPercentageAuto.FromRaw(Compact(node.GetValue<ulong>(), type));
        if (type == typeof(Dimension)) return Dimension.FromRaw(Compact(node.GetValue<ulong>(), type));
        if (type == typeof(MinTrackSizingFunction)) return MinTrackSizingFunction.FromRaw(Compact(node.GetValue<ulong>(), type));
        if (type == typeof(MaxTrackSizingFunction)) return MaxTrackSizingFunction.FromRaw(Compact(node.GetValue<ulong>(), type));
        if (type == typeof(Contain)) return (Contain)node.GetValue<byte>();
        if (type == typeof(AlignItems))
        {
            var (name, safety) = Alignment(node);
            if (!Enum.TryParse<AlignItemsKeyword>(name, out var keyword) || !Enum.IsDefined(keyword) || keyword.ToString() != name || safety != AlignmentSafety.Default && keyword is AlignItemsKeyword.Normal or AlignItemsKeyword.Baseline or AlignItemsKeyword.Stretch) throw new JsonException("Invalid item alignment");
            return new AlignItems(keyword, safety);
        }
        if (type == typeof(AlignContent))
        {
            var (name, safety) = Alignment(node);
            if (!Enum.TryParse<AlignContentKeyword>(name, out var keyword) || !Enum.IsDefined(keyword) || keyword.ToString() != name || safety != AlignmentSafety.Default && keyword is not (AlignContentKeyword.Start or AlignContentKeyword.End or AlignContentKeyword.FlexStart or AlignContentKeyword.FlexEnd or AlignContentKeyword.Center)) throw new JsonException("Invalid content alignment");
            return new AlignContent(keyword, safety);
        }
        if (type == typeof(AvailableSpace)) return node is JsonObject obj ? AvailableSpace.Definite(Required(obj, "Definite").GetValue<float>()) : node.GetValue<string>() switch { "MinContent" => AvailableSpace.MinContent, "MaxContent" => AvailableSpace.MaxContent, _ => throw new JsonException("Invalid available space") };
        if (type == typeof(GridPlacement))
        {
            if (node is JsonValue) return node.GetValue<string>() == "Auto" ? GridPlacement.Auto : throw new JsonException("Invalid placement");
            var variant = node.AsObject().Single(); var value = variant.Value!;
            return variant.Key switch
            {
                "Line" => GridPlacement.FromLine(value.GetValue<short>()), "Span" => GridPlacement.FromSpanCount(value.GetValue<ushort>()),
                "NamedLine" => GridPlacement.FromNamedLine(Required(value, 0).GetValue<string>(), Required(value, 1).GetValue<short>()),
                "NamedSpan" => GridPlacement.FromNamedSpan(Required(value, 0).GetValue<string>(), Required(value, 1).GetValue<ushort>()), _ => throw new JsonException("Invalid placement"),
            };
        }
        if (type == typeof(RepetitionCount)) return node is JsonObject counts ? RepetitionCount.Count(Required(counts, "Count").GetValue<ushort>()) : node.GetValue<string>() switch { "AutoFit" => RepetitionCount.AutoFit, "AutoFill" => RepetitionCount.AutoFill, _ => throw new JsonException("Invalid repetition") };
        if (type == typeof(GridTemplateComponent))
        {
            var variant = node.AsObject().Single();
            return variant.Key switch { "Single" => GridTemplateComponent.FromSingle((MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>)Read(variant.Value, typeof(MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>))!), "Repeat" => GridTemplateComponent.FromRepeat((GridTemplateRepetition)Read(variant.Value, typeof(GridTemplateRepetition))!), _ => throw new JsonException("Invalid grid template") };
        }
        if (type == typeof(GridTemplateRepetition)) return new GridTemplateRepetition((RepetitionCount)Read(node["count"], typeof(RepetitionCount))!, (ImmutableList<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>>)Read(node["tracks"], typeof(ImmutableList<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>>))!, (ImmutableList<ImmutableList<string>>)Read(node["line_names"], typeof(ImmutableList<ImmutableList<string>>))!);
        if (type == typeof(NamedGridLine)) return new NamedGridLine(Required(node, "name").GetValue<string>(), Required(node, "index").GetValue<ushort>());
        if (type == typeof(GridTemplateArea)) return new GridTemplateArea(Required(node, "name").GetValue<string>(), Required(node, "row_start").GetValue<ushort>(), Required(node, "row_end").GetValue<ushort>(), Required(node, "column_start").GetValue<ushort>(), Required(node, "column_end").GetValue<ushort>());
        if (type == typeof(string)) return node.GetValue<string>();
        if (type == typeof(float)) return node.GetValue<float>();
        if (type == typeof(bool)) return node.GetValue<bool>();
        if (type == typeof(ushort)) return node.GetValue<ushort>();
        if (type == typeof(Display)) return EnumValue<Display>(node);
        if (type == typeof(Position)) return EnumValue<Position>(node);
        if (type == typeof(Direction)) return EnumValue<Direction>(node);
        if (type == typeof(Float)) return EnumValue<Float>(node);
        if (type == typeof(Clear)) return EnumValue<Clear>(node);
        if (type == typeof(Overflow)) return EnumValue<Overflow>(node);
        if (type == typeof(BoxSizing)) return EnumValue<BoxSizing>(node);
        if (type == typeof(TextAlign)) return EnumValue<TextAlign>(node);
        if (type == typeof(FlexDirection)) return EnumValue<FlexDirection>(node);
        if (type == typeof(FlexWrap)) return EnumValue<FlexWrap>(node);
        if (type == typeof(GridAutoFlow)) return EnumValue<GridAutoFlow>(node);
        if (type == typeof(Size<Dimension>)) return new Size<Dimension>((Dimension)Read(node["width"], typeof(Dimension))!, (Dimension)Read(node["height"], typeof(Dimension))!);
        if (type == typeof(Size<LengthPercentage>)) return new Size<LengthPercentage>((LengthPercentage)Read(node["width"], typeof(LengthPercentage))!, (LengthPercentage)Read(node["height"], typeof(LengthPercentage))!);
        if (type == typeof(Size<LengthPercentageAuto>)) return new Size<LengthPercentageAuto>((LengthPercentageAuto)Read(node["width"], typeof(LengthPercentageAuto))!, (LengthPercentageAuto)Read(node["height"], typeof(LengthPercentageAuto))!);
        if (type == typeof(Size<float>)) return new Size<float>((float)Read(node["width"], typeof(float))!, (float)Read(node["height"], typeof(float))!);
        if (type == typeof(Point<Overflow>)) return new Point<Overflow>((Overflow)Read(node["x"], typeof(Overflow))!, (Overflow)Read(node["y"], typeof(Overflow))!);
        if (type == typeof(Point<float>)) return new Point<float>((float)Read(node["x"], typeof(float))!, (float)Read(node["y"], typeof(float))!);
        if (type == typeof(Rect<LengthPercentage>)) return new Rect<LengthPercentage>((LengthPercentage)Read(node["left"], typeof(LengthPercentage))!, (LengthPercentage)Read(node["right"], typeof(LengthPercentage))!, (LengthPercentage)Read(node["top"], typeof(LengthPercentage))!, (LengthPercentage)Read(node["bottom"], typeof(LengthPercentage))!);
        if (type == typeof(Rect<LengthPercentageAuto>)) return new Rect<LengthPercentageAuto>((LengthPercentageAuto)Read(node["left"], typeof(LengthPercentageAuto))!, (LengthPercentageAuto)Read(node["right"], typeof(LengthPercentageAuto))!, (LengthPercentageAuto)Read(node["top"], typeof(LengthPercentageAuto))!, (LengthPercentageAuto)Read(node["bottom"], typeof(LengthPercentageAuto))!);
        if (type == typeof(Rect<float>)) return new Rect<float>((float)Read(node["left"], typeof(float))!, (float)Read(node["right"], typeof(float))!, (float)Read(node["top"], typeof(float))!, (float)Read(node["bottom"], typeof(float))!);
        if (type == typeof(Line<GridPlacement>)) return new Line<GridPlacement>(node["start"] is { } line0 ? (GridPlacement)Read(line0, typeof(GridPlacement))! : GridPlacement.Auto, node["end"] is { } line1 ? (GridPlacement)Read(line1, typeof(GridPlacement))! : GridPlacement.Auto);
        if (type == typeof(Line<float>)) return new Line<float>(node["start"] is { } line0 ? (float)Read(line0, typeof(float))! : 0f, node["end"] is { } line1 ? (float)Read(line1, typeof(float))! : 0f);
        if (type == typeof(MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>)) return new MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>((MinTrackSizingFunction)Read(node["min"], typeof(MinTrackSizingFunction))!, (MaxTrackSizingFunction)Read(node["max"], typeof(MaxTrackSizingFunction))!);
        if (type == typeof(ImmutableList<string>)) return node.AsArray().Select(item => (string)Read(item, typeof(string))!).ToImmutableList();
        if (type == typeof(ImmutableList<ImmutableList<string>>)) return node.AsArray().Select(item => (ImmutableList<string>)Read(item, typeof(ImmutableList<string>))!).ToImmutableList();
        if (type == typeof(ImmutableList<GridTemplateComponent>)) return node.AsArray().Select(item => (GridTemplateComponent)Read(item, typeof(GridTemplateComponent))!).ToImmutableList();
        if (type == typeof(ImmutableList<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>>)) return node.AsArray().Select(item => (MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>)Read(item, typeof(MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>))!).ToImmutableList();
        if (type == typeof(ImmutableList<GridTemplateArea>)) return node.AsArray().Select(item => (GridTemplateArea)Read(item, typeof(GridTemplateArea))!).ToImmutableList();
        if (type == typeof(GridTemplateTracks<GridTemplateComponent>)) return new GridTemplateTracks<GridTemplateComponent>((ImmutableList<GridTemplateComponent>)Read(node["tracks"], typeof(ImmutableList<GridTemplateComponent>))!, (ImmutableList<ImmutableList<string>>)Read(node["line_names"], typeof(ImmutableList<ImmutableList<string>>))!);
        if (type == typeof(GridTemplateTracks<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>>)) return new GridTemplateTracks<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>>((ImmutableList<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>>)Read(node["tracks"], typeof(ImmutableList<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>>))!, (ImmutableList<ImmutableList<string>>)Read(node["line_names"], typeof(ImmutableList<ImmutableList<string>>))!);
        if (type == typeof(GridAutoTracks)) return new GridAutoTracks((ImmutableList<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>>)Read(node, typeof(ImmutableList<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>>))!);
        if (type == typeof(Style))
        {
            var style = new Style();
            foreach (var (name, value) in node.AsObject()) switch (name)
            {
                case "dummy": if (value != null) throw new JsonException("The native string marker must be null"); break;
                case "display": style.Display = (Display)Read(value, typeof(Display))!; break;
                case "item_is_table": style.ItemIsTable = (bool)Read(value, typeof(bool))!; break;
                case "item_is_replaced": style.ItemIsReplaced = (bool)Read(value, typeof(bool))!; break;
                case "item_is_compressible_replaced": style.ItemIsCompressibleReplaced = (bool)Read(value, typeof(bool))!; break;
                case "box_sizing": style.BoxSizingValue = (BoxSizing)Read(value, typeof(BoxSizing))!; break;
                case "overflow": style.OverflowValue = (Point<Overflow>)Read(value, typeof(Point<Overflow>))!; break;
                case "scrollbar_width": style.ScrollbarWidthValue = (float)Read(value, typeof(float))!; break;
                case "float": style.FloatValue = (Float)Read(value, typeof(Float))!; break;
                case "clear": style.ClearValue = (Clear)Read(value, typeof(Clear))!; break;
                case "direction": style.DirectionValue = (Direction)Read(value, typeof(Direction))!; break;
                case "position": style.PositionValue = (Position)Read(value, typeof(Position))!; break;
                case "contain": style.ContainValue = (Contain)Read(value, typeof(Contain))!; break;
                case "flex_line_count": style.FlexLineCountValue = (ushort)Read(value, typeof(ushort))!; break;
                case "inset": style.Inset = (Rect<LengthPercentageAuto>)Read(value, typeof(Rect<LengthPercentageAuto>))!; break;
                case "size": style.SizeValue = (Size<Dimension>)Read(value, typeof(Size<Dimension>))!; break;
                case "min_size": style.MinSizeValue = ((Size<LengthPercentageAuto>)Read(value, typeof(Size<LengthPercentageAuto>))!).Map<Dimension>(v => v); break;
                case "max_size": style.MaxSizeValue = ((Size<LengthPercentageAuto>)Read(value, typeof(Size<LengthPercentageAuto>))!).Map<Dimension>(v => v); break;
                case "aspect_ratio": style.AspectRatioValue = (float?)Read(value, typeof(float?))!; break;
                case "margin": style.MarginValue = (Rect<LengthPercentageAuto>)Read(value, typeof(Rect<LengthPercentageAuto>))!; break;
                case "padding": style.PaddingValue = (Rect<LengthPercentage>)Read(value, typeof(Rect<LengthPercentage>))!; break;
                case "border": style.BorderValue = (Rect<LengthPercentage>)Read(value, typeof(Rect<LengthPercentage>))!; break;
                case "align_items": style.AlignItemsValue = (AlignItems)Read(value, typeof(AlignItems))!; break;
                case "align_self": style.AlignSelfValue = (AlignItems?)Read(value, typeof(AlignItems?))!; break;
                case "justify_items": style.JustifyItemsValue = (AlignItems)Read(value, typeof(AlignItems))!; break;
                case "justify_self": style.JustifySelfValue = (AlignItems?)Read(value, typeof(AlignItems?))!; break;
                case "align_content": style.AlignContentValue = (AlignContent)Read(value, typeof(AlignContent))!; break;
                case "justify_content": style.JustifyContentValue = (AlignContent)Read(value, typeof(AlignContent))!; break;
                case "gap": style.GapValue = (Size<LengthPercentage>)Read(value, typeof(Size<LengthPercentage>))!; break;
                case "text_align": style.TextAlignValue = (TextAlign)Read(value, typeof(TextAlign))!; break;
                case "flex_direction": style.FlexDirectionValue = (FlexDirection)Read(value, typeof(FlexDirection))!; break;
                case "flex_wrap": style.FlexWrapValue = (FlexWrap)Read(value, typeof(FlexWrap))!; break;
                case "flex_basis": style.FlexBasisValue = (Dimension)Read(value, typeof(Dimension))!; break;
                case "flex_grow": style.FlexGrowValue = (float)Read(value, typeof(float))!; break;
                case "flex_shrink": style.FlexShrinkValue = (float)Read(value, typeof(float))!; break;
                case "grid_template_rows": style.GridTemplateRows = (ImmutableList<GridTemplateComponent>)Read(value, typeof(ImmutableList<GridTemplateComponent>))!; break;
                case "grid_template_columns": style.GridTemplateColumns = (ImmutableList<GridTemplateComponent>)Read(value, typeof(ImmutableList<GridTemplateComponent>))!; break;
                case "grid_auto_rows": style.GridAutoRows = (ImmutableList<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>>)Read(value, typeof(ImmutableList<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>>))!; break;
                case "grid_auto_columns": style.GridAutoColumns = (ImmutableList<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>>)Read(value, typeof(ImmutableList<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>>))!; break;
                case "grid_auto_flow": style.GridAutoFlowValue = (GridAutoFlow)Read(value, typeof(GridAutoFlow))!; break;
                case "grid_template_areas":
                    if (value != null)
                    {
                        var areas = value is JsonArray ? StructSequence(value, false, "areas", "row_count", "column_count") : value;
                        style.GridTemplateAreasSpecified = true; style.GridTemplateAreaRowCount = Required(areas, "row_count").GetValue<ushort>(); style.GridTemplateAreaColumnCount = Required(areas, "column_count").GetValue<ushort>();
                        style.GridTemplateAreas = (ImmutableList<GridTemplateArea>)Read(areas["areas"], typeof(ImmutableList<GridTemplateArea>))!;
                    }
                    break;
                case "grid_template_column_names": style.GridTemplateColumnNames = (ImmutableList<ImmutableList<string>>)Read(value, typeof(ImmutableList<ImmutableList<string>>))!; break;
                case "grid_template_row_names": style.GridTemplateRowNames = (ImmutableList<ImmutableList<string>>)Read(value, typeof(ImmutableList<ImmutableList<string>>))!; break;
                case "grid_row": style.GridRow = (Line<GridPlacement>)Read(value, typeof(Line<GridPlacement>))!; break;
                case "grid_column": style.GridColumn = (Line<GridPlacement>)Read(value, typeof(Line<GridPlacement>))!; break;
            }
            return style;
        }
        throw new NotSupportedException($"No native style JSON mapping for {type}");
    }
    private static T EnumValue<T>(JsonNode node) where T : struct, Enum
    {
        var text = node.GetValue<string>();
        if (!Enum.TryParse<T>(text, out var value) || !Enum.IsDefined(value) || value.ToString() != text) throw new JsonException("Unknown enum variant");
        return value;
    }
    private static JsonNode Required(JsonNode value, string field) => value[field] ?? throw new JsonException("Missing field " + field);
    private static JsonNode Required(JsonNode value, int index) => value[index] ?? throw new JsonException("Missing tuple item " + index);
    private static JsonObject StructSequence(JsonNode node, bool defaults, params string[] fields)
    {
        var array = node.AsArray();
        if (array.Count > fields.Length || !defaults && array.Count != fields.Length) throw new JsonException("Invalid struct sequence length");
        var result = new JsonObject(); for (int i = 0; i < array.Count; i++) result[fields[i]] = array[i]?.DeepClone(); return result;
    }
}
