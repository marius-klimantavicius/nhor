using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Marius.Winter.Tests;

public class MenuInteractionTests
{
    internal static MenuPopup[] Popups(Window window) => ((System.Collections.Generic.List<Element>)typeof(Window).GetField("_overlays", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!).OfType<MenuPopup>().ToArray();

    [Fact]
    public void ModalMenuScopeConsumesChildEscapeAndEnterBeforeDialogActions() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(500, 360, "C09 modal menus", Theme.Light);
        var background = new MenuBar { Bounds = new RectF(0, 0, 500, 28) }; int outside = 0, command = 0, defaults = 0;
        background.AddMenu("Background").AddItem("Background action", () => outside++); window.AddChild(background);
        using var body = new Panel { Layout = new StackLayout { Spacing = 8 } };
        var bar = new MenuBar(); bar.AddMenu("Scoped").AddSubMenu("Zoom").AddItem("125%", () => command++); body.AddChild(bar);
        var owner = new Button("Scoped owner") { ContextMenu = new Menu().AddItem("Context action", () => command++) }; body.AddChild(owner);
        using var modal = new ModalDialog { Body = body, InitialFocus = () => owner, Width = 420, Height = 270,
            DefaultAction = _ => { defaults++; return ValueTask.FromResult(true); } };
        window.AddChild(modal); modal.IsOpen = true;
        window.Closing += () => { modal.Dispose(); body.Dispose(); return ValueTask.CompletedTask; };
        var input = Task.Run(async () =>
        {
            try
            {
                using var x = new FormX11(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!)); await Task.Delay(160); x.Focus();
                RectF bounds = default; await window.Dispatcher.InvokeAsync(() => { owner.WindowToLocal(0, 0, out var px, out var py); bounds = new RectF(-px, -py, owner.Bounds.W, owner.Bounds.H); });
                x.Move((int)bounds.X + 12, (int)bounds.Y + 10); x.Click(); x.Key(0xffc7); x.Key(0xff53); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => Assert.Equal(2, Popups(window).Length));
                x.Key(0xff1b); await Task.Delay(40); await window.Dispatcher.InvokeAsync(() => { Assert.Single(Popups(window)); Assert.True(modal.IsOpen); });
                x.Key(0xff53); x.Key(0xff0d); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => { Assert.Equal(1, command); Assert.Equal(0, defaults); Assert.Equal(0, outside); Assert.True(modal.IsOpen); Assert.Same(owner, window.FocusedElement); });
                x.Key(0xff67); await Task.Delay(40); await window.Dispatcher.InvokeAsync(() => Assert.Single(Popups(window)));
                x.Key(0xff1b); await Task.Delay(40); await window.Dispatcher.InvokeAsync(() => { Assert.Empty(Popups(window)); Assert.True(modal.IsOpen); });
                x.Key(0xff1b); await Task.Delay(50); await window.Dispatcher.InvokeAsync(() => Assert.False(modal.IsOpen));
                x.CloseNormally();
            }
            catch { window.Close(); throw; }
        });
        window.Run(); input.GetAwaiter().GetResult();
    });

    [Fact]
    public void F10EntersMenusArrowsOpenSubmenuEscapeReturnsThenRestoresOwner() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(480, 300, "C09 menu keyboard", Theme.Light, RenderBackend.SW);
        var owner = new TextBox("Preview owner") { Bounds = new RectF(10, 80, 320, 30) };
        var bar = new MenuBar { Bounds = new RectF(0, 0, 480, 28) };
        var preview = bar.AddMenu("Preview"); int zoom = 0;
        preview.AddSubMenu("Zoom").AddItem("125%", () => zoom++);
        window.AddChild(owner); window.AddChild(bar); window.SetFocus(owner);
        var input = Task.Run(async () =>
        {
            try
            {
                using var x = new FormX11(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!));
                await Task.Delay(150); x.Focus(); x.Move(40, 90); x.Click(); await Task.Delay(50);
                await window.Dispatcher.InvokeAsync(() => Assert.Same(owner, window.FocusedElement));
                x.Key(0xffc7); await Task.Delay(70);
                await window.Dispatcher.InvokeAsync(() => Assert.Single(Popups(window)));
                x.TypeAscii("z");
                await window.Dispatcher.InvokeAsync(() => Assert.Equal("Preview owner", owner.Text));
                x.Key(0xff53); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => Assert.Equal(2, Popups(window).Length));
                x.Key(0xff1b); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => { Assert.Single(Popups(window)); Assert.Equal(0, zoom); });
                x.Key(0xff53); x.Key(0xff0d); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => { Assert.Empty(Popups(window)); Assert.Equal(1, zoom); Assert.Same(owner, window.FocusedElement); });
                x.Key(0xffc7); x.Key(0xff1b); await Task.Delay(60);
                await window.Dispatcher.InvokeAsync(() => { Assert.Empty(Popups(window)); Assert.Same(owner, window.FocusedElement); });
                x.CloseNormally();
            }
            catch { window.Close(); throw; }
        });
        window.Run(); input.GetAwaiter().GetResult(); Assert.True(window.IsDisposed);
    });
}
