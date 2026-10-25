using System;
using System.Linq;
using System.Numerics;
using Xunit;

namespace Marius.Winter.Tests;

public class ModelessLayoutTests
{
    [Fact]
    public void ModelessOverflowRemainsInteractiveWithoutExpandingItsParent()
    {
        using var viewport = new Panel { Bounds = new RectF(0, 0, 500, 400) };
        var parent = new Panel { Bounds = new RectF(20, 30, 420, 60) };
        viewport.AddChild(parent);
        var dialog = new DialogWindow { Bounds = new RectF(60, 100, 280, 170) };
        parent.AddChild(dialog);
        Assert.Same(dialog, viewport.HitTest(20 + 60 + 20, 30 + 100 + 15));
        Assert.Same(dialog, viewport.HitTest(20 + 60 + 278, 30 + 100 + 168));
        Assert.Same(parent, dialog.Parent);
        Assert.Equal(60, parent.Bounds.H);
        // Ordinary overflow retains its existing parent clipping policy.
        var normal = new Panel { Bounds = new RectF(0, 90, 40, 30) };
        parent.AddChild(normal);
        Assert.NotSame(normal, viewport.HitTest(25, 125));
        dialog.Visible = false;
        Assert.NotSame(dialog, viewport.HitTest(100, 145));
        dialog.Visible = true;
        Assert.Same(dialog, viewport.HitTest(100, 145));
    }

    [Theory]
    [InlineData("flex", false)]
    [InlineData("flex", true)]
    [InlineData("grid", false)]
    [InlineData("grid", true)]
    [InlineData("stack", false)]
    [InlineData("stack", true)]
    public void OpeningHidingRemovingAndReparentingNeverMovesNormalSiblings(string kind, bool taffy)
    {
        using var parent = Container(kind);
        using var other = Container(kind);
        var first = new Box(80, 20); var second = new Box(110, 32); var third = new Box(60, 24);
        parent.AddChild(first); parent.AddChild(second); parent.AddChild(third);
        var engine = new TaffyLayoutEngine();
        void Layout(Panel panel)
        {
            panel.Bounds = new RectF(0, 0, 420, 360);
            panel.Measure(420, 360);
            if (taffy) engine.ComputeLayout(panel, 420, 360);
            else panel.Arrange(new RectF(0, 0, 420, 360));
        }
        Layout(parent);
        var original = new[] { first.Bounds, second.Bounds, third.Bounds };
        var bounds = new RectF(60, 100, 280, 170);
        using var dialog = new DialogWindow("Modeless") { Bounds = bounds, Layout = new StackLayout { Spacing = 8 } };
        var content = new Box(70, 18); dialog.AddChild(content);
        parent.InsertChild(dialog, 1);
        Layout(parent);
        Assert.Equal(original, new[] { first.Bounds, second.Bounds, third.Bounds });
        Assert.Equal(bounds, dialog.Bounds);
        Assert.True(content.Bounds.W > 0 && content.Bounds.H > 0, "Out-of-flow content must still be arranged.");
        Assert.Same(parent, dialog.Parent);
        Assert.Equal(parent.Children.Select(c => c.Scene), parent.Scene.Paints().Skip(1));

        // A normal reflow still arranges new content inside the manual window.
        content.Height = 31; content.InvalidateMeasure(); Layout(parent);
        Assert.Equal(31, content.Bounds.H);
        dialog.Bounds = bounds = new RectF(90, 120, 310, 190);
        Layout(parent);
        Assert.Equal(bounds, dialog.Bounds);
        Assert.Equal(original, new[] { first.Bounds, second.Bounds, third.Bounds });
        for (int cycle = 0; cycle < 2; cycle++)
        {
            dialog.Visible = false; Layout(parent);
            Assert.Equal(original, new[] { first.Bounds, second.Bounds, third.Bounds });
            dialog.Visible = true; Layout(parent);
            Assert.Equal(bounds, dialog.Bounds);
            Assert.Equal(original, new[] { first.Bounds, second.Bounds, third.Bounds });
        }
        parent.RemoveChild(dialog); Layout(parent);
        Assert.Equal(original, new[] { first.Bounds, second.Bounds, third.Bounds });
        other.AddChild(dialog); Layout(other);
        Assert.Same(other, dialog.Parent); Assert.Equal(bounds, dialog.Bounds);
        other.RemoveChild(dialog); parent.AddChild(dialog); Layout(parent);
        Assert.Equal(original, new[] { first.Bounds, second.Bounds, third.Bounds });
        Assert.Same(dialog.Scene, parent.Scene.Paints().Last());
        Assert.Same(dialog, parent.HitTest(bounds.X + 20, bounds.Y + 15));
    }

    [Theory]
    [InlineData("flex")]
    [InlineData("grid")]
    [InlineData("stack")]
    public void ModelessBoundsDoNotContributeToParentOrAncestorIntrinsics(string kind)
    {
        using var ancestor = Container("flex");
        var parent = Container(kind);
        ancestor.AddChild(parent); ancestor.AddChild(new Box(45, 15));
        parent.AddChild(new Box(80, 20)); parent.AddChild(new Box(110, 32));
        var original = ancestor.Measure(600, 600);
        var parentSize = parent.DesiredSize;
        using var dialog = new DialogWindow { Bounds = new RectF(60, 100, 280, 170) };
        parent.InsertChild(dialog, 1);
        Assert.Equal(original, ancestor.Measure(600, 600));
        Assert.Equal(parentSize, parent.DesiredSize);
        dialog.Bounds = new RectF(120, 160, 500, 350);
        Assert.Equal(original, ancestor.Measure(600, 600));
        parent.RemoveChild(dialog);
        Assert.Equal(original, ancestor.Measure(600, 600));
    }

    internal static Panel Container(string kind) => new()
    {
        Padding = new Thickness(7),
        Layout = kind switch
        {
            "flex" => new FlexLayout { Direction = Orientation.Vertical, Gap = 11, JustifyContent = JustifyContent.Center },
            "grid" => new GridLayout { Columns = [TrackSize.Auto(), TrackSize.Auto()], ColumnGap = 13, RowGap = 11 },
            _ => new StackLayout { Spacing = 11, CrossAlignment = Alignment.Start }
        }
    };

    private sealed class Box(float width, float height) : Element
    {
        public float Height = height;
        protected override Vector2 MeasureCore(float availableWidth, float availableHeight) => new(width, Height);
    }
}
