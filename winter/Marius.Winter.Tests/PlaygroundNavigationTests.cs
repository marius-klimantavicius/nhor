using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Marius.Winter.Blazor;
using Xunit;
using S = Marius.Winter.Playground.Scenarios;

namespace Marius.Winter.Tests;

public class PlaygroundNavigationTests
{
    [Fact]
    public void NativeTenPageHostPreservesEveryModuleAndSelectsBothNewPages() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(900, 720, "Native registered inventory", Theme.Light, RenderBackend.SW);
        using var profile = new S.ProfileAndVolume(window); using var help = new S.HelpRichDisplayScenario(window);
        using var workspace = new S.ResizableWorkspaceScenario(window); using var operation = new S.OperationFeedbackScenario(window);
        using var gallery = new S.LegacyGallery(window); using var project = new S.ProjectBrowserScenario(window);
        using var environment = new S.EnvironmentAndTabsScenario(window); using var notes = new S.NotesScenario(window);
        using var installer = new S.InstallerSetupScenario(window); using var dialogs = new S.WindowsAndDialogsScenario(window);
        using var host = new S.ScenarioHost(window);
        string[] names = ["Profile and volume", "Help and rich display", "Resizable workspace", "Operation feedback", "Existing demos", "Project browser", "Environment and tabs", "Notes", "Installer setup", "Windows and dialogs"];
        string[] titles = ["Profile and volume", "Help and rich display", "Resizable workspace", "Operation feedback · Preview pack", "Winter GUI Framework", "Project browser", "Environment and tabs", "Notes", "Installer setup", "Windows and dialogs"];
        Element[] pages = [profile.Content, help.Content, workspace.Content, operation.Content, gallery.Content, project.Content, environment.Content, notes.Content, installer.Content, dialogs.Content];
        for (int i = 0; i < names.Length; i++) host.AddScenario(names[i], pages[i]);
        window.AddChild(host.Content); window.SetAutoLayout(host.Content); WindowRegressionOwner.Flush(window);
        var selector = Walk(host.Content).OfType<ComboBox>().First(); Assert.Equal(names, selector.Items);
        var editor = Walk(profile.Content).OfType<TextBox>().First();
        for (int i = 0; i < names.Length; i++)
        {
            selector.SelectFromUser(i); WindowRegressionOwner.Flush(window);
            Assert.True(pages[i].IsEffectivelyVisible);
            Assert.Contains(Walk(pages[i]).OfType<Label>(), label => label.IsEffectivelyVisible && label.Text == titles[i]);
            Assert.Same(editor, Walk(profile.Content).OfType<TextBox>().First());
        }
    });

    [Fact]
    public async Task CompiledRazorHostSelectsEveryRegisteredPageAndPreservesProfile()
    {
        using var pump = new NativePump(w =>
        {
            var mounted = w.UseBlazor().AddComponent<Blazor.Playground.App>(new Dictionary<string, object> { ["OwnerWindow"] = w });
            Assert.True(mounted.IsCompletedSuccessfully);
        });
        string[] names = ["Profile and volume", "Help and rich display", "Resizable workspace", "Operation feedback", "Existing demos", "Project browser", "Environment and tabs", "Notes", "Installer setup", "Windows and dialogs"];
        string[] titles = ["Profile and volume", "Help and rich display", "Resizable workspace", "Operation feedback · Preview pack", "Winter Blazor Playground", "Project browser", "Environment and tabs", "Notes", "Installer setup", "Windows and dialogs"];
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
