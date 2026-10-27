using System;
using System.Collections.Generic;
using System.Linq;
using Marius.Winter.Blazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;
using static Glfw.GLFW;

namespace Marius.Winter.Tests;

public partial class ScrollbarInputTests
{
    public static IEnumerable<object[]> ScrollCases()
    {
        foreach (string kind in new[] { "Markdown", "TextArea", "RichTextBox", "LogView", "ScrollPanel", "TreeView" })
        foreach (bool horizontal in new[] { false, true })
        foreach (bool blazor in new[] { false, true }) yield return [kind, horizontal, blazor];
    }

    public sealed class ScrollbarProbe : ComponentBase
    {
        [Parameter] public string Kind { get; set; } = "Markdown";
        protected override void BuildRenderTree(RenderTreeBuilder b)
        {
            b.OpenComponent<Marius.Winter.Blazor.Elements.Panel>(0);
            b.AddAttribute(1, "ChildContent", (RenderFragment)(child =>
            {
                Type type = Kind switch
                {
                    "Markdown" => typeof(Marius.Winter.Blazor.Elements.MarkdownView),
                    "TextArea" => typeof(Marius.Winter.Blazor.Elements.TextArea),
                    "RichTextBox" => typeof(Marius.Winter.Blazor.Elements.RichTextBox),
                    "LogView" => typeof(Marius.Winter.Blazor.Elements.LogView),
                    "ScrollPanel" => typeof(Marius.Winter.Blazor.Elements.ScrollPanel),
                    _ => typeof(Marius.Winter.Blazor.Elements.TreeView)
                };
                child.OpenComponent(0, type);
                if (Kind == "Markdown") child.AddAttribute(1, "Markdown", "[link](action:test)\n\n```\n" + Text + "\n```");
                else if (Kind is "TextArea" or "RichTextBox" or "LogView") child.AddAttribute(2, "Text", Text);
                if (Kind == "LogView") child.AddAttribute(3, "FollowTail", false);
                child.CloseComponent();
            }));
            b.CloseComponent();
        }
    }
    private static IEnumerable<Element> Walk(Element e)
    { yield return e; foreach (var child in e.Children) foreach (var item in Walk(child)) yield return item; }
    private static Element Mount(Window window, string kind)
    {
        var task = window.UseBlazor().AddComponent<ScrollbarProbe>(new() { ["Kind"] = kind });
        Assert.True(task.IsCompletedSuccessfully);
        var e = Walk(window).Single(e => kind switch
        {
            "Markdown" => e is MarkdownView, "TextArea" => e is TextArea, "RichTextBox" => e is RichTextBox,
            "LogView" => e is LogView, "ScrollPanel" => e is ScrollPanel, _ => e is TreeView
        });
        if (e is ScrollPanel panel) panel.Layout = new LargeLayout();
        return e;
    }

    [Theory]
    [MemberData(nameof(ScrollCases))]
    public void ThumbGripBothEndClampsAndSmallTracksUsePaintedGeometry(string kind, bool horizontal, bool blazor) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(900, 700, "Scrollbar thumb and tiny tracks", Theme.Light, RenderBackend.SW);
        var e = Attach(window, kind, blazor); var thumb = PaintBounds(e, horizontal, true);
        float x = thumb.X + thumb.W * .73f, y = thumb.Y + thumb.H * .73f;
        At(window, e, x, y); Button(window, true); Assert.Equal(0, Offset(e, horizontal));
        At(window, e, x + (horizontal ? 8 : 0), y + (horizontal ? 0 : 8));
        var moved = PaintBounds(e, horizontal, true);
        Assert.Equal((horizontal ? thumb.X : thumb.Y) + 8, horizontal ? moved.X : moved.Y, 2);
        Move(window, 1500, 1100); Assert.Equal(Total(e, horizontal) - Viewport(e, horizontal), Offset(e, horizontal), 2);
        Move(window, -100, -100); Assert.Equal(0, Offset(e, horizontal)); Button(window, false);
        e.Bounds = new(e.Bounds.X, e.Bounds.Y, 42, 42);
        foreach (bool axis in new[] { false, true })
        {
            var track = PaintBounds(e, axis, false); thumb = PaintBounds(e, axis, true);
            Assert.InRange(axis ? thumb.W : thumb.H, 0, axis ? track.W : track.H);
            Assert.InRange(axis ? thumb.X : thumb.Y, axis ? track.X : track.Y, axis ? track.Right : track.Bottom);
        }
    });

    private static void ReplaceSource(Element e)
    {
        switch (e)
        {
            case MarkdownView md: md.Markdown = md.Markdown.Replace("line", "row ", StringComparison.Ordinal); break;
            case TextArea text: text.Text = Text.Replace("line", "row ", StringComparison.Ordinal); break;
            case RichTextBox rich: rich.Text = Text.Replace("line", "row ", StringComparison.Ordinal); break;
            case LogView log: log.Text = Text.Replace("line", "row ", StringComparison.Ordinal); break;
            case TreeView tree: tree.AddNode("replacement"); break;
            case ScrollPanel panel: panel.Layout = new LargeLayout(); break;
        }
    }

    [Theory]
    [MemberData(nameof(ScrollCases))]
    public void HeldGestureExpiresOnResizeSourceThemeScaleAncestorMoveAndLifetimeChanges(string kind, bool horizontal, bool blazor) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(900, 700, "Scrollbar lifetime", Theme.Light, RenderBackend.SW);
        var e = Attach(window, kind, blazor);
        Action[] changes = [
            () => e.Bounds = new(e.Bounds.X, e.Bounds.Y, e.Bounds.W + 20, e.Bounds.H + 30),
            () => ReplaceSource(e), () => { var style = e.Style.Clone(); style.FontSize *= 1.2f; e.Style = style; },
            () => window.Theme = Theme.Dark,
            () => window.GlfwWindow!.Callbacks.Scale!(window.GlfwWindow, 1.5f, 1.5f),
            () => e.Parent!.Bounds = new(63, 41, 650, 480),
            () => { e.Visible = false; e.Visible = true; },
            () => { e.Enabled = false; e.Enabled = true; },
            () => { var parent = e.Parent!; parent.RemoveChild(e); parent.AddChild(e); }
        ];
        foreach (var change in changes)
        {
            var thumb = PaintBounds(e, horizontal, true);
            At(window, e, thumb.X + thumb.W / 2, thumb.Y + thumb.H / 2); Button(window, true);
            change(); float offset = Offset(e, horizontal);
            Move(window, 1500, 1100); Button(window, false); Move(window, -100, -100);
            Assert.Equal(offset, Offset(e, horizontal), 2);
            Assert.Null(typeof(Window).GetField("_capturedElement", Private)!.GetValue(window));
        }
    });

    [Fact]
    public void ParentScrollbarPreservesFocusedEditorAndNeverDispatchesClickOrDoubleClick() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(900, 700, "Scrollbar focus and clicks", Theme.Light, RenderBackend.SW);
        var panel = new ClickScrollPanel { Bounds = new(40, 30, 320, 210), Layout = new LargeLayout() };
        var editor = new TextBox("interactive child"); panel.AddChild(editor); window.AddChild(panel); WindowRegressionOwner.Flush(window);
        window.SetFocus(editor); editor.OnKeyDown(GLFW_KEY_A, GLFW_MOD_CONTROL, false);
        int doubles = 0; panel.DoubleClicked += () => doubles++;
        var track = PaintBounds(panel, false, false);
        for (int i = 0; i < 2; i++)
        {
            At(window, panel, track.X + track.W / 2, track.Bottom - 2); Button(window, true); Button(window, false);
            Assert.Same(editor, window.FocusedElement);
        }
        Assert.Equal(0, panel.Clicks); Assert.Equal(0, doubles);
        editor.OnTextInput("kept focus"); Assert.Equal("kept focus", editor.Text);
    });

    private sealed class ClickScrollPanel : ScrollPanel
    {
        internal int Clicks;
        public override void OnClick() => Clicks++;
    }

    private sealed class SmallLayout : ILayout
    {
        public System.Numerics.Vector2 Measure(Element parent, float width, float height) => new(30, 20);
        public void Arrange(Element parent, RectF bounds) { foreach (var child in parent.Children) child.Arrange(new(0, 0, 30, 20)); }
    }

    [Theory]
    [MemberData(nameof(ScrollCases))]
    public void SourceShrinkHidesBothBarsAndAlwaysNeverModesStayInert(string kind, bool horizontal, bool blazor) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(900, 700, "Scrollbar source shrink", Theme.Light, RenderBackend.SW);
        var e = Attach(window, kind, blazor); var thumb = PaintBounds(e, horizontal, true);
        At(window, e, thumb.X + thumb.W / 2, thumb.Y + thumb.H / 2); Button(window, true);
        Move(window, 1500, 1100); Button(window, false);
        switch (e)
        {
            case MarkdownView md: md.Markdown = "small"; break;
            case TextArea text: text.Text = "small"; break;
            case RichTextBox rich: rich.Text = "small"; break;
            case LogView log: log.Text = "small"; break;
            case TreeView tree: tree.BeginUpdate(); foreach (var node in tree.Nodes.ToArray()) tree.RemoveNode(node); tree.AddNode("small"); tree.EndUpdate(); break;
            case ScrollPanel panel: panel.Layout = new SmallLayout(); panel.Arrange(panel.Bounds); break;
        }
        Assert.Equal(0, Offset(e, false)); Assert.Equal(0, Offset(e, true));
        foreach (bool axis in new[] { false, true })
        {
            Assert.False(((ThorVG.Shape)Field(e, PaintName(e, axis, false))).IsVisible());
            Assert.False(((ThorVG.Shape)Field(e, PaintName(e, axis, true))).IsVisible());
        }
        if (e is ScrollPanel p)
        {
            p.VerticalScroll = p.HorizontalScroll = ScrollMode.Always; p.Arrange(p.Bounds);
            var track = PaintBounds(p, false, false);
            Assert.True(((ThorVG.Shape)Field(p, "_scrollTrackV")).IsVisible());
            Assert.False(((ThorVG.Shape)Field(p, "_scrollThumbV")).IsVisible());
            At(window, p, track.X + track.W / 2, track.Y + 20); Button(window, true); Move(window, 1500, 1100); Button(window, false);
            Assert.Equal(0, Offset(p, false));
            p.VerticalScroll = p.HorizontalScroll = ScrollMode.Never; p.Arrange(p.Bounds);
            Assert.False(((ThorVG.Shape)Field(p, "_scrollTrackV")).IsVisible());
            Assert.False(((ThorVG.Shape)Field(p, "_scrollTrackH")).IsVisible());
        }
    });

    [Fact]
    public void ComboPopupTrackPagesThenSamePressDragsWithoutSelectingAnItem() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(900, 700, "Combo popup scrollbar", Theme.Light, RenderBackend.SW);
        var combo = new ComboBox(Enumerable.Range(0, 80).Select(i => "item " + i).ToArray()) { Bounds = new(40, 40, 300, 32) };
        window.AddChild(combo); WindowRegressionOwner.Flush(window);
        At(window, combo, 20, 15); Button(window, true); Button(window, false);
        var popup = (Element)Field(combo, "_popup"); var original = combo.SelectedIndex;
        float page = popup.Bounds.H - 4;
        At(window, popup, popup.Bounds.W - 6, popup.Bounds.H - 3); Button(window, true);
        Assert.Equal(page, Number(popup, "_scroll"), 2);
        At(window, popup, popup.Bounds.W - 6, popup.Bounds.H - 13);
        Assert.True(Number(popup, "_scroll") < page); CursorIs(window, CursorType.Arrow);
        Move(window, 1500, 1100); float end = Number(popup, "_scroll"); Button(window, false);
        Move(window, 10, 10); Assert.Equal(end, Number(popup, "_scroll"));
        Assert.True(combo.IsDropDownOpen); Assert.Equal(original, combo.SelectedIndex);
    });
}
