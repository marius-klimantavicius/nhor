using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
namespace Marius.Winter.Blazor.Tests;
[Collection("Native rendering")]
public class MenuAdditionalBindingTests(ITestOutputHelper output)
{
    private static void Drain(Window w) { while (w.DispatcherQueue.TryDequeue(out var action)) action(); }
    [Theory]
    [InlineData("default")] [InlineData("getset")]
    public async Task ChangedParentValueRemainsAuthoritativeAfterQueueDrain(string mode)
    {
        using var w = new Window(400, 250, "C09 independent parent consequence");
        using var renderer = new TestRenderer(); var root = new Panel(); w.AddChild(root);
        var p = await renderer.AddComponent<MenuBindingAuthorityProbe>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Mode"] = mode });
        var item = ((MenuBar)root.Children.Single()).Menus[0].Entries[0];
        await renderer.Dispatcher.InvokeAsync(() => { item.Action!(); item.Action!(); p.ParentSet(true); Drain(w); });
        output.WriteLine("Parent expected true; parent={0}, native={1}, checks=[{2}], clicks={3}", p.Checked, item.IsChecked, string.Join(",", p.Checks), p.Clicks);
        Assert.True(p.Checked);
        Assert.True(item.IsChecked);
        Assert.Empty(p.Checks);
        Assert.Equal(0, p.Clicks);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ReplacingOrClearingPhysicalContextAttachmentInvalidatesOldQueuedAction(bool clear)
    {
        using var w = new Window(400, 250, "C09 context attachment identity");
        using var renderer = new TestRenderer(); var root = new Panel(); w.AddChild(root);
        var p = await renderer.AddComponent<MenuContextAttachmentProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var owner = root.Children.Single(); var original = owner.ContextMenu!;
        var replacement = clear ? null : new Menu().AddItem("Replacement");
        await renderer.Dispatcher.InvokeAsync(() => { original.Entries[0].Action!(); owner.ContextMenu = replacement; Drain(w); });
        output.WriteLine("Current attachment changed={0}; obsolete action calls={1}", !ReferenceEquals(original, owner.ContextMenu), p.Clicks);
        Assert.Same(replacement, owner.ContextMenu);
        Assert.Equal(0, p.Clicks);
    }
}
