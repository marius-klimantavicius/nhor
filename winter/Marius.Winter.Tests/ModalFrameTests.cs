using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Marius.Winter.Tests;

public class ModalFrameTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CaptionCentersOnTheFrameAndReservesCloseSpaceWhenNarrow(bool dark) => WindowRegressionOwner.Run(() =>
    {
        var theme = dark ? Theme.Dark : Theme.Light;
        theme.Label.FontSize = theme.Panel.FontSize = 22;
        using var window = new Window(640, 480, "Modal frame geometry", theme, RenderBackend.SW);
        using var body = new TextBox("Borrowed editor");
        using var modal = new ModalDialog { Title = "Edit profile", Body = body, IsOpen = true };
        window.AddChild(modal);
        WindowRegressionOwner.Flush(window);
        var title = ModalScenarioInputTests.Walk(modal.ModalRoot).OfType<Label>().Single();
        var close = ModalScenarioInputTests.Walk(modal.ModalRoot).OfType<Button>().Single();
        var caption = ModalScenarioInputTests.Absolute(title);
        var card = modal.DialogBounds;
        Assert.InRange(Math.Abs(caption.X + caption.W / 2 - (card.X + card.W / 2)), 0, 1);
        Assert.InRange(caption.Y, card.Y, card.Y + 16);
        var closeBounds = ModalScenarioInputTests.Absolute(close);
        Assert.True(caption.Right + 6 <= closeBounds.X, "The centered caption must leave a gap before the close target.");
        Assert.True(closeBounds.W >= 28 && closeBounds.H >= 28, "Vector X keeps a usable pointer and keyboard target.");

        modal.Title = "Review the winter workspace profile and display preferences before saving";
        modal.Width = 270;
        modal.Height = 240;
        modal.UpdatePlacement();
        caption = ModalScenarioInputTests.Absolute(title);
        closeBounds = ModalScenarioInputTests.Absolute(close);
        Assert.InRange(Math.Abs(caption.X + caption.W / 2 - (modal.DialogBounds.X + modal.DialogBounds.W / 2)), 0, 1);
        Assert.True(caption.Right + 6 <= closeBounds.X, "Long titles cannot paint underneath the X.");
        Assert.InRange(caption.Bottom, modal.DialogBounds.Y, modal.DialogBounds.Y + modal.DialogBounds.H / 2);
        Assert.True(body.Bounds.H > 0, "A wrapped title must leave body space in a short dialog.");

        modal.ShowCloseButton = false;
        modal.UpdatePlacement();
        Assert.False(close.Visible);
        Assert.True(title.Bounds.W > caption.W, "Hiding the X gives the caption its space back.");
        modal.ShowCloseButton = true;
        modal.UpdatePlacement();
        Assert.True(close.Visible);
    });

    [Fact]
    public void HeaderCloseUsesNormalInputFocusAndGuardWithoutDuplicateResults() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(480, 360, "Modal frame close input", Theme.Light, RenderBackend.SW);
        var opener = new Button("Open") { Bounds = new RectF(8, 8, 80, 32) };
        using var body = new TextBox("Borrowed editor");
        using var modal = new ModalDialog { Title = "Edit profile", Body = body, InitialFocus = () => body };
        opener.Clicked = () => modal.IsOpen = true;
        window.AddChild(opener); window.AddChild(modal);
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int requests = 0, results = 0;
        modal.CloseGuard = (result, _) => { Assert.Equal(DialogCloseReason.CloseButton, result.Reason); return new(gate.Task); };
        modal.CloseRequested += _ => requests++;
        modal.Closed += result => { if (results == 0) Assert.Equal(DialogCloseReason.CloseButton, result.Reason); results++; };
        window.Closing += () => { modal.Dispose(); body.Dispose(); return ValueTask.CompletedTask; };
        var input = Task.Run(async () =>
        {
            try
            {
                using var x = new SelectionX11(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!));
                await Task.Delay(170); x.Focus(); x.Key(0xff09); x.Key(0xff0d); await Task.Delay(70);
                Button close = null!; RectF target = default;
                await window.Dispatcher.InvokeAsync(() =>
                {
                    close = ModalScenarioInputTests.Walk(modal.ModalRoot).OfType<Button>().Single();
                    target = ModalScenarioInputTests.Absolute(close);
                    Assert.Equal(CursorType.Hand, close.Cursor);
                    Assert.Same(body, window.FocusedElement);
                });
                x.Move((int)(target.X + target.W / 2), (int)(target.Y + target.H / 2)); x.Click(); x.Click();
                await Task.Delay(65);
                await window.Dispatcher.InvokeAsync(() => { Assert.True(modal.IsClosing); Assert.Equal(1, requests); Assert.Equal(0, results); Assert.Same(close, window.FocusedElement); });
                gate.SetResult(false); await Task.Delay(65);
                await window.Dispatcher.InvokeAsync(() => { Assert.True(modal.IsOpen); Assert.False(modal.IsClosing); modal.CloseGuard = null; });
                x.Key(0x20); await Task.Delay(65);
                await window.Dispatcher.InvokeAsync(() => { Assert.False(modal.IsOpen); Assert.Equal(2, requests); Assert.Equal(1, results); Assert.Same(opener, window.FocusedElement); modal.IsOpen = true; modal.ShowCloseButton = false; });
                x.Key(0xff09); await Task.Delay(50);
                await window.Dispatcher.InvokeAsync(() => { Assert.NotSame(close, window.FocusedElement); Assert.True(modal.IsOpen); modal.ShowCloseButton = true; });
                x.Key(0xff1b); await Task.Delay(50);
                x.CloseNormally();
            }
            catch { window.Close(); throw; }
        });
        window.Run(); input.GetAwaiter().GetResult();
    });
}
