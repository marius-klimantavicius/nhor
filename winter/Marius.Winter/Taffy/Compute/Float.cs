// Port of taffy/src/compute/float.rs at 802e497b69689e1c93be2d16e9d53a973971bcb2.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static Marius.Winter.Taffy.TaffySys;

namespace Marius.Winter.Taffy;

public struct ContentSlot
{
    public int? SegmentId;
    public float X, Y, Width, Height;
}

public struct BfcSlot
{
    public int? SegmentId;
    public float X, Y, BorderWidth, StretchWidth;
}

public struct PlacedFloatedBox
{
    public ulong Id;
    public float Width, Height, XInset, Y;
}

internal struct Segment
{
    public float YStart, YEnd, Inset0, Inset1;
    public bool HasFloat0, HasFloat1;
    public readonly float GetInset(int side) => side == 0 ? Inset0 : Inset1;
    public readonly bool HasFloat(int side) => side == 0 ? HasFloat0 : HasFloat1;
    public void SetFloat(int side, float inset)
    {
        if (side == 0) { Inset0 = inset; HasFloat0 = true; }
        else { Inset1 = inset; HasFloat1 = true; }
    }
}

internal struct FloatFitter
{
    private readonly float _bfcWidth, _cbInset0, _cbInset1;
    private double _slotHeight;
    private float _inset0, _inset1;

    public FloatFitter(float width, float height, float[] cbInsets)
    {
        _bfcWidth = width;
        _slotHeight = height;
        _cbInset0 = cbInsets[0];
        _cbInset1 = cbInsets[1];
        _inset0 = _inset1 = 0;
    }

    public void UnionInsets(Segment segment)
    {
        _inset0 = F32Max(_inset0, segment.Inset0);
        _inset1 = F32Max(_inset1, segment.Inset1);
    }

    public readonly float PlacedInset(FloatDirection direction) => direction == FloatDirection.Left
        ? F32Max(_inset0, _cbInset0) : F32Max(_inset1, _cbInset1);
    public readonly bool FitsHorizontally(float width, FloatDirection direction) =>
        FloatContext.FloatFitsHorizontally(width, direction, _bfcWidth, _inset0, _inset1, _cbInset0, _cbInset1);
    public void AddHeight(float height) => _slotHeight += height;
    public readonly bool FitsVertically(float height) => _slotHeight >= height;
}

[InlineArray(2)]
internal struct FloatSidePair<T>
{
    private T _element0;
}

public class FloatContext
{
    internal const float FIT_TOLERANCE = 0.001f;
    private float _availableWidth;
    private bool _hasFloats;
    private readonly List<PlacedFloatedBox> _leftFloats = new();
    private readonly List<PlacedFloatedBox> _rightFloats = new();
    private readonly List<Segment> _segments = new();
    private FloatSidePair<int> _lastPlacedStarts;
    private FloatSidePair<int> _lastPlacedEnds;
    private FloatSidePair<float?> _clearBottoms;
    private float? _floatCeiling;

    internal static bool FloatFitsHorizontally(float width, FloatDirection direction, float bfcWidth,
        float inset0, float inset1, float cbInset0, float cbInset1)
    {
        float lead = direction == FloatDirection.Left ? inset0 : inset1;
        float trail = direction == FloatDirection.Left ? inset1 : inset0;
        float cbLead = direction == FloatDirection.Left ? cbInset0 : cbInset1;
        float cbTrail = direction == FloatDirection.Left ? cbInset1 : cbInset0;
        float xInset = F32Max(lead, cbLead);
        bool fitsOppositeFloats = trail == 0 || xInset + width <= bfcWidth - trail + FIT_TOLERANCE;
        bool fitsContainingBlock = lead == 0 || xInset + width <= bfcWidth - cbTrail + FIT_TOLERANCE;
        return fitsOppositeFloats && fitsContainingBlock;
    }

    public bool HasFloats() => _hasFloats;
    public bool HasActiveFloats(float minY) => _hasFloats && _segments.Count > 0 && _segments[^1].YEnd > minY;
    public void SetWidth(float width) => _availableWidth = width;
    public IReadOnlyList<PlacedFloatedBox> LeftFloats => _leftFloats;
    public IReadOnlyList<PlacedFloatedBox> RightFloats => _rightFloats;

    private void SubdivideSegment(int index, float divideAtY)
    {
        var old = _segments[index];
        if (!(old.YStart < divideAtY && divideAtY < old.YEnd))
            throw new InvalidOperationException("Float segment subdivision must be strictly inside the segment");
        var next = old;
        next.YStart = divideAtY;
        old.YEnd = divideAtY;
        _segments[index] = old;
        _segments.Insert(index + 1, next);
    }

    internal Point<float> PlacedPosition(PlacedFloatedBox placed, FloatDirection direction) =>
        new(direction == FloatDirection.Left ? placed.XInset : _availableWidth - placed.XInset - placed.Width, placed.Y);

    private void UpdateLastPlacedFloat(FloatDirection direction, int start, int end)
    {
        int side = (int)direction;
        _lastPlacedStarts[side] = Math.Max(_lastPlacedStarts[side], start);
        _lastPlacedEnds[side] = Math.Max(_lastPlacedEnds[side], end);
    }

    // Compatibility overload for existing Winter block callers; the ID overload supports FloatMark tracking.
    public Point<float> PlaceFloatedBox(Size<float> size, float minY, float[] insets, FloatDirection direction, Clear clear) =>
        PlaceFloatedBox(0, size, minY, insets, direction, clear);

    public Point<float> PlaceFloatedBox(ulong id, Size<float> size, float minY, float[] insets, FloatDirection direction, Clear clear)
    {
        _hasFloats = true;
        var placed = PlaceFloatedBoxInner(id, size, minY, insets, direction, clear);
        int side = (int)direction;
        float bottom = placed.Y + placed.Height;
        _clearBottoms[side] = _clearBottoms[side] is float previous ? F32Max(previous, bottom) : bottom;
        _floatCeiling = _floatCeiling is float ceiling ? F32Max(ceiling, placed.Y) : placed.Y;
        (direction == FloatDirection.Left ? _leftFloats : _rightFloats).Add(placed);
        return PlacedPosition(placed, direction);
    }

    private PlacedFloatedBox PlaceFloatedBoxInner(ulong id, Size<float> size, float minY, float[] cbInsets,
        FloatDirection direction, Clear clear)
    {
        int side = (int)direction;
        minY = F32Max(F32Max(minY, _floatCeiling ?? float.NegativeInfinity), ClearedThreshold(clear) ?? float.NegativeInfinity);
        int floatStart = Math.Max(_lastPlacedStarts[0], _lastPlacedStarts[1]);
        int hwm = clear switch
        {
            Clear.Left => Math.Max(floatStart, _lastPlacedEnds[0] + 1),
            Clear.Right => Math.Max(floatStart, _lastPlacedEnds[1] + 1),
            Clear.Both => Math.Max(_lastPlacedEnds[0], _lastPlacedEnds[1]) + 1,
            _ => floatStart,
        };
        int startIndex = FindSegment(hwm, minY);
        float startY = minY;
        int endIndex = startIndex;
        int? start, end;
        float placedInset;
        while (true)
        {
            if (startIndex >= _segments.Count)
            {
                start = end = null;
                placedInset = cbInsets[side];
                break;
            }
            var first = _segments[startIndex];
            if (!FloatFitsHorizontally(size.Width, direction, _availableWidth, first.Inset0, first.Inset1, cbInsets[0], cbInsets[1]))
            {
                startIndex++;
                endIndex = Math.Max(endIndex, startIndex);
                continue;
            }
            startY = F32Max(startY, first.YStart);
            var fitter = new FloatFitter(_availableWidth, first.YEnd - startY, cbInsets);
            fitter.UnionInsets(first);
            while (true)
            {
                if (endIndex >= _segments.Count)
                {
                    start = startIndex;
                    end = null;
                    placedInset = fitter.PlacedInset(direction);
                    goto fitted;
                }
                var last = _segments[endIndex];
                fitter.UnionInsets(last);
                if (!fitter.FitsHorizontally(size.Width, direction))
                {
                    startIndex++;
                    endIndex = Math.Max(endIndex, startIndex);
                    break;
                }
                if (endIndex != startIndex) fitter.AddHeight(last.YEnd - last.YStart);
                if (!fitter.FitsVertically(size.Height)) { endIndex++; continue; }
                start = startIndex;
                end = endIndex;
                placedInset = fitter.PlacedInset(direction);
                goto fitted;
            }
        }
    fitted:
        if (size.Height == 0)
            return new PlacedFloatedBox { Id = id, Width = size.Width, Height = size.Height, Y = startY, XInset = placedInset };
        if (!start.HasValue)
        {
            float lastEnd = _segments.Count > 0 ? _segments[^1].YEnd : float.NegativeInfinity;
            if (startY > lastEnd) _segments.Add(new Segment { YStart = lastEnd, YEnd = startY });
            startY = F32Max(lastEnd, startY);
            var segment = new Segment { YStart = startY, YEnd = startY + size.Height, Inset0 = cbInsets[0], Inset1 = cbInsets[1] };
            segment.SetFloat(side, segment.GetInset(side) + size.Width);
            _segments.Add(segment);
            UpdateLastPlacedFloat(direction, _segments.Count - 1, _segments.Count);
            return new PlacedFloatedBox { Id = id, Width = size.Width, Height = size.Height, Y = startY, XInset = cbInsets[side] };
        }
        startIndex = start.Value;
        if (startY != _segments[startIndex].YStart)
        {
            SubdivideSegment(startIndex, startY);
            startIndex++;
            if (end.HasValue) end++;
        }
        float endY = startY + size.Height;
        if (!end.HasValue)
        {
            float lastEnd = _segments[^1].YEnd;
            if (endY > lastEnd)
                _segments.Add(new Segment { YStart = lastEnd, YEnd = endY, Inset0 = cbInsets[0], Inset1 = cbInsets[1] });
            endIndex = _segments.Count - 1;
        }
        else
        {
            endIndex = end.Value;
            while (endIndex > startIndex && endY <= _segments[endIndex].YStart) endIndex--;
            if (_segments[endIndex].YStart < endY && endY < _segments[endIndex].YEnd)
                SubdivideSegment(endIndex, endY);
        }
        for (int index = startIndex; index <= endIndex; index++)
        {
            var segment = _segments[index];
            segment.SetFloat(side, placedInset + size.Width);
            _segments[index] = segment;
        }
        UpdateLastPlacedFloat(direction, startIndex, endIndex + 1);
        return new PlacedFloatedBox { Id = id, Width = size.Width, Height = size.Height, Y = startY, XInset = placedInset };
    }

    private int FindSegment(int hwm, float minY)
    {
        for (int i = hwm; i < _segments.Count; i++)
            if (_segments[i].YEnd > minY) return i;
        return _segments.Count;
    }

    private int? ClearedSegment(Clear clear) => clear switch
    {
        Clear.Left when _lastPlacedEnds[0] > 0 => _lastPlacedEnds[0],
        Clear.Right when _lastPlacedEnds[1] > 0 => _lastPlacedEnds[1],
        Clear.Both when _lastPlacedEnds[0] > 0 || _lastPlacedEnds[1] > 0 => Math.Max(_lastPlacedEnds[0], _lastPlacedEnds[1]),
        _ => null,
    };

    public float? ClearedThreshold(Clear clear) => clear switch
    {
        Clear.Left => _clearBottoms[0],
        Clear.Right => _clearBottoms[1],
        Clear.Both => _clearBottoms[0] is float left && _clearBottoms[1] is float right
            ? F32Max(left, right) : _clearBottoms[0] ?? _clearBottoms[1],
        _ => null,
    };

    public ContentSlot FindContentSlot(float minY, float[] cbInsets, Clear clear, int? after)
    {
        ContentSlot EmptySlot(float y) => new() { X = cbInsets[0], Y = y,
            Width = _availableWidth - cbInsets[0] - cbInsets[1], Height = float.PositiveInfinity };
        if (!HasActiveFloats(minY)) return EmptySlot(minY);
        minY = F32Max(minY, ClearedThreshold(clear) ?? float.NegativeInfinity);
        int hwm = Math.Max(after.HasValue ? after.Value + 1 : 0, ClearedSegment(clear) is int cleared ? cleared + 1 : 0);
        int start = FindSegment(hwm, minY);
        if (start == _segments.Count) return EmptySlot(minY);
        var segment = _segments[start];
        float left = F32Max(segment.Inset0, cbInsets[0]);
        float right = F32Max(segment.Inset1, cbInsets[1]);
        return new ContentSlot { SegmentId = start, X = left, Y = F32Max(segment.YStart, minY),
            Width = _availableWidth - left - right, Height = float.PositiveInfinity };
    }

    public BfcSlot FindBfcSlot(float minY, float[] cbInsets, float[] margins, Direction direction, Clear clear, int? after)
    {
        Span<float> marginInsets = stackalloc float[] { cbInsets[0] + margins[0], cbInsets[1] + margins[1] };
        float noFloatWidth = _availableWidth - marginInsets[0] - marginInsets[1];
        var noFloatSlot = new BfcSlot { X = marginInsets[0], Y = minY, BorderWidth = noFloatWidth, StretchWidth = noFloatWidth };
        if (!HasActiveFloats(minY)) return noFloatSlot;
        minY = F32Max(minY, ClearedThreshold(clear) ?? float.NegativeInfinity);
        int hwm = Math.Max(after.HasValue ? after.Value + 1 : 0, ClearedSegment(clear) is int cleared ? cleared + 1 : 0);
        int start = FindSegment(hwm, minY);
        if (start == _segments.Count)
        {
            noFloatSlot.Y = F32Max(_segments.Count > 0 ? _segments[^1].YEnd : minY, minY);
            return noFloatSlot;
        }
        var segment = _segments[start];
        int lead = direction.IsRtl() ? 1 : 0;
        int trail = 1 - lead;
        Span<float> fit = stackalloc float[2];
        Span<float> stretch = stackalloc float[2];
        fit[lead] = segment.HasFloat(lead) ? F32Max(segment.GetInset(lead), marginInsets[lead]) : marginInsets[lead];
        stretch[lead] = fit[lead];
        fit[trail] = segment.HasFloat(trail) ? F32Max(segment.GetInset(trail), cbInsets[trail]) : F32Min(marginInsets[trail], cbInsets[trail]);
        stretch[trail] = segment.HasFloat(trail) ? F32Max(segment.GetInset(trail), marginInsets[trail]) : marginInsets[trail];
        return new BfcSlot { SegmentId = start, X = fit[0], Y = F32Max(segment.YStart, minY),
            BorderWidth = _availableWidth - fit[0] - fit[1], StretchWidth = _availableWidth - stretch[0] - stretch[1] };
    }
}

public struct FloatIntrinsicWidthCalculator
{
    private readonly AvailableSpace _availableWidth;
    private float _leftSum, _rightSum, _contribution, _widest;
    public FloatIntrinsicWidthCalculator(AvailableSpace availableWidth)
    {
        _availableWidth = availableWidth;
        _leftSum = _rightSum = _contribution = _widest = 0;
    }
    public void AddFloat(float width, FloatDirection direction, Clear clear)
    {
        if (_availableWidth == AvailableSpace.MinContent)
            _contribution = F32Max(_contribution, width);
        else
        {
            if (clear is Clear.Left or Clear.Both) _leftSum = 0;
            if (clear is Clear.Right or Clear.Both) _rightSum = 0;
            if (direction == FloatDirection.Left) _leftSum += width;
            else _rightSum += width;
            _contribution = F32Max(_contribution, _leftSum + _rightSum);
        }
        _widest = F32Max(_widest, width);
    }
    public readonly float Result() => _availableWidth.IsDefinite()
        ? F32Max(F32Min(_contribution, _availableWidth.Unwrap()), _widest) : _contribution;
}
