// Port of taffy/src/style/alignment.rs

using System;

namespace Marius.Winter.Taffy;

/// <summary>The position-keyword half of <see cref="AlignItems"/>.</summary>
public enum AlignItemsKeyword
{
    Normal,
    Start,
    End,
    FlexStart,
    FlexEnd,
    SelfStart,
    SelfEnd,
    Center,
    Baseline,
    Stretch,
}

/// <summary>The position-keyword half of <see cref="AlignContent"/>.</summary>
public enum AlignContentKeyword
{
    Normal,
    Start,
    End,
    FlexStart,
    FlexEnd,
    Center,
    Stretch,
    SpaceBetween,
    SpaceEvenly,
    SpaceAround,
}

public static class AlignContentKeywordExtensions
{
    /// <summary>Returns the reversed keyword for RTL (right-to-left) contexts.</summary>
    public static AlignContentKeyword Reversed(this AlignContentKeyword self)
    {
        return self switch
        {
            AlignContentKeyword.Start => AlignContentKeyword.End,
            AlignContentKeyword.End => AlignContentKeyword.Start,
            AlignContentKeyword.FlexStart => AlignContentKeyword.FlexEnd,
            AlignContentKeyword.FlexEnd => AlignContentKeyword.FlexStart,
            AlignContentKeyword.Stretch => AlignContentKeyword.End,
            _ => self,
        };
    }
}

/// <summary>The overflow-position modifier from CSS Box Alignment.</summary>
public enum AlignmentSafety
{
    Default,
    Safe,
    Unsafe,
}

/// <summary>
/// Used to control how child nodes are aligned. For Flexbox it controls alignment in the
/// cross axis. For Grid it controls alignment in the block axis.
/// </summary>
public readonly struct AlignItems : IEquatable<AlignItems>
{
    public static readonly AlignItems Normal = new(AlignItemsKeyword.Normal, AlignmentSafety.Default);
    public static readonly AlignItems SelfStart = new(AlignItemsKeyword.SelfStart, AlignmentSafety.Default);
    public static readonly AlignItems SelfEnd = new(AlignItemsKeyword.SelfEnd, AlignmentSafety.Default);
    public static readonly AlignItems SafeSelfStart = new(AlignItemsKeyword.SelfStart, AlignmentSafety.Safe);
    public static readonly AlignItems SafeSelfEnd = new(AlignItemsKeyword.SelfEnd, AlignmentSafety.Safe);
    public bool IsStretchOrNormal => Keyword is AlignItemsKeyword.Normal or AlignItemsKeyword.Stretch;
    public AlignItems WithSafety(AlignmentSafety safety) => new(Keyword, safety);
    public AlignItems ResolveSelfRelative(Direction itemDirection, Direction containerDirection, bool axisIsInline)
    {
        bool flip = axisIsInline && itemDirection != containerDirection;
        return new(Keyword switch
        {
            AlignItemsKeyword.SelfStart => flip ? AlignItemsKeyword.End : AlignItemsKeyword.Start,
            AlignItemsKeyword.SelfEnd => flip ? AlignItemsKeyword.Start : AlignItemsKeyword.End,
            _ => Keyword,
        }, Safety);
    }
    public static readonly AlignItems Start = new AlignItems(AlignItemsKeyword.Start, AlignmentSafety.Default);
    public static readonly AlignItems End = new AlignItems(AlignItemsKeyword.End, AlignmentSafety.Default);
    public static readonly AlignItems FlexStart = new AlignItems(AlignItemsKeyword.FlexStart, AlignmentSafety.Default);
    public static readonly AlignItems FlexEnd = new AlignItems(AlignItemsKeyword.FlexEnd, AlignmentSafety.Default);
    public static readonly AlignItems Center = new AlignItems(AlignItemsKeyword.Center, AlignmentSafety.Default);
    public static readonly AlignItems Baseline = new AlignItems(AlignItemsKeyword.Baseline, AlignmentSafety.Default);
    public static readonly AlignItems Stretch = new AlignItems(AlignItemsKeyword.Stretch, AlignmentSafety.Default);
    public static readonly AlignItems SafeStart = new AlignItems(AlignItemsKeyword.Start, AlignmentSafety.Safe);
    public static readonly AlignItems SafeEnd = new AlignItems(AlignItemsKeyword.End, AlignmentSafety.Safe);
    public static readonly AlignItems SafeFlexStart = new AlignItems(AlignItemsKeyword.FlexStart, AlignmentSafety.Safe);
    public static readonly AlignItems SafeFlexEnd = new AlignItems(AlignItemsKeyword.FlexEnd, AlignmentSafety.Safe);
    public static readonly AlignItems SafeCenter = new AlignItems(AlignItemsKeyword.Center, AlignmentSafety.Safe);
    public static readonly AlignItems UnsafeStart = new(AlignItemsKeyword.Start, AlignmentSafety.Unsafe);
    public static readonly AlignItems UnsafeEnd = new(AlignItemsKeyword.End, AlignmentSafety.Unsafe);
    public static readonly AlignItems UnsafeFlexStart = new(AlignItemsKeyword.FlexStart, AlignmentSafety.Unsafe);
    public static readonly AlignItems UnsafeFlexEnd = new(AlignItemsKeyword.FlexEnd, AlignmentSafety.Unsafe);
    public static readonly AlignItems UnsafeSelfStart = new(AlignItemsKeyword.SelfStart, AlignmentSafety.Unsafe);
    public static readonly AlignItems UnsafeSelfEnd = new(AlignItemsKeyword.SelfEnd, AlignmentSafety.Unsafe);
    public static readonly AlignItems UnsafeCenter = new(AlignItemsKeyword.Center, AlignmentSafety.Unsafe);

    public AlignItemsKeyword Keyword { get; }
    public AlignmentSafety Safety { get; }
    public bool IsSafe => Safety == AlignmentSafety.Safe;

    public AlignItems(AlignItemsKeyword keyword, AlignmentSafety safety)
    {
        Keyword = keyword;
        Safety = safety;
    }

    public bool Equals(AlignItems other) => Keyword == other.Keyword && Safety == other.Safety;
    public override bool Equals(object? obj) => obj is AlignItems other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Keyword, Safety);
    public static bool operator ==(AlignItems left, AlignItems right) => left.Equals(right);
    public static bool operator !=(AlignItems left, AlignItems right) => !left.Equals(right);
}

// In Rust: AlignSelf, JustifyItems, and JustifySelf are aliases of AlignItems.

/// <summary>
/// Sets the distribution of space between and around content items. For Flexbox it controls
/// alignment in the cross axis. For Grid it controls alignment in the block axis.
/// </summary>
public readonly struct AlignContent : IEquatable<AlignContent>
{
    public static readonly AlignContent Normal = new(AlignContentKeyword.Normal, AlignmentSafety.Default);
    public AlignContent WithSafety(AlignmentSafety safety) => new(Keyword, safety);
    public static readonly AlignContent Start = new AlignContent(AlignContentKeyword.Start, AlignmentSafety.Default);
    public static readonly AlignContent End = new AlignContent(AlignContentKeyword.End, AlignmentSafety.Default);
    public static readonly AlignContent FlexStart = new AlignContent(AlignContentKeyword.FlexStart, AlignmentSafety.Default);
    public static readonly AlignContent FlexEnd = new AlignContent(AlignContentKeyword.FlexEnd, AlignmentSafety.Default);
    public static readonly AlignContent Center = new AlignContent(AlignContentKeyword.Center, AlignmentSafety.Default);
    public static readonly AlignContent Stretch = new AlignContent(AlignContentKeyword.Stretch, AlignmentSafety.Default);
    public static readonly AlignContent SpaceBetween = new AlignContent(AlignContentKeyword.SpaceBetween, AlignmentSafety.Default);
    public static readonly AlignContent SpaceEvenly = new AlignContent(AlignContentKeyword.SpaceEvenly, AlignmentSafety.Default);
    public static readonly AlignContent SpaceAround = new AlignContent(AlignContentKeyword.SpaceAround, AlignmentSafety.Default);
    public static readonly AlignContent SafeStart = new AlignContent(AlignContentKeyword.Start, AlignmentSafety.Safe);
    public static readonly AlignContent SafeEnd = new AlignContent(AlignContentKeyword.End, AlignmentSafety.Safe);
    public static readonly AlignContent SafeFlexStart = new AlignContent(AlignContentKeyword.FlexStart, AlignmentSafety.Safe);
    public static readonly AlignContent SafeFlexEnd = new AlignContent(AlignContentKeyword.FlexEnd, AlignmentSafety.Safe);
    public static readonly AlignContent SafeCenter = new AlignContent(AlignContentKeyword.Center, AlignmentSafety.Safe);
    public static readonly AlignContent UnsafeStart = new(AlignContentKeyword.Start, AlignmentSafety.Unsafe);
    public static readonly AlignContent UnsafeEnd = new(AlignContentKeyword.End, AlignmentSafety.Unsafe);
    public static readonly AlignContent UnsafeFlexStart = new(AlignContentKeyword.FlexStart, AlignmentSafety.Unsafe);
    public static readonly AlignContent UnsafeFlexEnd = new(AlignContentKeyword.FlexEnd, AlignmentSafety.Unsafe);
    public static readonly AlignContent UnsafeCenter = new(AlignContentKeyword.Center, AlignmentSafety.Unsafe);

    public AlignContentKeyword Keyword { get; }
    public AlignmentSafety Safety { get; }
    public bool IsSafe => Safety == AlignmentSafety.Safe;

    public AlignContent(AlignContentKeyword keyword, AlignmentSafety safety)
    {
        Keyword = keyword;
        Safety = safety;
    }

    public bool Equals(AlignContent other) => Keyword == other.Keyword && Safety == other.Safety;
    public override bool Equals(object? obj) => obj is AlignContent other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Keyword, Safety);
    public static bool operator ==(AlignContent left, AlignContent right) => left.Equals(right);
    public static bool operator !=(AlignContent left, AlignContent right) => !left.Equals(right);
}

// In Rust: JustifyContent is an alias of AlignContent.
