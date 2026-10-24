using System;
using System.Collections.Generic;
using System.Linq;
using Marius.Winter.Blazor;
using Xunit;

namespace Marius.Winter.Tests;

public class ProfileScenarioLifetimeTests
{
    [Fact]
    public void RazorProfileCancelsAndAwaitsItsOwnedSaveDuringWindowClose()
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(900, 720, "Razor profile closing");
            var host = window.UseBlazor();
            var scenario = host.AddComponent<Blazor.Playground.Scenarios.ProfileAndVolume>();
            Assert.True(scenario.IsCompletedSuccessfully);
            WindowRegressionOwner.Flush(window);
            var save = Descendants(window).OfType<Button>().Single(b => b.Text == "Save after delay");
            using var scope = window.Dispatcher.EnterScope();
            save.OnClick(); save.OnClick();
            Assert.Contains(Descendants(window).OfType<Label>(), l => l.Text == "Saving profile…");
            window.Dispose();
            Assert.True(window.IsDisposed); Assert.Empty(window.Children);
            Assert.True(host.DisposeAsync().IsCompletedSuccessfully);
        });
    }

    [Fact]
    public void NativeProfileClosesDuringPendingSaveWithoutAwaitingDroppedApplicationContext()
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(900, 720, "Profile closing");
            using var scenario = new Playground.Scenarios.ProfileAndVolume(window);
            window.AddChild(scenario.Content); window.SetAutoLayout(scenario.Content);
            WindowRegressionOwner.Flush(window);
            var save = Descendants(scenario.Content).OfType<Button>().Single(b => b.Text == "Save after delay");
            using var scope = window.Dispatcher.EnterScope();
            save.OnClick(); save.OnClick(); // Supersede one pending generation, then close the newer one.
            Assert.Contains(Descendants(scenario.Content).OfType<Label>(), l => l.Text == "Saving profile…");
            window.Dispose();
            Assert.True(window.IsDisposed); Assert.True(scenario.Content.IsDisposed);
        });
    }

    private static IEnumerable<Element> Descendants(Element root)
    {
        foreach (var child in root.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
