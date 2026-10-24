using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Marius.Winter.Blazor.Tests;

[Collection("Native rendering")]
public class MenuBindingInputTests
{
    [Theory]
    [InlineData(RenderBackend.SW)] [InlineData(RenderBackend.GL)]
    public void CompiledModelMethodsAcceptActualPointerKeyboardAndReplacedContext(RenderBackend backend)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var window = new Window(440, 270, "C09 compiled model input", Theme.Light, backend);
                var root = new Panel { Bounds = new RectF(0, 0, 440, 270) }; window.AddChild(root);
                var host = window.UseBlazor();
                var added = host.Renderer.AddComponent<MenuQueuedTransitionProbe>(root); Assert.True(added.IsCompletedSuccessfully);
                var probe = added.Result;
                var button = (Button)root.Children.First(); button.Arrange(new RectF(10, 60, 300, 36));
                var bar = (MenuBar)root.Children.Last(); bar.Arrange(new RectF(10, 10, 400, 30));
                Element[] Popups() => ((IEnumerable<Element>)typeof(Window).GetField("_overlays", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!).Where(e => e.GetType().Name == "MenuPopup").ToArray();
                var input = Task.Run(async () =>
                {
                    try
                    {
                        var native = (Glfw.GlfwWindow)typeof(Window).GetProperty("GlfwWindow", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
                        using var x = new MenuBindingX11(Glfw.Glfw.glfwGetX11Window(native));
                        await Task.Delay(180); x.Focus(); x.Move(25, 25); x.Click(); await Task.Delay(60);
                        RectF popup = default;
                        await window.Dispatcher.InvokeAsync(() => { popup = Assert.Single(Popups()).Bounds; Assert.False(probe.Model.Checked); });
                        x.Move((int)popup.X + 50, (int)popup.Y + 15); x.Click(); await Task.Delay(80);
                        await window.Dispatcher.InvokeAsync(() =>
                        {
                            Assert.Equal(new[] { true }, probe.Model.Checks); Assert.Equal(1, probe.Model.Clicks); Assert.True(bar.Menus[0].Entries[0].IsChecked);
                            probe.RenderAgain();
                        });
                        x.Key(0xffc7); x.Key(0xff0d); await Task.Delay(80);
                        await window.Dispatcher.InvokeAsync(() => { Assert.Equal(new[] { true, false }, probe.Model.Checks); Assert.Equal(2, probe.Model.Clicks); Assert.False(probe.Model.Checked); });
                        x.Move(30, 78); x.Click(); x.Key(0xff67); x.Key(0xff57); x.Key(0xff53); x.Key(0xff0d); await Task.Delay(90);
                        await window.Dispatcher.InvokeAsync(() => { Assert.Equal(new[] { true, false, true }, probe.Model.Checks); Assert.Equal(3, probe.Model.Clicks); Assert.True(button.ContextMenu!.Entries[1].SubMenu!.Entries[0].IsChecked); });
                        x.Key(0xff67); await Task.Delay(40);
                        Menu original = null!; int borrowedCalls = 0;
                        var borrowed = new Menu().AddItem("Replacement context", () => borrowedCalls++);
                        await window.Dispatcher.InvokeAsync(() => { Assert.Single(Popups()); original = button.ContextMenu!; button.ContextMenu = borrowed; Assert.Empty(Popups()); });
                        x.Key(0xff67); x.Key(0xff0d); await Task.Delay(60);
                        await window.Dispatcher.InvokeAsync(() => { Assert.Equal(1, borrowedCalls); Assert.Equal(3, probe.Model.Clicks); button.ContextMenu = original; });
                        x.Key(0xff67); await Task.Delay(40);
                        await window.Dispatcher.InvokeAsync(() => Assert.Single(Popups()));
                        x.CloseNormally();
                    }
                    catch { window.Close(); throw; }
                });
                window.Run(); input.GetAwaiter().GetResult(); Assert.True(window.IsDisposed);
            }
            catch (Exception ex) { failure = ex; }
        }) { IsBackground = true };
        thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(12)), "Owned input fixture timed out.");
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
