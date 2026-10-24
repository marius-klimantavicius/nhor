using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
namespace Marius.Winter.Blazor.Tests;
[Collection("Native rendering")]
public class MenuModelSetterTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ModelSetterWithStableParentClickSurvivesUnrelatedRender(bool render)
    {
        using var w = new Window(400, 250, "C09 model setter identity");
        using var renderer = new TestRenderer(); var root = new Panel(); w.AddChild(root);
        var p = await renderer.AddComponent<MenuModelSetterProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var item = ((MenuBar)root.Children.Single()).Menus[0].Entries[0];
        await renderer.Dispatcher.InvokeAsync(() =>
        {
            item.Action!(); item.Action!(); if (render) p.RenderAgain();
            while (w.DispatcherQueue.TryDequeue(out var action)) action();
        });
        output.WriteLine("render={0}; model setter values=[{1}]; Clicks={2}", render, string.Join(",", p.Model.Checks), p.Clicks);
        Assert.Equal(new[] { true, false }, p.Model.Checks);
        Assert.Equal(2, p.Clicks);
    }
}
