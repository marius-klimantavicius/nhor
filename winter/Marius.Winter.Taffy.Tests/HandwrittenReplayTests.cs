using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Marius.Winter.Taffy.Tests;

// Captures the pinned native hand-written tests' complete layout and measure callback traces.
// Every callback constraint, flag, order and output scalar is replayed without float tolerances.
public class HandwrittenReplayTests
{
    private static readonly JsonDocument Corpus = Load();
    private static JsonDocument Load()
    {
        using var raw = typeof(HandwrittenReplayTests).Assembly.GetManifestResourceStream("Marius.Winter.Taffy.Tests.Native.handwritten-corpus.json.gz")!;
        using var gzip = new GZipStream(raw, CompressionMode.Decompress);
        return JsonDocument.Parse(gzip);
    }
    public static IEnumerable<object[]> Cases() => Corpus.RootElement.EnumerateArray().Select((c, i) => new object[] { i, c.GetProperty("name").GetString()! });
    private static float Float(JsonElement bits) => BitConverter.UInt32BitsToSingle(bits.GetUInt32());
    private static float? Optional(JsonElement bits) => bits.ValueKind == JsonValueKind.Null ? null : Float(bits);
    private static T EnumValue<T>(JsonElement value) where T : struct, Enum => Enum.Parse<T>(value.GetString()!);
    private static AvailableSpace Space(JsonElement value) => value.ValueKind == JsonValueKind.String
        ? value.GetString() == "MinContent" ? AvailableSpace.MinContent : AvailableSpace.MaxContent : AvailableSpace.Definite(Float(value));
    private static Size<T> Size<T>(JsonElement value, Func<JsonElement, T> parse) => new(parse(value.GetProperty("width")), parse(value.GetProperty("height")));
    private static Rect<T> Rect<T>(JsonElement value, Func<JsonElement, T> parse) => new(parse(value.GetProperty("left")), parse(value.GetProperty("right")), parse(value.GetProperty("top")), parse(value.GetProperty("bottom")));
    private static CompactLength Compact(JsonElement value)
    {
        ulong raw = value.GetUInt64(); byte tag = (byte)(raw >> 32); float number = BitConverter.UInt32BitsToSingle((uint)raw);
        return tag switch
        {
            CompactLength.LENGTH_TAG => CompactLength.Length(number), CompactLength.PERCENT_TAG => CompactLength.Percent(number),
            CompactLength.AUTO_TAG => CompactLength.AUTO, CompactLength.FR_TAG => CompactLength.Fr(number),
            CompactLength.CONTENT_TAG => CompactLength.Content(), CompactLength.MIN_CONTENT_TAG => CompactLength.MIN_CONTENT,
            CompactLength.MAX_CONTENT_TAG => CompactLength.MAX_CONTENT, CompactLength.FIT_CONTENT_PX_TAG => CompactLength.FitContentPx(number),
            CompactLength.FIT_CONTENT_PERCENT_TAG => CompactLength.FitContentPercent(number), CompactLength.FIT_CONTENT_KEYWORD_TAG => CompactLength.FitContentKeyword(),
            CompactLength.STRETCH_TAG => CompactLength.Stretch(), _ => throw new InvalidOperationException($"Untranslated native compact tag {tag}"),
        };
    }
    private static Dimension Dimension(JsonElement value) => Taffy.Dimension.FromRaw(Compact(value));
    private static LengthPercentage Length(JsonElement value) => LengthPercentage.FromRaw(Compact(value));
    private static LengthPercentageAuto AutoLength(JsonElement value) => LengthPercentageAuto.FromRaw(Compact(value));
    private static AlignItems? Items(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null) return null;
        string name = value.GetString()!; var safety = AlignmentSafety.Default;
        if (name.StartsWith("Safe")) { safety = AlignmentSafety.Safe; name = name[4..]; }
        if (name.StartsWith("Unsafe")) { safety = AlignmentSafety.Unsafe; name = name[6..]; }
        return new(Enum.Parse<AlignItemsKeyword>(name), safety);
    }
    private static AlignContent Content(JsonElement value)
    {
        string name = value.GetString()!; var safety = AlignmentSafety.Default;
        if (name.StartsWith("Safe")) { safety = AlignmentSafety.Safe; name = name[4..]; }
        if (name.StartsWith("Unsafe")) { safety = AlignmentSafety.Unsafe; name = name[6..]; }
        return new(Enum.Parse<AlignContentKeyword>(name), safety);
    }
    private static GridPlacement Placement(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String) { Assert.Equal("Auto", value.GetString()); return GridPlacement.Auto; }
        var property = value.EnumerateObject().Single(); var v = property.Value;
        return property.Name switch
        {
            "Line" => GridPlacement.FromLine(v.GetInt16()), "Span" => GridPlacement.FromSpanCount(v.GetUInt16()),
            "NamedLine" => GridPlacement.FromNamedLine(v[0].GetString()!, v[1].GetInt16()),
            "NamedSpan" => GridPlacement.FromNamedSpan(v[0].GetString()!, v[1].GetUInt16()),
            _ => throw new InvalidOperationException(property.Name),
        };
    }
    private static Line<GridPlacement> PlacementLine(JsonElement value) => new(Placement(value.GetProperty("start")), Placement(value.GetProperty("end")));
    private static MinMax<MinTrackSizingFunction, MaxTrackSizingFunction> Track(JsonElement value) => new(MinTrackSizingFunction.FromRaw(Compact(value.GetProperty("min"))), MaxTrackSizingFunction.FromRaw(Compact(value.GetProperty("max"))));
    private static ImmutableList<ImmutableList<string>> Names(JsonElement value) => value.EnumerateArray().Select(v => v.EnumerateArray().Select(n => n.GetString()!).ToImmutableList()).ToImmutableList();
    private static GridTemplateComponent Template(JsonElement value)
    {
        var property = value.EnumerateObject().Single(); var v = property.Value;
        if (property.Name == "Single") return GridTemplateComponent.FromSingle(Track(v));
        Assert.Equal("Repeat", property.Name);
        var count = v.GetProperty("count");
        var repetitions = count.ValueKind == JsonValueKind.String ? count.GetString() == "AutoFill" ? RepetitionCount.AutoFill : RepetitionCount.AutoFit : RepetitionCount.Count(count.GetProperty("Count").GetUInt16());
        return GridTemplateComponent.FromRepeat(new(repetitions, v.GetProperty("tracks").EnumerateArray().Select(Track).ToImmutableList(), Names(v.GetProperty("line_names"))));
    }
    private static Style Style(JsonElement input)
    {
        var style = new Style();
        foreach (var p in input.EnumerateObject())
        {
            var v = p.Value;
            switch (p.Name)
            {
                case "display": style.Display = EnumValue<Display>(v); break;
                case "position": style.PositionValue = EnumValue<Position>(v); break;
                case "direction": style.DirectionValue = EnumValue<Direction>(v); break;
                case "box_sizing": style.BoxSizingValue = EnumValue<BoxSizing>(v); break;
                case "float": style.FloatValue = EnumValue<Float>(v); break;
                case "clear": style.ClearValue = EnumValue<Clear>(v); break;
                case "contain": style.ContainValue = (Contain)v.GetByte(); break;
                case "size": style.SizeValue = Size(v, Dimension); break;
                case "min_size": style.MinSizeValue = Size(v, Dimension); break;
                case "max_size": style.MaxSizeValue = Size(v, Dimension); break;
                case "margin": style.MarginValue = Rect(v, AutoLength); break;
                case "inset": style.Inset = Rect(v, AutoLength); break;
                case "padding": style.PaddingValue = Rect(v, Length); break;
                case "border": style.BorderValue = Rect(v, Length); break;
                case "aspect_ratio": style.AspectRatioValue = v.ValueKind == JsonValueKind.Null ? null : v.GetSingle(); break;
                case "overflow": style.OverflowValue = new(EnumValue<Overflow>(v.GetProperty("x")), EnumValue<Overflow>(v.GetProperty("y"))); break;
                case "scrollbar_width": style.ScrollbarWidthValue = v.GetSingle(); break;
                case "align_items": style.AlignItemsValue = Items(v); break;
                case "align_self": style.AlignSelfValue = Items(v); break;
                case "justify_items": style.JustifyItemsValue = Items(v); break;
                case "justify_self": style.JustifySelfValue = Items(v); break;
                case "align_content": style.AlignContentValue = Content(v); break;
                case "justify_content": style.JustifyContentValue = Content(v); break;
                case "flex_direction": style.FlexDirectionValue = EnumValue<FlexDirection>(v); break;
                case "flex_wrap": style.FlexWrapValue = EnumValue<FlexWrap>(v); break;
                case "flex_basis": style.FlexBasisValue = Dimension(v); break;
                case "flex_grow": style.FlexGrowValue = v.GetSingle(); break;
                case "flex_shrink": style.FlexShrinkValue = v.GetSingle(); break;
                case "flex_line_count": style.FlexLineCountValue = v.GetUInt16(); break;
                case "text_align": style.TextAlignValue = EnumValue<TextAlign>(v); break;
                case "gap": style.GapValue = Size(v, Length); break;
                case "item_is_replaced": style.ItemIsReplaced = v.GetBoolean(); break;
                case "item_is_compressible_replaced": style.ItemIsCompressibleReplaced = v.GetBoolean(); break;
                case "item_is_table": style.ItemIsTable = v.GetBoolean(); break;
                case "grid_auto_flow": style.GridAutoFlowValue = EnumValue<GridAutoFlow>(v); break;
                case "grid_auto_rows": style.GridAutoRows = v.EnumerateArray().Select(Track).ToImmutableList(); break;
                case "grid_auto_columns": style.GridAutoColumns = v.EnumerateArray().Select(Track).ToImmutableList(); break;
                case "grid_template_rows": style.GridTemplateRows = v.EnumerateArray().Select(Template).ToImmutableList(); break;
                case "grid_template_columns": style.GridTemplateColumns = v.EnumerateArray().Select(Template).ToImmutableList(); break;
                case "grid_template_row_names": style.GridTemplateRowNames = Names(v); break;
                case "grid_template_column_names": style.GridTemplateColumnNames = Names(v); break;
                case "grid_row": style.GridRow = PlacementLine(v); break;
                case "grid_column": style.GridColumn = PlacementLine(v); break;
                case "grid_template_areas":
                    if (v.ValueKind == JsonValueKind.Null) break;
                    style.GridTemplateAreaRowCount = v.GetProperty("row_count").GetUInt16(); style.GridTemplateAreaColumnCount = v.GetProperty("column_count").GetUInt16();
                    style.GridTemplateAreas = v.GetProperty("areas").EnumerateArray().Select(a => new GridTemplateArea(a.GetProperty("name").GetString()!, a.GetProperty("row_start").GetUInt16(), a.GetProperty("row_end").GetUInt16(), a.GetProperty("column_start").GetUInt16(), a.GetProperty("column_end").GetUInt16())).ToImmutableList(); break;
                case "dummy": Assert.Equal(JsonValueKind.Null, v.ValueKind); break; // Rust-only generic-string marker has no layout behavior.
                default: throw new InvalidOperationException($"Untranslated native style {p.Name}");
            }
        }
        return style;
    }
    private static void CheckInput(LayoutInput actual, JsonElement expected)
    {
        static uint? Bits(float? f) => f.HasValue ? BitConverter.SingleToUInt32Bits(f.Value) : null;
        static uint? Expected(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : e.GetUInt32();
        Assert.Equal(expected.GetProperty("run").GetString(), actual.RunMode.ToString());
        Assert.Equal(expected.GetProperty("mode").GetString(), actual.SizingMode.ToString());
        Assert.Equal(expected.GetProperty("axis").GetString(), actual.Axis.ToString());
        Assert.Equal(expected.GetProperty("known").EnumerateArray().Select(Expected), new[] { Bits(actual.KnownDimensions.Width), Bits(actual.KnownDimensions.Height) });
        Assert.Equal(expected.GetProperty("parent").EnumerateArray().Select(Expected), new[] { Bits(actual.ParentSize.Width), Bits(actual.ParentSize.Height) });
        Assert.Equal(new Size<AvailableSpace>(Space(expected.GetProperty("space")[0]), Space(expected.GetProperty("space")[1])), actual.AvailableSpace);
        Assert.Equal(expected.GetProperty("definite").EnumerateArray().Select(v => v.GetBoolean()), new[] { actual.KnownDimensionsAreDefinite.Width, actual.KnownDimensionsAreDefinite.Height });
        Assert.Equal(expected.GetProperty("collapse").EnumerateArray().Select(v => v.GetBoolean()), new[] { actual.VerticalMarginsAreCollapsible.Start, actual.VerticalMarginsAreCollapsible.End });
    }
    [Theory]
    [MemberData(nameof(Cases))]
    public void ReplayNativeHandwritten(int index, string name)
    {
        var tree = new TaffyTree<object>(); var nodes = new Dictionary<ulong, NodeId>();
        var previous = new Dictionary<ulong, string>();
        int phaseIndex = -1;
        foreach (var phase in Corpus.RootElement[index].GetProperty("phases").EnumerateArray())
        {
            phaseIndex++;
            var definitions = phase.GetProperty("nodes").EnumerateArray().ToArray();
            foreach (var n in definitions)
            {
                ulong id = n.GetProperty("id").GetUInt64(); string serialized = n.GetProperty("style").GetRawText();
                if (!nodes.ContainsKey(id)) nodes[id] = tree.NewLeaf(Style(n.GetProperty("style")));
                else if (previous[id] != serialized) tree.SetStyle(nodes[id], Style(n.GetProperty("style")));
                previous[id] = serialized;
            }
            foreach (var n in definitions)
            {
                var id = nodes[n.GetProperty("id").GetUInt64()]; var children = n.GetProperty("children").EnumerateArray().Select(c => nodes[c.GetUInt64()]).ToArray();
                if (!tree.Children(id).SequenceEqual(children)) tree.SetChildren(id, children);
                if (n.GetProperty("dirty").GetBoolean()) tree.MarkDirty(id);
            }
            if (phase.GetProperty("rounding").GetBoolean()) tree.EnableRounding(); else tree.DisableRounding();
            var calls = phase.GetProperty("calls").EnumerateArray().ToArray(); int cursor = 0;
            tree.ComputeLayoutWithMeasure(nodes[phase.GetProperty("root").GetUInt64()], new(Space(phase.GetProperty("available")[0]), Space(phase.GetProperty("available")[1])),
                (input, node, _, _) =>
                {
                    Assert.True(cursor < calls.Length, $"{name}: unexpected callback {cursor}");
                    var call = calls[cursor++]; Assert.Equal(nodes[call.GetProperty("node").GetUInt64()], node);
                    try { CheckInput(input, call.GetProperty("input")); }
                    catch (Xunit.Sdk.XunitException error) { throw new Xunit.Sdk.XunitException($"{name}, phase {phaseIndex}, callback {cursor - 1}, node {call.GetProperty("node")}: {error.Message}"); }
                    var output = call.GetProperty("output"); var size = output.GetProperty("size"); var rect = output.GetProperty("rect"); var baselines = output.GetProperty("baselines");
                    var result = LayoutOutput.FromSizesAndBaselines(new Size<float>(Float(size[0]), Float(size[1])), new Rect<float>(Float(rect[0]), Float(rect[1]), Float(rect[2]), Float(rect[3])), new Baselines(Optional(baselines[0]), Optional(baselines[1])));
                    result.MarginsCanCollapseThrough = output.GetProperty("through").GetBoolean(); return result;
                });
            Assert.Equal(calls.Length, cursor);
            foreach (var expected in phase.GetProperty("expected").EnumerateArray())
            {
                var id = nodes[expected.GetProperty("id").GetUInt64()]; var l = tree.GetLayout(id);
                var actual = new[] { l.Location.X,l.Location.Y,l.Size.Width,l.Size.Height,l.ScrollWidth(),l.ScrollHeight(),l.Border.Left,l.Border.Right,l.Border.Top,l.Border.Bottom,l.Padding.Left,l.Padding.Right,l.Padding.Top,l.Padding.Bottom,l.Margin.Left,l.Margin.Right,l.Margin.Top,l.Margin.Bottom,l.ScrollableOverflowRect.Left,l.ScrollableOverflowRect.Right,l.ScrollableOverflowRect.Top,l.ScrollableOverflowRect.Bottom,l.ScrollbarSize.Width,l.ScrollbarSize.Height };
                Assert.Equal(expected.GetProperty("layout").EnumerateArray().Select(v => v.GetUInt32()), actual.Select(BitConverter.SingleToUInt32Bits));
                Assert.Equal(expected.GetProperty("hoisted").EnumerateArray().Select(v => nodes[v.GetUInt64()]), tree.HoistedChildren(id).ToArray());
                var detailed = expected.GetProperty("detailed"); var info = tree.GetDetailedGridInfo(id);
                if (detailed.ValueKind == JsonValueKind.Null) { Assert.Null(info); continue; }
                Assert.NotNull(info);
                foreach (var query in detailed.GetProperty("absolute_areas").EnumerateArray())
                {
                    var row = PlacementLine(query.GetProperty("row")); var column = PlacementLine(query.GetProperty("column"));
                    var rect = info.ResolveAbsoluteGridArea(row, column, EnumValue<Direction>(query.GetProperty("direction")), new(3.25f, 123.5f, 7.5f, 87.75f));
                    Assert.Equal(query.GetProperty("rect").EnumerateArray().Select(v => v.GetUInt32()), new[] { rect.Left, rect.Right, rect.Top, rect.Bottom }.Select(BitConverter.SingleToUInt32Bits));
                }
                Assert.Equal(detailed.GetProperty("row_strings").EnumerateArray().Select(v => v.GetString()), new[] { info.GridTemplateRows(), info.GridTemplateRows(2) });
                Assert.Equal(detailed.GetProperty("column_strings").EnumerateArray().Select(v => v.GetString()), new[] { info.GridTemplateColumns(), info.GridTemplateColumns(2) });
                foreach (var (key, tracks) in new[] { ("rows", info.Rows), ("columns", info.Columns) })
                {
                    var names = detailed.GetProperty(key).EnumerateArray().ToArray(); var actualNames = tracks.IterLineNames().ToArray();
                    Assert.Equal(names.Length, actualNames.Length);
                    for (int i = 0; i < names.Length; i++) Assert.Equal(names[i].EnumerateArray().Select(v => v.GetString()), actualNames[i]);
                }
                int itemIndex = 0;
                foreach (var area in detailed.GetProperty("areas").EnumerateArray())
                {
                    var actualArea = info.ItemGridArea(itemIndex++);
                    if (area.ValueKind == JsonValueKind.Null) Assert.Null(actualArea);
                    else
                    {
                        Assert.True(actualArea.HasValue);
                        var (location, size) = actualArea.Value;
                        Assert.Equal(area.EnumerateArray().Select(v => v.GetUInt32()), new[] { location.X, location.Y, size.Width, size.Height }.Select(BitConverter.SingleToUInt32Bits));
                    }
                }
            }
        }
    }
}
