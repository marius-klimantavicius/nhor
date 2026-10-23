using System.Numerics;
using Xunit;

namespace Marius.Winter.Tests;

public class LayoutInvalidationTests
{
    [Theory]
    [InlineData("MinWidth", 80f, 80f, 10f)]
    [InlineData("MinHeight", 40f, 20f, 40f)]
    [InlineData("MaxWidth", 8f, 8f, 10f)]
    [InlineData("MaxHeight", 4f, 20f, 4f)]
    public void ConstraintChangeInvalidatesCachedMeasurement(string name, float value, float w, float h)
    {
        var root = new Panel(); var box = new Box(); root.AddChild(box);
        box.Measure(100, 100); root.Measure(100, 100);
        typeof(Element).GetProperty(name)!.SetValue(box, (float?)value);
        Assert.True(box.IsMeasureDirty); Assert.True(root.IsMeasureDirty);
        Assert.Equal(new Vector2(w, h), box.Measure(100, 100));
    }

    private class StyledBox : Element
    {
        protected override Vector2 MeasureCore(float w, float h) => new(Style.FontSize + Style.Padding.HorizontalTotal, Style.FontSize);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StyleReplacementAndMutationInvalidateMeasurement(bool mutate)
    {
        var box = new StyledBox { Style = new Style { FontSize = 10, Padding = new Thickness(0) } };
        box.Measure(100, 100);
        if (mutate) box.Style.FontSize = 50;
        else box.Style = new Style { FontSize = 50, Padding = new Thickness(0) };
        Assert.True(box.IsMeasureDirty); Assert.Equal(new Vector2(50, 50), box.Measure(100, 100));
    }

    [Fact]
    public void ClonedStyleDoesNotNotifyTheOriginalElements()
    {
        var box = new StyledBox { Style = new Style() }; box.Measure(100, 100);
        var clone = box.Style.Clone(); clone.FontSize = 50;
        Assert.False(box.IsMeasureDirty);
    }

    [Fact]
    public void PanelPaddingOverridesExplicitStyleInBothLayoutPaths()
    {
        var panel = new Panel { Layout = new StackLayout { Orientation = Orientation.Horizontal },
            Style = new Style { Padding = new Thickness(1) }, Padding = new Thickness(7) };
        var box = new Box(); panel.AddChild(box);
        panel.Arrange(new RectF(0, 0, 100, 50)); Assert.Equal(7, box.Bounds.X);
        new TaffyLayoutEngine().ComputeLayout(panel, 100, 50); Assert.Equal(7, box.Bounds.X);
    }

    [Fact]
    public void HiddenChildIsCollapsedInBothLayoutPathsAfterCachedLayout()
    {
        var panel = new Panel { Layout = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 4 },
            Style = new Style { Padding = new Thickness(0) } };
        var a = new Box(); var b = new Box(); panel.AddChild(a); panel.AddChild(b);
        var engine = new TaffyLayoutEngine(); engine.ComputeLayout(panel, 100, 50); panel.Measure(100, 50);
        a.Visible = false; Assert.True(panel.IsMeasureDirty);
        panel.Arrange(new RectF(0, 0, 100, 50)); float manualX = b.Bounds.X; Assert.Equal(0, manualX);
        engine.ComputeLayout(panel, 100, 50); Assert.Equal(manualX, b.Bounds.X);
        Assert.Equal(Vector2.Zero, a.Measure(100, 50));
        Assert.Equal(0, a.Bounds.W); Assert.Equal(0, a.Bounds.H);
        a.Visible = true; engine.ComputeLayout(panel, 100, 50); Assert.Equal(24, b.Bounds.X);
    }
}
