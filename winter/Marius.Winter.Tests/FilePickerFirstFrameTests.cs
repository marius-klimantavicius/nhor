using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Marius.Winter.Blazor;
using ThorVG;
using Xunit;

namespace Marius.Winter.Tests;

public class FilePickerFirstFrameTests
{
    [Theory]
    [InlineData(false, FilePickerMode.OpenFile)]
    [InlineData(true, FilePickerMode.OpenFile)]
    [InlineData(false, FilePickerMode.SaveFile)]
    [InlineData(true, FilePickerMode.SaveFile)]
    [InlineData(false, FilePickerMode.SelectFolder)]
    [InlineData(true, FilePickerMode.SelectFolder)]
    public void ActualModulesFirstUnscrolledFramePaintsLocationEntriesActionsAndSameModalConfirmation(bool razor, FilePickerMode mode) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(960, 800, "C07 first frame", Theme.Light, RenderBackend.SW);
        Playground.Scenarios.FilesScenario? scenario = null;
        if (razor) Assert.True(window.UseBlazor().AddComponent<Blazor.Playground.Scenarios.Files>(new() { ["OwnerWindow"] = window }).IsCompletedSuccessfully);
        else { scenario = new(window); window.AddChild(scenario.Content); window.SetAutoLayout(scenario.Content); }
        WindowRegressionOwner.Flush(window);
        var text = mode switch { FilePickerMode.SaveFile => "Save file", FilePickerMode.SelectFolder => "Choose folder", _ => "Open file" };
        ModalScenarioInputTests.Walk(window).OfType<Button>().Single(b => b.Text == text).OnClick();
        var picker = ModalScenarioInputTests.Walk(window).OfType<FilePicker>().Single(p => p.Dialog.IsOpen);
        var until = DateTime.UtcNow.AddSeconds(2);
        while (picker.Controller!.State.LoadState == FilePickerLoadState.Loading && DateTime.UtcNow < until)
        { Drain(); Thread.Yield(); }
        Drain(); Assert.Equal(FilePickerLoadState.Ready, picker.Controller.State.LoadState);
        var canvas = (Canvas)typeof(Window).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        void Draw(string name)
        { WindowRegressionOwner.Flush(window); Contracts.Draw(canvas, true); Save(WindowRuntimeTests.Buffer(window), name); }
        Draw($"c07-first-{razor}-{mode}");
        var body = picker.Dialog.Body!;
        var location = ModalScenarioInputTests.Walk(body).OfType<TextBox>().First();
        var entries = ModalScenarioInputTests.Walk(body).OfType<DataTree<FilePickerEntry>>().Single();
        var action = ModalScenarioInputTests.Walk(picker.Dialog.Footer!).OfType<Button>().First(b => b.Visible);
        Assert.True(Ink(location) > 25, "The current directory must paint in the first frame.");
        Assert.True(Ink(entries) > 80, "Folder and file names must paint before scrolling.");
        Assert.True(Ink(action) > 15, "The mode-specific action must paint in the first footer frame.");
        Assert.False(mode == FilePickerMode.SelectFolder && ModalScenarioInputTests.Walk(body).OfType<Field>().Any());
        if (mode == FilePickerMode.SaveFile)
        {
            var request = picker.Controller.AcceptAsync(); while (!request.IsCompleted) Drain(); request.GetAwaiter().GetResult(); Drain();
            Assert.NotNull(picker.Controller.State.Overwrite); Assert.NotSame(body, picker.Dialog.Body);
            Draw($"c07-first-{razor}-overwrite");
            var warning = ModalScenarioInputTests.Walk(picker.Dialog.Body!).OfType<Label>().Single();
            Assert.Contains("already exists", warning.Text); Assert.True(Ink(warning) > 100, "Overwrite warning must paint in the first confirmation frame.");
            Assert.Single(ModalScenarioInputTests.Walk(window).OfType<FilePicker>(), p => p.Dialog.IsOpen);
        }
        picker.Controller.Reset(); scenario?.Dispose();
        void Drain() { while (window.DispatcherQueue.TryDequeue(out var action)) action(); }
        int Ink(Element element)
        {
            var rect = ModalScenarioInputTests.Absolute(element); var pixels = WindowRuntimeTests.Buffer(window); int count = 0;
            for (int y = Math.Max(0, (int)rect.Y + 3); y < Math.Min(800, rect.Bottom - 3); y++)
                for (int x = Math.Max(0, (int)rect.X + 4); x < Math.Min(960, rect.Right - 4); x++)
                { var c = pixels[y * 960 + x]; if ((c & 255) < 110 && ((c >> 8) & 255) < 110 && ((c >> 16) & 255) < 110) count++; }
            return count;
        }
    });
    private static void Save(uint[] pixels, string name)
    {
        var evidence = Environment.GetEnvironmentVariable("WINTER_C07_EVIDENCE"); if (string.IsNullOrEmpty(evidence)) return;
        using var output = File.Create(Path.Combine(evidence, name + ".ppm"));
        output.Write(System.Text.Encoding.ASCII.GetBytes("P6\n960 800\n255\n"));
        foreach (var pixel in pixels) { output.WriteByte((byte)(pixel >> 16)); output.WriteByte((byte)(pixel >> 8)); output.WriteByte((byte)pixel); }
    }
}
