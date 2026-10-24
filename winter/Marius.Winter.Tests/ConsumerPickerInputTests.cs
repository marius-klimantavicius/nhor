using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Marius.Winter.Blazor;
using Xunit;

namespace Marius.Winter.Tests;

public class ConsumerPickerInputTests
{
    private const string Svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"32\" height=\"32\"><rect width=\"32\" height=\"32\" fill=\"#2878cb\"/></svg>";
    private const string Lottie = """
{"v":"5.7.4","fr":10,"ip":0,"op":2,"w":32,"h":32,"nm":"Fixture","ddd":0,"assets":[],"layers":[{"ddd":0,"ind":1,"ty":4,"nm":"Shape","sr":1,"ks":{"o":{"a":0,"k":100},"r":{"a":0,"k":0},"p":{"a":0,"k":[16,16,0]},"a":{"a":0,"k":[0,0,0]},"s":{"a":0,"k":[100,100,100]}},"shapes":[{"ty":"rc","p":{"a":0,"k":[0,0]},"s":{"a":0,"k":[20,20]},"r":{"a":0,"k":2}},{"ty":"fl","c":{"a":0,"k":[0.2,0.6,0.8,1]},"o":{"a":0,"k":100},"r":1}],"ip":0,"op":2,"st":0,"bm":0}]}
""";
    [Fact]
    public void ActualConverterOpenPreviewConvertSaveCancelOverwriteReadListWriteErrorsAndClose() => Run(() =>
    {
        var root = Fixture(); var svg = Path.Combine(root, "cover.svg"); var png = Path.Combine(root, "cover.png");
        File.WriteAllText(svg, Svg); File.WriteAllText(Path.Combine(root, "motion.json"), Lottie); File.WriteAllText(png, "existing output");
        var provider = new LocalFixtureProvider(root);
        using var window = new Window(1000, 780, "C07 actual Converter", Theme.Light, RenderBackend.SW);
        var host = window.UseBlazor();
        Assert.True(host.AddComponent<Blazor.Converter.App>(new()
        { ["OwnerWindow"] = window, ["WorkspaceDirectory"] = root, ["SettingsDirectory"] = Path.Combine(root, "settings"), ["FileProvider"] = provider }).IsCompletedSuccessfully);
        var input = Task.Run(async () =>
        {
            try
            {
                using var x = new FormX11(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!));
                var ui = new PickerInput(window, x);
                await Task.Delay(150); x.Focus();
                await ui.ClickExternal("Browse…"); await ui.Ready();
                // Real non-directory listing failure, then recover by editing location.
                await ui.Type(() => ui.Location(), svg); await ui.ClickAction("Go");
                await ui.Wait(() => ui.Active().Controller!.State.LoadState == FilePickerLoadState.Error, "Local list error");
                await ui.Type(() => ui.Location(), root); await ui.ClickAction("Go"); await ui.Ready();
                x.Key(0xff1b); await ui.Wait(() => !ui.Pickers().Any(p => p.Dialog.IsOpen), "Cancel source picker");
                Assert.Equal("existing output", File.ReadAllText(png));
                await ui.ClickExternal("Browse…"); await ui.Ready();
                // Stat succeeds for an existing unreadable file; the real app read reports failure.
                if (OperatingSystem.IsLinux()) File.SetUnixFileMode(svg, UnixFileMode.None);
                await ui.Type(() => ui.Name(), "cover.svg"); await ui.ClickAction("Open");
                await ui.Wait(() => ui.Status().Contains("Read error:"), "Visible read error");
                if (OperatingSystem.IsLinux()) File.SetUnixFileMode(svg, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                await ui.ClickExternal("Reload source"); await ui.Wait(() => ui.Status().Contains("Loaded cover.svg"), "Read retry");
                await ui.ClickExternal("Convert"); await ui.Wait(() => ui.Status().Contains("Converted to PNG"), "Actual PNG conversion");
                await ui.Capture("c07-converter-png", 1000, 780);
                await ui.ClickExternal("Save As…"); await ui.Ready(); await ui.ClickAction("Save"); await ui.Overwrite();
                await ui.ClickAction("Keep existing"); await ui.ClickAction("Cancel");
                Assert.Equal("existing output", File.ReadAllText(png));
                // Ordinary write permission failure after explicit overwrite; app retains bytes for retry.
                if (OperatingSystem.IsLinux()) File.SetUnixFileMode(png, UnixFileMode.UserRead);
                await ui.ClickExternal("Save As…"); await ui.Ready(); await ui.ClickAction("Save"); await ui.Overwrite(); await ui.ClickAction("Overwrite");
                await ui.Wait(() => ui.Status().Contains("Write error:"), "Visible write error");
                if (OperatingSystem.IsLinux()) File.SetUnixFileMode(png, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                await ui.ClickExternal("Retry"); await ui.Ready(); await ui.ClickAction("Save"); await ui.Overwrite(); await ui.ClickAction("Overwrite");
                await ui.Wait(() => ui.Status().Contains("Saved to"), "Write retry");
                Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, File.ReadAllBytes(png).Take(8));
                await ui.ClickExternal("Browse…"); await ui.Ready(); await ui.Type(() => ui.Name(), "motion.json"); await ui.ClickAction("Open");
                await ui.Wait(() => ui.Status().Contains("Loaded motion.json"), "Lottie loaded");
                await ui.ClickExternal("Convert"); await ui.Wait(() => ui.Status().Contains("Converted to GIF"), "Actual GIF conversion");
                await ui.ClickExternal("Save As…"); await ui.Ready(); await ui.ClickAction("Save");
                await ui.Wait(() => ui.Status().Contains("Saved to"), "GIF saved");
                Assert.Equal("GIF89a", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(root, "motion.gif")), 0, 6));
                await ui.Capture("c07-converter-gif", 1000, 780);
                await window.Dispatcher.InvokeAsync(() => provider.HoldNext = true);
                await ui.ClickExternal("Browse…"); await ui.Wait(() => ui.Active().Controller!.State.LoadState == FilePickerLoadState.Loading, "Consumer pending listing");
                x.CloseNormally();
            }
            catch { using var failure = new FormX11(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!)); failure.Capture("c07-converter-failure", 1000, 780); window.Close(); throw; }
        });
        window.Run(); input.GetAwaiter().GetResult(); Assert.True(provider.PendingToken.IsCancellationRequested); Assert.False(provider.Disposed);
        Assert.True(File.Exists(Path.Combine(root, "settings", "sidebar.txt"))); Directory.Delete(root, true);
    });

    [Fact]
    public void ActualInstallerLocalPathBrowseCancelResetValidationSimulationCancelAndOwnerClose() => Run(() =>
    {
        var root = Fixture(); var chosen = Path.Combine(root, "Applications"); Directory.CreateDirectory(chosen);
        var provider = new LocalFixtureProvider(root);
        using var window = new Window(800, 1000, "C07 actual Installer", Theme.Light, RenderBackend.SW);
        Assert.True(window.UseBlazor().AddComponent<Blazor.InstallerExample.App>(new() { ["WorkspaceDirectory"] = root, ["FileProvider"] = provider }).IsCompletedSuccessfully);
        var input = Task.Run(async () =>
        {
            try
            {
                using var x = new FormX11(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!)); var ui = new PickerInput(window, x);
                await Task.Delay(130); x.Focus();
                await ui.ClickExternal("Browse…"); await ui.Ready(); x.Key(0xff1b);
                await ui.Wait(() => !ui.Pickers().Any(p => p.Dialog.IsOpen), "Installer cancel");
                await window.Dispatcher.InvokeAsync(() => Assert.Equal(new PickerPath(root), ui.Path().Value));
                await ui.Type(() => ui.Path().TextEditor, "relative/path"); await ui.ClickExternal("Install");
                await ui.Wait(() => ui.Labels().Any(l => l.Text.Contains("Enter an absolute destination")), "Real EditContext path validation");
                await ui.ClickExternal("Reset");
                await ui.ClickExternal("Browse…"); await ui.Ready(); x.Key(0xff50);
                await ui.ClickAction("Select folder");
                await ui.Wait(() => ui.Path().Value == new PickerPath(chosen), "Actual local folder bound");
                await ui.Capture("c07-installer-path", 800, 1000);
                await ui.ClickExternal("Install"); await ui.Wait(() => ui.Labels().Any(l => l.Text == "Installing..."), "Simulated installation");
                await ui.ClickExternal("Cancel");
                await ui.Wait(() => ui.Labels().Any(l => l.Text.Contains("Simulation canceled.")), "Cooperative simulation canceled");
                await window.Dispatcher.InvokeAsync(() => Assert.Equal(new PickerPath(chosen), ui.Path().Value));
                await window.Dispatcher.InvokeAsync(() => provider.HoldNext = true);
                await ui.ClickExternal("Browse…"); await ui.Wait(() => ui.Active().Controller!.State.LoadState == FilePickerLoadState.Loading, "Installer pending list"); x.CloseNormally();
            }
            catch { using var failure = new FormX11(Glfw.Glfw.glfwGetX11Window(window.GlfwWindow!)); failure.Capture("c07-installer-failure", 800, 1000); window.Close(); throw; }
        });
        window.Run(); input.GetAwaiter().GetResult(); Assert.True(provider.PendingToken.IsCancellationRequested); Assert.False(provider.Disposed);
        Assert.Empty(Directory.GetFiles(chosen)); Directory.Delete(root, true);
    });

    private static string Fixture()
    {
        var evidence = Environment.GetEnvironmentVariable("WINTER_C07_EVIDENCE") ?? Path.Combine(Environment.CurrentDirectory, "tmp", "winter-components");
        var path = Path.Combine(evidence, "consumer-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path;
    }
    private static void Run(Action action)
    {
        Exception? error = null; var owner = new Thread(() => { try { action(); } catch (Exception e) { error = e; } }) { IsBackground = true };
        owner.Start(); Assert.True(owner.Join(TimeSpan.FromSeconds(45)), "The bounded real-consumer input sequence did not finish.");
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
    private sealed class LocalFixtureProvider(string root) : IFilePickerProvider, IDisposable
    {
        private readonly LocalFilePickerProvider _local = new(root);
        public bool HoldNext, Disposed;
        public CancellationToken PendingToken;
        public PickerPath Normalize(string path) => _local.Normalize(path);
        public PickerPath? Parent(PickerPath path) => _local.Parent(path);
        public PickerPath Child(PickerPath directory, string name) => _local.Child(directory, name);
        public async Task<IReadOnlyList<FilePickerEntry>> ListAsync(PickerPath directory, CancellationToken token)
        {
            if (HoldNext) { HoldNext = false; PendingToken = token; await Task.Delay(3000, token); }
            return await _local.ListAsync(directory, token);
        }
        public Task<FilePickerEntry?> GetEntryAsync(PickerPath path, CancellationToken token) => _local.GetEntryAsync(path, token);
        public void Dispose() => Disposed = true;
    }
    private sealed class PickerInput(Window window, FormX11 x)
    {
        private IEnumerable<Element> Walk(Element e) => ModalScenarioInputTests.Walk(e);
        public IEnumerable<FilePicker> Pickers() => Walk(window).OfType<FilePicker>();
        public FilePicker Active() => Pickers().Single(p => p.Dialog.IsOpen);
        public PathPicker Path() => Walk(window).OfType<PathPicker>().Single();
        public IEnumerable<Label> Labels() => Walk(window).OfType<Label>();
        public string Status() => Labels().Select(l => l.Text).FirstOrDefault(t => t.StartsWith("Success:") || t.StartsWith("Error:") || t.StartsWith("Information:")) ?? "";
        public TextBox Location() => Walk(Active().Dialog.Body!).OfType<TextBox>().First();
        public TextBox Name() => (TextBox)Walk(Active().Dialog.Body!).OfType<Field>().Single(f => f.Label == "File name").Editor!;
        public async Task Wait(Func<bool> predicate, string message)
        {
            try { await FilePickerInputTests.Until(window, predicate, message); }
            catch (TimeoutException)
            {
                var state = await window.Dispatcher.InvokeAsync(() => string.Join(" | ", Labels().Select(l => l.Text)) + " | " + string.Join(" | ", Pickers().Where(p => p.Dialog.IsOpen).Select(p => p.Controller!.State.ToString())));
                throw new TimeoutException(message + " | " + state);
            }
        }
        public Task Ready() => Wait(() => Active().Controller!.State.LoadState == FilePickerLoadState.Ready, "Picker ready");
        public Task Overwrite() => Wait(() => Active().Controller!.State.Overwrite != null, "Explicit overwrite confirmation");
        public Task ClickExternal(string text) => Click(() => Walk(window).OfType<Button>().Single(b => b.Text == text));
        public Task ClickAction(string text) => Click(() => Walk(Active().Dialog.Body!).Concat(Walk(Active().Dialog.Footer!)).OfType<Button>().Single(b => b.Text == text));
        private async Task Click(Func<Element> element)
        {
            var frame = typeof(Window).GetField("_lastFrameTime", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var previous = await window.Dispatcher.InvokeAsync(() => (double)frame.GetValue(window)!);
            await Wait(() => (double)frame.GetValue(window)! > previous, "Current layout presented before pointer input");
            var bounds = await window.Dispatcher.InvokeAsync(() => ModalScenarioInputTests.Absolute(element()));
            x.Move((int)bounds.X + Math.Min(12, (int)bounds.W / 2), (int)bounds.Y + Math.Min(12, (int)bounds.H / 2)); x.Click(); await Task.Delay(40);
        }
        public async Task Type(Func<TextBox> field, string text)
        {
            await Click(() => field()); x.Chord(0xffe3, (nuint)'a');
            foreach (char c in text) { if (char.IsUpper(c)) x.Chord(0xffe1, char.ToLowerInvariant(c)); else x.Key(c); }
            await Task.Delay(35);
        }
        public async Task Capture(string name, int width, int height)
        {
            var frame = typeof(Window).GetField("_lastFrameTime", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var previous = await window.Dispatcher.InvokeAsync(() => (double)frame.GetValue(window)!);
            // Lottie/GIF playback legitimately dirties every frame. A subsequent
            // owning-loop turn proves the preceding draw/swap has finished.
            await Wait(() => (double)frame.GetValue(window)! > previous, "Consumer frame presented");
            x.Capture(name, width, height);
        }
    }
}
