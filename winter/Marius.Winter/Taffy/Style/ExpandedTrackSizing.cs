// Ported from the expanded grid track enums in src/style/grid.rs.
using System;

namespace Marius.Winter.Taffy;

public enum ExpandedTrackKind { Length, Percent, Auto, MinContent, MaxContent, FitContentPx, FitContentPercent, Fr, Calc, FitContentCalc }

public readonly record struct ExpandedMaxTrackSizingFunction
{
    public ExpandedTrackKind Kind { get; }
    public float Value { get; }
    public IntPtr CalcValue { get; }
    private ExpandedMaxTrackSizingFunction(ExpandedTrackKind kind, float value = 0, IntPtr calcValue = default) { Kind = kind; Value = value; CalcValue = calcValue; }
    public static ExpandedMaxTrackSizingFunction Length(float value) => new(ExpandedTrackKind.Length, value);
    public static ExpandedMaxTrackSizingFunction Percent(float value) => new(ExpandedTrackKind.Percent, value);
    public static ExpandedMaxTrackSizingFunction Auto => new(ExpandedTrackKind.Auto);
    public static ExpandedMaxTrackSizingFunction MinContent => new(ExpandedTrackKind.MinContent);
    public static ExpandedMaxTrackSizingFunction MaxContent => new(ExpandedTrackKind.MaxContent);
    public static ExpandedMaxTrackSizingFunction FitContentPx(float value) => new(ExpandedTrackKind.FitContentPx, value);
    public static ExpandedMaxTrackSizingFunction FitContentPercent(float value) => new(ExpandedTrackKind.FitContentPercent, value);
    public static ExpandedMaxTrackSizingFunction Fr(float value) => new(ExpandedTrackKind.Fr, value);
    public static ExpandedMaxTrackSizingFunction Calc(IntPtr value) => new(ExpandedTrackKind.Calc, calcValue: value);
    public static ExpandedMaxTrackSizingFunction FitContentCalc(IntPtr value) => new(ExpandedTrackKind.FitContentCalc, calcValue: value);
    public MaxTrackSizingFunction IntoMaxTrackSizingFunction() => Kind switch
    {
        ExpandedTrackKind.Length => MaxTrackSizingFunction.Length(Value), ExpandedTrackKind.Percent => MaxTrackSizingFunction.Percent(Value),
        ExpandedTrackKind.Auto => MaxTrackSizingFunction.AUTO, ExpandedTrackKind.MinContent => MaxTrackSizingFunction.MIN_CONTENT, ExpandedTrackKind.MaxContent => MaxTrackSizingFunction.MAX_CONTENT,
        ExpandedTrackKind.FitContentPx => MaxTrackSizingFunction.FitContentPx(Value), ExpandedTrackKind.FitContentPercent => MaxTrackSizingFunction.FitContentPercent(Value),
        ExpandedTrackKind.Fr => MaxTrackSizingFunction.Fr(Value), ExpandedTrackKind.Calc => MaxTrackSizingFunction.Calc(CalcValue), ExpandedTrackKind.FitContentCalc => MaxTrackSizingFunction.FitContentCalc(CalcValue),
        _ => throw new InvalidOperationException("Invalid expanded maximum track sizing function"),
    };
    public bool Equals(ExpandedMaxTrackSizingFunction other) => Kind == other.Kind && Value == other.Value && CalcValue == other.CalcValue;
    public override int GetHashCode() => HashCode.Combine(Kind, Value, CalcValue);
    public static implicit operator ExpandedMaxTrackSizingFunction(MaxTrackSizingFunction value) => value.Expand();
    public static implicit operator MaxTrackSizingFunction(ExpandedMaxTrackSizingFunction value) => value.IntoMaxTrackSizingFunction();
}

public readonly record struct ExpandedMinTrackSizingFunction
{
    public ExpandedTrackKind Kind { get; }
    public float Value { get; }
    public IntPtr CalcValue { get; }
    private ExpandedMinTrackSizingFunction(ExpandedTrackKind kind, float value = 0, IntPtr calcValue = default) { Kind = kind; Value = value; CalcValue = calcValue; }
    public static ExpandedMinTrackSizingFunction Length(float value) => new(ExpandedTrackKind.Length, value);
    public static ExpandedMinTrackSizingFunction Percent(float value) => new(ExpandedTrackKind.Percent, value);
    public static ExpandedMinTrackSizingFunction Auto => new(ExpandedTrackKind.Auto);
    public static ExpandedMinTrackSizingFunction MinContent => new(ExpandedTrackKind.MinContent);
    public static ExpandedMinTrackSizingFunction MaxContent => new(ExpandedTrackKind.MaxContent);
    public static ExpandedMinTrackSizingFunction Calc(IntPtr value) => new(ExpandedTrackKind.Calc, calcValue: value);
    public MinTrackSizingFunction IntoMinTrackSizingFunction() => Kind switch
    {
        ExpandedTrackKind.Length => MinTrackSizingFunction.Length(Value), ExpandedTrackKind.Percent => MinTrackSizingFunction.Percent(Value),
        ExpandedTrackKind.Auto => MinTrackSizingFunction.AUTO, ExpandedTrackKind.MinContent => MinTrackSizingFunction.MIN_CONTENT, ExpandedTrackKind.MaxContent => MinTrackSizingFunction.MAX_CONTENT,
        ExpandedTrackKind.Calc => MinTrackSizingFunction.Calc(CalcValue), _ => throw new InvalidOperationException("Invalid expanded minimum track sizing function"),
    };
    public bool Equals(ExpandedMinTrackSizingFunction other) => Kind == other.Kind && Value == other.Value && CalcValue == other.CalcValue;
    public override int GetHashCode() => HashCode.Combine(Kind, Value, CalcValue);
    public static implicit operator ExpandedMinTrackSizingFunction(MinTrackSizingFunction value) => value.Expand();
    public static implicit operator MinTrackSizingFunction(ExpandedMinTrackSizingFunction value) => value.IntoMinTrackSizingFunction();
}
