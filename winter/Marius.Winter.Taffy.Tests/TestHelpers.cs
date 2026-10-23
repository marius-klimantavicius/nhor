using System;
using Marius.Winter.Taffy;

namespace Marius.Winter.Taffy.Tests;

/// <summary>Whether text is horizontal or vertical</summary>
public enum WritingMode
{
    Horizontal,
    Vertical,
}

/// <summary>A shared node context for tests</summary>
public class TestNodeContext
{
    /// <summary>How many times the measure function has been called</summary>
    public int Count;

    /// <summary>The kind of measure data</summary>
    public TestMeasureKind Kind;

    // Data fields (interpretation depends on Kind)
    public float FixedWidth;
    public float FixedHeight;
    public float AspectWidth;
    public float AspectHeightRatio;
    public string? TextContent;
    public WritingMode TextWritingMode;

    private TestNodeContext() { }

    /// <summary>Create a zero-sized node context</summary>
    public static TestNodeContext Zero() => new() { Kind = TestMeasureKind.Zero };

    /// <summary>Create a fixed-sized node context</summary>
    public static TestNodeContext Fixed(float width, float height) => new()
    {
        Kind = TestMeasureKind.Fixed,
        FixedWidth = width,
        FixedHeight = height,
    };

    /// <summary>Create a node context with aspect ratio</summary>
    public static TestNodeContext AspectRatio(float width, float heightRatio) => new()
    {
        Kind = TestMeasureKind.AspectRatio,
        AspectWidth = width,
        AspectHeightRatio = heightRatio,
    };

    /// <summary>Create a node context with Ahem text</summary>
    public static TestNodeContext AhemText(string text, WritingMode writingMode) => new()
    {
        Kind = TestMeasureKind.AhemText,
        TextContent = text,
        TextWritingMode = writingMode,
    };
}

public enum TestMeasureKind
{
    Zero,
    Fixed,
    AspectRatio,
    AhemText,
}

public static class TestHelpers
{
    public static readonly Size<AvailableSpace> MaxContentSize =
        new(AvailableSpace.MaxContent, AvailableSpace.MaxContent);

    public static TaffyTree<TestNodeContext> NewTestTree() => new();

    public static Size<float> MeasureFunction(
        Size<float?> knownDimensions,
        Size<AvailableSpace> availableSpace,
        NodeId nodeId,
        TestNodeContext? context,
        Style style)
    {
        if (knownDimensions.Width.HasValue && knownDimensions.Height.HasValue)
            return new Size<float>(knownDimensions.Width.Value, knownDimensions.Height.Value);

        if (context == null)
            return new Size<float>(knownDimensions.Width ?? 0f, knownDimensions.Height ?? 0f);

        context.Count++;

        Size<float> computeSize;
        switch (context.Kind)
        {
            case TestMeasureKind.Zero:
                computeSize = new Size<float>(0f, 0f);
                break;

            case TestMeasureKind.Fixed:
                computeSize = new Size<float>(context.FixedWidth, context.FixedHeight);
                break;

            case TestMeasureKind.AspectRatio:
            {
                var width = knownDimensions.Width ?? context.AspectWidth;
                var height = knownDimensions.Height ?? (width * context.AspectHeightRatio);
                computeSize = new Size<float>(width, height);
                break;
            }

            case TestMeasureKind.AhemText:
                computeSize = MeasureAhemText(context.TextContent!, context.TextWritingMode, knownDimensions, availableSpace);
                break;

            default:
                computeSize = new Size<float>(0f, 0f);
                break;
        }

        return new Size<float>(
            knownDimensions.Width ?? computeSize.Width,
            knownDimensions.Height ?? computeSize.Height);
    }

    private static Size<float> MeasureAhemText(
        string text,
        WritingMode writingMode,
        Size<float?> knownDimensions,
        Size<AvailableSpace> availableSpace)
    {
        const char ZWS = '\u200B';
        const float H_WIDTH = 10.0f;
        const float H_HEIGHT = 10.0f;

        var inlineAxis = writingMode == WritingMode.Horizontal ? AbsoluteAxis.Horizontal : AbsoluteAxis.Vertical;
        var blockAxis = inlineAxis == AbsoluteAxis.Horizontal ? AbsoluteAxis.Vertical : AbsoluteAxis.Horizontal;

        var lines = text.Split(ZWS);
        if (lines.Length == 0)
            return new Size<float>(0f, 0f);

        nuint minLineLength = 0;
        nuint maxLineLength = 0;
        foreach (var line in lines)
        {
            nuint length = CountUtf8Bytes(line);
            if (length > minLineLength) minLineLength = length;
            maxLineLength += length;
        }

        float? knownInline = GetAbs(knownDimensions, inlineAxis);
        float? knownBlock = GetAbs(knownDimensions, blockAxis);
        var availInline = GetAbsAvail(availableSpace, inlineAxis);

        float inlineSize;
        if (knownInline.HasValue)
        {
            inlineSize = knownInline.Value;
        }
        else
        {
            float computed;
            if (availInline == AvailableSpace.MinContent)
                computed = (float)minLineLength * H_WIDTH;
            else if (availInline == AvailableSpace.MaxContent)
                computed = (float)maxLineLength * H_WIDTH;
            else
                computed = TaffySys.F32Min(availInline.Unwrap(), (float)maxLineLength * H_WIDTH);

            inlineSize = computed;
        }
        inlineSize = TaffySys.F32Max(inlineSize, (float)minLineLength * H_WIDTH);

        float blockSize;
        if (knownBlock.HasValue)
        {
            blockSize = knownBlock.Value;
        }
        else
        {
            // Rust's float-to-usize cast saturates (including infinity and NaN).
            nuint inlineLineLength = nuint.CreateSaturating(MathF.Floor(inlineSize / H_WIDTH));
            nuint lineCount = 1;
            nuint currentLineLength = 0;
            foreach (var line in lines)
            {
                nuint length = CountUtf8Bytes(line);
                if (currentLineLength + length > inlineLineLength)
                {
                    if (currentLineLength > 0) lineCount++;
                    currentLineLength = length;
                }
                else
                {
                    currentLineLength += length;
                }
            }
            blockSize = (float)lineCount * H_HEIGHT;
        }

        return writingMode == WritingMode.Horizontal
            ? new Size<float>(inlineSize, blockSize)
            : new Size<float>(blockSize, inlineSize);
    }

    // Bound each Encoding call's int result, then accumulate native usize counts.
    // A pair straddling a chunk must be encoded together, including normal UTF-8
    // replacement behavior for genuinely unpaired UTF-16 surrogates.
    internal const int Utf8CountChunkLength = 4096;
    internal static nuint CountUtf8Bytes(ReadOnlySpan<char> text)
    {
        nuint count = 0;
        while (!text.IsEmpty)
        {
            int length = Math.Min(text.Length, Utf8CountChunkLength);
            if (length < text.Length && char.IsHighSurrogate(text[length - 1]) && char.IsLowSurrogate(text[length]))
                length--;
            count += (nuint)System.Text.Encoding.UTF8.GetByteCount(text[..length]);
            text = text[length..];
        }
        return count;
    }

    private static float? GetAbs(Size<float?> size, AbsoluteAxis axis) =>
        axis == AbsoluteAxis.Horizontal ? size.Width : size.Height;

    private static AvailableSpace GetAbsAvail(Size<AvailableSpace> size, AbsoluteAxis axis) =>
        axis == AbsoluteAxis.Horizontal ? size.Width : size.Height;
}
