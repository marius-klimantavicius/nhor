using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Marius.Winter.Taffy.Tests;

/// <summary>Executes the pinned upstream XML inputs and every expected field, using its assertion rules.</summary>
public class TargetFixtureTests
{
    private static string Fixtures => Path.Combine(AppContext.BaseDirectory, "TargetFixtures");
    public static IEnumerable<object[]> Cases() => Directory.EnumerateFiles(Fixtures, "*.xml", SearchOption.AllDirectories)
        .OrderBy(p => p, StringComparer.Ordinal).Select(p => new object[] { Path.GetRelativePath(Fixtures, p) });

    [Theory]
    [MemberData(nameof(Cases))]
    public void TargetXml(string path) => Run(Path.Combine(Fixtures, path));

    internal static void Run(string path)
    {
        var xml = XDocument.Load(path, LoadOptions.PreserveWhitespace).Root!;
        var tree = new TaffyTree<TestNodeContext>();
        if ((string?)xml.Attribute("use-rounding") == "false") tree.DisableRounding();
        NodeId Build(XElement element)
        {
            var children = element.Elements().Select(Build).ToArray();
            var style = ParseStyle(element);
            if (children.Length != 0) return tree.NewWithChildren(style, children);
            var text = element.Nodes().OfType<XText>().FirstOrDefault()?.Value.Trim();
            return text == null ? tree.NewLeaf(style) : tree.NewLeafWithContext(style,
                TestNodeContext.AhemText(text, ((string?)element.Attribute("writing-mode"))?.Contains("vertical") == true ? WritingMode.Vertical : WritingMode.Horizontal));
        }
        var root = Build(xml.Element("input")!.Elements().Single());
        var viewport = xml.Element("viewport")!;
        tree.ComputeLayoutWithMeasure(root, new(Space((string?)viewport.Attribute("width") ?? "max-content"),
            Space((string?)viewport.Attribute("height") ?? "max-content")), TestHelpers.MeasureFunction);
        void Check(NodeId node, XElement expected)
        {
            var layout = tree.GetLayout(node);
            foreach (var attribute in expected.Attributes())
            {
                var metric = attribute.Name.LocalName;
                if (metric is "resolved-rows" or "resolved-columns")
                {
                    var info = tree.GetDetailedGridInfo(node);
                    // The native tree dispatches childless nodes through leaf measurement, including
                    // display:grid leaves. Their used track list is empty, with no DetailedGridInfo.
                    string actualTracks;
                    if (info != null) actualTracks = metric == "resolved-rows" ? info.GridTemplateRows() : info.GridTemplateColumns();
                    else
                    {
                        Assert.True(tree.GetStyle(node).Display == Display.Grid && tree.ChildCount(node) == 0,
                            $"{path}: missing detailed grid output for {metric}");
                        actualTracks = "none";
                    }
                    CompareTracks(attribute.Value, actualTracks, path);
                    continue;
                }
                float actual = metric switch
                {
                    "x" => layout.Location.X, "y" => layout.Location.Y,
                    "width" => layout.Size.Width, "height" => layout.Size.Height,
                    "scroll_width" => layout.ScrollWidth(), "scroll_height" => layout.ScrollHeight(),
                    _ => throw new InvalidOperationException($"Untranslated expectation {metric} in {path}"),
                };
                float value = Number(attribute.Value);
                Assert.True(MathF.Abs(value - actual) < 0.1f, $"{path}: {node} {metric}: expected {value:R}, actual {actual:R}");
            }
            var children = tree.Children(node);
            var expectedChildren = expected.Elements().ToArray();
            Assert.Equal(expectedChildren.Length, children.Count);
            for (int i = 0; i < children.Count; i++) Check(children[i], expectedChildren[i]);
        }
        Check(root, xml.Element("expectations")!.Elements().Single());
    }
    private static void CompareTracks(string expected, string actual, string path)
    {
        static string[] Tokens(string value) => value.Trim() == "none" ? []
            : Regex.Matches(value, @"\[[^\]]*\]|\S+").Select(m => m.Value).ToArray();
        var e = Tokens(expected); var a = Tokens(actual);
        Assert.True(e.Length == a.Length, $"{path}: expected tracks {expected}, actual {actual}");
        for (int i = 0; i < e.Length; i++)
        {
            if (e[i].StartsWith('[')) Assert.Equal(e[i], a[i]);
            else Assert.True(a[i].EndsWith("px") && MathF.Abs(Number(e[i][..^2]) - Number(a[i][..^2])) < 0.1f,
                $"{path}: expected tracks {expected}, actual {actual}");
        }
    }
    private static float Number(string value) => float.Parse(value, CultureInfo.InvariantCulture);
    private static T Keyword<T>(string value) where T : struct, Enum => Enum.Parse<T>(value.Replace("-", "").Replace(" ", ""), true);
    private static AvailableSpace Space(string value) => value switch
    {
        "min-content" => AvailableSpace.MinContent, "max-content" => AvailableSpace.MaxContent,
        _ => AvailableSpace.Definite(Number(value.EndsWith("px") ? value[..^2] : value)),
    };
    private static Dimension DimensionValue(string value) => value switch
    {
        "auto" => Dimension.AUTO, "min-content" => Dimension.MinContent(), "max-content" => Dimension.MaxContent(),
        "fit-content" => Dimension.FitContent(), "stretch" => Dimension.Stretch(), "content" => Dimension.Content(),
        _ when value.StartsWith("fit-content(") => Dimension.FromRaw(CompactLength.FitContent(Length(value[12..^1]))),
        _ when value.EndsWith('%') => Dimension.Percent(Number(value[..^1]) / 100),
        _ when value.EndsWith("px") => Dimension.Length(Number(value[..^2])),
        _ => throw new InvalidOperationException($"Untranslated dimension {value}"),
    };
    private static LengthPercentage Length(string value) => value.EndsWith('%')
        ? LengthPercentage.Percent(Number(value[..^1]) / 100) : value.EndsWith("px")
        ? LengthPercentage.Length(Number(value[..^2])) : throw new InvalidOperationException(value);
    private static LengthPercentageAuto AutoLength(string value) => value == "auto" ? LengthPercentageAuto.AUTO
        : value.EndsWith('%') ? LengthPercentageAuto.Percent(Number(value[..^1]) / 100)
        : value.EndsWith("px") ? LengthPercentageAuto.Length(Number(value[..^2])) : throw new InvalidOperationException(value);
    private static AlignItems Items(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return new(Keyword<AlignItemsKeyword>(parts[^1]), parts.Length == 1 ? AlignmentSafety.Default : Keyword<AlignmentSafety>(parts[0]));
    }
    private static AlignContent Content(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return new(Keyword<AlignContentKeyword>(parts[^1]), parts.Length == 1 ? AlignmentSafety.Default : Keyword<AlignmentSafety>(parts[0]));
    }
    private static GridPlacement Placement(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        bool span = parts.Contains("span");
        short index = 1; string? name = null; bool hasIndex = false;
        foreach (var part in parts)
        {
            if (part == "auto") return GridPlacement.Auto;
            if (part == "span") continue;
            if (short.TryParse(part, CultureInfo.InvariantCulture, out short n)) { index = n; hasIndex = true; }
            else if (name == null) name = part;
            else throw new InvalidOperationException(value);
        }
        if (name != null && !span && !hasIndex) index = 0;
        return span ? name == null ? GridPlacement.FromSpanCount(checked((ushort)index)) : GridPlacement.FromNamedSpan(name, checked((ushort)index))
            : name == null ? GridPlacement.FromLine(index) : GridPlacement.FromNamedLine(name, index);
    }
    private static List<string> Split(string value, char separator = ' ')
    {
        var tokens = new List<string>(); int depth = 0, start = 0;
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] is '(' or '[') depth++;
            if (value[i] is ')' or ']') depth--;
            if (depth == 0 && value[i] == separator) { if (i > start) tokens.Add(value[start..i].Trim()); start = i + 1; }
        }
        if (start < value.Length) tokens.Add(value[start..].Trim());
        return tokens;
    }
    private static MinMax<MinTrackSizingFunction, MaxTrackSizingFunction> Track(string value)
    {
        if (value.StartsWith("minmax("))
        {
            var parts = Split(value[7..^1], ',');
            return new(MinTrackSizingFunction.FromRaw(DimensionValue(parts[0]).IntoRaw()), Track(parts[1]).Max);
        }
        if (value.StartsWith("fit-content(")) return TrackSizingFunctionExtensions.FitContent(Length(value[12..^1]));
        if (value.EndsWith("fr")) return TrackSizingFunctionExtensions.FromFr(Number(value[..^2]));
        return TrackSizingFunctionExtensions.FromDimension(DimensionValue(value));
    }
    private static (ImmutableList<GridTemplateComponent> Tracks, ImmutableList<ImmutableList<string>> Names) Template(string value)
    {
        if (value == "none") return (ImmutableList<GridTemplateComponent>.Empty, ImmutableList<ImmutableList<string>>.Empty);
        var tracks = ImmutableList.CreateBuilder<GridTemplateComponent>();
        var names = ImmutableList.CreateBuilder<ImmutableList<string>>(); names.Add(ImmutableList<string>.Empty);
        foreach (string token in Split(value))
        {
            if (token.StartsWith('[')) { names[^1] = names[^1].AddRange(token[1..^1].Split(' ', StringSplitOptions.RemoveEmptyEntries)); continue; }
            if (token.StartsWith("repeat("))
            {
                var parts = Split(token[7..^1], ',');
                var repetition = Template(parts[1]);
                var count = parts[0] switch { "auto-fill" => RepetitionCount.AutoFill, "auto-fit" => RepetitionCount.AutoFit,
                    _ => RepetitionCount.Count(ushort.Parse(parts[0], CultureInfo.InvariantCulture)) };
                tracks.Add(GridTemplateComponent.FromRepeat(new(count, repetition.Tracks.Select(t => t.Single).ToImmutableList(), repetition.Names)));
            }
            else tracks.Add(GridTemplateComponent.FromSingle(Track(token)));
            names.Add(ImmutableList<string>.Empty);
        }
        return (tracks.ToImmutable(), names.ToImmutable());
    }
    internal static Style ParseStyle(XElement element)
    {
        var s = new Style();
        foreach (var a in element.Attributes())
        {
            string value = a.Value;
            switch (a.Name.LocalName)
            {
                case "display": s.Display = Keyword<Display>(value); break;
                case "direction": s.DirectionValue = Keyword<Direction>(value); break;
                case "box-sizing": s.BoxSizingValue = Keyword<BoxSizing>(value); break;
                case "position": s.PositionValue = Keyword<Position>(value); break;
                case "contain": s.ContainValue = Keyword<Contain>(value); break;
                case "float": s.FloatValue = Keyword<Float>(value); break;
                case "clear": s.ClearValue = Keyword<Clear>(value); break;
                case "overflow-x": s.OverflowValue.X = Keyword<Overflow>(value); break;
                case "overflow-y": s.OverflowValue.Y = Keyword<Overflow>(value); break;
                case "scrollbar-width": s.ScrollbarWidthValue = Number(value); break;
                case "width": s.SizeValue.Width = DimensionValue(value); break;
                case "height": s.SizeValue.Height = DimensionValue(value); break;
                case "min-width": s.MinSizeValue.Width = DimensionValue(value); break;
                case "min-height": s.MinSizeValue.Height = DimensionValue(value); break;
                case "max-width": s.MaxSizeValue.Width = DimensionValue(value); break;
                case "max-height": s.MaxSizeValue.Height = DimensionValue(value); break;
                case "left": s.Inset.Left = AutoLength(value); break;
                case "right": s.Inset.Right = AutoLength(value); break;
                case "top": s.Inset.Top = AutoLength(value); break;
                case "bottom": s.Inset.Bottom = AutoLength(value); break;
                case "margin-left": s.MarginValue.Left = AutoLength(value); break;
                case "margin-right": s.MarginValue.Right = AutoLength(value); break;
                case "margin-top": s.MarginValue.Top = AutoLength(value); break;
                case "margin-bottom": s.MarginValue.Bottom = AutoLength(value); break;
                case "padding-left": s.PaddingValue.Left = Length(value); break;
                case "padding-right": s.PaddingValue.Right = Length(value); break;
                case "padding-top": s.PaddingValue.Top = Length(value); break;
                case "padding-bottom": s.PaddingValue.Bottom = Length(value); break;
                case "border-left": s.BorderValue.Left = Length(value); break;
                case "border-right": s.BorderValue.Right = Length(value); break;
                case "border-top": s.BorderValue.Top = Length(value); break;
                case "border-bottom": s.BorderValue.Bottom = Length(value); break;
                case "column-gap": s.GapValue.Width = Length(value); break;
                case "row-gap": s.GapValue.Height = Length(value); break;
                case "aspect-ratio": s.AspectRatioValue = Number(value); break;
                case "align-items": s.AlignItemsValue = Items(value); break;
                case "align-self": s.AlignSelfValue = value == "auto" ? null : Items(value); break;
                case "justify-items": s.JustifyItemsValue = Items(value); break;
                case "justify-self": s.JustifySelfValue = value == "auto" ? null : Items(value); break;
                case "align-content": s.AlignContentValue = Content(value); break;
                case "justify-content": s.JustifyContentValue = Content(value); break;
                case "flex-direction": s.FlexDirectionValue = Keyword<FlexDirection>(value); break;
                case "flex-wrap": s.FlexWrapValue = value switch { "balance" => FlexWrap.Balance, "wrap-reverse balance" => FlexWrap.BalanceReverse, _ => Keyword<FlexWrap>(value) }; break;
                case "flex-line-count": s.FlexLineCountValue = ushort.Parse(value, CultureInfo.InvariantCulture); break;
                case "flex-grow": s.FlexGrowValue = Number(value); break;
                case "flex-shrink": s.FlexShrinkValue = Number(value); break;
                case "flex-basis": s.FlexBasisValue = DimensionValue(value); break;
                case "text-align": s.TextAlignValue = value switch { "-webkit-left" => TextAlign.LegacyLeft, "-webkit-right" => TextAlign.LegacyRight, "-webkit-center" => TextAlign.LegacyCenter, _ => throw new InvalidOperationException(value) }; break;
                case "grid-auto-flow": s.GridAutoFlowValue = Keyword<GridAutoFlow>(value); break;
                case "grid-template-rows": (s.GridTemplateRows, s.GridTemplateRowNames) = Template(value); break;
                case "grid-template-columns": (s.GridTemplateColumns, s.GridTemplateColumnNames) = Template(value); break;
                case "grid-auto-rows": s.GridAutoRows = Split(value).Select(Track).ToImmutableList(); break;
                case "grid-auto-columns": s.GridAutoColumns = Split(value).Select(Track).ToImmutableList(); break;
                case "grid-row-start": s.GridRow.Start = Placement(value); break;
                case "grid-row-end": s.GridRow.End = Placement(value); break;
                case "grid-column-start": s.GridColumn.Start = Placement(value); break;
                case "grid-column-end": s.GridColumn.End = Placement(value); break;
                case "writing-mode": break; // Consumed by the Ahem text measurement context above, as in the native runner.
                default: throw new InvalidOperationException($"Untranslated input {a.Name}");
            }
        }
        return s;
    }
}
