using System;
using System.Reflection;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ThorVG;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace Marius.Winter.Tests;

public class CollapsedTextBoxTests
{
    private const string Value = "Žąsis e\u0301 👩🏽‍💻";
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AttachedHiddenZeroAndTinyWidthsRetainUnicodeSelectionAndRestorePaint(bool hidden) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(480, 220, "Attached collapsed textbox", Theme.Light, RenderBackend.SW);
        var panel = new Panel { Bounds = new RectF(0, 0, 480, 220), Padding = new Thickness(0) };
        var box = new TextBox(Value, "Name or type") { Bounds = new RectF(20, 20, 380, 36) }; panel.AddChild(box); window.AddChild(panel);
        box.OnKeyDown(65, 2, false); Draw(window);
        var initialPixels = Pixels(window);
        var before = Selected(box); var caret = Field<Shape>(box, "_cursorShape"); var selection = Field<Shape>(box, "_selectionShape");
        Assert.Equal(Value, before); int cursor = Field<int>(box, "_cursorPos"), anchor = Field<int>(box, "_selectionStart");
        if (hidden) panel.Visible = false;
        foreach (float width in new float[] { 0, 1, 10, 0 })
        {
            box.Arrange(new RectF(20, 20, width, 22)); Draw(window);
            Assert.Equal(Value, box.Text); Assert.Equal(cursor, Field<int>(box, "_cursorPos")); Assert.Equal(anchor, Field<int>(box, "_selectionStart"));
            Assert.Equal(Value, Selected(box));
            caret.Bounds(out _, out _, out var cw, out var ch); Assert.True(cw <= 0 || ch <= 0, "A collapsed text viewport must not retain the old caret rectangle.");
            selection.Bounds(out _, out _, out var sw, out var sh); Assert.True(sw <= 0 || sh <= 0, "A collapsed text viewport must not retain the old selection rectangle.");
        }
        panel.Visible = true; box.Arrange(new RectF(20, 20, 380, 36)); Draw(window);
        Assert.Equal(initialPixels, Pixels(window));
        Assert.Equal(Value, Selected(box)); selection.Bounds(out _, out _, out var widthAfter, out var heightAfter); Assert.True(widthAfter > 30); Assert.Equal(30, heightAfter, 2);
        box.OnKeyDown(262, 0, false); window.SetFocus(box); Draw(window);
        caret.Bounds(out var cx, out _, out var restoredWidth, out var restoredHeight); Assert.Equal(1, restoredWidth, 2); Assert.Equal(30, restoredHeight, 2); Assert.InRange(cx, 20, 400);
    });

    [Theory]
    [InlineData(RenderBackend.SW)] [InlineData(RenderBackend.GL)]
    public void ActualInputCopyEditReadonlyAndNormalCloseSurviveCollapseAndReturn(RenderBackend backend) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(480, 220, "Collapsed textbox actual input", Theme.Light, backend);
        var panel = new Panel { Bounds = new RectF(0, 0, 480, 220), Padding = new Thickness(0) };
        var box = new TextBox(Value) { Bounds = new RectF(20, 20, 380, 36) }; panel.AddChild(box); window.AddChild(panel);
        int edits = 0, commits = 0; box.UserTextChanged += _ => edits++; box.TextCommitted += _ => commits++;
        var input = Task.Run(async () =>
        {
            try
            {
                using var x = new FormX11(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!)); await Task.Delay(160); x.Focus(); x.Move(40, 35); x.Click(); x.Chord(0xffe3, 0x61); x.Chord(0xffe3, 0x63); await Task.Delay(50);
                async Task SettledText(string stage)
                {
                    var until = DateTime.UtcNow.AddSeconds(2); int ink;
                    while ((ink = FrontInk(window)) < 90 && DateTime.UtcNow < until) await Task.Delay(25);
                    Assert.True(ink >= 90, $"{stage} must paint actual glyph ink in the settled front buffer; observed {ink} dark pixels.");
                }
                await SettledText("Initial selection");
                await window.Dispatcher.InvokeAsync(() =>
                {
                    Assert.Equal(Value, Glfw.Glfw.glfwGetClipboardString(null));
                    box.Arrange(new RectF(20, 20, 0, 22)); Assert.Equal(Value, Selected(box)); Assert.Equal(Value, box.Text);
                });
                await Task.Delay(40);
                await window.Dispatcher.InvokeAsync(() => { box.Arrange(new RectF(20, 20, 380, 36)); panel.Visible = true; window.SetFocus(box); Assert.Equal(Value, Selected(box)); Assert.Equal(0, edits); Assert.Equal(0, commits); });
                await SettledText("Restored selection");
                x.Capture($"collapsed-{backend}-selection-restored", 480, 220); x.Key(0xff53); x.TypeAscii("X"); x.Key(0xff0d); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => { Assert.Equal(Value + "x", box.Text); Assert.Equal(1, edits); Assert.Equal(1, commits); box.ReadOnly = true; });
                x.Chord(0xffe3, 0x61); x.Chord(0xffe3, 0x63); x.TypeAscii("blocked"); x.Key(0xffff); await Task.Delay(50);
                await window.Dispatcher.InvokeAsync(() => { Assert.Equal(Value + "x", box.Text); Assert.Equal(Value + "x", Glfw.Glfw.glfwGetClipboardString(null)); Assert.Equal(1, edits); Assert.Equal(1, commits); });
                await SettledText("Read-only selection");
                x.Capture($"collapsed-{backend}-readonly-restored", 480, 220); x.CloseNormally();
            }
            catch { window.Close(); throw; }
        });
        window.Run(); input.GetAwaiter().GetResult(); Assert.True(window.IsDisposed);
    });
    private static T Field<T>(TextBox box, string name) => (T)typeof(TextBox).GetField(name, Private)!.GetValue(box)!;
    private static string Selected(TextBox box) => (string)typeof(TextBox).GetMethod("GetSelectedText", Private)!.Invoke(box, null)!;
    private static uint[] Pixels(Window window) => Enumerable.Range(20, 36).SelectMany(y => WindowRuntimeTests.Buffer(window).Skip(y * 480 + 20).Take(380)).ToArray();
    private static void Draw(Window window) { WindowRegressionOwner.Flush(window); Contracts.Draw((Canvas)typeof(Window).GetField("_canvas", Private)!.GetValue(window)!, true); }
    private static int FrontInk(Window window)
    {
        var display = XOpenDisplay(Environment.GetEnvironmentVariable("DISPLAY")!); Assert.NotEqual(0, display);
        try
        {
            var image = XGetImage(display, Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!), 20, 20, 380, 36, nuint.MaxValue, 2); Assert.NotEqual(0, image);
            try
            {
                var info = Marshal.PtrToStructure<XImage>(image); Assert.Equal(32, info.BitsPerPixel); var bytes = new byte[info.BytesPerLine * 36]; Marshal.Copy(info.Data, bytes, 0, bytes.Length); int count = 0;
                for (int y = 0; y < 36; y++) for (int x = 0; x < 380; x++) { int i = y * info.BytesPerLine + x * 4; if (bytes[i] < 100 && bytes[i+1] < 100 && bytes[i+2] < 100) count++; }
                return count;
            }
            finally { XDestroyImage(image); }
        }
        finally { XCloseDisplay(display); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct XImage { internal int Width, Height, XOffset, Format; internal nint Data; internal int ByteOrder, BitmapUnit, BitmapBitOrder, BitmapPad, Depth, BytesPerLine, BitsPerPixel; }
    [DllImport("libX11.so.6")] private static extern nint XOpenDisplay(string name);
    [DllImport("libX11.so.6")] private static extern int XCloseDisplay(nint display);
    [DllImport("libX11.so.6")] private static extern nint XGetImage(nint display, nuint window, int x, int y, uint width, uint height, nuint mask, int format);
    [DllImport("libX11.so.6")] private static extern int XDestroyImage(nint image);
}
