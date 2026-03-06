// Port of taffy/src/style/available_space.rs

using System;
using System.Runtime.CompilerServices;

namespace Marius.Winter.Taffy;

/// <summary>
/// The amount of space available to a node in a given axis.
/// This is a Rust enum with data variants, ported as a readonly struct with a tag + value.
/// </summary>
public readonly struct AvailableSpace : IEquatable<AvailableSpace>
{
    private enum Tag : byte
    {
        Definite,
        MinContent,
        MaxContent,
    }

    private readonly Tag _tag;
    private readonly float _value;

    private AvailableSpace(Tag tag, float value = 0f)
    {
        _tag = tag;
        _value = value;
    }

    // --- Static factory methods ---

    /// <summary>The amount of space available is the specified number of pixels.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AvailableSpace Definite(float value) => new(Tag.Definite, value);

    /// <summary>
    /// The amount of space available is indefinite and the node should be laid out
    /// under a min-content constraint.
    /// </summary>
    public static readonly AvailableSpace MinContent = new(Tag.MinContent);

    /// <summary>
    /// The amount of space available is indefinite and the node should be laid out
    /// under a max-content constraint.
    /// </summary>
    public static readonly AvailableSpace MaxContent = new(Tag.MaxContent);

    // --- Trait-like constants ---

    /// <summary>TaffyZero::ZERO</summary>
    public static readonly AvailableSpace ZERO = Definite(0f);

    /// <summary>TaffyMaxContent::MAX_CONTENT</summary>
    public static readonly AvailableSpace MAX_CONTENT = MaxContent;

    /// <summary>TaffyMinContent::MIN_CONTENT</summary>
    public static readonly AvailableSpace MIN_CONTENT = MinContent;

    // --- FromLength ---

    /// <summary>Creates a Definite AvailableSpace from a length value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AvailableSpace FromLength(float value) => Definite(value);

    // --- Instance methods ---

    /// <summary>Returns true for definite values, else false.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsDefinite() => _tag == Tag.Definite;

    /// <summary>
    /// Convert to nullable float.
    /// Definite values become the value. Constraints become null.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float? IntoOption()
    {
        return _tag == Tag.Definite ? _value : null;
    }

    /// <summary>Return the definite value or a default value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float UnwrapOr(float defaultValue)
    {
        return _tag == Tag.Definite ? _value : defaultValue;
    }

    /// <summary>Return the definite value. Throws if the value is not definite.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Unwrap()
    {
        if (_tag != Tag.Definite)
            throw new InvalidOperationException("AvailableSpace is not Definite");
        return _value;
    }

    /// <summary>Return self if definite or a default value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AvailableSpace Or(AvailableSpace defaultValue)
    {
        return _tag == Tag.Definite ? this : defaultValue;
    }

    /// <summary>Return self if definite or the result of the default value callback.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AvailableSpace OrElse(Func<AvailableSpace> defaultCb)
    {
        return _tag == Tag.Definite ? this : defaultCb();
    }

    /// <summary>Return the definite value or the result of the default value callback.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float UnwrapOrElse(Func<float> defaultCb)
    {
        return _tag == Tag.Definite ? _value : defaultCb();
    }

    /// <summary>
    /// If passed value is Some then return AvailableSpace.Definite containing that value,
    /// else return self.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AvailableSpace MaybeSet(float? value)
    {
        return value.HasValue ? Definite(value.Value) : this;
    }

    /// <summary>
    /// If this is a Definite value, apply the map function to it and return a new Definite.
    /// Otherwise return self unchanged.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AvailableSpace MapDefiniteValue(Func<float, float> mapFunction)
    {
        return _tag == Tag.Definite ? Definite(mapFunction(_value)) : this;
    }

    /// <summary>Compute free_space given the passed used_space.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ComputeFreeSpace(float usedSpace)
    {
        return _tag switch
        {
            Tag.MaxContent => float.PositiveInfinity,
            Tag.MinContent => 0f,
            Tag.Definite => _value - usedSpace,
            _ => 0f,
        };
    }

    /// <summary>
    /// Compare equality with another AvailableSpace, treating definite values
    /// that are within float.Epsilon of each other as equal.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsRoughlyEqual(AvailableSpace other)
    {
        if (_tag != other._tag)
            return false;

        return _tag != Tag.Definite || MathF.Abs(_value - other._value) < float.Epsilon;
    }

    // --- Conversions ---

    /// <summary>Implicit conversion from float to AvailableSpace.Definite.</summary>
    public static implicit operator AvailableSpace(float value) => Definite(value);

    /// <summary>Implicit conversion from nullable float to AvailableSpace.</summary>
    public static implicit operator AvailableSpace(float? option)
    {
        return option.HasValue ? Definite(option.Value) : MaxContent;
    }

    // --- Equality ---

    public bool Equals(AvailableSpace other)
    {
        if (_tag != other._tag)
            return false;

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        return _tag != Tag.Definite || _value == other._value;
    }

    public override bool Equals(object? obj) => obj is AvailableSpace other && Equals(other);

    public override int GetHashCode() => _tag == Tag.Definite
        ? HashCode.Combine(_tag, _value)
        : HashCode.Combine(_tag);

    public static bool operator ==(AvailableSpace left, AvailableSpace right) => left.Equals(right);
    public static bool operator !=(AvailableSpace left, AvailableSpace right) => !left.Equals(right);

    public override string ToString()
    {
        return _tag switch
        {
            Tag.Definite => $"Definite({_value})",
            Tag.MinContent => "MinContent",
            Tag.MaxContent => "MaxContent",
            _ => "Unknown",
        };
    }
}
