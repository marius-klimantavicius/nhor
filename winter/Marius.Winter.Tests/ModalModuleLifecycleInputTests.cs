using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Marius.Winter.Blazor;
using Xunit;

namespace Marius.Winter.Tests;

public class ModalModuleLifecycleInputTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothModuleGuardsOpenerRemovalConditionalRecreateAndResetAreInteractive(bool razor) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(800, 620, "C06 module lifecycle", Theme.Light, RenderBackend.SW);
        if (razor) Assert.True(window.UseBlazor().AddComponent<Blazor.Playground.Scenarios.WindowsAndDialogs>(new Dictionary<string, object> { ["OwnerWindow"] = window }).IsCompletedSuccessfully);
        else
        {
            var scenario = new Playground.Scenarios.WindowsAndDialogsScenario(window);
            window.AddChild(scenario.Content); window.SetAutoLayout(scenario.Content);
        }
        IEnumerable<Element> All() => ModalScenarioInputTests.Walk(window);
        ModalDialog Modal() => All().OfType<ModalDialog>().Single();
        IEnumerable<Element> Content() => ModalScenarioInputTests.Walk(Modal().ModalRoot);
        var input = Task.Run(async () =>
        {
            try
            {
                using var x = new SelectionX11(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!));
                async Task Click(Func<Element> find)
                {
                    RectF r = default; await window.Dispatcher.InvokeAsync(() => r = ModalScenarioInputTests.Absolute(find()));
                    x.Move((int)r.X + 8, (int)r.Y + 8); x.Click(); await Task.Delay(65);
                }
                Element BackgroundButton(string text) => All().OfType<Button>().Single(b => b.Text == text);
                Element ModalButton(string text) => Content().OfType<Button>().Single(b => b.Text == text);
                await Task.Delay(160); x.Focus();
                await Click(() => BackgroundButton("Edit profile"));
                x.Key(0xff57); x.Key((nuint)'z');
                await Click(() => Content().OfType<Checkbox>().Single());
                x.Key(0xff1b); x.Key(0xff1b); await Task.Delay(430);
                await window.Dispatcher.InvokeAsync(() =>
                {
                    Assert.True(Modal().IsOpen); Assert.False(Modal().IsClosing);
                    Assert.Contains(Content().OfType<Label>(), l => l.Text == "Unsaved edits: Save, or turn off protection to discard them.");
                });
                await Click(() => Content().OfType<Checkbox>().Single());
                await Click(() => ModalButton("Remove opener"));
                x.Key(0xff1b); await Task.Delay(65);
                await window.Dispatcher.InvokeAsync(() => { Assert.False(Modal().IsOpen); Assert.NotNull(window.FocusedElement); Assert.True(window.FocusedElement!.IsActive); Assert.DoesNotContain(All().OfType<Button>(), b => b.Text == "Edit profile"); });
                await Click(() => BackgroundButton("Reset"));
                await Click(() => BackgroundButton("Edit profile"));
                var removed = Modal();
                await Click(() => ModalButton("Remove dialog"));
                await window.Dispatcher.InvokeAsync(() => { Assert.True(removed.IsDisposed); Assert.DoesNotContain(All(), e => e is ModalDialog); });
                await Click(() => BackgroundButton("Recreate dialog"));
                await Click(() => BackgroundButton("Edit profile"));
                await window.Dispatcher.InvokeAsync(() => { Assert.True(Modal().IsOpen); Assert.NotSame(removed, Modal()); });
                await Click(() => ModalButton("Cancel"));
                await Click(() => All().OfType<Checkbox>().Single()); // enable backdrop closing
                await Click(() => BackgroundButton("Edit profile"));
                x.Move(3, 3); x.Click(); await Task.Delay(65);
                await window.Dispatcher.InvokeAsync(() => { Assert.False(Modal().IsOpen); Assert.Contains(All().OfType<Label>(), l => l.Text == "Canceled (Backdrop); kept Marius."); });
                await Click(() => BackgroundButton("Modeless Settings"));
                await window.Dispatcher.InvokeAsync(() => Assert.Contains(All().OfType<DialogWindow>(), d => d.Title == "Modeless Settings"));
                x.CloseNormally();
            }
            catch { window.Close(); throw; }
        });
        window.Run(); input.GetAwaiter().GetResult();
    });
}
