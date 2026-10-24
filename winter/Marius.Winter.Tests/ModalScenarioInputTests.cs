using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Threading.Tasks;
using Marius.Winter.Blazor;
using Xunit;

namespace Marius.Winter.Tests;

public class ModalScenarioInputTests
{
    [Theory]
    [InlineData(false, RenderBackend.SW, false, false)]
    [InlineData(true, RenderBackend.SW, false, false)]
    [InlineData(false, RenderBackend.SW, true, true)]
    [InlineData(true, RenderBackend.SW, true, true)]
    [InlineData(false, RenderBackend.GL, false, true)]
    [InlineData(true, RenderBackend.GL, false, true)]
    public void BothActualModulesProfileEditPopupEscapeDefaultCancelResizeAndClose(bool razor, RenderBackend backend, bool dark, bool large) => WindowRegressionOwner.Run(() =>
    {
        var theme = dark ? Theme.Dark : Theme.Light;
        if (large) foreach (var style in new[] { theme.TextBox, theme.Panel, theme.Button, theme.Label, theme.Checkbox }) style.FontSize = 22;
        var name = $"C06-{(razor ? "razor" : "native")}-{backend}-{dark}-{large}";
        using var window = new Window(800, 620, name, theme, backend);
        Playground.Scenarios.WindowsAndDialogsScenario? scenario = null;
        if (razor)
        {
            var host = window.UseBlazor();
            Assert.True(host.AddComponent<Blazor.Playground.Scenarios.WindowsAndDialogs>(new Dictionary<string, object> { ["OwnerWindow"] = window }).IsCompletedSuccessfully);
        }
        else
        {
            scenario = new Playground.Scenarios.WindowsAndDialogsScenario(window);
            window.AddChild(scenario.Content); window.SetAutoLayout(scenario.Content);
        }
        Element Root() => window;
        Button Button(string text) => Walk(Root()).OfType<Button>().Single(b => b.Text == text);
        ModalDialog Modal() => Walk(Root()).OfType<ModalDialog>().Single();
        TextBox Editor() => Walk(Modal().Body!).OfType<TextBox>().Single();
        Label Result() => Walk(Root()).OfType<Label>().Single(l => l.Text.StartsWith("Saved profile:") || l.Text.StartsWith("Canceled ("));
        var input = Task.Run(async () =>
        {
            try
            {
                using var x = new SelectionX11(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!));
                await Task.Delay(170); x.Focus(); x.Key(0xff09); x.Key(0xff0d); await Task.Delay(80);
                await window.Dispatcher.InvokeAsync(() =>
                {
                    Assert.True(Modal().IsOpen); Assert.Same(Editor(), window.FocusedElement);
                    Assert.InRange(Modal().DialogBounds.Bottom, 0, window.Bounds.H);
                });
                using (new HeldKey(0xffe3)) x.Key((nuint)'a');
                x.Key(0xff08); x.Key(0xff0d); await Task.Delay(70);
                await window.Dispatcher.InvokeAsync(() =>
                {
                    Assert.True(Modal().IsOpen);
                    Assert.Contains(Walk(Modal().Body!).OfType<Label>(), l => l.Text == "Enter a display name before saving.");
                });
                x.Key(0xff1b); await Task.Delay(60); x.Key(0xff0d); await Task.Delay(65);
                x.Key(0xff57); using (new HeldKey(0xffe1)) x.Key(0xff50);
                await Task.Delay(85);
                await window.Dispatcher.InvokeAsync(() => Assert.Equal("Marius", (string)typeof(TextBox).GetMethod("GetSelectedText", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Editor(), null)!));
                // Shift+Tab from the first editor goes to a modal action, never the opener.
                using (new HeldKey(0xffe1)) x.Key(0xff09);
                await Task.Delay(45);
                await window.Dispatcher.InvokeAsync(() => { Assert.NotSame(Button("Edit profile"), window.FocusedElement); window.SetFocus(Editor()); });
                // Current typed text is accepted through Enter, with no blur first.
                x.Key(0xff57); x.Key((nuint)'X'); x.Key(0xff0d); await Task.Delay(100);
                await window.Dispatcher.InvokeAsync(() => { Assert.False(Modal().IsOpen); Assert.Equal("Saved profile: Mariusx.", Result().Text); });
                RectF opener = default;
                await window.Dispatcher.InvokeAsync(() => opener = Absolute(Button("Edit profile")));
                x.Move((int)opener.X + 10, (int)opener.Y + 10); x.Click(); await Task.Delay(80);
                // Cancel preserves the last accepted data after another edit.
                x.Key(0xff57); x.Key((nuint)'Y'); x.Key(0xff1b); await Task.Delay(90);
                await window.Dispatcher.InvokeAsync(() => { Assert.False(Modal().IsOpen); Assert.Contains("kept Mariusx", Result().Text); });
                x.Key(0xff0d); await Task.Delay(70);
                // The background counter is visibly blocked by the modal backdrop.
                RectF counter = default;
                await window.Dispatcher.InvokeAsync(() => counter = Absolute(Button("Background counter")));
                x.Move((int)counter.X + 10, (int)counter.Y + 10); x.Click(); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => Assert.Contains(Walk(Root()).OfType<Label>(), l => l.Text == "Profile: Mariusx · Background counter: 0"));
                // Native popup routing: first Escape cancels its tentative value only.
                await window.Dispatcher.InvokeAsync(() => window.SetFocus(Walk(Modal().Body!).OfType<ComboBox>().Single()));
                x.Key(0x20); x.Key(0xff57); x.Key(0xff1b); await Task.Delay(70);
                await window.Dispatcher.InvokeAsync(() =>
                {
                    var combo = Walk(Modal().Body!).OfType<ComboBox>().Single();
                    Assert.True(Modal().IsOpen); Assert.False(combo.IsDropDownOpen); Assert.Equal(0, combo.SelectedIndex);
                    window.SetFocus(Walk(Modal().Body!).OfType<TextArea>().Single());
                });
                x.Key(0xff0d); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => { Assert.True(Modal().IsOpen); Assert.Contains("\n", Walk(Modal().Body!).OfType<TextArea>().Single().Text); });
                x.Resize(340, 240); await Task.Delay(100);
                await window.Dispatcher.InvokeAsync(() =>
                {
                    Assert.InRange(Modal().DialogBounds.X, 0, window.Bounds.W);
                    Assert.InRange(Modal().DialogBounds.Right, 0, window.Bounds.W);
                    Assert.InRange(Absolute(Walk(Modal().Footer!).OfType<Button>().First()).Bottom, 0, window.Bounds.H);
                });
                x.Key(0xff1b); await Task.Delay(70);
                await window.Dispatcher.InvokeAsync(() => Assert.False(Modal().IsOpen));
                x.Resize(800, 620); await Task.Delay(80);
                // Repeat open/close, and a normal WM_DELETE_WINDOW with active content.
                x.Key(0xff0d); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => Assert.True(Modal().IsOpen));
                x.CloseNormally();
            }
            catch { window.Close(); throw; }
        });
        window.Run(); input.GetAwaiter().GetResult(); scenario?.Dispose();
        Assert.True(window.IsDisposed);
    });

    internal static RectF Absolute(Element element)
    { element.WindowToLocal(0, 0, out var x, out var y); return new RectF(-x, -y, element.Bounds.W, element.Bounds.H); }
    internal static IEnumerable<Element> Walk(Element element)
    { yield return element; foreach (var child in element.Children) foreach (var next in Walk(child)) yield return next; }

    private sealed class HeldKey : IDisposable
    {
        private readonly nint _display;
        private readonly uint _code;
        internal HeldKey(nuint symbol)
        { _display = XOpenDisplay(Environment.GetEnvironmentVariable("DISPLAY")!); Assert.NotEqual(0, _display); _code = XKeysymToKeycode(_display, symbol); XTestFakeKeyEvent(_display, _code, 1, 0); XSync(_display, 0); }
        public void Dispose() { XTestFakeKeyEvent(_display, _code, 0, 0); XSync(_display, 0); XCloseDisplay(_display); }
        [DllImport("libX11.so.6")] private static extern nint XOpenDisplay(string display);
        [DllImport("libX11.so.6")] private static extern int XCloseDisplay(nint display);
        [DllImport("libX11.so.6")] private static extern byte XKeysymToKeycode(nint display, nuint symbol);
        [DllImport("libX11.so.6")] private static extern int XSync(nint display, int discard);
        [DllImport("libXtst.so.6")] private static extern int XTestFakeKeyEvent(nint display, uint key, int press, nuint delay);
    }
}
