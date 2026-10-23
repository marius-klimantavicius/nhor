using System;
using System.Reflection;
using Xunit;
using static Glfw.GLFW;

namespace Marius.Winter.Tests;

// All native tests must run under an owned display, e.g. xvfb-run -a dotnet test ...
public class SubtreeLifecycleTests
{
    [Theory]
    [InlineData("remove")]
    [InlineData("clear")]
    [InlineData("hide")]
    [InlineData("disable")]
    public void InactiveSubtreeCannotKeepFocusInputOrBlinkAnimation(string operation)
    {
        var window = new Window(160, 100, "Winter lifecycle regression");
        try
        {
            Assert.NotNull(window.GlfwWindow);
            typeof(Window).GetMethod("SetupCallbacks", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            var parent = new Panel(); var editor = new TextBox { Text = "A" }; parent.AddChild(editor); window.AddChild(parent);
            editor.Arrange(new RectF(0, 0, 100, 30)); window.SetFocus(editor);
            Assert.True(window.Animator.HasActiveAnimations);
            if (operation == "remove") window.RemoveChild(parent);
            else if (operation == "clear") window.ClearChildren();
            else if (operation == "hide") parent.Visible = false;
            else parent.Enabled = false;
            var callback = Glfw.Glfw.glfwSetCharCallback(window.GlfwWindow!, null)!;
            Glfw.Glfw.glfwSetCharCallback(window.GlfwWindow!, callback);
            callback(window.GlfwWindow!, 'B');
            Assert.Equal("A", editor.Text); Assert.Null(window.FocusedElement);
            Assert.Equal(ElementState.Normal, editor.State & ElementState.Focused);
            Assert.False(window.Animator.HasActiveAnimations);
            window.SetFocus(editor); Assert.Null(window.FocusedElement);
        }
        finally { window.Dispose(); }
    }

    [Fact]
    public void WindowAttachmentAndDetachmentReachDescendantsWithOwnerIntact()
    {
        var window = new Window(160, 100, "Winter attachment regression");
        try
        {
            var parent = new Panel(); var box = new Box(); parent.AddChild(box); int before = box.Attachments;
            window.AddChild(parent); Assert.Equal(before + 1, box.Attachments);
            window.RemoveChild(parent); Assert.Equal(1, box.Detachments); Assert.Same(window, box.DetachingWindow);
            Assert.Null(box.OwnerWindow);
        }
        finally { window.Dispose(); }
    }

    [Fact]
    public void DetachClearsCaptureHoverTooltipAndLastClickReferences()
    {
        var window = new Window(160, 100, "Winter pointer regression");
        try
        {
            var parent = new Panel(); var box = new Box(); parent.AddChild(box); window.AddChild(parent);
            string[] names = { "_capturedElement", "_hoveredElement", "_tooltipElement", "_lastClickElement" };
            foreach (string name in names) typeof(Window).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, box);
            box.State = ElementState.Hovered | ElementState.Pressed;
            window.RemoveChild(parent);
            foreach (string name in names) Assert.Null(typeof(Window).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window));
            Assert.Equal(ElementState.Normal, box.State);
        }
        finally { window.Dispose(); }
    }

    [Theory]
    [InlineData("hide", false)]
    [InlineData("disable", false)]
    [InlineData("remove", false)]
    [InlineData("hide", true)]
    [InlineData("disable", true)]
    [InlineData("remove", true)]
    public void PrebuiltAnimationStartsStopsAndResumesWithSubtreeActivity(string operation, bool gif)
    {
        var window = new Window(160, 100, "Winter animation regression");
        try
        {
            var parent = new Panel();
            Element animation;
            if (gif)
            {
                var image = new GifImage();
                // Two valid one-pixel frames with a global two-color palette.
                image.LoadFromBytes(Convert.FromHexString("47494638396101000100800000000000FFFFFF21F90400010000002C000000000100010000020244010021F90400010000002C00000000010001000002024401003B"));
                Assert.Equal(2, image.FrameCount); animation = image;
            }
            else
            {
                var lottie = new LottieImage { Source = "{\"v\":\"5.7.4\",\"fr\":30,\"ip\":0,\"op\":60,\"w\":16,\"h\":16,\"nm\":\"test\",\"ddd\":0,\"assets\":[],\"layers\":[]}" };
                Assert.True(lottie.TotalFrames > 1); animation = lottie;
            }
            parent.AddChild(animation); window.AddChild(parent);
            Assert.True(window.Animator.HasActiveAnimations);
            if (operation == "hide") parent.Visible = false;
            else if (operation == "disable") parent.Enabled = false;
            else window.RemoveChild(parent);
            Assert.False(window.Animator.HasActiveAnimations);
            if (operation == "hide") parent.Visible = true;
            else if (operation == "disable") parent.Enabled = true;
            else window.AddChild(parent);
            Assert.True(window.Animator.HasActiveAnimations);
            window.RemoveChild(parent); Assert.False(window.Animator.HasActiveAnimations);
        }
        finally { window.Dispose(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LabelStyleFontChangeUpdatesMeasurementAndRetainedText(bool mutate)
    {
        var window = new Window(160, 100, "Winter style regression");
        try
        {
            var label = new Label("Winter") { Style = new Style { FontSize = 12 } }; window.AddChild(label);
            var before = label.Measure(300, 100);
            if (mutate) label.Style.FontSize = 36;
            else label.Style = new Style { FontSize = 36 };
            Assert.True(label.IsMeasureDirty);
            var after = label.Measure(300, 100); Assert.True(after.X > before.X * 2);
            var own = Assert.IsType<ThorVG.Scene>(Assert.Single(label.Scene.Paints()));
            var textScene = Assert.IsType<ThorVG.Scene>(Assert.Single(own.Paints()));
            var text = Assert.IsType<ThorVG.Text>(Assert.Single(textScene.Paints()));
            text.GetTextSize(out float width, out _); Assert.True(width > before.X * 2);
            window.Dirty = false; label.Style.Foreground = new Color4(1, 0, 0);
            Assert.True(window.Dirty);
        }
        finally { window.Dispose(); }
    }

    [Theory]
    [InlineData("hide", false)]
    [InlineData("disable", false)]
    [InlineData("remove", false)]
    [InlineData("hide", true)]
    [InlineData("disable", true)]
    [InlineData("remove", true)]
    [InlineData("dismiss", false)]
    [InlineData("dismiss", true)]
    public void InactivePopupOwnerSubtreeClosesItsPopupAndFadeAnimation(string operation, bool menu)
    {
        var window = new Window(160, 100, "Winter popup regression");
        try
        {
            var panel = new Panel(); Element control;
            if (menu) { var bar = new MenuBar(); bar.AddMenu("File").AddSubMenu("Recent").AddItem("Open"); control = bar; }
            else { var combo = new ComboBox(); combo.SetItems(new[] { "One", "Two" }); control = combo; }
            panel.AddChild(control); window.AddChild(panel); control.Arrange(new RectF(0, 0, 100, 30));
            if (menu) { control.OnMouseMove(5, 5); control.OnMouseDown(0, 5, 5); control.OnClick(); }
            else control.OnClick();
            var overlayField = typeof(Window).GetField("_overlays", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var popup = Assert.Single((System.Collections.Generic.List<Element>)overlayField.GetValue(window)!);
            Element? submenu = null;
            if (menu)
            {
                popup.OnMouseMove(popup.Bounds.X + 5, popup.Bounds.Y + 6);
                var open = (System.Collections.Generic.List<Element>)overlayField.GetValue(window)!;
                Assert.Equal(2, open.Count); submenu = open[1];
            }
            if (operation == "hide") panel.Visible = false;
            else if (operation == "disable") panel.Enabled = false;
            else if (operation == "dismiss") window.DismissAllOverlays();
            else window.RemoveChild(panel);
            Assert.Empty((System.Collections.Generic.List<Element>)overlayField.GetValue(window)!);
            Assert.False(window.Animator.HasActiveAnimations);
            Assert.True(popup.IsDisposed); Assert.Empty(popup.Scene.Paints());
            if (submenu != null) { Assert.True(submenu.IsDisposed); Assert.Empty(submenu.Scene.Paints()); }
        }
        finally { window.Dispose(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovingTooltipOrContextMenuSourceDisposesTemporaryOverlay(bool tooltip)
    {
        var window = new Window(160, 100, "Winter transient overlay regression");
        try
        {
            var panel = new Panel(); var box = new Box { Tooltip = "Details", ContextMenu = new Menu().AddItem("Open") };
            panel.AddChild(box); window.AddChild(panel);
            if (tooltip)
            {
                typeof(Window).GetMethod("UpdateTooltipTarget", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, new object[] { box, 5f, 5f });
                typeof(Window).GetMethod("ShowTooltip", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            }
            else typeof(Window).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, new object[] { box, 5f, 5f });
            var overlays = (System.Collections.Generic.List<Element>)typeof(Window).GetField("_overlays", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            var overlay = Assert.Single(overlays);
            window.RemoveChild(panel);
            Assert.Empty(overlays); Assert.False(window.Animator.HasActiveAnimations);
            Assert.True(overlay.IsDisposed); Assert.Empty(overlay.Scene.Paints());
        }
        finally { window.Dispose(); }
    }

    [Fact]
    public void ThemeRefreshDoesNotRaiseWindowResizeWithoutBoundsChange()
    {
        var window = new Window(160, 100, "Winter theme regression");
        try
        {
            int resized = 0; window.Resized = () => resized++;
            window.Theme = Theme.Light;
            Assert.Equal(0, resized);
        }
        finally { window.Dispose(); }
    }
}
