using System.Collections.Generic;
using System.Threading.Tasks;
using Marius.Winter.Blazor.Core;
using Microsoft.AspNetCore.Components;
using Xunit;
using B = Marius.Winter.Blazor.Elements;

namespace Marius.Winter.Blazor.Tests;

[Collection("Native rendering")]
public class HelpRichDisplayTests
{
    private const string Star = "<svg xmlns='http://www.w3.org/2000/svg' width='24' height='24'><path fill='#f5b400' d='M12 1l3 7 8 1-6 5 2 9-7-4-7 4 2-9-6-5 8-1z'/></svg>";

    [Fact]
    public async Task Tooltip_ObjectTransportResolvesRichContentAndClearsThroughRendererDiffs()
    {
        using var renderer = new TestRenderer();
        var root = new Panel();
        var rich = new TooltipContent("Favorite · Žvaigždė", Star) { TrailingSvg = Star };
        var host = await renderer.AddComponent<BatchBRegressionTests.AttributeHost>(
            TestRenderer.CreateRootHandler(renderer, root), new()
            {
                ["Control"] = typeof(B.Button),
                ["Attributes"] = new[] { new KeyValuePair<string, object>("Tooltip", rich) }
            });
        var button = host.Native<Button>();
        Assert.Same(rich, button.Tooltip);
        var replacement = new TooltipContent(Svg: Star);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(new KeyValuePair<string, object>("Tooltip", replacement)));
        Assert.Same(replacement, button.Tooltip);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(new KeyValuePair<string, object>("Tooltip", "Plain fallback")));
        Assert.Equal("Plain fallback", button.Tooltip);
        await renderer.Dispatcher.InvokeAsync(() => host.Render());
        Assert.Null(button.Tooltip);
    }

    private static RenderFragment View<T>(params (string name, object? value)[] parameters) where T : IComponent => b =>
    {
        b.OpenComponent<T>(0);
        foreach (var (name, value) in parameters) b.AddAttribute(1, name, value);
        b.CloseComponent();
    };

    [Fact]
    public async Task Tooltip_PublicWrapperUsesNonNullRichPrecedenceAndExplicitParentReset()
    {
        using var renderer = new TestRenderer();
        var root = new Panel();
        RenderFragment ButtonView(string? text, TooltipContent? rich) => View<B.Button>(("Tooltip", text), ("TooltipContent", rich));
        var rich = new TooltipContent("Favorite", Star) { TrailingSvg = Star };
        var host = await renderer.AddComponent<BatchBRegressionTests.ContentHost>(TestRenderer.CreateRootHandler(renderer, root),
            new() { ["Content"] = ButtonView("Fallback", rich) });
        var button = Assert.IsType<Button>(Assert.Single(root.Children));
        Assert.Same(rich, button.Tooltip);
        for (var i = 0; i < 3; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => host.Render(ButtonView("New fallback", rich)));
            Assert.Same(rich, button.Tooltip);
            await renderer.Dispatcher.InvokeAsync(() => host.Render(ButtonView("New fallback", null)));
            Assert.Equal("New fallback", button.Tooltip);
            // Empty rich content still takes precedence: only null selects fallback.
            var empty = new TooltipContent();
            await renderer.Dispatcher.InvokeAsync(() => host.Render(ButtonView("Fallback", empty)));
            Assert.Same(empty, button.Tooltip);
            await renderer.Dispatcher.InvokeAsync(() => host.Render(ButtonView(null, null)));
            Assert.Null(button.Tooltip);
        }
        await renderer.Dispatcher.InvokeAsync(() => host.Render(b => { }));
        Assert.Empty(root.Children);
        Assert.Null(button.Parent);
    }

    [Fact]
    public async Task RichLabel_FontAndColorUpdateAndClearWithoutReplacingNativeIdentity()
    {
        using var renderer = new TestRenderer();
        var root = new Panel();
        RenderFragment LabelView(float? font, Color4? color) => View<B.RichLabel>(("Text", "Žvaigždė · Café"), ("FontSize", font), ("Color", color));
        var color = new Color4(.1f, .3f, .7f, 1);
        var host = await renderer.AddComponent<BatchBRegressionTests.ContentHost>(TestRenderer.CreateRootHandler(renderer, root),
            new() { ["Content"] = LabelView(22, color) });
        var label = Assert.IsType<RichLabel>(Assert.Single(root.Children));
        Assert.Equal(22, label.FontSize);
        Assert.Equal(color, label.Color);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(LabelView(30, new Color4(.6f, .2f, .1f, 1))));
        Assert.Same(label, Assert.Single(root.Children));
        Assert.Equal(30, label.FontSize);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(LabelView(null, null)));
        Assert.Equal(Theme.Dark.Label.FontSize, label.FontSize);
        Assert.Equal(Theme.Dark.TextColor, label.Color);
    }

    [Fact]
    public async Task SvgImage_RequestedDimensionsIndependentlyUpdateAndClearToIntrinsic()
    {
        using var renderer = new TestRenderer();
        var root = new Panel();
        RenderFragment ImageView(float? width, float? height, string? svg = Star) => View<B.SvgImage>(("Svg", svg), ("RequestedWidth", width), ("RequestedHeight", height));
        var host = await renderer.AddComponent<BatchBRegressionTests.ContentHost>(TestRenderer.CreateRootHandler(renderer, root),
            new() { ["Content"] = ImageView(36, 48) });
        var image = Assert.IsType<SvgImage>(Assert.Single(root.Children));
        Assert.Equal(36, image.RequestedWidth);
        Assert.Equal(48, image.RequestedHeight);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(ImageView(64, null)));
        Assert.Same(image, Assert.Single(root.Children));
        Assert.Equal(64, image.RequestedWidth);
        Assert.Equal(0, image.RequestedHeight);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(ImageView(null, 52)));
        Assert.Equal(0, image.RequestedWidth);
        Assert.Equal(52, image.RequestedHeight);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(ImageView(null, null, null)));
        Assert.Equal(0, image.RequestedWidth);
        Assert.Equal(0, image.RequestedHeight);
        Assert.Null(image.Svg);
    }
}
