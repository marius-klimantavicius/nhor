using System;
using Xunit;

namespace Marius.Winter.Tests;

public class SplitPaneTests
{
    [Fact]
    public void LogicalExtentClampsWhileShrinkingAndRestoresOnExpansion()
    {
        using var split = new SplitPane { Position = 200, FirstMinimum = 100, SecondMinimum = 120 };
        split.First = new Panel();
        split.Second = new Panel();
        split.Arrange(new RectF(0, 0, 500, 240));
        Assert.Equal(200, split.First.Bounds.W);
        Assert.Equal(500 - 200 - split.DividerSize, split.Second.Bounds.W);
        split.Arrange(new RectF(0, 0, 180 + split.DividerSize, 240));
        Assert.Equal(180 * 100f / 220, split.First.Bounds.W, 3);
        Assert.Equal(180 * 120f / 220, split.Second.Bounds.W, 3);
        Assert.Equal(200, split.Position);
        split.Arrange(new RectF(0, 0, 500, 240));
        Assert.Equal(200, split.VisiblePosition);
        split.Orientation = Orientation.Vertical;
        Assert.Equal(120, split.Second.Bounds.H);
        Assert.Equal(240 - 120 - split.DividerSize, split.VisiblePosition);
        split.Arrange(new RectF(0, 0, 0, 0));
        Assert.All(split.Children, c => Assert.True(c.Bounds.W >= 0 && c.Bounds.H >= 0));
    }

    [Fact]
    public void OnlyDividerHandlesInputAndProgrammaticResetDoesNotEcho()
    {
        using var split = new SplitPane { Position = 180, FirstMinimum = 80, SecondMinimum = 90, ResetPosition = 160 };
        split.First = new Panel();
        split.Second = new Panel();
        split.Arrange(new RectF(20, 30, 500, 250));
        int changes = 0;
        split.PositionChanged = _ => changes++;
        Assert.False(split.OnMouseDown(0, 40, 60));
        var divider = Assert.Single(split.Children, c => c.Focusable);
        Assert.True(divider.OnMouseDown(0, 202, 80));
        divider.OnMouseMove(242, 80);
        divider.OnMouseUp(0, 242, 80);
        Assert.Equal(220, split.Position);
        divider.OnKeyDown(262, 0, false);
        Assert.Equal(230, split.Position);
        divider.OnKeyDown(269, 0, false); // End
        Assert.Equal(500 - split.DividerSize - 90, split.Position);
        divider.OnKeyDown(268, 0, false); // Home
        Assert.Equal(80, split.Position);
        divider.OnKeyDown(13, 0, false); // Irrelevant key
        split.Reset();
        Assert.Equal(160, split.Position);
        Assert.Equal(5, changes);
        split.Position = 200;
        Assert.Equal(5, changes);
        divider.OnMouseDown(0, 222, 80);
        divider.OnDragEnd();
        divider.OnMouseMove(322, 80);
        Assert.Equal(200, split.Position);
    }

    [Fact]
    public void ReplacingPaneDetachesWithoutDisposingCallerOwnedContent()
    {
        using var split = new SplitPane();
        using var first = new Panel();
        using var replacement = new Panel();
        split.First = first;
        split.Arrange(new RectF(0, 0, 400, 200));
        split.First = replacement;
        Assert.Null(first.Parent);
        Assert.False(first.IsDisposed);
        Assert.Equal(split.VisiblePosition, replacement.Bounds.W);
        Assert.Throws<InvalidOperationException>(() => split.Second = replacement);
        Assert.Null(split.Second);
        split.First = null;
        Assert.Null(replacement.Parent);
        Assert.False(replacement.IsDisposed);
    }
}
