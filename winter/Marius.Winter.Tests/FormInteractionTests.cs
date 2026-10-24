using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Marius.Winter.Blazor;
using Xunit;

namespace Marius.Winter.Tests;

public class FormInteractionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActualReadOnlyUnicodeInputKeepsSelectionCopyAndBlocksEveryMutation(bool multiline) => WindowRegressionOwner.Run(() =>
    {
        const string value = "Žąsis e\u0301 👩🏽‍💻";
        using var window = new Window(540, 240, "Read-only Unicode field", Theme.Light, RenderBackend.SW);
        using Element editor = multiline ? new TextArea(value) { ReadOnly = true, MinHeight = 90 } : new TextBox(value) { ReadOnly = true };
        using var field = new Field("Read-only example", editor) { ReadOnly = true };
        window.AddChild(field); window.SetAutoLayout(field);
        window.Closing += () => { field.Dispose(); editor.Dispose(); return ValueTask.CompletedTask; };
        var edits = 0;
        if (editor is TextBox text) text.UserTextChanged += _ => edits++; else ((TextArea)editor).UserTextChanged += _ => edits++;
        var input = Task.Run(async () =>
        {
            try
            {
                using var x = new FormX11(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!));
                await Task.Delay(150); x.Focus(); x.Move(20, 10); x.Click();
                x.Chord(0xffe3, 0x61); x.Chord(0xffe3, 0x63); await Task.Delay(50);
                await window.Dispatcher.InvokeAsync(() => { Assert.Same(editor, window.FocusedElement); Assert.Equal(value, Glfw.Glfw.glfwGetClipboardString(null)); });
                x.Capture($"readonly-unicode-{multiline}-selection", 540, 240);
                x.TypeAscii("blocked"); x.Key(0xffff); x.Key(0xff08); x.Chord(0xffe3, 0x78); x.Chord(0xffe3, 0x76); x.Key(0xff0d);
                x.Chord(0xffe3, 0xff50); x.Chord(0xffe1, 0xff53); x.Chord(0xffe3, 0x63); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() =>
                {
                    Assert.Equal(value, editor is TextBox box ? box.Text : ((TextArea)editor).Text);
                    Assert.Equal("Ž", Glfw.Glfw.glfwGetClipboardString(null)); Assert.Equal(0, edits);
                });
                x.CloseNormally();
            }
            catch { window.Close(); throw; }
        });
        window.Run(); input.GetAwaiter().GetResult(); Assert.True(window.IsDisposed);
    });

    [Theory]
    [InlineData(false, false, false, RenderBackend.SW)]
    [InlineData(true, false, false, RenderBackend.SW)]
    [InlineData(false, true, true, RenderBackend.SW)]
    [InlineData(true, true, true, RenderBackend.SW)]
    [InlineData(false, false, true, RenderBackend.GL)]
    [InlineData(true, false, true, RenderBackend.GL)]
    public void ActualInstallerSetupKeyboardPointerPixelsResetReplacementAndClose(bool razor, bool dark, bool large, RenderBackend backend) => WindowRegressionOwner.Run(() =>
    {
        var theme = dark ? Theme.Dark : Theme.Light;
        if (large) { theme.TextBox.FontSize = 22; theme.Label.FontSize = 22; theme.Button.FontSize = 22; theme.Panel.FontSize = 22; }
        using var window = new Window(800, 1050, "C04 Installer setup input", theme, backend);
        using var native = razor ? null : new Playground.Scenarios.InstallerSetupScenario(window);
        if (razor) Assert.True(window.UseBlazor().AddComponent<Blazor.Playground.Scenarios.InstallerSetup>().IsCompletedSuccessfully);
        else { window.AddChild(native!.Content); window.SetAutoLayout(native.Content); }
        var input = Task.Run(async () =>
        {
            try
            {
                using var x = new FormX11(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!));
                await Task.Delay(170); x.Focus();
                var prefix = $"setup-{(razor ? "razor" : "native")}-{dark}-{large}-{backend}";
                x.Capture(prefix + "-first", 800, 1050);
                async Task Click(Element element)
                {
                    float px = 0, py = 0;
                    await window.Dispatcher.InvokeAsync(() => { element.WindowToLocal(0, 0, out var lx, out var ly); px = Math.Min(12, element.Bounds.W / 2) - lx; py = element.Bounds.H / 2 - ly; });
                    x.Move((int)px, (int)py); x.Click(); await Task.Delay(55);
                }
                Field Field(string label) => InstallerConfigurationTests.Walk(window).OfType<Field>().Single(f => f.Label == label);
                Button Button(string text) => InstallerConfigurationTests.Walk(window).OfType<Button>().Single(b => b.Text == text);
                Checkbox Checkbox(string text) => InstallerConfigurationTests.Walk(window).OfType<Checkbox>().Single(c => c.Text == text);
                TextBox name = null!, destination = null!; TextArea notes = null!; ComboBox environment = null!;
                await window.Dispatcher.InvokeAsync(() =>
                {
                    name = (TextBox)Field("Application name").Editor!; destination = (TextBox)Field("Destination").Editor!;
                    notes = (TextArea)Field("Notes (optional)").Editor!; environment = (ComboBox)Field("Environment").Editor!;
                    Assert.True(name.Bounds.H >= 18); Assert.True(Field("Destination").Bounds.H > destination.Bounds.H);
                    Assert.Equal(0, InstallerConfigurationTests.Walk(window).OfType<ScrollPanel>().First().Scroll);
                });
                await Click(Field("Application name").LabelElement);
                await window.Dispatcher.InvokeAsync(() => Assert.Same(name, window.FocusedElement));
                x.Chord(0xffe3, 0x61); x.Key(0xff08); x.Key(0xff0d); await Task.Delay(90);
                await window.Dispatcher.InvokeAsync(() => { Assert.True(Field("Application name").Invalid); Assert.Same(name, window.FocusedElement); });
                await Click(destination); x.Chord(0xffe3, 0x61); x.TypeAscii("relative/path"); x.Key(0xff0d); await Task.Delay(90);
                x.Capture(prefix + "-invalid", 800, 1050);
                await window.Dispatcher.InvokeAsync(() => { Assert.True(Field("Destination").Invalid); Assert.Same(name, window.FocusedElement); });
                await Click(name); x.TypeAscii("MyApp");
                await Click(destination); x.Chord(0xffe3, 0x61); x.TypeAscii("/opt/myapp");
                await Click(environment); x.Key(0xff57); x.Key(0xff0d); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => { Assert.Equal(2, environment.SelectedIndex); Assert.False(environment.IsDropDownOpen); Assert.DoesNotContain(InstallerConfigurationTests.Walk(window).OfType<Label>(), l => l.Text.Contains("submitted #")); });
                await Click(notes); x.Key(0xff0d); x.Chord(0xffe3, 0xff0d); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => Assert.DoesNotContain(InstallerConfigurationTests.Walk(window).OfType<Label>(), l => l.Text.Contains("submitted #")));
                await Click(Checkbox("Disable notes"));
                await window.Dispatcher.InvokeAsync(() => { Assert.False(notes.IsEffectivelyEnabled); Assert.Contains("disabled", Field("Notes (optional)").LabelElement.Text); });
                await Click(Checkbox("Disable notes"));
                await Click(Button("Submit"));
                await window.Dispatcher.InvokeAsync(() => Assert.Contains(InstallerConfigurationTests.Walk(window).OfType<Label>(), l => l.Text.Contains("submitted #1:") && l.Text.Contains("development")));
                await Click(Checkbox("Read-only destination")); await Click(destination);
                x.Chord(0xffe3, 0x61); x.Chord(0xffe3, 0x63); x.TypeAscii("blocked"); x.Key(0xff08); x.Chord(0xffe3, 0x78); x.Chord(0xffe3, 0x76); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => { Assert.True(destination.ReadOnly); Assert.Equal("/opt/myapp", destination.Text); Assert.Equal("/opt/myapp", Glfw.Glfw.glfwGetClipboardString(null)); });
                x.Capture(prefix + "-readonly-selection", 800, 1050);
                await Click(Button("Reset")); await Click(Button("Replace configuration"));
                await window.Dispatcher.InvokeAsync(() => { name = (TextBox)Field("Application name").Editor!; destination = (TextBox)Field("Destination").Editor!; Assert.Equal("Replacement app", name.Text); });
                await Click(Button("Toggle notes")); await Click(Button("Toggle notes")); await Click(Button("Reset"));
                x.Resize(440, 740); await Task.Delay(90);
                x.Capture(prefix + "-narrow", 440, 740);
                await window.Dispatcher.InvokeAsync(() => { Assert.Equal("MyApp", name.Text); Assert.False(destination.ReadOnly); Assert.False(Field("Application name").Invalid); });
                x.CloseNormally();
            }
            catch { window.Close(); throw; }
        });
        window.Run(); input.GetAwaiter().GetResult(); Assert.True(window.IsDisposed);
    });
}

internal sealed class FormX11 : IDisposable
{
    private readonly SelectionX11 _input;
    private readonly nint _display;
    private readonly nuint _window;
    internal FormX11(nuint window) { _window = window; _input = new SelectionX11(window); _display = XOpenDisplay(Environment.GetEnvironmentVariable("DISPLAY")!); Assert.NotEqual(0, _display); }
    internal void Focus() => _input.Focus();
    internal void Move(int x, int y) => _input.Move(x, y);
    internal void Click() => _input.Click();
    internal void Key(nuint symbol) => _input.Key(symbol);
    internal void Chord(nuint modifier, nuint key)
    {
        var code = XKeysymToKeycode(_display, modifier); XTestFakeKeyEvent(_display, code, 1, 0); XSync(_display, 0);
        Key(key); XTestFakeKeyEvent(_display, code, 0, 0); XSync(_display, 0);
    }
    internal void TypeAscii(string text) { foreach (char c in text) Key(c); }
    internal void Resize(uint w, uint h) => _input.Resize(w, h);
    internal void CloseNormally() => _input.CloseNormally();
    internal void Capture(string name, int width, int height)
    {
        var path = Environment.GetEnvironmentVariable("WINTER_C04_EVIDENCE");
        if (string.IsNullOrEmpty(path)) return;
        Directory.CreateDirectory(path);
        var image = XGetImage(_display, _window, 0, 0, (uint)width, (uint)height, nuint.MaxValue, 2); Assert.NotEqual(0, image);
        try
        {
            var info = Marshal.PtrToStructure<XImage>(image); Assert.Equal(32, info.BitsPerPixel);
            var bytes = new byte[info.BytesPerLine * height]; Marshal.Copy(info.Data, bytes, 0, bytes.Length);
            using var stream = File.Create(Path.Combine(path, name + ".ppm"));
            var header = System.Text.Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n"); stream.Write(header);
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) { var i = y * info.BytesPerLine + x * 4; stream.WriteByte(bytes[i + 2]); stream.WriteByte(bytes[i + 1]); stream.WriteByte(bytes[i]); }
        }
        finally { XDestroyImage(image); }
    }
    public void Dispose() { _input.Dispose(); XCloseDisplay(_display); }
    [StructLayout(LayoutKind.Sequential)] private struct XImage
    {
        public int Width, Height, XOffset, Format; public nint Data;
        public int ByteOrder, BitmapUnit, BitmapBitOrder, BitmapPad, Depth, BytesPerLine, BitsPerPixel;
    }
    [DllImport("libX11.so.6")] private static extern nint XOpenDisplay(string name);
    [DllImport("libX11.so.6")] private static extern int XCloseDisplay(nint display);
    [DllImport("libX11.so.6")] private static extern int XSync(nint display, int discard);
    [DllImport("libX11.so.6")] private static extern byte XKeysymToKeycode(nint display, nuint symbol);
    [DllImport("libXtst.so.6")] private static extern int XTestFakeKeyEvent(nint display, uint key, int press, nuint delay);
    [DllImport("libX11.so.6")] private static extern nint XGetImage(nint display, nuint window, int x, int y, uint width, uint height, nuint planeMask, int format);
    [DllImport("libX11.so.6")] private static extern int XDestroyImage(nint image);
}
