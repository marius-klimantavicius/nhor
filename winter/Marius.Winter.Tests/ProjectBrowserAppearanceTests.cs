using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Marius.Winter.Blazor;
using Marius.Winter.Playground.Scenarios;
using ThorVG;
using Xunit;

namespace Marius.Winter.Tests;

public class ProjectBrowserAppearanceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void NativeAndCompiledRazorToggleActualLightDarkPixelsRepeatedly(bool razor, bool initiallyDark)
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(940, 740, "Project browser appearance", initiallyDark ? Theme.Dark : Theme.Light, RenderBackend.SW);
            ProjectBrowserScenario? native = null;
            try
            {
                if (razor)
                    Assert.True(window.UseBlazor().AddComponent<Blazor.Playground.Scenarios.ProjectBrowser>(new() { ["OwnerWindow"] = window }).IsCompletedSuccessfully);
                else
                {
                    native = new(window); window.AddChild(native.Content); window.SetAutoLayout(native.Content);
                }
                var toggle = Walk(window).OfType<Button>().Single(b => b.Text == "Light / dark");
                uint original = Pixel();
                for (int i = 0; i < 4; i++)
                {
                    toggle.OnClick();
                    while (window.DispatcherQueue.TryDequeue(out var work)) work();
                    uint actual = Pixel();
                    if (i % 2 == 0) Assert.NotEqual(original, actual);
                    else Assert.Equal(original, actual);
                }
            }
            finally { native?.Dispose(); }

            uint Pixel()
            {
                WindowRegressionOwner.Flush(window);
                Contracts.Draw((Canvas)typeof(Window).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!, true);
                return WindowRuntimeTests.Buffer(window)[20 * 940 + 930];
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GlobalAppearancePreservesAndTogglesTheProjectPagesCurrentTone(bool razor)
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(940, 740, "Registered project appearance", Theme.Light, RenderBackend.SW);
            using var project = razor ? null : new ProjectBrowserScenario(window);
            using var host = razor ? null : new ScenarioHost(window);
            if (razor)
                Assert.True(window.UseBlazor().AddComponent<Blazor.Playground.App>(new() { ["OwnerWindow"] = window }).IsCompletedSuccessfully);
            else
            {
                host!.AddScenario("Project browser", project!.Content);
                window.AddChild(host.Content); window.SetAutoLayout(host.Content);
            }
            WindowRegressionOwner.Flush(window);
            if (razor) Walk(window).OfType<ComboBox>().First().SelectFromUser(5);
            Drain();
            Click("Light / dark");
            Assert.True(window.Theme.WindowBackground.R < .5f);
            Click("Larger text"); // The global action must preserve the active tone.
            Assert.True(window.Theme.WindowBackground.R < .5f);
            Click("Toggle theme");
            Assert.True(window.Theme.WindowBackground.R > .5f);

            void Drain() { while (window.DispatcherQueue.TryDequeue(out var work)) work(); }
            void Click(string text)
            {
                Walk(window).OfType<Button>().First(b => b.IsEffectivelyVisible && b.Text == text).OnClick(); Drain();
            }
        });
    }

    private static IEnumerable<Element> Walk(Element e) => new[] { e }.Concat(e.Children.SelectMany(Walk));
}
