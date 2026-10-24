using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Marius.Winter.Blazor;
using Marius.Winter.Tests;
using Xunit;

namespace Marius.Winter.Tests;

public class ConverterCorrectionTests
{
    [Fact]
    public void ActualConverterNormalCloseAwaitsPresentationBoundary() => ConverterClose(false);
    [Fact]
    public void ActualConverterNormalCloseAfterCompletedConversion() => ConverterClose(true);
    [Fact]
    public void ActualConverterNormalCloseCancelsQueuedOwnerConversion() => OnOwner(() =>
    {
        var root = Path.Combine(Environment.GetEnvironmentVariable("WINTER_C07_EVIDENCE")!, "queued-conversion-close"); Directory.CreateDirectory(root);
        var source = Path.Combine(root, "source.svg");
        File.WriteAllText(source, "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"32\" height=\"32\"><rect width=\"32\" height=\"32\" fill=\"#00ff00\"/></svg>");
        using var window = new Window(1000, 780, "C07 queued conversion close", Theme.Light, RenderBackend.SW);
        var host = window.UseBlazor();
        var app = host.AddComponent<Blazor.Converter.App>(new() { ["OwnerWindow"] = window, ["WorkspaceDirectory"] = root, ["SettingsDirectory"] = Path.Combine(root, "settings") }).GetAwaiter().GetResult();
        using var clicked = new System.Threading.ManualResetEventSlim();
        using var closeSent = new System.Threading.ManualResetEventSlim();
        var taskField = typeof(Blazor.Converter.App).GetField("_conversionTask", BindingFlags.Instance | BindingFlags.NonPublic)!;
        bool pendingAtRequest = false;
        var input = Task.Run(async () =>
        {
            using var x = new FormX11(Xid(window));
            try
            {
                await Task.Delay(150); x.Focus();
                await Type(window, x, () => Walk(window).OfType<PathPicker>().Single().TextEditor, source); x.Key(0xff0d);
                await Wait(window, () => Walk(window).OfType<Label>().Any(l => l.Text.Contains("Loaded source.svg")), "queued-close source loaded");
                var convert = await window.Dispatcher.InvokeAsync(() => Walk(window).OfType<Button>().Single(b => b.Text == "Convert"));
                // Observe the real pointer handler after it has started conversion.
                // Hold this input turn only until the private display has received
                // WM_DELETE. A no-op ahead of the owner's queued invocation makes
                // the run-loop close boundary deterministic without invoking it.
                await window.Dispatcher.InvokeAsync(() => convert.Clicked += () =>
                {
                    window.DispatcherQueue.Enqueue(() => { }); clicked.Set();
                    Assert.True(closeSent.Wait(TimeSpan.FromSeconds(3)), "Owned normal close must be sent.");
                });
                await Frame(window);
                var bounds = await window.Dispatcher.InvokeAsync(() => ModalScenarioInputTests.Absolute(convert));
                x.Move((int)bounds.X + 12, (int)bounds.Y + 12); x.Click();
                Assert.True(clicked.Wait(TimeSpan.FromSeconds(3)));
                var until = DateTime.UtcNow.AddSeconds(2);
                while (window.DispatcherQueue.Count < 2 && DateTime.UtcNow < until) await Task.Delay(1).ConfigureAwait(false);
                Assert.True(window.DispatcherQueue.Count >= 2, "Presentation completed and owner execution is queued.");
                pendingAtRequest = !((Task)taskField.GetValue(app)!).IsCompleted;
                Assert.True(pendingAtRequest, "The tracked task must await its queued owner invocation before WM_DELETE.");
                x.CloseNormally(); closeSent.Set();
            }
            finally { x.CloseNormally(); closeSent.Set(); }
        });
        window.Run(); input.GetAwaiter().GetResult();
        Assert.True(pendingAtRequest); Assert.True(((Task)taskField.GetValue(app)!).IsCompletedSuccessfully);
        Assert.True(window.IsDisposed); Assert.True(host.DisposeAsync().IsCompletedSuccessfully);
        Assert.Null(typeof(Blazor.Converter.App).GetField("_convertedSaveData", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(app));
        Console.WriteLine("Queued owner boundary: pending at WM_DELETE; tracked task completed; no conversion result; Window and host disposed.");
    }, timeoutSeconds: 12);
    private static void ConverterClose(bool completedFirst) => OnOwner(() =>
    {
        var root = Path.Combine(Environment.GetEnvironmentVariable("WINTER_C07_EVIDENCE")!, "conversion-close"); Directory.CreateDirectory(root);
        var source = Path.Combine(root, "source.svg");
        File.WriteAllText(source, "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"32\" height=\"32\"><rect width=\"32\" height=\"32\" fill=\"#00ff00\"/></svg>");
        using var window = new Window(1000, 780, "C07 Converter normal close", Theme.Light, RenderBackend.SW);
        Blazor.Converter.App? app = null; bool pendingAtClose = false;
        var taskField = typeof(Blazor.Converter.App).GetField("_conversionTask", BindingFlags.Instance | BindingFlags.NonPublic)!;
        window.Closing += () =>
        {
            var task = (Task)taskField.GetValue(app)!; pendingAtClose = !task.IsCompleted;
            Console.WriteLine($"Closing observer: completed_first={completedFirst}; conversion={task.Status}; pending={pendingAtClose}; shutdown={window.ShutdownToken.IsCancellationRequested}");
            return ValueTask.CompletedTask;
        };
        var host = window.UseBlazor();
        app = host.AddComponent<Blazor.Converter.App>(new() { ["OwnerWindow"] = window, ["WorkspaceDirectory"] = root, ["SettingsDirectory"] = Path.Combine(root, "settings") }).GetAwaiter().GetResult();
        var input = Task.Run(async () =>
        {
            using var x = new FormX11(Xid(window));
            try
            {
                await Task.Delay(150); x.Focus();
                await Type(window, x, () => Walk(window).OfType<PathPicker>().Single().TextEditor, source); x.Key(0xff0d);
                await Wait(window, () => Walk(window).OfType<Label>().Any(l => l.Text.Contains("Loaded source.svg")), "source before close");
                await Frame(window);
                var rect = await window.Dispatcher.InvokeAsync(() =>
                {
                    var e = Walk(window).OfType<Button>().Single(b => b.Text == "Convert"); e.WindowToLocal(0, 0, out var px, out var py);
                    return new RectF(-px, -py, e.Bounds.W, e.Bounds.H);
                });
                x.Move((int)rect.X + 12, (int)rect.Y + 12); x.Click();
                if (completedFirst) await Wait(window, () => Walk(window).OfType<Label>().Any(l => l.Text.Contains("Converted to PNG")), "complete conversion before normal close control");
            }
            finally { x.CloseNormally(); }
            await Task.Delay(250).ConfigureAwait(false);
            Console.WriteLine($"After normal close: conversion={((Task)taskField.GetValue(app)!).Status}; app_disposed={typeof(Blazor.Converter.App).GetField("_disposed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(app)}; window_disposed={window.IsDisposed}; input_driver_completed=True");
        });
        window.Run(); input.GetAwaiter().GetResult();
        var conversion = (Task)taskField.GetValue(app)!;
        Console.WriteLine($"Converter pending_at_normal_close={pendingAtClose}; task={conversion.Status}; disposed={window.IsDisposed}; host_disposal={host.DisposeAsync().IsCompletedSuccessfully}");
        Assert.Equal(!completedFirst, pendingAtClose);
        Assert.True(conversion.IsCompletedSuccessfully); Assert.True(window.IsDisposed); Assert.True(host.DisposeAsync().IsCompletedSuccessfully);
        Assert.False(File.Exists(Path.Combine(root, "source.png")));
    }, timeoutSeconds: 12);
    [Fact]
    public void ActualConverterEditedSourceMustMatchConvertedPreview() => OnOwner(() =>
    {
        var evidence = Environment.GetEnvironmentVariable("WINTER_C07_EVIDENCE")!;
        var root = Path.Combine(evidence, "independent-converter"); Directory.CreateDirectory(root);
        string Svg(string color) => $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"32\" height=\"32\"><rect width=\"32\" height=\"32\" fill=\"{color}\"/></svg>";
        var red = Path.Combine(root, "red.svg"); var blue = Path.Combine(root, "blue.svg");
        File.WriteAllText(red, Svg("#ff0000")); File.WriteAllText(blue, Svg("#0000ff"));
        using var window = new Window(1000, 780, "C07 Converter edited source", Theme.Light, RenderBackend.SW);
        var app = window.UseBlazor().AddComponent<Blazor.Converter.App>(new() { ["OwnerWindow"] = window, ["WorkspaceDirectory"] = root, ["SettingsDirectory"] = Path.Combine(root, "settings") }).GetAwaiter().GetResult();
        string? preview = null, saveName = null; bool saved = false;
        var input = Task.Run(async () =>
        {
            using var x = new FormX11(Xid(window));
            try
            {
                await Task.Delay(150); x.Focus();
                PathPicker PathControl() => Walk(window).OfType<PathPicker>().Single();
                Button Button(string text) => Walk(window).OfType<Button>().Single(b => b.Text == text);
                await Type(window, x, () => PathControl().TextEditor, red); x.Key(0xff0d);
                await Wait(window, () => Walk(window).OfType<Label>().Any(l => l.Text.Contains("Loaded red.svg")), "load red by Enter");
                await Capture(window, x, "converter-red-loaded", 1000, 780);
                await Type(window, x, () => PathControl().TextEditor, blue);
                await Click(window, x, () => Button("Convert"));
                await Wait(window, () => Walk(window).OfType<Label>().Any(l => l.Text.Contains("Converted to PNG")), "convert after editing draft source");
                preview = await window.Dispatcher.InvokeAsync(() => Walk(window).OfType<SvgImage>().Single().Svg);
                await Capture(window, x, "converter-red-preview-blue-output", 1000, 780);
                await Click(window, x, () => Button("Save As…"));
                await Wait(window, () => Walk(window).OfType<FilePicker>().Any(p => p.Dialog.IsOpen && p.Controller!.State.LoadState == FilePickerLoadState.Ready), "save listing");
                saveName = await window.Dispatcher.InvokeAsync(() => Walk(window).OfType<FilePicker>().Single(p => p.Dialog.IsOpen).Controller!.State.FileName);
                await Click(window, x, () => Walk(Walk(window).OfType<FilePicker>().Single(p => p.Dialog.IsOpen).Dialog.Footer!).OfType<Button>().Single(b => b.Text == "Save"));
                await Wait(window, () => Walk(window).OfType<Label>().Any(l => l.Text.Contains("Saved to")), "save converted draft");
                saved = File.Exists(Path.Combine(root, "blue.png"));
                await Capture(window, x, "converter-saved-blue", 1000, 780);
            }
            finally { x.CloseNormally(); }
        });
        window.Run(); input.GetAwaiter().GetResult();
        Console.WriteLine($"Converter preview={preview}; save_name={saveName}; blue_saved={saved}; normal_closed={window.IsDisposed}");
        Assert.True(saved); Assert.Equal("blue.png", saveName);
        Assert.Contains("#0000ff", preview);
    });
    internal static IEnumerable<Element> Walk(Element e)
    { yield return e; foreach (var child in e.Children) foreach (var nested in Walk(child)) yield return nested; }
    internal static nuint Xid(Window w) => Glfw.Glfw.glfwGetX11Window((Glfw.GlfwWindow)typeof(Window).GetField("_window", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(w)!);
    internal static async Task Wait(Window w, Func<bool> predicate, string message)
    {
        var until = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < until) { if (await w.Dispatcher.InvokeAsync(predicate)) return; await Task.Delay(20); }
        throw new TimeoutException(message);
    }
    internal static async Task Frame(Window w)
    {
        var field = typeof(Window).GetField("_lastFrameTime", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var previous = await w.Dispatcher.InvokeAsync(() => (double)field.GetValue(w)!);
        await Wait(w, () => (double)field.GetValue(w)! > previous, "frame presentation");
    }
    internal static async Task Click(Window w, FormX11 x, Func<Element> get)
    {
        await Frame(w);
        var rect = await w.Dispatcher.InvokeAsync(() =>
        {
            var e = get(); e.WindowToLocal(0, 0, out var px, out var py);
            return new RectF(-px, -py, e.Bounds.W, e.Bounds.H);
        });
        x.Move((int)rect.X + Math.Min(12, (int)rect.W / 2), (int)rect.Y + Math.Min(12, (int)rect.H / 2)); x.Click(); await Task.Delay(40);
    }
    internal static async Task Type(Window w, FormX11 x, Func<TextBox> get, string text)
    {
        await Click(w, x, () => get()); x.Chord(0xffe3, 'a');
        foreach (var c in text) { if (char.IsUpper(c)) x.Chord(0xffe1, char.ToLowerInvariant(c)); else x.Key(c); }
        await Task.Delay(60);
    }
    internal static async Task Capture(Window w, FormX11 x, string name, int width, int height)
    { await Frame(w); x.Capture(name, width, height); }

    internal static void OnOwner(Action action, int timeoutSeconds = 60)
    {
        Exception? error = null;
        var thread = new System.Threading.Thread(() => { try { action(); } catch (Exception e) { error = e; } }) { IsBackground = true };
        thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(timeoutSeconds)), "Owned Window operation must finish before the bounded review timeout.");
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}
