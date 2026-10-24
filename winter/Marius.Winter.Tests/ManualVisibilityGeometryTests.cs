using System.Numerics;
using Xunit;
using Xunit.Abstractions;

namespace Marius.Winter.Tests;

public class ManualVisibilityGeometryTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ManualRootOrChildSurvivesHideAndShowWithoutLosingItsRectangle(bool child, bool boundsSetter)
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(240, 180, "Winter W2-05 manual visibility");
            Element parent = window;
            if (child)
            {
                var manual = new Panel(); window.AddChild(manual);
                manual.Arrange(new RectF(5, 7, 220, 160)); parent = manual;
            }
            var button = new Button("Apply"); parent.AddChild(button);
            var original = new RectF(20, 25, 120, 40);
            if (boundsSetter) button.Bounds = original;
            else button.Arrange(original);
            WindowRegressionOwner.Flush(window);
            Assert.Equal(original, button.Bounds);
            for (int i = 0; i < 2; i++)
            {
                button.Visible = false; WindowRegressionOwner.Flush(window);
                Assert.Equal(Vector2.Zero, button.DesiredSize);
                Assert.Equal(0, button.Bounds.W); Assert.Equal(0, button.Bounds.H);
                Assert.Null(button.HitTest(30, 35));
                button.Visible = true; WindowRegressionOwner.Flush(window);
                output.WriteLine($"child={child} boundsSetter={boundsSetter} after hide/show: {button.Bounds.X},{button.Bounds.Y},{button.Bounds.W},{button.Bounds.H}");
                Assert.Equal(original.W, button.Bounds.W);
                Assert.Equal(original.H, button.Bounds.H);
                Assert.Equal(original, button.Bounds);
                Assert.Same(button, window.HitTest(child ? 35 : 30, child ? 42 : 35));
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitGeometryAssignedWhileHiddenBecomesTheRestoredRectangle(bool boundsSetter)
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(240, 180, "Winter hidden geometry update");
            var button = new Button("Apply"); window.AddChild(button);
            button.Arrange(new RectF(20, 25, 120, 40)); WindowRegressionOwner.Flush(window);
            button.Visible = false; WindowRegressionOwner.Flush(window);
            var updated = new RectF(50, 60, 130, 45);
            if (boundsSetter) button.Bounds = updated;
            else button.Arrange(updated);
            button.InvalidateMeasure(); WindowRegressionOwner.Flush(window);
            Assert.Equal(0, button.Bounds.W); Assert.Equal(0, button.Bounds.H);
            button.Visible = true; WindowRegressionOwner.Flush(window);
            Assert.Equal(updated, button.Bounds);
            Assert.Same(button, window.HitTest(55, 65));
        });
    }

    [Fact]
    public void HidingAManualContainerPreservesItsAndItsChildrensGeometry()
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(240, 180, "Winter hidden manual container");
            var parent = new Panel(); window.AddChild(parent);
            var button = new Button("Apply"); parent.AddChild(button);
            var parentBounds = new RectF(5, 7, 220, 160);
            var childBounds = new RectF(20, 25, 120, 40);
            parent.Arrange(parentBounds); button.Arrange(childBounds); WindowRegressionOwner.Flush(window);
            parent.Visible = false; WindowRegressionOwner.Flush(window);
            Assert.Equal(0, parent.Bounds.W); Assert.False(button.IsActive);
            Assert.NotSame(button, window.HitTest(35, 42));
            parent.Visible = true; WindowRegressionOwner.Flush(window);
            Assert.Equal(parentBounds, parent.Bounds); Assert.Equal(childBounds, button.Bounds);
            Assert.Same(button, window.HitTest(35, 42));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LayoutManagedChildrenStillCollapseAndReflow(bool automaticRoot)
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(240, 180, "Winter layout visibility baseline");
            var panel = new Panel { Layout = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 4 }, Padding = new Thickness(0) };
            var first = new Box(); var second = new Box(); panel.AddChild(first); panel.AddChild(second); window.AddChild(panel);
            if (automaticRoot) window.SetAutoLayout(panel);
            else panel.Arrange(new RectF(12, 9, 180, 100));
            WindowRegressionOwner.Flush(window);
            var original = panel.Bounds;
            Assert.Equal(24, second.Bounds.X);
            first.Visible = false; WindowRegressionOwner.Flush(window);
            Assert.Equal(0, first.Bounds.W); Assert.Equal(0, first.Bounds.H); Assert.Equal(0, second.Bounds.X);
            first.Visible = true; WindowRegressionOwner.Flush(window);
            Assert.Equal(20, first.Bounds.W); Assert.Equal(24, second.Bounds.X);
            panel.Visible = false; WindowRegressionOwner.Flush(window);
            Assert.Equal(0, panel.Bounds.W); Assert.Equal(0, panel.Bounds.H);
            panel.Visible = true; WindowRegressionOwner.Flush(window);
            Assert.Equal(original, panel.Bounds); Assert.Equal(24, second.Bounds.X);
        });
    }

    [Fact]
    public void SelectingTabsRestoresTheActiveContentAndCollapsesTheOther()
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(240, 180, "Winter tab visibility baseline");
            var tabs = new TabWidget(); var first = new Panel(); var second = new Panel();
            tabs.AddTab("one", first); tabs.AddTab("two", second); window.AddChild(tabs);
            tabs.Arrange(new RectF(5, 7, 220, 160)); WindowRegressionOwner.Flush(window);
            var expected = first.Bounds;
            Assert.True(expected.W > 0); Assert.Equal(0, second.Bounds.W);
            tabs.ActiveTab = 1; WindowRegressionOwner.Flush(window);
            Assert.Equal(0, first.Bounds.W); Assert.Equal(expected, second.Bounds);
            tabs.ActiveTab = 0; WindowRegressionOwner.Flush(window);
            Assert.Equal(expected, first.Bounds); Assert.Equal(0, second.Bounds.W);
        });
    }
}
