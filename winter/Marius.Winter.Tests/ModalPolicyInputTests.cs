using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;

namespace Marius.Winter.Tests;

public class ModalPolicyInputTests
{
    [Fact]
    public void CommitHandlerReopeningTheDialogCannotAcceptTheNewSessionWithTheOldEnter() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(400, 300, "C06 reentrant commit identity", backend: RenderBackend.SW);
        using var editor = new TextBox("Draft");
        using var modal = new ModalDialog { Body = editor, InitialFocus = () => editor, IsOpen = true };
        int defaults = 0, results = 0;
        modal.DefaultAction = _ => { defaults++; return ValueTask.FromResult(true); };
        modal.Closed += _ => results++;
        editor.TextCommitted += _ => modal.ResetSession(true);
        window.AddChild(modal); long opening = modal.SessionId;
        window.Closing += () => { modal.Dispose(); editor.Dispose(); return ValueTask.CompletedTask; };
        var input = Task.Run(async () =>
        {
            try
            {
                using var x = new SelectionX11(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!));
                await Task.Delay(160); x.Focus(); x.Key(0xff57); x.Key((nuint)'x'); x.Key(0xff0d); await Task.Delay(80);
                await window.Dispatcher.InvokeAsync(() => { Assert.True(modal.SessionId > opening); Assert.True(modal.IsOpen); Assert.Equal(0, defaults); Assert.Equal(0, results); });
                x.CloseNormally();
            }
            catch { window.Close(); throw; }
        });
        window.Run(); input.GetAwaiter().GetResult();
    });

    [Fact]
    public void AChildSubmitBoundaryConsumesDefaultEnterOnceAfterCurrentTextCommit() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(400, 300, "C06 child submit routing", backend: RenderBackend.SW);
        using var editor = new TextBox("Draft"); using var boundary = new SubmitBoundary();
        boundary.AddChild(editor);
        using var modal = new ModalDialog { Body = boundary, InitialFocus = () => editor, IsOpen = true };
        var defaults = 0; var committed = "";
        editor.TextCommitted += args => committed = args.Text;
        modal.DefaultAction = _ => { defaults++; return ValueTask.FromResult(true); };
        window.AddChild(modal);
        window.Closing += () => { modal.Dispose(); boundary.Dispose(); return ValueTask.CompletedTask; };
        var input = Task.Run(async () =>
        {
            try
            {
                using var x = new SelectionX11(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!));
                await Task.Delay(160); x.Focus(); x.Key(0xff57); x.Key((nuint)'x'); x.Key(0xff0d); await Task.Delay(80);
                await window.Dispatcher.InvokeAsync(() => { Assert.Equal("Draftx", committed); Assert.Equal(1, boundary.Submits); Assert.Equal(0, defaults); Assert.True(modal.IsOpen); });
                x.CloseNormally();
            }
            catch { window.Close(); throw; }
        });
        window.Run(); input.GetAwaiter().GetResult();
    });

    // Only a test boundary; C04 owns the reusable Form API and validation.
    private sealed class SubmitBoundary : Panel
    {
        internal int Submits;
        public override void OnKeyDown(int key, int mods, bool repeat)
        { if (key == 257 || key == 335) { if (!repeat) Submits++; return; } base.OnKeyDown(key, mods, repeat); }
    }

    [Fact]
    public void RepeatedKeysAwaitedGuardBackdropPolicyAndRichMultilineUseNativeRouting() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(600, 460, "C06 policy input", Theme.Light, RenderBackend.SW);
        using var rich = new RichTextBox("A rich biography");
        using var combo = new ComboBox(Enumerable.Range(1, 30).Select(i => $"Workspace {i:00}").ToArray());
        using var modal = new ModalDialog { Body = rich, InitialFocus = () => rich, IsOpen = true };
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int requests = 0, rejections = 0, results = 0, defaults = 0;
        CancellationToken token = default;
        modal.CloseGuard = (_, ct) => { token = ct; return new ValueTask<bool>(gate.Task); };
        modal.DefaultAction = _ => { defaults++; return ValueTask.FromResult(true); };
        modal.CloseRequested += _ => requests++; modal.CloseRejected += _ => rejections++; modal.Closed += _ => results++;
        window.AddChild(modal);
        window.Closing += () => { modal.Dispose(); rich.Dispose(); combo.Dispose(); return ValueTask.CompletedTask; };
        var input = Task.Run(async () =>
        {
            try
            {
                using var x = new SelectionX11(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!));
                await Task.Delay(170); x.Focus(); x.Key(0xff0d); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => { Assert.Contains("\n", rich.Text); Assert.True(modal.IsOpen); Assert.Equal(0, defaults); });
                x.Move(3, 3); x.Click(); await Task.Delay(50);
                await window.Dispatcher.InvokeAsync(() => Assert.Equal(0, requests));
                x.Key(0xff1b); x.Key(0xff1b); await Task.Delay(70);
                await window.Dispatcher.InvokeAsync(() => { Assert.True(modal.IsClosing); Assert.Equal(1, requests); Assert.Equal(0, results); });
                gate.SetResult(false); await Task.Delay(70);
                await window.Dispatcher.InvokeAsync(() => { Assert.Equal(1, rejections); Assert.True(modal.IsOpen); Assert.False(modal.IsClosing); });
                gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
                x.Key(0xff1b); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => { Assert.True(modal.IsClosing); modal.ResetSession(true); Assert.True(token.IsCancellationRequested); });
                gate.SetResult(true); await Task.Delay(70);
                await window.Dispatcher.InvokeAsync(() => { Assert.True(modal.IsOpen); Assert.Equal(0, results); modal.CloseGuard = null; modal.CloseOnBackdrop = true; });
                x.Move(3, 3); x.Click(); await Task.Delay(70);
                await window.Dispatcher.InvokeAsync(() => { Assert.False(modal.IsOpen); Assert.Equal(1, results); Assert.Equal(0, defaults); });
                await window.Dispatcher.InvokeAsync(() => { modal.Body = combo; modal.InitialFocus = () => combo; modal.IsOpen = true; });
                x.Key(0x20); await Task.Delay(60);
                RectF popup = default;
                await window.Dispatcher.InvokeAsync(() => popup = ((Element)typeof(ComboBox).GetField("_popup", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(combo)!).Bounds);
                x.Move((int)popup.Right - 5, (int)popup.Y + 10);
                using (new HeldPointer())
                {
                    await Task.Delay(55);
                    await window.Dispatcher.InvokeAsync(() =>
                    {
                        var capture = typeof(Window).GetField("_capturedElement", BindingFlags.Instance | BindingFlags.NonPublic)!;
                        Assert.NotNull(capture.GetValue(window));
                        modal.IsOpen = false;
                        Assert.False(combo.IsDropDownOpen); Assert.Null(capture.GetValue(window)); Assert.Equal(1, results);
                    });
                }
                await Task.Delay(50);
                x.CloseNormally();
            }
            catch { window.Close(); throw; }
        });
        window.Run(); input.GetAwaiter().GetResult();
    });

    private sealed class HeldPointer : IDisposable
    {
        private readonly nint _display;
        internal HeldPointer()
        { _display = XOpenDisplay(Environment.GetEnvironmentVariable("DISPLAY")!); Assert.NotEqual(0, _display); XTestFakeButtonEvent(_display, 1, 1, 0); XSync(_display, 0); }
        public void Dispose() { XTestFakeButtonEvent(_display, 1, 0, 0); XSync(_display, 0); XCloseDisplay(_display); }
        [DllImport("libX11.so.6")] private static extern nint XOpenDisplay(string name);
        [DllImport("libX11.so.6")] private static extern int XCloseDisplay(nint display);
        [DllImport("libX11.so.6")] private static extern int XSync(nint display, int discard);
        [DllImport("libXtst.so.6")] private static extern int XTestFakeButtonEvent(nint display, uint button, int press, nuint delay);
    }
}
