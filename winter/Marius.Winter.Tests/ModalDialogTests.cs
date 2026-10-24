using System;
using System.Threading.Tasks;
using Xunit;

namespace Marius.Winter.Tests;

public class ModalDialogTests
{
    [Fact]
    public void NativeRoutingTrapsFocusAndBlocksBackgroundThenRestoresOpener() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(480, 320, "C06 modal routing", Theme.Light, RenderBackend.SW);
        var backgroundClicks = 0;
        var opener = new Button("Edit profile") { Bounds = new RectF(10, 10, 130, 36) };
        var background = new Button("Background counter") { Bounds = new RectF(10, 60, 170, 36), Clicked = () => backgroundClicks++ };
        using var editor = new TextBox("Marius");
        using var cancel = new Button("Cancel");
        using var modal = new ModalDialog { Title = "Edit profile", Body = editor, Footer = cancel, InitialFocus = () => editor };
        var closed = 0;
        modal.Closed += _ => closed++;
        opener.Clicked = () => modal.IsOpen = true;
        cancel.Clicked = () => _ = modal.RequestCloseAsync(DialogCloseReason.Cancel);
        window.AddChild(opener); window.AddChild(background); window.AddChild(modal);
        window.Closing += () => { modal.Dispose(); editor.Dispose(); cancel.Dispose(); return ValueTask.CompletedTask; };
        var input = Task.Run(async () =>
        {
            try
            {
                using var x = new SelectionX11(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!));
                await Task.Delay(180); x.Focus(); x.Key(0xff09); x.Key(0xff0d); await Task.Delay(80);
                await window.Dispatcher.InvokeAsync(() => Assert.Same(editor, window.FocusedElement));
                x.Key(0xff09); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => Assert.Same(cancel, window.FocusedElement));
                x.Key(0xff09); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => { Assert.NotSame(opener, window.FocusedElement); Assert.NotSame(background, window.FocusedElement); });
                x.Key(0xff09); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => Assert.Same(editor, window.FocusedElement));
                x.Move(45, 78); x.Click(); await Task.Delay(70);
                await window.Dispatcher.InvokeAsync(() => { Assert.Equal(0, backgroundClicks); Assert.True(modal.IsOpen); });
                x.Key(0xff1b); await Task.Delay(70);
                await window.Dispatcher.InvokeAsync(() => { Assert.False(modal.IsOpen); Assert.Equal(1, closed); Assert.Same(opener, window.FocusedElement); });
                x.Move(45, 78); x.Click(); await Task.Delay(70);
                await window.Dispatcher.InvokeAsync(() => Assert.Equal(1, backgroundClicks));
                x.CloseNormally();
            }
            catch { window.Close(); throw; }
        });
        window.Run(); input.GetAwaiter().GetResult();
    });
}
