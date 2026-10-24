using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Marius.Winter.Blazor;
using Xunit;

namespace Marius.Winter.Tests;

public class InstallerConfigurationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActualInstallerEnterStartsOneSimulationAndNormalCloseAwaitsOwnedWork(bool closePending) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(800, 1000, "Actual Installer simulation", Theme.Light, RenderBackend.SW);
        var host = window.UseBlazor(); var added = host.AddComponent<Blazor.InstallerExample.App>(); Assert.True(added.IsCompletedSuccessfully);
        var app = added.Result;
        var input = Task.Run(async () =>
        {
            try
            {
                using var x = new FormX11(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!));
                await Task.Delay(150); x.Focus(); x.Key(0xff09); x.Key(0xff0d); x.Key(0xff0d); await Task.Delay(150);
                await window.Dispatcher.InvokeAsync(() =>
                {
                    Assert.Contains(Walk(window).OfType<Label>(), l => l.Text == "Installing...");
                    Assert.Equal(1L, (long)typeof(Blazor.InstallerExample.App).GetField("_generation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(app)!);
                });
                x.Capture($"installer-{closePending}-progress", 800, 1000);
                if (!closePending)
                {
                    await Task.Delay(5600);
                    await window.Dispatcher.InvokeAsync(() =>
                    {
                        Assert.Contains(Walk(window).OfType<Label>(), l => l.Text == "Installation Simulation Complete");
                        Assert.Contains(Walk(window).OfType<Label>(), l => l.Text == "Environment: Production");
                        Assert.Contains(Walk(window).OfType<Label>(), l => l.Text == "Desktop shortcut selected.");
                    });
                    x.Capture("installer-complete", 800, 1000);
                }
                x.CloseNormally();
            }
            catch { window.Close(); throw; }
        });
        window.Run(); input.GetAwaiter().GetResult();
        Assert.True(window.IsDisposed); Assert.True(host.DisposeAsync().IsCompletedSuccessfully);
    });

    [Fact]
    public void ActualInstallerRejectsOrdinaryInvalidConfigurationBeforeStartingSimulation() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(700, 800, "Installer invalid configuration");
        var host = window.UseBlazor();
        var added = host.AddComponent<Blazor.InstallerExample.App>(); Assert.True(added.IsCompletedSuccessfully);
        using var scope = window.Dispatcher.EnterScope();
        var boxes = Walk(window).OfType<TextBox>().ToArray();
        var fields = Walk(window).OfType<Field>().ToArray();
        var name = fields.FirstOrDefault(f => f.Label == "Application name")?.Editor as TextBox ?? boxes[1];
        var destination = fields.FirstOrDefault(f => f.Label == "Destination")?.Editor as TextBox ?? boxes[0];
        name.OnKeyDown(65, 2, false); name.OnKeyDown(259, 0, false); name.OnKeyDown(257, 0, false);
        destination.OnKeyDown(65, 2, false); destination.OnTextInput("relative/path"); destination.OnKeyDown(257, 0, false);
        Walk(window).OfType<Button>().Single(b => b.Text == "Install").OnClick();
        Assert.DoesNotContain(Walk(window).OfType<Label>(), l => l.Text == "Installing...");
        Assert.Contains(Walk(window).OfType<Label>(), l => l.Text.Contains("Enter an application name."));
        Assert.Contains(Walk(window).OfType<Label>(), l => l.Text.Contains("Enter an absolute destination"));
    });
    internal static IEnumerable<Element> Walk(Element root)
    {
        foreach (var child in root.Children) { yield return child; foreach (var nested in Walk(child)) yield return nested; }
    }
}
