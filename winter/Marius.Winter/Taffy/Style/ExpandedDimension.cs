// Port of the Expanded* enums and conversions in src/style/dimension.rs.
using System;

namespace Marius.Winter.Taffy;

public enum ExpandedLengthKind
{
    Length, Percent, Auto, MinContent, MaxContent, FitContentPx, FitContentPercent,
    FitContent, Stretch, Content, Calc, FitContentCalc,
}

public readonly record struct ExpandedDimension
{
    public ExpandedLengthKind Kind { get; }
    public float Value { get; }
    public IntPtr CalcValue { get; }
    private ExpandedDimension(ExpandedLengthKind kind, float value = 0f, IntPtr calcValue = default)
    {
        Kind = kind;
        Value = value;
        CalcValue = calcValue;
    }
    public static ExpandedDimension Length(double value) => Length((float)value);
    public static ExpandedDimension Length(float value) => new(ExpandedLengthKind.Length, value);
    public static ExpandedDimension Percent(double value) => Percent((float)value);
    public static ExpandedDimension Percent(float value) => new(ExpandedLengthKind.Percent, value);
    public static ExpandedDimension Auto => new(ExpandedLengthKind.Auto);
    public static ExpandedDimension MinContent => new(ExpandedLengthKind.MinContent);
    public static ExpandedDimension MaxContent => new(ExpandedLengthKind.MaxContent);
    public static ExpandedDimension FitContent => new(ExpandedLengthKind.FitContent);
    public static ExpandedDimension FitContentPx(double value) => FitContentPx((float)value);
    public static ExpandedDimension FitContentPx(float value) => new(ExpandedLengthKind.FitContentPx, value);
    public static ExpandedDimension FitContentPercent(double value) => FitContentPercent((float)value);
    public static ExpandedDimension FitContentPercent(float value) => new(ExpandedLengthKind.FitContentPercent, value);
    public static ExpandedDimension Stretch => new(ExpandedLengthKind.Stretch);
    public static ExpandedDimension Content => new(ExpandedLengthKind.Content);
    public static ExpandedDimension Calc(IntPtr value) => new(ExpandedLengthKind.Calc, calcValue: value);
    public static ExpandedDimension FitContentCalc(IntPtr value) => new(ExpandedLengthKind.FitContentCalc, calcValue: value);
    public Dimension IntoDimension() => Kind switch
    {
        ExpandedLengthKind.Length => Dimension.Length(Value), ExpandedLengthKind.Percent => Dimension.Percent(Value),
        ExpandedLengthKind.Auto => Dimension.AUTO, ExpandedLengthKind.MinContent => Dimension.MinContent(),
        ExpandedLengthKind.MaxContent => Dimension.MaxContent(), ExpandedLengthKind.FitContent => Dimension.FitContent(),
        ExpandedLengthKind.FitContentPx => Dimension.FitContentPx(Value), ExpandedLengthKind.FitContentPercent => Dimension.FitContentPercent(Value),
        ExpandedLengthKind.Stretch => Dimension.Stretch(), ExpandedLengthKind.Content => Dimension.Content(),
        ExpandedLengthKind.Calc => Dimension.Calc(CalcValue), ExpandedLengthKind.FitContentCalc => Dimension.FitContentCalc(CalcValue),
        _ => throw new InvalidOperationException("Invalid expanded dimension"),
    };
    public bool Equals(ExpandedDimension other) => Kind == other.Kind && Value == other.Value && CalcValue == other.CalcValue;
    public override int GetHashCode() => HashCode.Combine(Kind, Value, CalcValue);
    public static implicit operator ExpandedDimension(Dimension value) => value.Expand();
}

public readonly record struct ExpandedLengthPercentage
{
    private readonly ExpandedDimension _value;
    public ExpandedLengthKind Kind => _value.Kind;
    public float Value => _value.Value;
    public IntPtr CalcValue => _value.CalcValue;
    private ExpandedLengthPercentage(ExpandedDimension value) => _value = value;
    public static ExpandedLengthPercentage Length(double value) => Length((float)value);
    public static ExpandedLengthPercentage Length(float value) => new(ExpandedDimension.Length(value));
    public static ExpandedLengthPercentage Percent(double value) => Percent((float)value);
    public static ExpandedLengthPercentage Percent(float value) => new(ExpandedDimension.Percent(value));
    public static ExpandedLengthPercentage Calc(IntPtr value) => new(ExpandedDimension.Calc(value));
    public LengthPercentage IntoLengthPercentage() => Kind switch
    {
        ExpandedLengthKind.Length => LengthPercentage.Length(Value), ExpandedLengthKind.Percent => LengthPercentage.Percent(Value),
        ExpandedLengthKind.Calc => LengthPercentage.Calc(CalcValue),
        _ => throw new InvalidOperationException("Invalid expanded length-percentage"),
    };
    public static implicit operator ExpandedLengthPercentage(LengthPercentage value) => value.Expand();
}

public readonly record struct ExpandedLengthPercentageAuto
{
    private readonly ExpandedDimension _value;
    public ExpandedLengthKind Kind => _value.Kind;
    public float Value => _value.Value;
    public IntPtr CalcValue => _value.CalcValue;
    private ExpandedLengthPercentageAuto(ExpandedDimension value) => _value = value;
    public static ExpandedLengthPercentageAuto Length(double value) => Length((float)value);
    public static ExpandedLengthPercentageAuto Length(float value) => new(ExpandedDimension.Length(value));
    public static ExpandedLengthPercentageAuto Percent(double value) => Percent((float)value);
    public static ExpandedLengthPercentageAuto Percent(float value) => new(ExpandedDimension.Percent(value));
    public static ExpandedLengthPercentageAuto Calc(IntPtr value) => new(ExpandedDimension.Calc(value));
    public static ExpandedLengthPercentageAuto Auto => new(ExpandedDimension.Auto);
    public LengthPercentageAuto IntoLengthPercentageAuto() => Kind switch
    {
        ExpandedLengthKind.Length => LengthPercentageAuto.Length(Value), ExpandedLengthKind.Percent => LengthPercentageAuto.Percent(Value),
        ExpandedLengthKind.Auto => LengthPercentageAuto.AUTO, ExpandedLengthKind.Calc => LengthPercentageAuto.Calc(CalcValue),
        _ => throw new InvalidOperationException("Invalid expanded length-percentage-auto"),
    };
    public static implicit operator ExpandedLengthPercentageAuto(LengthPercentageAuto value) => value.Expand();
}
