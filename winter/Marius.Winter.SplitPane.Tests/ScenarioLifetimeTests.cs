using System;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using ThorVG;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Marius.Winter.Blazor.Tests;

public class ScenarioLifetimeTests
{
    [Fact]
    public void NativeScenarioReleasesPreparedDetachedPreviewBeforeWindowRuntimeCloses()
    {
        Exception? failure = null;
        var owner = new Thread(() =>
        {
            try
            {
                using var window = new Window(800, 600, "C10 scenario lifetime", Theme.Light, RenderBackend.SW);
                using var scenario = new Marius.Winter.Playground.Scenarios.ResizableWorkspaceScenario(window);
                window.AddChild(scenario.Content);
                scenario.Content.Arrange(new RectF(0, 0, 800, 600));
                Draw(window);
                Button replace = Walk(scenario.Content).OfType<Button>().Single(b => b.Text == "Replace preview");
                replace.Clicked!();
                Draw(window);
                var preview = Walk(scenario.Content).OfType<Label>().Single(l => l.Text == "Preview details").Parent!;
                replace.Clicked!();
                Assert.Null(preview.Parent);
                // Its prepared paints must be disposed during Window.Closing,
                // before the runtime release, even when the scenario caller's
                // using scope naturally ends after Window.Run/Dispose.
                window.Dispose();
                Assert.True(preview.IsDisposed);
                Assert.True(scenario.Content.IsDisposed);
            }
            catch (Exception ex) { failure = ex; }
        });
        owner.Start();
        Assert.True(owner.Join(TimeSpan.FromSeconds(15)), "Owned-window setup timed out.");
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
    private static System.Collections.Generic.IEnumerable<Element> Walk(Element element)
    {
        yield return element;
        foreach (var child in element.Children) foreach (var descendant in Walk(child)) yield return descendant;
    }
    private static void Draw(Window window)
    {
        typeof(Window).GetMethod("FlushLayout", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
        var canvas = (Canvas)typeof(Window).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        Assert.Equal(Result.Success, canvas.Update());
        Assert.Equal(Result.Success, canvas.Draw(true));
        Assert.Equal(Result.Success, canvas.Sync());
    }
}
