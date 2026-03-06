using System;
using System.Collections.Generic;
using System.Numerics;

namespace Marius.Winter;

public enum JustifyContent { Start, Center, End, SpaceBetween, SpaceAround, SpaceEvenly }
public enum FlexWrap { NoWrap, Wrap }

/// <summary>
/// Per-child layout hints for FlexLayout. Set on Element.LayoutData.
/// </summary>
public class FlexItem
{
    /// <summary>How much this child grows to fill remaining space (0 = don't grow).</summary>
    public float Grow { get; set; }

    /// <summary>How much this child shrinks when space is insufficient (1 = normal shrink).</summary>
    public float Shrink { get; set; } = 1f;

    /// <summary>Base size before grow/shrink. NaN = use child's desired size.</summary>
    public float Basis { get; set; } = float.NaN;

    /// <summary>Override the container's AlignItems for this child. null = inherit.</summary>
    public Alignment? AlignSelf { get; set; }
}

/// <summary>
/// CSS Flexbox-style layout. Supports direction, wrapping, justify-content,
/// align-items, gap, and per-child grow/shrink/basis via FlexItem.
/// </summary>
public class FlexLayout : ILayout
{
    public Orientation Direction { get; set; } = Orientation.Horizontal;
    public FlexWrap Wrap { get; set; } = FlexWrap.NoWrap;
    public JustifyContent JustifyContent { get; set; } = JustifyContent.Start;
    public Alignment AlignItems { get; set; } = Alignment.Stretch;
    public float Gap { get; set; } = 4f;

    private struct FlexChild
    {
        public Element Element;
        public Vector2 DesiredSize;
        public float MainSize;  // resolved basis or desired main size
        public float CrossSize; // desired cross size
        public float Grow;
        public float Shrink;
        public Alignment Align;
    }

    private struct FlexLine
    {
        public int Start, Count;
        public float TotalMain;  // sum of child main sizes
        public float MaxCross;   // tallest child in line
        public float TotalGrow;
        public float TotalShrink;
    }

    public Vector2 Measure(Element container, float availableWidth, float availableHeight)
    {
        var children = container.Children;
        var padding = container.Style.Padding;
        float innerW = availableWidth - padding.HorizontalTotal;
        float innerH = availableHeight - padding.VerticalTotal;
        bool isRow = Direction == Orientation.Horizontal;

        float mainAvail = isRow ? innerW : innerH;
        float crossAvail = isRow ? innerH : innerW;

        // Measure all visible children
        var items = new List<FlexChild>();
        for (int i = 0; i < children.Count; i++)
        {
            if (!children[i].Visible) continue;
            var child = children[i];
            var hint = child.LayoutData as FlexItem;
            var sz = child.Measure(innerW, innerH);

            float basis = (hint != null && !float.IsNaN(hint.Basis)) ? hint.Basis : (isRow ? sz.X : sz.Y);

            items.Add(new FlexChild
            {
                Element = child,
                DesiredSize = sz,
                MainSize = basis,
                CrossSize = isRow ? sz.Y : sz.X,
                Grow = hint?.Grow ?? 0f,
                Shrink = hint?.Shrink ?? 1f,
                Align = hint?.AlignSelf ?? AlignItems,
            });
        }

        // Build lines (wrapping)
        var lines = BuildLines(items, mainAvail);

        // Compute total size
        float totalCross = 0;
        float maxMain = 0;
        for (int li = 0; li < lines.Count; li++)
        {
            var line = lines[li];
            if (line.TotalMain > maxMain) maxMain = line.TotalMain;
            totalCross += line.MaxCross;
            if (li > 0) totalCross += Gap;
        }

        if (isRow)
            return new Vector2(maxMain + padding.HorizontalTotal, totalCross + padding.VerticalTotal);
        else
            return new Vector2(totalCross + padding.HorizontalTotal, maxMain + padding.VerticalTotal);
    }

    public void Arrange(Element container, RectF bounds)
    {
        var children = container.Children;
        var padding = container.Style.Padding;
        float startX = bounds.X + padding.Left;
        float startY = bounds.Y + padding.Top;
        float innerW = bounds.W - padding.HorizontalTotal;
        float innerH = bounds.H - padding.VerticalTotal;
        bool isRow = Direction == Orientation.Horizontal;

        float mainAvail = isRow ? innerW : innerH;
        float crossAvail = isRow ? innerH : innerW;

        // Measure all visible children
        var items = new List<FlexChild>();
        for (int i = 0; i < children.Count; i++)
        {
            if (!children[i].Visible) continue;
            var child = children[i];
            var hint = child.LayoutData as FlexItem;
            var sz = child.DesiredSize;

            float basis = (hint != null && !float.IsNaN(hint.Basis)) ? hint.Basis : (isRow ? sz.X : sz.Y);

            items.Add(new FlexChild
            {
                Element = child,
                DesiredSize = sz,
                MainSize = basis,
                CrossSize = isRow ? sz.Y : sz.X,
                Grow = hint?.Grow ?? 0f,
                Shrink = hint?.Shrink ?? 1f,
                Align = hint?.AlignSelf ?? AlignItems,
            });
        }

        var lines = BuildLines(items, mainAvail);

        // Resolve flex grow/shrink per line
        for (int li = 0; li < lines.Count; li++)
        {
            var line = lines[li];
            // TotalMain from BuildLines already includes gaps between children
            float remaining = mainAvail - line.TotalMain;

            if (remaining > 0 && line.TotalGrow > 0)
            {
                // Distribute extra space by grow factor
                for (int ci = line.Start; ci < line.Start + line.Count; ci++)
                {
                    var item = items[ci];
                    item.MainSize += remaining * (item.Grow / line.TotalGrow);
                    items[ci] = item;
                }
            }
            else if (remaining < 0 && line.TotalShrink > 0)
            {
                // Shrink children proportionally
                float deficit = -remaining;
                for (int ci = line.Start; ci < line.Start + line.Count; ci++)
                {
                    var item = items[ci];
                    float shrinkAmount = deficit * (item.Shrink / line.TotalShrink);
                    item.MainSize = MathF.Max(0, item.MainSize - shrinkAmount);
                    items[ci] = item;
                }
            }
        }

        // Position children
        float crossOffset = 0;
        for (int li = 0; li < lines.Count; li++)
        {
            var line = lines[li];
            float gapTotal = (line.Count > 1) ? Gap * (line.Count - 1) : 0;

            // Recompute total main after flex
            float totalMain = 0;
            for (int ci = line.Start; ci < line.Start + line.Count; ci++)
                totalMain += items[ci].MainSize;

            float freeSpace = mainAvail - totalMain - gapTotal;
            float mainOffset = ComputeJustifyOffset(freeSpace, line.Count, out float itemGap);
            float effectiveGap = Gap + itemGap;

            for (int ci = line.Start; ci < line.Start + line.Count; ci++)
            {
                var item = items[ci];

                // Cross-axis alignment — Stretch fills the full container cross size,
                // not just the line's tallest/widest child.
                float lineCross = lines.Count == 1 ? crossAvail : line.MaxCross;
                float crossSize = item.Align == Alignment.Stretch ? lineCross : item.CrossSize;
                float crossPos = crossOffset;
                if (item.Align == Alignment.Center)
                    crossPos += (lineCross - crossSize) / 2f;
                else if (item.Align == Alignment.End)
                    crossPos += lineCross - crossSize;

                if (isRow)
                {
                    item.Element.Arrange(new RectF(
                        startX + mainOffset, startY + crossPos,
                        item.MainSize, crossSize));
                }
                else
                {
                    item.Element.Arrange(new RectF(
                        startX + crossPos, startY + mainOffset,
                        crossSize, item.MainSize));
                }

                mainOffset += item.MainSize + effectiveGap;
            }

            crossOffset += line.MaxCross + Gap;
        }
    }

    private List<FlexLine> BuildLines(List<FlexChild> items, float mainAvail)
    {
        var lines = new List<FlexLine>();
        if (items.Count == 0) return lines;

        int lineStart = 0;
        float lineMain = 0;
        float lineCross = 0;
        float lineGrow = 0;
        float lineShrink = 0;
        int lineCount = 0;

        for (int i = 0; i < items.Count; i++)
        {
            float childMain = items[i].MainSize;
            float childCross = items[i].CrossSize;

            bool wouldOverflow = lineCount > 0 &&
                                 (lineMain + Gap + childMain > mainAvail);

            if (Wrap == FlexWrap.Wrap && wouldOverflow)
            {
                // Finish current line
                lines.Add(new FlexLine
                {
                    Start = lineStart,
                    Count = lineCount,
                    TotalMain = lineMain,
                    MaxCross = lineCross,
                    TotalGrow = lineGrow,
                    TotalShrink = lineShrink,
                });
                lineStart = i;
                lineMain = 0;
                lineCross = 0;
                lineGrow = 0;
                lineShrink = 0;
                lineCount = 0;
            }

            if (lineCount > 0) lineMain += Gap;
            lineMain += childMain;
            if (childCross > lineCross) lineCross = childCross;
            lineGrow += items[i].Grow;
            lineShrink += items[i].Shrink;
            lineCount++;
        }

        // Final line
        if (lineCount > 0)
        {
            lines.Add(new FlexLine
            {
                Start = lineStart,
                Count = lineCount,
                TotalMain = lineMain,
                MaxCross = lineCross,
                TotalGrow = lineGrow,
                TotalShrink = lineShrink,
            });
        }

        return lines;
    }

    private float ComputeJustifyOffset(float freeSpace, int itemCount, out float itemGap)
    {
        itemGap = 0;
        if (freeSpace <= 0 || itemCount == 0)
            return 0;

        switch (JustifyContent)
        {
            case JustifyContent.Start:
                return 0;
            case JustifyContent.End:
                return freeSpace;
            case JustifyContent.Center:
                return freeSpace / 2f;
            case JustifyContent.SpaceBetween:
                if (itemCount > 1) itemGap = freeSpace / (itemCount - 1);
                return 0;
            case JustifyContent.SpaceAround:
                if (itemCount > 0) itemGap = freeSpace / itemCount;
                return itemGap / 2f;
            case JustifyContent.SpaceEvenly:
                if (itemCount > 0) itemGap = freeSpace / (itemCount + 1);
                return itemGap;
            default:
                return 0;
        }
    }
}
