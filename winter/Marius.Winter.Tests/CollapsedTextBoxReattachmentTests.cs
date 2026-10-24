using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
namespace Marius.Winter.Tests;
public class CollapsedTextBoxReattachmentTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(RenderBackend.SW)] [InlineData(RenderBackend.GL)]
    public void ScrolledEditorClampsOnResizeAndHiddenReplacementSurvivesReattachment(RenderBackend backend) => WindowRegressionOwner.Run(() =>
    {
        const string shortText = "Restored Žąsis e\u0301 · current text";
        string longText = string.Join(" · ", Enumerable.Repeat("Žąsis e\u0301 current text", 12));
        using var window = new Window(480, 220, "C09 scrolled collapsed reattachment", Theme.Light, backend);
        var panel = new Panel { Bounds = new RectF(0, 0, 480, 220), Padding = new Thickness(0) };
        var box = new TextBox(longText) { Bounds = new RectF(20, 20, 180, 36), ReadOnly = true };
        panel.AddChild(box); window.AddChild(panel);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        float Scroll() => (float)typeof(TextBox).GetField("_scrollOffset", flags)!.GetValue(box)!;
        float Cursor() => (float)typeof(TextBox).GetMethod("GetCursorX", flags)!.Invoke(box, null)!;
        var input = Task.Run(async () =>
        {
            try
            {
                using var x = new FormX11(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!));
                using var screen = new MenuScreen(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!));
                await Task.Delay(160); x.Focus();
                float original = 0;
                await window.Dispatcher.InvokeAsync(() =>
                {
                    original = Scroll(); Assert.True(original > 1000);
                    box.Arrange(new RectF(20, 20, 0, 22)); panel.RemoveChild(box); panel.AddChild(box);
                    Assert.Equal(original, Scroll());
                    box.Arrange(new RectF(20, 20, 380, 36));
                    Assert.InRange(Scroll(), 1, original - 100);
                    Assert.InRange(Cursor() - Scroll(), 0, 380 - 2 * 36 * .3f);
                });
                await Task.Delay(60); x.Capture($"reattach-long-{backend}-wide", 480, 220);
                await window.Dispatcher.InvokeAsync(() =>
                {
                    box.Arrange(new RectF(20, 20, 100, 36));
                    Assert.True(Scroll() > original);
                    Assert.InRange(Cursor() - Scroll(), 0, 100 - 2 * 36 * .3f);
                    box.Arrange(new RectF(20, 20, 0, 22));
                    box.Text = ""; box.ResetText(shortText); panel.RemoveChild(box); panel.AddChild(box);
                    box.Arrange(new RectF(20, 20, 380, 36));
                    Assert.Equal(shortText, box.Text); Assert.Equal(0, Scroll());
                });
                var until = DateTime.UtcNow.AddSeconds(2);
                while (screen.Read(480, 220).Ink(new RectF(24, 24, 370, 28), false) < 250 && DateTime.UtcNow < until) await Task.Delay(25);
                Assert.True(screen.Read(480, 220).Ink(new RectF(24, 24, 370, 28), false) >= 250);
                x.Capture($"reattach-long-{backend}-hidden-reset", 480, 220);
                x.Move(50, 35); x.Click(); x.Chord(0xffe3, 0x61); x.Chord(0xffe3, 0x63); await Task.Delay(50);
                await window.Dispatcher.InvokeAsync(() =>
                {
                    Assert.Equal(shortText, Glfw.Glfw.glfwGetClipboardString(null));
                    var caret = (ThorVG.Shape)typeof(TextBox).GetField("_cursorShape", flags)!.GetValue(box)!;
                    var selection = (ThorVG.Shape)typeof(TextBox).GetField("_selectionShape", flags)!.GetValue(box)!;
                    caret.Bounds(out _, out _, out var cw, out var ch); Assert.Equal(1, cw, 2); Assert.Equal(30, ch, 2);
                    selection.Bounds(out _, out _, out var sw, out var sh); Assert.True(sw > 150); Assert.Equal(30, sh, 2);
                });
                x.Capture($"reattach-long-{backend}-selection", 480, 220); x.CloseNormally();
            }
            catch { window.Close(); throw; }
        });
        window.Run(); input.GetAwaiter().GetResult(); Assert.True(window.IsDisposed);
    });

    [Theory]
    [InlineData(RenderBackend.SW, false)] [InlineData(RenderBackend.GL, false)]
    [InlineData(RenderBackend.SW, true)] [InlineData(RenderBackend.GL, true)]
    public void AttachedEditorReinsertAtCollapsedWidthThenRestoreKeepsItsCompleteText(RenderBackend backend, bool collapse)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                const string value = "Restored Žąsis e\u0301 · current text";
                using var w = new Window(480, 220, "C09 independent collapsed reattachment", Theme.Light, backend);
                var panel = new Panel { Bounds = new RectF(0, 0, 480, 220), Padding = new Thickness(0) };
                var box = new TextBox(value) { Bounds = new RectF(20, 20, 380, 36), ReadOnly = true };
                panel.AddChild(box); w.AddChild(panel);
                var input = Task.Run(async () =>
                {
                    try
                    {
                        using var x = new FormX11(Glfw.Glfw.glfwGetX11Window(w.GlfwWindow!));
                        using var screen = new MenuScreen(Glfw.Glfw.glfwGetX11Window(w.GlfwWindow!));
                        await Task.Delay(180); x.Focus();
                        byte[] Crop()
                        {
                            var f = screen.Read(480, 220); return Enumerable.Range(24, 28).SelectMany(y => f.Pixels.Skip(y * f.Stride + 24 * 4).Take(370 * 4)).ToArray();
                        }
                        var expected = Crop(); x.Capture($"reattach-{backend}-{collapse}-before", 480, 220);
                        await w.Dispatcher.InvokeAsync(() => { if (collapse) box.Arrange(new RectF(20, 20, 0, 22)); });
                        await Task.Delay(80);
                        await w.Dispatcher.InvokeAsync(() => { panel.RemoveChild(box); panel.AddChild(box); box.Arrange(new RectF(20, 20, 380, 36)); });
                        var until = DateTime.UtcNow.AddSeconds(2); bool equal;
                        while (!(equal = expected.SequenceEqual(Crop())) && DateTime.UtcNow < until) await Task.Delay(25);
                        x.Capture($"reattach-{backend}-{collapse}-after", 480, 220);
                        output.WriteLine("{0} collapse={1}; complete={2}; Text={3}; scrollOffset={4}", backend, collapse, equal, box.Text, typeof(TextBox).GetField("_scrollOffset", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(box));
                        x.CloseNormally();
                        Assert.Equal(value, box.Text); Assert.True(equal, "Full text fits the restored width; reattachment must retain all original glyph pixels.");
                    }
                    catch { w.Close(); throw; }
                });
                w.Run(); input.GetAwaiter().GetResult();
            }
            catch (Exception e) { failure = e; }
        });
        thread.Start(); Assert.True(thread.Join(15000));
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
