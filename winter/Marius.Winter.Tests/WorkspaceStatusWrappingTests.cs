using System;
using System.Collections.Generic;
using System.Linq;
using Marius.Winter.Playground.Scenarios;
using ThorVG;
using Xunit;

namespace Marius.Winter.Tests;

public class WorkspaceStatusWrappingTests
{
    [Theory]
    [InlineData(14)]
    [InlineData(24)]
    public void SmallWorkspaceReservesMeasuredHeightForEntireStatus(int fontSize)
    {
        WindowRegressionOwner.Run(() =>
        {
            var theme = Theme.Light; theme.Label.FontSize = fontSize;
            using var window = new Window(420, 560, "Workspace wrapping", theme, RenderBackend.SW);
            using var scenario = new ResizableWorkspaceScenario(window);
            window.AddChild(scenario.Content); window.SetAutoLayout(scenario.Content);
            foreach (int width in new[] { 420, 760, 420 })
            foreach (var orientation in new[] { Orientation.Horizontal, Orientation.Vertical })
            {
                var split = Walk(scenario.Content).OfType<SplitPane>().First();
                split.Orientation = orientation;
                scenario.Content.Arrange(new RectF(0, 0, width, 560));
                scenario.Content.Arrange(new RectF(0, 0, width, 560)); // Visible-position result settles after layout.
                var status = Walk(scenario.Content).OfType<Label>().Single(l => l.Text.StartsWith("Browser:"));
                using var reference = new Label(status.Text) { TextWrapping = TextWrap.Word };
                window.AddChild(reference);
                var expected = reference.Measure(status.Bounds.W, 560);
                Assert.InRange(status.DesiredSize.X, 0, status.Bounds.W + .01f);
                Assert.InRange(status.Bounds.H, expected.Y - .01f, expected.Y + .01f);
                Assert.True(status.Bounds.Y + status.Bounds.H <= 560, "Entire status must remain inside the workspace.");
                window.RemoveChild(reference);
            }
        });
    }

    private static IEnumerable<Element> Walk(Element e)
    {
        yield return e;
        foreach (var child in e.Children) foreach (var descendant in Walk(child)) yield return descendant;
    }
}
