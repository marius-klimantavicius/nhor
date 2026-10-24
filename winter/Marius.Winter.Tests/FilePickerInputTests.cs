using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Marius.Winter.Blazor;
using Xunit;

namespace Marius.Winter.Tests;

public class FilePickerInputTests
{
    [Theory]
    [InlineData(false, RenderBackend.SW, false, false)]
    [InlineData(true, RenderBackend.SW, false, false)]
    [InlineData(false, RenderBackend.SW, true, true)]
    [InlineData(true, RenderBackend.SW, true, true)]
    [InlineData(false, RenderBackend.GL, false, false)]
    [InlineData(true, RenderBackend.GL, false, false)]
    public void ActualFilesModulesKeyboardPointerThreeModesRetryOverwriteResizeAndClose(bool razor, RenderBackend backend, bool dark, bool large) => WindowRegressionOwner.Run(() =>
    {
        var theme = dark ? Theme.Dark : Theme.Light;
        if (large) foreach (var style in new[] { theme.Label, theme.Panel, theme.Button, theme.TextBox, theme.Checkbox }) style.FontSize = 22;
        var name = $"c07-{(razor ? "razor" : "native")}-{backend}-{dark}-{large}";
        using var window = new Window(960, 800, name, theme, backend);
        Playground.Scenarios.FilesScenario? scenario = null;
        if (razor) Assert.True(window.UseBlazor().AddComponent<Blazor.Playground.Scenarios.Files>(new() { ["OwnerWindow"] = window }).IsCompletedSuccessfully);
        else { scenario = new(window); window.AddChild(scenario.Content); window.SetAutoLayout(scenario.Content); }
        IEnumerable<Element> Walk(Element e) => ModalScenarioInputTests.Walk(e);
        FilePicker Active() => Walk(window).OfType<FilePicker>().Single(p => p.Dialog.IsOpen);
        Button External(string text) => Walk(window).OfType<Button>().Single(b => b.Text == text);
        Button Action(string text) => Walk(Active().Dialog.Footer!).Concat(Walk(Active().Dialog.Body!)).OfType<Button>().Single(b => b.Text == text);
        string Result() => Walk(window).OfType<Label>().Select(l => l.Text).FirstOrDefault(t => t.StartsWith("OpenFile:") || t.StartsWith("SaveFile:") || t.StartsWith("SelectFolder:") || t.StartsWith("Canceled;")) ?? "";
        var input = Task.Run(async () =>
        {
            try
            {
                using var x = new FormX11(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!));
                async Task Wait(Func<bool> predicate, string message) => await Until(window, predicate, message);
                async Task Click(Func<Element> element)
                {
                    var frame = typeof(Window).GetField("_lastFrameTime", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    var previous = await window.Dispatcher.InvokeAsync(() => (double)frame.GetValue(window)!);
                    await Wait(() => (double)frame.GetValue(window)! > previous, "Current layout presented before pointer input");
                    var bounds = await window.Dispatcher.InvokeAsync(() => ModalScenarioInputTests.Absolute(element()));
                    x.Move((int)(bounds.X + Math.Min(12, bounds.W / 2)), (int)(bounds.Y + Math.Min(12, bounds.H / 2))); x.Click();
                    await Task.Delay(35);
                }
                async Task Capture(string suffix, int width = 960, int height = 800)
                {
                    var frame = typeof(Window).GetField("_lastFrameTime", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    var previous = await window.Dispatcher.InvokeAsync(() => (double)frame.GetValue(window)!);
                    await Wait(() => (double)frame.GetValue(window)! > previous, "Frame presented before capture");
                    x.Capture(name + suffix, width, height);
                }
                await Task.Delay(130); x.Focus(); x.Key(0xff09); x.Key(0xff0d);
                await Wait(() => Active().Controller!.State.LoadState == FilePickerLoadState.Ready, "Open listing ready");
                await Capture("-open");
                x.Key(0xff50); x.Key(0xff54); x.Key(0xff54); x.Key(0xff0d);
                await Wait(() => Result().Contains("OpenFile: /Files/cover.svg"), "Keyboard open result");
                await Click(() => External("Save file"));
                await Wait(() => Active().Controller!.State.LoadState == FilePickerLoadState.Ready, "Save listing ready");
                x.Key(0xff0d);
                await Wait(() => Active().Controller!.State.Overwrite != null, "Explicit overwrite state");
                await Capture("-overwrite");
                await Click(() => Action("Keep existing"));
                await Wait(() => Active().Controller!.State.Overwrite == null, "Overwrite rejected");
                await Click(() => Action("Save"));
                await Wait(() => Active().Controller!.State.Overwrite != null, "Overwrite repeated");
                await Click(() => Action("Overwrite"));
                await Wait(() => Result().Contains("SaveFile: /Files/cover.svg") && Result().Contains("confirmed"), "Overwrite accepted result");
                await Click(() => External("Save file"));
                await Wait(() => Active().Controller!.State.LoadState == FilePickerLoadState.Ready, "Save new ready");
                x.Chord(0xffe3, (nuint)'a'); x.TypeAscii("export.svg"); x.Key(0xff0d);
                await Wait(() => Result().Contains("SaveFile: /Files/export.svg"), "Typed filename saved");
                await Click(() => External("Choose folder"));
                await Wait(() => Active().Controller!.State.LoadState == FilePickerLoadState.Ready, "Folder listing ready");
                x.Key(0xff50); x.Key(0xff0d);
                await Wait(() => Active().Controller!.CurrentDirectory.Value == "/Files/Documents" && Active().Controller!.State.LoadState == FilePickerLoadState.Ready, "Enter navigates folder");
                await Click(() => Action("Back"));
                await Wait(() => Active().Controller!.CurrentDirectory.Value == "/Files" && Active().Controller!.State.LoadState == FilePickerLoadState.Ready, "Back navigates");
                await Capture("-folder");
                await Click(() => Action("Select folder"));
                await Wait(() => Result().Contains("SelectFolder: /Files"), "Folder accepted");
                await Click(() => External("Retryable listing"));
                await Wait(() => Active().Controller!.State.LoadState == FilePickerLoadState.Error, "Ordinary listing error visible");
                await Click(() => Action("Retry listing"));
                await Wait(() => Active().Controller!.State.LoadState == FilePickerLoadState.Ready, "Retry listing succeeded");
                // The actual filter popup is keyboard controlled.
                var filterBounds = await window.Dispatcher.InvokeAsync(() => ModalScenarioInputTests.Absolute(Walk(Active().Dialog.Body!).OfType<ComboBox>().Single()));
                x.Move((int)filterBounds.X + 15, (int)filterBounds.Y + 10); x.Click(); x.Key(0xff54); x.Key(0xff0d);
                await Wait(() => Active().Controller!.State.Filter?.Name == "SVG" && Active().Controller!.State.LoadState == FilePickerLoadState.Ready, "Filter committed");
                await window.Dispatcher.InvokeAsync(() => Assert.DoesNotContain(Active().Controller!.Items, i => i.Name == "motion.json"));
                x.Resize(420, 340); await Task.Delay(140);
                await window.Dispatcher.InvokeAsync(() =>
                {
                    Assert.InRange(Active().Dialog.DialogBounds.Right, 0, window.Bounds.W);
                    Assert.InRange(Active().Dialog.DialogBounds.Bottom, 0, window.Bounds.H);
                    Assert.InRange(ModalScenarioInputTests.Absolute(Action("Cancel")).Bottom, 0, window.Bounds.H);
                });
                await Capture("-narrow", 420, 340);
                x.Key(0xff1b); await Wait(() => Result().StartsWith("Canceled;"), "Escape cancels");
                x.Resize(960, 800); await Task.Delay(100);
                await Click(() => External("Reset"));
                var path = await window.Dispatcher.InvokeAsync(() => Walk(window).OfType<PathPicker>().Single());
                await Click(() => Walk(window).OfType<Checkbox>().Single(c => c.Text == "Read-only destination"));
                await Click(() => External("Browse…"));
                await window.Dispatcher.InvokeAsync(() => { Assert.True(path.ReadOnly); Assert.False(path.Picker.Dialog.IsOpen); Assert.Equal(new PickerPath("/Files"), path.Value); });
                await Click(() => External("Reset"));
                await Click(() => Walk(window).OfType<Checkbox>().Single(c => c.Text == "Disable destination"));
                await Click(() => External("Browse…"));
                await window.Dispatcher.InvokeAsync(() => { Assert.False(path.Enabled); Assert.False(path.Picker.Dialog.IsOpen); });
                await Click(() => External("Reset"));
                await Click(() => External("Browse…"));
                await Wait(() => path.Picker.Controller!.State.LoadState == FilePickerLoadState.Ready, "Path browse ready");
                x.Key(0xff50); await Click(() => Action("Select folder"));
                await Wait(() => path.Value == new PickerPath("/Files/Documents"), "Typed path binding from browse");
                await Click(() => External("Browse…")); await Wait(() => path.Picker.Controller!.State.LoadState == FilePickerLoadState.Ready, "Repeated path browse ready");
                x.Key(0xff1b); await Wait(() => !path.Picker.Dialog.IsOpen, "Path browse canceled");
                await window.Dispatcher.InvokeAsync(() => Assert.Equal(new PickerPath("/Files/Documents"), path.Value));
                await Click(() => External("Reset"));
                await Click(() => External("Replace pending provider"));
                await Wait(() => Walk(window).OfType<Label>().Any(l => l.Text == "Pending provider replaced; the new fixture is ready.") && Active().Controller!.State.LoadState == FilePickerLoadState.Ready, "Replacement completes newer listing");
                x.Key(0xff1b); await Task.Delay(40);
                await Click(() => External("Pending listing"));
                await Wait(() => Active().Controller!.State.LoadState == FilePickerLoadState.Loading, "Pending list visible");
                var pendingController = await window.Dispatcher.InvokeAsync(() => Active().Controller!);
                x.Key(0xff1b); await Wait(() => !pendingController.State.IsOpen, "Pending listing canceled");
                await Click(() => External("Open file"));
                await Wait(() => Active().Controller!.State.LoadState == FilePickerLoadState.Ready, "Reopen after canceled list");
                x.Key(0xff1b); await Task.Delay(40);
                await Click(() => External("Pending listing"));
                await Wait(() => Active().Controller!.State.LoadState == FilePickerLoadState.Loading, "Close while listing awaits");
                x.CloseNormally();
            }
            catch { window.Close(); throw; }
        });
        window.Run(); input.GetAwaiter().GetResult(); scenario?.Dispose(); Assert.True(window.IsDisposed);
    });

    internal static async Task Until(Window window, Func<bool> predicate, string message)
    {
        var until = DateTime.UtcNow.AddSeconds(3); Exception? last = null;
        while (DateTime.UtcNow < until)
        {
            try { if (await window.Dispatcher.InvokeAsync(predicate)) return; }
            catch (InvalidOperationException error) { last = error; }
            await Task.Delay(20);
        }
        throw new TimeoutException(message + (last == null ? "" : ": " + last.Message));
    }
}
