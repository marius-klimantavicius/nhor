// Ported from src/compute/grid/types/named.rs at 802e497b.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Marius.Winter.Taffy;

public sealed class NamedLineResolver
{
    private readonly Dictionary<string, List<int>>[] _lines = [new(StringComparer.Ordinal), new(StringComparer.Ordinal)];
    private readonly Dictionary<string, int[]> _areas = new(StringComparer.Ordinal);
    private readonly List<(int Line, string Name)>[] _pairs = [new(), new()];
    private readonly ushort[] _explicit = new ushort[2];
    public ushort AreaColumnCount { get; }
    public ushort AreaRowCount { get; }
    private static int Axis(GridAreaAxis axis) => axis == GridAreaAxis.Column ? 0 : 1;
    private static (string Name, int End)? SplitEdge(string name) => name.EndsWith("-start", StringComparison.Ordinal)
        ? (name[..^6], 0) : name.EndsWith("-end", StringComparison.Ordinal) ? (name[..^4], 1) : null;
    private int AreaLine(string name, int axis, int end) => _areas.TryGetValue(name, out var area) ? area[axis * 2 + end] : 0;
    private void Add(int axis, string name, int value)
    {
        int areaLine = 0;
        if (SplitEdge(name) is { } edge)
        {
            if (!_areas.TryGetValue(edge.Name, out var area)) _areas.Add(edge.Name, area = new int[4]);
            int i = axis * 2 + edge.End; areaLine = area[i];
            area[i] = areaLine == 0 ? value : Math.Min(areaLine, value);
        }
        if (!_lines[axis].TryGetValue(name, out var lines))
        {
            _lines[axis].Add(name, lines = new());
            if (areaLine != 0) lines.Add(areaLine);
        }
        lines.Add(value); _pairs[axis].Add((value, name));
    }
    public NamedLineResolver(IGridContainerStyle style, ushort columnAutoRepetitions, ushort rowAutoRepetitions)
    {
        AreaColumnCount = style.GetGridTemplateAreaColumnCount(); AreaRowCount = style.GetGridTemplateAreaRowCount();
        foreach (var area in style.GetGridTemplateAreas())
            _areas[area.Name] = [area.ColumnStart, area.ColumnEnd, area.RowStart, area.RowEnd];
        Process(0, style.GetGridTemplateColumns(), style.GetGridTemplateColumnNames(), columnAutoRepetitions);
        Process(1, style.GetGridTemplateRows(), style.GetGridTemplateRowNames(), rowAutoRepetitions);
        foreach (var map in _lines) foreach (var lines in map.Values)
        {
            lines.Sort(); int count = 0;
            for (int i = 0; i < lines.Count; i++) if (count == 0 || lines[i] != lines[count - 1]) lines[count++] = lines[i];
            lines.RemoveRange(count, lines.Count - count);
        }
    }
    private void Process(int axis, ImmutableList<GridTemplateComponent> template, ImmutableList<ImmutableList<string>> names, ushort auto)
    {
        int current = 0;
        for (int i = 0; i < names.Count; i++)
        {
            current++;
            foreach (var name in names[i]) Add(axis, name, current);
            if (i >= template.Count || template[i].Repetition is not { } repeat) continue;
            int count = repeat.Count.IsCount ? repeat.Count.CountValue : auto;
            if (repeat.LineNames.Count != 0 && repeat.LineNames.Count != repeat.TrackCount + 1)
                throw new ArgumentException("Grid repetition must have no line name sets or exactly track count + 1 sets");
            for (int r = 0; r < count; r++)
            {
                for (int j = 0; j < repeat.LineNames.Count; j++) foreach (var name in repeat.LineNames[j]) Add(axis, name, current + j);
                current += repeat.TrackCount;
                if (current > 10000) break;
            }
            if (count > 0) current = Math.Max(current - 1, 0);
        }
    }
    public GridLineNames DetailedLineNames(bool columns)
    {
        int axis = columns ? 0 : 1;
        if (_pairs[axis].Count == 0) return new GridLineNames();
        var output = new GridLineNames(_pairs[axis].Count, _explicit[axis] + 1);
        var pairs = _pairs[axis].OrderBy(p => p.Line).ToArray(); int cursor = 0;
        for (int line = 1; line <= _explicit[axis] + 1; line++)
        {
            output.StartLine();
            while (cursor < pairs.Length && pairs[cursor].Line == line)
            {
                string name = pairs[cursor++].Name; if (!output.CurrentLineContains(name)) output.PushName(name);
            }
        }
        return output;
    }
    public static IReadOnlyList<IReadOnlyList<string>> ExplicitLineNames(IGridContainerStyle style, bool columns, ushort auto)
    {
        var resolver = new NamedLineResolver(style, columns ? auto : (ushort)0, columns ? (ushort)0 : auto);
        int axis = columns ? 0 : 1;
        long count = 0;
        foreach (var component in columns ? style.GetGridTemplateColumns() : style.GetGridTemplateRows())
            count += component.Repetition is { } repeat ? (long)repeat.TrackCount * (repeat.Count.IsCount ? repeat.Count.CountValue : auto) : 1;
        resolver._explicit[axis] = (ushort)Math.Min(count, 10000);
        return resolver.DetailedLineNames(columns);
    }
    private IReadOnlyList<int> LinesNamed(string name, int axis)
    {
        if (_lines[axis].TryGetValue(name, out var lines)) return lines;
        if (SplitEdge(name) is { } edge && AreaLine(edge.Name, axis, edge.End) is var line && line != 0) return new[] { line };
        return Array.Empty<int>();
    }
    private OriginZeroLine NthForward(IReadOnlyList<int> lines, int first, int n, int axis)
    {
        first = Math.Max(first, 0); int start = 0;
        while (start < lines.Count && lines[start] - 1 < first) start++;
        int result = n <= lines.Count - start ? lines[start + n - 1] - 1 : Math.Max(first, _explicit[axis] + 1) + n - (lines.Count - start) - 1;
        return new((short)Math.Clamp(result, -10000, 10000));
    }
    private OriginZeroLine NthBackward(IReadOnlyList<int> lines, int last, int n, int axis)
    {
        last = Math.Min(last, _explicit[axis]); int end = 0;
        while (end < lines.Count && lines[end] - 1 <= last) end++;
        int result = n <= end ? lines[end - n] - 1 : Math.Min(last, -1) - (n - end) + 1;
        return new((short)Math.Clamp(result, -10000, 10000));
    }
    private GridLine ToGrid(OriginZeroLine line, int axis) => new((short)(line.Value >= 0 ? line.Value + 1 : line.Value - _explicit[axis] - 1));
    private GridLine FindNamed(string name, short index, int axis, int end)
    {
        if (index == 0 && AreaLine(name, axis, end) is var line && line != 0)
            return ToGrid(new((short)Math.Clamp(line - 1, -10000, 10000)), axis);
        var lines = LinesNamed(name, axis);
        return ToGrid(index >= 0 ? NthForward(lines, 0, Math.Max((int)index, 1), axis) : NthBackward(lines, _explicit[axis], -(int)index, axis), axis);
    }
    public Line<GenericGridPlacement<GridLine>> ResolveLineNames(Line<GridPlacement> input, GridAreaAxis gridAxis)
    {
        int axis = Axis(gridAxis); var start = input.Start; var end = input.End;
        GridLine? first = start.Kind == GridPlacement.GridPlacementKind.NamedLine ? FindNamed(start.Name!, start.Value, axis, 0)
            : start.Kind == GridPlacement.GridPlacementKind.Line ? new GridLine(start.Value) : null;
        GridLine? last = end.Kind == GridPlacement.GridPlacementKind.NamedLine ? FindNamed(end.Name!, end.Value, axis, 1)
            : end.Kind == GridPlacement.GridPlacementKind.Line ? new GridLine(end.Value) : null;
        if (first is { } f && f.AsI16() != 0 && end.Kind == GridPlacement.GridPlacementKind.NamedSpan)
            last = ToGrid(NthForward(LinesNamed(end.Name!, axis), f.IntoOriginZeroLine(_explicit[axis]).Value + 1, Math.Max((int)(ushort)end.Value, 1), axis), axis);
        else if (last is { } l && l.AsI16() != 0 && start.Kind == GridPlacement.GridPlacementKind.NamedSpan)
            first = ToGrid(NthBackward(LinesNamed(start.Name!, axis), l.IntoOriginZeroLine(_explicit[axis]).Value - 1, Math.Max((int)(ushort)start.Value, 1), axis), axis);
        static GenericGridPlacement<GridLine> Convert(GridPlacement p, GridLine? line) => line is { } l ? GenericGridPlacement<GridLine>.FromLine(l)
            : p.Kind == GridPlacement.GridPlacementKind.Span ? GenericGridPlacement<GridLine>.FromSpan((ushort)p.Value)
            : p.Kind == GridPlacement.GridPlacementKind.NamedSpan ? GenericGridPlacement<GridLine>.FromSpan(1) : GenericGridPlacement<GridLine>.AutoPlacement;
        return new(Convert(start, first), Convert(end, last));
    }
    public Line<GenericGridPlacement<GridLine>> ResolveRowNames(Line<GridPlacement> line) => ResolveLineNames(line, GridAreaAxis.Row);
    public Line<GenericGridPlacement<GridLine>> ResolveColumnNames(Line<GridPlacement> line) => ResolveLineNames(line, GridAreaAxis.Column);
    public void SetExplicitColumnCount(ushort count) => _explicit[0] = count;
    public void SetExplicitRowCount(ushort count) => _explicit[1] = count;
}
