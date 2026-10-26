using System;
using Marius.Winter.Scenarios;

namespace Marius.Winter.Playground.Scenarios;

public sealed class MarkdownScenario : IDisposable
{
    public Element Content { get; }
    public MarkdownScenario()
    {
        var root = new Panel { Layout = new GridLayout { Columns = [TrackSize.Fr()], Rows = [TrackSize.Auto(), TrackSize.Fr(), TrackSize.Auto()], RowGap = 8 } };
        var actions = new Panel { Layout = new FlexLayout { Wrap = FlexWrap.Wrap, Gap = 8 } };
        var viewer = new MarkdownView(MarkdownScenarioData.Document) { ImageResolver = MarkdownScenarioData.ResolveImage, MinHeight = 180 };
        var result = new Label("Drag across styled text or blocks to select. Shift+arrows extends; Ctrl+A/C copies rendered text. Alt+Left/Right focuses links; Enter activates. Show selection reports your range.") { TextWrapping = ThorVG.TextWrap.Word };
        viewer.LinkClicked += destination => result.Text = "Destination: " + destination;
        bool narrow = false, updated = false;
        actions.AddChild(new Button("Update Markdown") { Clicked = () => { updated = !updated; viewer.Markdown = updated ? MarkdownScenarioData.Updated : MarkdownScenarioData.Document; } });
        actions.AddChild(new Button("Toggle width") { Clicked = () => { narrow = !narrow; viewer.MaxWidth = narrow ? 460 : null; } });
        actions.AddChild(new Button("Show selection") { Clicked = () => result.Text = $"Selected [{viewer.SelectionStart}, {viewer.SelectionLength}]: {viewer.SelectedText}" });
        actions.AddChild(new Button("Select sample") { Clicked = () => { int start = viewer.Text.IndexOf("readable documents", StringComparison.Ordinal); if (start >= 0) { viewer.Select(start, 18); viewer.ScrollTo(0, 0); viewer.Focus(); } } });
        actions.AddChild(new Button("Long document") { Clicked = () => { viewer.Markdown = MarkdownScenarioData.Moderate(); result.Text = "24 sections loaded. Scroll or press Ctrl+End to reach the last section. Drag outside the viewport to extend selection while scrolling."; } });
        actions.AddChild(new Button("Reset") { Clicked = () => { viewer.Markdown = MarkdownScenarioData.Document; viewer.MaxWidth = null; updated = narrow = false; result.Text = "Reset field notes."; } });
        root.AddChild(actions); root.AddChild(viewer); root.AddChild(result); Content = root;
    }
    public void Dispose() => Content.Dispose();
}
