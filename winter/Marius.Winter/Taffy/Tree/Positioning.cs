// Ported from src/tree/layout.rs at 802e497b.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Marius.Winter.Taffy;

/// <summary>Flags whose zero-initialized representation is definite in both axes.</summary>
public struct DefiniteDimensions
{
    private bool _widthIndefinite, _heightIndefinite;
    public bool Width { readonly get => !_widthIndefinite; set => _widthIndefinite = !value; }
    public bool Height { readonly get => !_heightIndefinite; set => _heightIndefinite = !value; }
    public DefiniteDimensions(bool width, bool height) { _widthIndefinite = !width; _heightIndefinite = !height; }
    public readonly DefiniteDimensions Transpose() => new(Height, Width);
    public static implicit operator Size<bool>(DefiniteDimensions flags) => new(flags.Width, flags.Height);
    public static implicit operator DefiniteDimensions(Size<bool> flags) => new(flags.Width, flags.Height);
}

public readonly record struct Baselines(float? First, float? Last)
{
    public static readonly Baselines None = new(null, null);
    public static Baselines FromFirst(float? first) => new(first, null);
    public bool Equals(Baselines other) => First == other.First && Last == other.Last;
    public override int GetHashCode() => HashCode.Combine(First, Last);
}

public enum AxisStaticEdge { Start, Center, End }
public readonly record struct AxisStaticAlign(AxisStaticEdge Keyword, AlignmentSafety Safety, AxisStaticEdge Fallback)
{
    public static AxisStaticAlign FromKeyword(AxisStaticEdge keyword) => new(keyword, AlignmentSafety.Default, keyword);
}
public struct AxisStaticPosition : IEquatable<AxisStaticPosition>
{
    public Line<float> Area;
    public AxisStaticAlign Align;
    public readonly bool Equals(AxisStaticPosition other) => Area == other.Area && Align == other.Align;
    public override readonly bool Equals(object? value) => value is AxisStaticPosition other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(Area, Align);
    public AxisStaticPosition(Line<float> area, AxisStaticAlign align) { Area = area; Align = align; }
    public static AxisStaticPosition FromEdge(float anchor, AxisStaticEdge edge) => new(new(anchor, anchor), AxisStaticAlign.FromKeyword(edge));
    public static AxisStaticPosition FromAlignment(AlignItems alignment, Line<float> area, bool axisIsRtl)
    {
        static AxisStaticEdge Edge(AlignItemsKeyword keyword, bool rtl) => keyword == AlignItemsKeyword.Center
            ? AxisStaticEdge.Center
            : (!(keyword is AlignItemsKeyword.End or AlignItemsKeyword.FlexEnd) ^ rtl) ? AxisStaticEdge.Start : AxisStaticEdge.End;
        return new(area, new(Edge(alignment.Keyword, axisIsRtl), alignment.Safety,
            Edge(alignment.IsSafe ? AlignItemsKeyword.Start : alignment.Keyword, axisIsRtl)));
    }
}
public struct OofCandidate : IEquatable<OofCandidate>
{
    public NodeId Node;
    public uint Order;
    public Position Position;
    public Point<AxisStaticPosition> StaticPosition;
    public readonly bool Equals(OofCandidate other) => Node == other.Node && Order == other.Order && Position == other.Position && StaticPosition == other.StaticPosition;
    public override readonly bool Equals(object? value) => value is OofCandidate other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(Node, Order, Position, StaticPosition);
}

/// <summary>Lazily allocated list. Clone at cache boundaries, just like Rust's owned LayoutOutput.</summary>
public struct OofCandidates : IEquatable<OofCandidates>
{
    private List<OofCandidate>? _items;
    public readonly bool Equals(OofCandidates other)
    {
        if ((_items == null) != (other._items == null)) return false;
        var left = AsSpan(); var right = other.AsSpan();
        if (left.Length != right.Length) return false;
        for (int i = 0; i < left.Length; i++) if (!left[i].Equals(right[i])) return false;
        return true;
    }
    public override readonly bool Equals(object? value) => value is OofCandidates other && Equals(other);
    public override readonly int GetHashCode()
    {
        var hash = new HashCode(); hash.Add(_items != null); foreach (var item in AsSpan()) hash.Add(item); return hash.ToHashCode();
    }
    public readonly bool IsEmpty => _items == null || _items.Count == 0;
    public readonly ReadOnlySpan<OofCandidate> AsSpan() => _items == null ? [] : CollectionsMarshal.AsSpan(_items);
    public void Push(OofCandidate candidate) => (_items ??= new()).Add(candidate);
    public OofCandidates Take() { var old = this; this = default; return old; }
    public void Clear() => this = default;
    public readonly OofCandidates Clone() => new() { _items = _items == null ? null : new(_items) };
    public void Append(ref OofCandidates other)
    {
        if (other.IsEmpty) return;
        if (_items == null) _items = other._items;
        else _items.AddRange(other._items!);
        other.Clear();
    }
    public void Translate(Point<float> offset)
    {
        if (_items == null) return;
        foreach (ref var candidate in CollectionsMarshal.AsSpan(_items))
        {
            candidate.StaticPosition.X.Area.Start += offset.X;
            candidate.StaticPosition.X.Area.End += offset.X;
            candidate.StaticPosition.Y.Area.Start += offset.Y;
            candidate.StaticPosition.Y.Area.End += offset.Y;
        }
    }
    public void Transpose()
    {
        if (_items == null) return;
        foreach (ref var candidate in CollectionsMarshal.AsSpan(_items))
            candidate.StaticPosition = new(candidate.StaticPosition.Y, candidate.StaticPosition.X);
    }
}
public readonly record struct OofPositioningArea(Size<float> Size, Point<float> Offset);

public interface ILayoutContainingBlock : ILayoutPartialTree
{
    IOofItemStyle GetOofItemStyle(NodeId nodeId);
    void ClearHoistedChildren(NodeId nodeId);
    void AddHoistedChildren(NodeId nodeId, ReadOnlySpan<NodeId> children);
    DetailedGridInfo? GetDetailedGridInfo(NodeId nodeId);
}
