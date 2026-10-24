using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Marius.Winter.Playground.Scenarios;
using ThorVG;
using Xunit;

namespace Marius.Winter.Tests;

public class RegisteredScenarioLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RegisteredPagesReleasePreparedDetachedContentBeforeRuntimeShutdown(bool returnToReview)
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(900, 720, "Registered scenario lifetime", Theme.Light, RenderBackend.SW);
            using var help = new HelpRichDisplayScenario(window);
            using var workspace = new ResizableWorkspaceScenario(window);
            using var host = new ScenarioHost(window);
            host.AddScenario("Help and rich display", help.Content);
            host.AddScenario("Resizable workspace", workspace.Content);
            window.AddChild(host.Content); window.SetAutoLayout(host.Content);
            Draw(window);
            var favorite = Walk(help.Content).OfType<Button>().Single(b => b.Text == "Favorite");
            Click(help.Content, "Remove favorite");
            Assert.Null(favorite.Parent); Assert.False(favorite.IsDisposed);
            Walk(host.Content).OfType<ComboBox>().Single().SelectFromUser(1);
            Draw(window);
            var review = Walk(workspace.Content).OfType<TextArea>().Single().Parent!;
            Click(workspace.Content, "Replace preview");
            Draw(window);
            var alternate = Walk(workspace.Content).OfType<Label>().Single(l => l.Text == "Preview details").Parent!;
            if (returnToReview) Click(workspace.Content, "Return to review");
            var detached = returnToReview ? alternate : review;
            Assert.Null(detached.Parent); Assert.False(detached.IsDisposed);
            // Program's using declarations naturally end after Run. Closing
            // handlers must release module-owned detached paints before then.
            window.Dispose();
            Assert.True(favorite.IsDisposed);
            Assert.True(detached.IsDisposed);
            Assert.True(help.Content.IsDisposed); Assert.True(workspace.Content.IsDisposed);
        });
    }

    private static void Click(Element root, string text) => Walk(root).OfType<Button>().Single(b => b.Text == text).OnClick();
    private static IEnumerable<Element> Walk(Element e)
    {
        yield return e;
        foreach (var child in e.Children) foreach (var descendant in Walk(child)) yield return descendant;
    }
    private static void Draw(Window window)
    {
        WindowRegressionOwner.Flush(window);
        Contracts.Draw((Canvas)typeof(Window).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!, true);
    }
}
