using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Marius.Winter.Blazor;
using Xunit;

namespace Marius.Winter.Tests;

public class PlaygroundNavigationTests
{
    [Fact]
    public async Task CompiledRazorHostSelectsEveryRegisteredPageAndPreservesProfile()
    {
        using var pump = new NativePump(w =>
        {
            var mounted = w.UseBlazor().AddComponent<Blazor.Playground.App>(new Dictionary<string, object> { ["OwnerWindow"] = w });
            Assert.True(mounted.IsCompletedSuccessfully);
        });
        string[] names = ["Profile and volume", "Help and rich display", "Resizable workspace", "Operation feedback", "Existing demos", "Project browser", "Environment and tabs", "Notes"];
        string[] titles = ["Profile and volume", "Help and rich display", "Resizable workspace", "Operation feedback · Preview pack", "Winter Blazor Playground", "Project browser", "Environment and tabs", "Notes"];
        Element profile = null!;
        await pump.Call(w =>
        {
            var selector = Walk(w).OfType<ComboBox>().First();
            Assert.Equal(names, selector.Items);
            profile = Walk(w).OfType<TextBox>().First();
        });
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            await pump.Call(w => Walk(w).OfType<ComboBox>().First().SelectFromUser(index));
            await pump.Call(w =>
            {
                Assert.Contains(Walk(w).OfType<Label>(), l => l.IsEffectivelyVisible && l.Text == titles[index]);
                Assert.Same(profile, Walk(w).OfType<TextBox>().First());
            });
        }
    }

    [Fact]
    public async Task RegisteredNotesSnapshotRetainsIdentityAcrossNavigationAndPendingTreeClose()
    {
        using var pump = new NativePump(w =>
            Assert.True(w.UseBlazor().AddComponent<Blazor.Playground.App>(new() { ["OwnerWindow"] = w }).IsCompletedSuccessfully));
        RichTextBox editor = null!;
        await pump.Call(w => Walk(w).OfType<ComboBox>().First().SelectFromUser(7));
        await pump.Call(w =>
        {
            editor = Walk(w).OfType<RichTextBox>().Single(e => e.IsEffectivelyVisible);
            Click(w, "Select phrase"); Click(w, "Blue"); Click(w, "Save snapshot");
        });
        await pump.Call(w =>
        {
            var preview = Walk(w).OfType<LogView>().Single(e => e.IsEffectivelyVisible);
            var saved = RichDocument.FromJson(preview.Text);
            Assert.Equal(editor.Document, saved);
            Assert.Contains("blue horizon", saved.Text);
            Walk(w).OfType<ComboBox>().First().SelectFromUser(5);
        });
        await pump.Call(w => Walk(w).OfType<ComboBox>().First().SelectFromUser(7));
        await pump.Call(w =>
        {
            Assert.Same(editor, Walk(w).OfType<RichTextBox>().Single(e => e.IsEffectivelyVisible));
            Click(w, "Restore snapshot");
            Walk(w).OfType<ComboBox>().First().SelectFromUser(5);
        });
        await pump.Call(w =>
        {
            var tree = Walk(w).OfType<TreeView>().Single(e => e.IsEffectivelyVisible);
            tree.SelectedNode = tree.Nodes[0].Children.Single(n => Equals(n.Tag, "src"));
            tree.OnKeyDown(Glfw.GLFW.GLFW_KEY_RIGHT, 0, false);
            Assert.Equal("Loading…", tree.SelectedNode.Children[0].Text);
        });
        // NativePump closes through the ordinary cooperative Window shutdown.
    }

    private static void Click(Element root, string text) => Walk(root).OfType<Button>().Single(b => b.IsEffectivelyVisible && b.Text == text).OnClick();

    private static IEnumerable<Element> Walk(Element e)
    {
        yield return e;
        foreach (var child in e.Children) foreach (var descendant in Walk(child)) yield return descendant;
    }
}
