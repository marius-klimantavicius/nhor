using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ThorVG;
using Xunit;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace Marius.Winter.Tests;

public class CommonControlMeasurementOwnershipTests(ITestOutputHelper output)
{
    // Keep the reviewer's ordinary six-update scenarios separate from the
    // supplemental helper/scene contracts below.
    [Theory]
    [InlineData("label")]
    [InlineData("combo")]
    [InlineData("log")]
    public async Task CommonControlTemporaryMeasurementsReleaseFontAcquisitions(string kind)
    {
        using var pump = new NativePump();
        await pump.Call(window =>
        {
            WithFont(kind == "log" ? "monospace" : "default", font =>
            {
                int before = font.sharing;
                using Element control = kind switch
                {
                    "label" => new Label("Winter"),
                    "combo" => new ComboBox(),
                    _ => new LogView(),
                };
                window.AddChild(control);
                control.Arrange(new RectF(0, 0, 180, 100));
                for (int i = 0; i < 6; i++)
                {
                    if (control is Label label) label.Text = "Winter " + i;
                    if (control is ComboBox combo) combo.SetItems(["one", "two"]);
                    if (control is LogView log) log.Text = "Winter " + i;
                    control.InvalidateMeasure();
                    control.Measure(180, 100);
                }
                control.Dispose();
                output.WriteLine($"{kind}: font sharing before={before} after={font.sharing}");
                Assert.Equal(before, font.sharing);
            });
        });
    }

    [Theory]
    [InlineData("label")]
    [InlineData("wrapped-label")]
    [InlineData("bold-label")]
    [InlineData("combo")]
    [InlineData("log")]
    [InlineData("button")]
    [InlineData("checkbox")]
    [InlineData("dialog")]
    [InlineData("tabs")]
    [InlineData("menu")]
    public async Task AttachedUpdatesKeepOnlySceneOwnedFontLeases(string kind)
    {
        using var pump = new NativePump();
        await pump.Call(window => WithFont(FontName(kind), font =>
        {
            int before = font.sharing;
            using var control = CreateControl(kind, "Winter");
            window.AddChild(control);
            for (int i = 0; i < 3; i++)
            {
                UpdateControl(control, "Winter " + i);
                control.InvalidateMeasure();
                control.Measure(180, 100);
                // Changing size exercises positioning as well as MeasureCore.
                control.Bounds = new RectF(0, 0, 180 + i, 100 + i);
                control.Arrange(control.Bounds);
            }
            AssertOnlySceneLeases(control, font, before);
            control.Dispose();
            Assert.Equal(before, font.sharing);
        }));
    }

    [Theory]
    [InlineData("label", "")]
    [InlineData("label", "Winter")]
    [InlineData("combo", "")]
    [InlineData("combo", "Winter")]
    [InlineData("checkbox", "")]
    [InlineData("checkbox", "Winter")]
    [InlineData("tabs", "")]
    [InlineData("tabs", "Winter")]
    [InlineData("log", "")]
    [InlineData("log", "Winter")]
    public async Task MeasurementsBeforeAttachmentReleaseLocalFontLeases(string kind, string text)
    {
        using var pump = new NativePump();
        await pump.Call(_ => WithFont(FontName(kind), font =>
        {
            int before = font.sharing;
            using var control = CreateControl(kind, text);
            for (int i = 0; i < 3; i++)
            {
                control.InvalidateMeasure();
                control.Measure(180, 100);
                if (control is TabWidget tabs)
                    Invoke(tabs, "MeasureTabWidth", 0);
                if (control is LogView log)
                {
                    Invoke(log, "MeasureTextWidth", text);
                    Invoke(log, "MeasureLineBounds", text, 0f, 0f, 0f, 0f);
                }
                Assert.Equal(before, font.sharing);
            }
        }));
    }

    [Theory]
    [InlineData("button", "")]
    [InlineData("button", " ")]
    [InlineData("checkbox", "")]
    [InlineData("checkbox", " ")]
    [InlineData("dialog", "")]
    [InlineData("dialog", " ")]
    [InlineData("combo", "")]
    [InlineData("combo", " ")]
    public async Task EmptyAndZeroInkPositioningRetainsOnlySceneFontLeases(string kind, string text)
    {
        using var pump = new NativePump();
        await pump.Call(window => WithFont("default", font =>
        {
            int before = font.sharing;
            using var control = CreateControl(kind, text);
            window.AddChild(control);
            control.Measure(180, 100);
            control.Bounds = new RectF(0, 0, 180, 100);
            control.Arrange(control.Bounds);
            // A space selects a font but has no ink: the positioning helpers
            // return after Bounds, rather than at their empty-string guard.
            foreach (var paint in SceneTexts(control.Scene))
            {
                paint.Bounds(out _, out _, out float width, out float height);
                Assert.True(width <= 0 || height <= 0);
            }
            AssertOnlySceneLeases(control, font, before);
            control.Dispose();
            Assert.Equal(before, font.sharing);
        }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LogWidthAndBoundsHelpersBalanceFontsWhileAttached(bool bounds)
    {
        using var pump = new NativePump();
        await pump.Call(window => WithFont("monospace", font =>
        {
            using var log = new LogView { Text = "Winter" };
            window.AddChild(log);
            log.Arrange(new RectF(0, 0, 180, 100));
            int before = font.sharing;
            foreach (string text in new[] { "", " ", "Winter" })
            {
                if (bounds) Invoke(log, "MeasureLineBounds", text, 0f, 0f, 0f, 0f);
                else Invoke(log, "MeasureTextWidth", text);
                Assert.Equal(before, font.sharing);
            }
        }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ComboPopupPositioningBalancesFontsBeforeAndAfterAttachment(bool attached)
    {
        using var pump = new NativePump();
        await pump.Call(window => WithFont("default", font =>
        {
            int before = font.sharing;
            using var owner = new ComboBox();
            using var popup = new ComboBoxPopup(owner, ["", " ", "Winter"], -1);
            if (attached) window.ShowOverlay(popup);
            popup.Arrange(new RectF(0, 0, 180, 100));
            popup.Arrange(new RectF(0, 0, 190, 110));
            AssertOnlySceneLeases(popup, font, before);
            popup.Dispose();
            Assert.Equal(before, font.sharing);
        }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MenuPopupMeasurementBalancesFontsAndPreservesScenePaints(bool attached)
    {
        using var pump = new NativePump();
        await pump.Call(window => WithFont("default", font =>
        {
            int before = font.sharing;
            using var popup = CreateMenuPopup(window);
            if (attached) window.ShowOverlay(popup);
            // ShowOverlay refreshes style and can create retained paints. The
            // measurement itself must preserve that existing scene unchanged.
            var paints = SceneTexts(popup.Scene).ToArray();
            int beforeMeasure = font.sharing;
            for (int i = 0; i < 3; i++)
            {
                var size = popup.MeasurePopup();
                Assert.True(size.X > 0 && size.Y > 0);
                Assert.Equal(paints, SceneTexts(popup.Scene).ToArray());
                Assert.Equal(beforeMeasure, font.sharing);
            }
            popup.Dispose();
            Assert.Equal(before, font.sharing);
        }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MenuPopupPositioningRetainsOnlySceneFonts(bool attached)
    {
        using var pump = new NativePump();
        await pump.Call(window => WithFont("default", font =>
        {
            int before = font.sharing;
            using var popup = CreateMenuPopup(window);
            if (attached) window.ShowOverlay(popup);
            popup.Arrange(new RectF(0, 0, 180, 100));
            popup.Arrange(new RectF(0, 0, 190, 110));
            AssertOnlySceneLeases(popup, font, before);
            popup.Dispose();
            Assert.Equal(before, font.sharing);
        }));
    }

    [Theory]
    [InlineData(false, "")]
    [InlineData(false, " ")]
    [InlineData(false, "Winter")]
    [InlineData(true, "")]
    [InlineData(true, " ")]
    [InlineData(true, "Winter")]
    public async Task TooltipMeasurementReleasesItsLocalPaint(bool attached, string text)
    {
        using var pump = new NativePump();
        await pump.Call(window => WithFont("default", font =>
        {
            int before = font.sharing;
            using var tooltip = new TooltipOverlay(text);
            if (attached) window.ShowOverlay(tooltip);
            tooltip.PositionNear(10, 10, 200, 120, window.Theme);
            tooltip.PositionNear(20, 20, 200, 120, window.Theme);
            if (text.Length == 0)
            {
                Assert.Empty(SceneTexts(tooltip.Scene));
                Assert.Equal(before, font.sharing);
            }
            else AssertOnlySceneLeases(tooltip, font, before);
            tooltip.Dispose();
            Assert.Equal(before, font.sharing);
        }));
    }

    private static string FontName(string kind) => kind switch
    {
        "log" => "monospace",
        "bold-label" => "default-bold",
        _ => "default",
    };

    private static Element CreateControl(string kind, string text)
    {
        switch (kind)
        {
            case "label": return new Label(text);
            case "wrapped-label": return new Label(text) { TextWrapping = TextWrap.Word };
            case "bold-label": return new Label(text) { Bold = true };
            case "combo": return new ComboBox([text, "Other"]);
            case "log": return new LogView { Text = text };
            case "button": return new Button(text);
            case "checkbox": return new Checkbox(text);
            case "dialog": return new DialogWindow(text);
            case "tabs":
                var tabs = new TabWidget();
                tabs.AddTab(text, new Panel());
                tabs.AddTab("Other", new Panel());
                return tabs;
            case "menu":
                var menu = new MenuBar();
                menu.AddMenu(text);
                menu.AddMenu("Other");
                return menu;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private static void UpdateControl(Element control, string text)
    {
        switch (control)
        {
            case Label label: label.Text = text; break;
            case ComboBox combo: combo.SetItems([text, "Other"]); combo.SelectedIndex = 0; break;
            case LogView log: log.Text = text; break;
            case Button button: button.Text = text; break;
            case Checkbox checkbox: checkbox.Text = text; break;
            case DialogWindow dialog: dialog.Title = text; break;
            case TabWidget tabs: tabs.UpdateTab(tabs.Children[0], text); tabs.ActiveTab = 1; break;
            case MenuBar menu: menu.UpdateMenu(menu.Menus[0], text); break;
        }
    }

    private static MenuPopup CreateMenuPopup(Window window)
    {
        var menu = new Menu();
        menu.AddItem("").AddItem(" ").AddSeparator();
        menu.AddSubMenu("Winter").AddItem("Recent");
        return new MenuPopup(window, menu.Items);
    }

    private static void WithFont(string name, Action<FontLoader> action)
    {
        var font = Assert.IsAssignableFrom<FontLoader>(LoaderMgr.Font(name));
        try { action(font); }
        finally { LoaderMgr.Retrieve(font); }
    }

    private static IEnumerable<ThorVG.Text> SceneTexts(Scene scene)
    {
        foreach (var paint in scene.Paints())
        {
            if (paint is ThorVG.Text text) yield return text;
            if (paint is Scene child)
                foreach (var descendant in SceneTexts(child)) yield return descendant;
        }
    }

    private static void AssertOnlySceneLeases(Element control, FontLoader font, int before)
    {
        var paints = SceneTexts(control.Scene).ToArray();
        Assert.NotEmpty(paints);
        foreach (var paint in paints)
        {
            Assert.True(paint.RefCnt() > 0);
            Assert.Same(font, typeof(ThorVG.Text).GetField("loader", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(paint));
        }
        Assert.Equal(before + paints.Length, font.sharing);
    }

    private static object? Invoke(Element control, string method, params object?[] args) =>
        control.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, args);
}
