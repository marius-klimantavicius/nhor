using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Xunit;
using B = Marius.Winter.Blazor.Elements;

namespace Marius.Winter.Blazor.Tests;

[Collection("Native rendering")]
public class ModelessSizingTests
{
    [Fact]
    public async Task WidthOnlyInitialSizingDoesNotBecomeControlledAfterUserResize()
    {
        using var renderer = new TestRenderer(); using var root = new Panel();
        RenderFragment View(float width) => b =>
        { b.OpenComponent<B.DialogWindow>(0); b.AddAttribute(1, "Width", width); b.CloseComponent(); };
        var host = await renderer.AddComponent<InputBindingTests.Host>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Content"] = View(310) });
        var dialog = Assert.IsType<DialogWindow>(Assert.Single(root.Children));
        Assert.Equal(310, dialog.Bounds.W);
        dialog.Bounds = new RectF(23, 34, 420, 280);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(330)));
        Assert.Equal(420, dialog.Bounds.W); Assert.Equal(280, dialog.Bounds.H);
        Assert.Equal(23, dialog.Bounds.X); Assert.Equal(34, dialog.Bounds.Y);
    }
}
