using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Xunit;
using B = Marius.Winter.Blazor.Elements;

namespace Marius.Winter.Blazor.Tests;

[Collection("Native rendering")]
public class ModelessLayoutBindingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MountRerenderAndUnmountKeepSiblingBoundsAndParentIntrinsics(bool grid)
    {
        using var renderer = new TestRenderer();
        using var root = new Panel
        {
            Padding = new Thickness(7),
            Layout = grid
                ? new GridLayout { Columns = [TrackSize.Auto(), TrackSize.Auto()], ColumnGap = 13, RowGap = 11 }
                : new FlexLayout { Direction = Orientation.Vertical, Gap = 11, JustifyContent = JustifyContent.Center }
        };
        RenderFragment View(bool mounted) => b =>
        {
            b.OpenComponent<B.Panel>(0); b.AddAttribute(1, "MinWidth", 80f); b.AddAttribute(2, "MinHeight", 20f); b.CloseComponent();
            if (mounted)
            {
                b.OpenComponent<B.DialogWindow>(3); b.AddAttribute(4, "Width", 280f); b.AddAttribute(5, "Height", 170f); b.CloseComponent();
            }
            b.OpenComponent<B.Panel>(6); b.AddAttribute(7, "MinWidth", 110f); b.AddAttribute(8, "MinHeight", 32f); b.CloseComponent();
        };
        var host = await renderer.AddComponent<InputBindingTests.Host>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Content"] = View(false) });
        var siblings = root.Children.ToArray();
        var desired = root.Measure(420, 360);
        root.Arrange(new RectF(0, 0, 420, 360));
        var original = siblings.Select(c => c.Bounds).ToArray();
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(true)));
        var dialog = Assert.Single(root.Children.OfType<DialogWindow>());
        Assert.Equal(desired, root.Measure(420, 360));
        root.Arrange(new RectF(0, 0, 420, 360));
        Assert.Equal(original, siblings.Select(c => c.Bounds));
        Assert.Same(root, dialog.Parent);
        Assert.Equal(new RectF(100, 60, 280, 170), dialog.Bounds);
        dialog.Bounds = new RectF(130, 90, 320, 210);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(true)));
        root.Measure(420, 360); root.Arrange(new RectF(0, 0, 420, 360));
        Assert.Equal(new RectF(130, 90, 320, 210), dialog.Bounds);
        Assert.Equal(original, siblings.Select(c => c.Bounds));
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(false)));
        Assert.True(dialog.IsDisposed); Assert.Null(dialog.Parent);
        Assert.Equal(siblings, root.Children);
        Assert.Equal(desired, root.Measure(420, 360));
        root.Arrange(new RectF(0, 0, 420, 360));
        Assert.Equal(original, siblings.Select(c => c.Bounds));
    }
}
