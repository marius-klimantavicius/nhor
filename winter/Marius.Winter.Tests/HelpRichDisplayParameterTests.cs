using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Marius.Winter.Blazor;
using Microsoft.AspNetCore.Components;
using Canvas = ThorVG.Canvas;
using Xunit;
using B = Marius.Winter.Blazor.Elements;

namespace Marius.Winter.Tests;

public class HelpRichDisplayParameterTests
{
    [Fact]
    public async Task RichLabel_FontSizeUpdateChangesRenderedInkAndResetRestoresIt()
    {
        var label = new RichLabel("Read") { FontSize = 18, Color = new Color4(1, 0, 0, 1) };
        using var pump = new NativePump(w =>
        {
            w.AddChild(label);
            label.Measure(180, 90);
            label.Arrange(new RectF(5, 5, 180, 90));
        });
        await pump.Call(w =>
        {
            int Width()
            {
                var canvas = (Canvas)typeof(Window).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(w)!;
                Contracts.Draw(canvas, true);
                var pixels = WindowRuntimeTests.Buffer(w);
                var xs = pixels.Select((p, i) => (p, x: i % (int)w.FramebufferWidth))
                    .Where(t => ((t.p >> 16) & 255) > 150 && ((t.p >> 8) & 255) < 50 && (t.p & 255) < 50)
                    .Select(t => t.x).ToArray();
                Assert.NotEmpty(xs);
                return xs.Max() - xs.Min() + 1;
            }
            int before = Width();
            label.FontSize = 32;
            int after = Width();
            Assert.True(after > before * 1.5f, $"32-unit text must grow visible ink: before={before}, after={after}.");
            label.FontSize = 18;
            Assert.Equal(before, Width());
        });
    }

    [Fact]
    public async Task CompiledRazorScenario_UpdatesAndClearsAttachedRichDisplayAndResetsHelp()
    {
        Task<Marius.Winter.Blazor.Playground.Scenarios.HelpRichDisplay> mounted = null!;
        using var pump = new NativePump(w =>
        {
            mounted = w.UseBlazor().AddComponent<Marius.Winter.Blazor.Playground.Scenarios.HelpRichDisplay>();
        });
        await mounted.WaitAsync(TimeSpan.FromSeconds(5));
        RichLabel label = null!;
        SvgImage icon = null!;
        await pump.Call(w =>
        {
            label = Descendants(w).OfType<RichLabel>().Single();
            icon = Descendants(w).OfType<SvgImage>().Single();
            var help = Assert.IsType<TooltipContent>(Button(w, "Favorite").Tooltip);
            Assert.Equal("Star icon", help.Text);
            Assert.NotNull(help.Svg);
            Assert.Equal(help.Svg, help.TrailingSvg);
            Assert.Equal(new Color4(.1f, .4f, .65f, 1), label.Color);
            Button(w, "Larger preview").Clicked!();
        });
        await pump.Call(w =>
        {
            Assert.Equal(28, label.FontSize);
            Assert.Equal(64, icon.RequestedWidth);
            Assert.Equal(64, icon.RequestedHeight);
            Button(w, "Theme sizes").Clicked!();
        });
        await pump.Call(w =>
        {
            Assert.Equal(w.Theme.Label.FontSize, label.FontSize);
            Assert.Equal(w.Theme.TextColor, label.Color);
            Assert.Equal(0, icon.RequestedWidth);
            Assert.Equal(0, icon.RequestedHeight);
            Button(w, "Clear display").Clicked!();
        });
        await pump.Call(w =>
        {
            Assert.Equal("", label.Text);
            Assert.Null(icon.Svg);
            Button(w, "Remove favorite").Clicked!();
        });
        await pump.Call(w =>
        {
            Assert.DoesNotContain(Descendants(w).OfType<Button>(), b => b.Text == "Favorite");
            Button(w, "Reset").Clicked!();
        });
        await pump.Call(w =>
        {
            Assert.IsType<TooltipContent>(Button(w, "Favorite").Tooltip);
            Assert.Equal("Favorite · Žvaigždė · Café", label.Text);
            Assert.NotNull(icon.Svg);
            Assert.Equal(32, icon.RequestedWidth);
            Assert.Same(label, Descendants(w).OfType<RichLabel>().Single());
        });
    }

    private static Button Button(Window window, string text) => Descendants(window).OfType<Button>().Single(b => b.Text == text);
    private static IEnumerable<Element> Descendants(Element root)
    {
        foreach (var child in root.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    [Fact]
    public async Task RichLabel_WrapperColorSurvivesAttachmentAndThemeChanges()
    {
        var color = new Color4(.1f, .4f, .65f, 1);
        Task<B.RichLabel> mounted = null!;
        using var pump = new NativePump(w =>
        {
            w.Theme = Theme.Light;
            mounted = w.UseBlazor().AddComponent<B.RichLabel>(new()
            {
                ["Text"] = "Favorite · Žvaigždė · Café", ["FontSize"] = 24f, ["Color"] = color
            });
        });
        var component = await mounted.WaitAsync(TimeSpan.FromSeconds(5));
        await pump.Call(w =>
        {
            var label = Assert.IsType<RichLabel>(Assert.Single(w.Children));
            Assert.Equal(color, label.Color);
            Assert.Equal(24, label.FontSize);
            w.Theme = Theme.Dark;
            Assert.Equal(color, label.Color);
            Assert.Equal(24, label.FontSize);
            Assert.True(label.Measure(500, 100).X > 150);
        });
        await pump.Window.Dispatcher.InvokeAsync(() => component.SetParametersAsync(ParameterView.FromDictionary(
            new Dictionary<string, object?> { ["FontSize"] = null, ["Color"] = null })));
        await pump.Call(w =>
        {
            var label = Assert.IsType<RichLabel>(Assert.Single(w.Children));
            Assert.Equal(w.Theme.Label.FontSize, label.FontSize);
            Assert.Equal(w.Theme.TextColor, label.Color);
            w.Theme = Theme.Light;
            Assert.Equal(w.Theme.Label.FontSize, label.FontSize);
            Assert.Equal(w.Theme.TextColor, label.Color);
        });
    }
}
