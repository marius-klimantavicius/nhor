using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.AspNetCore.Components;
using PickerCallbackIdentity = Marius.Winter.Blazor.Elements.PickerCallbackIdentity;
namespace Marius.Winter.Blazor.Tests;
[Collection("Native rendering")]
public class PathPickerCallbackTests
{
    [Fact]
    public void PickerHelperRetainsEquivalentExplicitReceiversForOpenAndResultCallbacks()
    {
        var receiver = new CallbackReceiver(); var other = new CallbackReceiver(); var target = new CallbackTarget();
        var open = new EventCallback<bool>(receiver, new Action<bool>(target.Open));
        var equivalentOpen = new EventCallback<bool>(receiver, new Action<bool>(target.Open));
        Assert.True(PickerCallbackIdentity.Same(open, equivalentOpen));
        Assert.False(PickerCallbackIdentity.Same(open, new EventCallback<bool>(other, new Action<bool>(target.Open))));
        Assert.False(PickerCallbackIdentity.Same(open, new EventCallback<bool>(receiver, new Action<bool>(new CallbackTarget().Open))));
        Assert.False(PickerCallbackIdentity.Same(open, new EventCallback<bool>(receiver, new Action<bool>(target.OtherOpen))));
        Assert.False(PickerCallbackIdentity.Same(open, default));
        var result = new EventCallback<FilePickerResult>(receiver, new Action<FilePickerResult>(target.Result));
        Assert.True(PickerCallbackIdentity.Same(result, new EventCallback<FilePickerResult>(receiver, new Action<FilePickerResult>(target.Result))));
        Assert.False(PickerCallbackIdentity.Same(result, new EventCallback<FilePickerResult>(other, new Action<FilePickerResult>(target.Result))));
        Assert.False(PickerCallbackIdentity.Same(result, default));
    }
    private sealed class CallbackTarget
    {
        internal void Open(bool _) { }
        internal void OtherOpen(bool _) { }
        internal void Result(FilePickerResult _) { }
    }
    private sealed class CallbackReceiver : IHandleEvent
    { public Task HandleEventAsync(EventCallbackWorkItem work, object? arg) => work.InvokeAsync(arg); }

    [Theory]
    [InlineData("reset")] [InlineData("provider")] [InlineData("target")] [InlineData("unmount")]
    public void AwaitingExplicitModelPickerCallbacksCannotCloseOrRetargetANewerSession(string transition) => OnOwner(() =>
    {
        using var window = new Window(640, 480, "C07 awaited result identity", backend: RenderBackend.SW);
        using var renderer = new OwnerRenderer(window); var root = new Panel(); window.AddChild(root);
        var probe = renderer.AddComponent<FilePickerAwaitProbe>(TestRenderer.CreateRootHandler(renderer, root)).GetAwaiter().GetResult();
        var picker = Walk(root).OfType<FilePicker>().Single(); var model = probe.Model;
        renderer.Dispatcher.InvokeAsync(() =>
        {
            probe.Refresh(); // New equivalent explicit receiver delegates on the open picker.
            picker.Controller!.Select(new("/image.svg")); picker.Controller.AcceptAsync().GetAwaiter().GetResult();
        }).GetAwaiter().GetResult();
        Assert.Equal(1, model.Changes); Assert.Single(model.Results); Assert.False(model.Open); Assert.Equal(0, probe.LegacyChanges);
        var session = picker.Controller!.State.SessionId;
        renderer.Dispatcher.InvokeAsync(() =>
        {
            if (transition == "target") probe.Model = new(); else model.Open = true;
            if (transition == "provider") probe.Provider = new();
            if (transition == "unmount") probe.Show = false;
            probe.Version++; probe.Refresh();
        }).GetAwaiter().GetResult();
        model.Gate.SetResult();
        var until = DateTime.UtcNow.AddSeconds(3);
        while (!model.Finished.Task.IsCompleted && DateTime.UtcNow < until)
        { while (window.DispatcherQueue.TryDequeue(out var action)) action(); Thread.Yield(); }
        Assert.True(model.Finished.Task.IsCompletedSuccessfully);
        while (window.DispatcherQueue.TryDequeue(out var action)) action();
        Assert.Equal(1, model.Changes); Assert.Single(model.Results); Assert.Equal(0, probe.LegacyChanges);
        if (transition == "unmount") Assert.Empty(root.Children);
        else
        {
            Assert.True(probe.Model.Open); Assert.True(picker.Dialog.IsOpen); Assert.True(picker.Controller.State.SessionId > session);
            if (transition == "target") { Assert.Equal(0, probe.Model.Changes); Assert.Empty(probe.Model.Results); }
        }
        renderer.Dispatcher.InvokeAsync(() => { probe.Show = false; probe.Refresh(); }).GetAwaiter().GetResult();
    });
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ModelMethodBindingPreservesQueuedEditAcrossOrdinaryRender(bool render) => OnOwner(() =>
    {
        using var window = new Window(640, 480, "C07 independent callback probe", backend: RenderBackend.SW);
        using var renderer = new OwnerRenderer(window);
        var root = new Panel(); window.AddChild(root);
        var probe = renderer.AddComponent<PathPickerCallbackProbe>(TestRenderer.CreateRootHandler(renderer, root)).GetAwaiter().GetResult();
        var path = PathPickerCallbackTests.Walk(root).OfType<PathPicker>().Single();
        renderer.Dispatcher.InvokeAsync(() =>
        {
            path.TextEditor.OnKeyDown(269, 0, false); path.TextEditor.OnTextInput("abcd");
            if (render) probe.Refresh();
            while (window.DispatcherQueue.TryDequeue(out var action)) action();
        }).GetAwaiter().GetResult();
        bool modified = probe.Form.CurrentEditContext.IsModified();
        Console.WriteLine($"render={render}; model={probe.Model.Path}; native={path.Value}; calls={probe.Model.Calls}; modified={modified}; diffs={renderer.AttributeUpdates.Count}");
        renderer.Dispatcher.InvokeAsync(probe.Remove).GetAwaiter().GetResult();
        Assert.Equal(new PickerPath("/originalabcd"), probe.Model.Path);
        Assert.Equal("/originalabcd", path.TextEditor.Text);
        Assert.Equal(1, probe.Model.Calls);
        Assert.True(modified);
        Assert.Contains(renderer.AttributeUpdates, a => a.Name == "Configuration");
    });
    internal static void OnOwner(Action action, int timeoutSeconds = 60)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch(Exception e) { error = e; } }) { IsBackground = true };
        thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(timeoutSeconds)), "Owned Window operation must finish before the bounded review timeout.");
        if(error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
    internal sealed class OwnerRenderer(Window w) : TestRenderer
    { public override Microsoft.AspNetCore.Components.Dispatcher Dispatcher { get; } = new OwnerDispatcher(w); }
    internal sealed class OwnerDispatcher(Window w) : Microsoft.AspNetCore.Components.Dispatcher
    {
        public override bool CheckAccess() => w.Dispatcher.CheckAccess();
        public override Task InvokeAsync(Action a) => w.Dispatcher.InvokeAsync(a);
        public override Task InvokeAsync(Func<Task> a) => w.Dispatcher.InvokeAsync(a);
        public override Task<T> InvokeAsync<T>(Func<T> a) => w.Dispatcher.InvokeAsync(a);
        public override Task<T> InvokeAsync<T>(Func<Task<T>> a) => w.Dispatcher.InvokeAsync(a);
    }
    [Theory]
    [InlineData(RenderBackend.SW, false)]
    [InlineData(RenderBackend.SW, true)]
    [InlineData(RenderBackend.GL, false)]
    [InlineData(RenderBackend.GL, true)]
    public void ActualKeyboardModelMethodBinding(RenderBackend backend, bool render) => OnOwner(() =>
    {
        using var window = new Window(900, 500, "C07 actual callback identity", Theme.Light, backend);
        var host = window.UseBlazor(); var probe = host.AddComponent<PathPickerCallbackProbe>().GetAwaiter().GetResult();
        var path = Walk(window).OfType<PathPicker>().Single();
        // Observe after the native control and wrapper have queued the edit. The
        // observer requests an ordinary unrelated parent render, never delivers an edit.
        if (render) path.TextEditor.UserTextChanged += _ => probe.Refresh();
        string? model = null, native = null; int calls = -1; bool modified = false;
        var input = Task.Run(async () =>
        {
            using var x = new C07X11(Xid(window));
            try
            {
                await Task.Delay(150); x.Focus();
                await Click(window, x, () => path.TextEditor); x.Key(0xff57); x.Key('z');
                await Task.Delay(150);
                await window.Dispatcher.InvokeAsync(() => { model = probe.Model.Path?.Value; native = path.Value?.Value; calls = probe.Model.Calls; modified = probe.Form.CurrentEditContext.IsModified(); });
                await Capture(window, x, $"callback-{backend}-{render}", 900, 500);
            }
            finally { x.CloseNormally(); }
        });
        window.Run(); input.GetAwaiter().GetResult();
        Console.WriteLine($"GUI backend={backend} render={render}; model={model}; native={native}; calls={calls}; modified={modified}; normal_closed={window.IsDisposed}");
        Assert.Equal("/originalz", model); Assert.Equal(1, calls); Assert.True(modified);
        Assert.Equal(model, native);
    });

    [Theory]
    [InlineData("ordinary")] [InlineData("method")] [InlineData("target")]
    [InlineData("receiver")] [InlineData("clear")] [InlineData("reset")]
    [InlineData("form reset")] [InlineData("context")] [InlineData("value")]
    [InlineData("provider")] [InlineData("unmount")]
    public void EquivalentCallbacksPreserveQueuedInputAndRealReplacementsInvalidateIt(string transition) => OnOwner(() =>
    {
        using var window = new Window(640, 480, "C07 callback replacement", backend: RenderBackend.SW);
        using var renderer = new OwnerRenderer(window); var root = new Panel(); window.AddChild(root);
        var probe = renderer.AddComponent<PathPickerIdentityProbe>(TestRenderer.CreateRootHandler(renderer, root)).GetAwaiter().GetResult();
        var originalModel = probe.Model; var context = probe.Context;
        var path = Walk(root).OfType<PathPicker>().Single();
        renderer.Dispatcher.InvokeAsync(() =>
        {
            path.TextEditor.OnKeyDown(269, 0, false); path.TextEditor.OnTextInput("abcd");
            switch (transition)
            {
                case "method": probe.OtherMethod = true; break;
                case "target": probe.Target = new(); break;
                case "receiver": probe.Receiver = new object(); break;
                case "clear": probe.Clear = true; break;
                case "reset": probe.Version++; break;
                case "form reset": probe.Form.ResetAsync().GetAwaiter().GetResult(); break;
                case "context": probe.Context = new(probe.Model); break;
                case "value": probe.Model.Path = new("/parent"); break;
                case "provider": probe.Provider = new(); break;
                case "unmount": probe.Show = false; break;
            }
            probe.Refresh();
            while (window.DispatcherQueue.TryDequeue(out var action)) action();
        }).GetAwaiter().GetResult();
        Assert.Equal(transition == "ordinary" ? 1 : 0, originalModel.Calls);
        Assert.Equal(transition == "ordinary", context.IsModified());
        if (transition != "unmount") Assert.Equal(probe.Model.Path, Walk(root).OfType<PathPicker>().Single().Value);
        renderer.Dispatcher.InvokeAsync(() => { probe.Show = false; probe.Refresh(); }).GetAwaiter().GetResult();
        Assert.Empty(root.Children);
    });

    [Theory]
    [InlineData(RenderBackend.SW)] [InlineData(RenderBackend.GL)]
    public void ActualFourCharacterBurstSurvivesUnrelatedParentRender(RenderBackend backend) => OnOwner(() =>
    {
        using var window = new Window(900, 500, "C07 four queued characters", Theme.Light, backend);
        var host = window.UseBlazor(); var probe = host.AddComponent<PathPickerCallbackProbe>().GetAwaiter().GetResult();
        var path = Walk(window).OfType<PathPicker>().Single();
        using var burstSent = new ManualResetEventSlim(); int characters = 0;
        path.TextEditor.UserTextChanged += _ =>
        {
            Assert.True(burstSent.Wait(TimeSpan.FromSeconds(2)), "The owned input burst must finish.");
            if (++characters == 4) probe.Refresh();
        };
        var input = Task.Run(async () =>
        {
            using var x = new C07X11(Xid(window));
            try
            {
                await Task.Delay(150); x.Focus(); await Click(window, x, () => path.TextEditor); x.Key(0xff57);
                x.TypeBurst("abcd"); burstSent.Set();
                await Wait(window, () => probe.Model.Path?.Value == "/originalabcd", "complete burst bound");
                await window.Dispatcher.InvokeAsync(() =>
                {
                    Assert.Equal(4, characters); Assert.Equal(1, probe.Model.Calls);
                    Assert.True(probe.Form.CurrentEditContext.IsModified()); Assert.Equal(probe.Model.Path, path.Value);
                });
                await Capture(window, x, $"callback-{backend}-four-characters", 900, 500);
                x.Chord(0xffe3, 'a'); x.Chord(0xffe3, 'c'); await Task.Delay(90);
                await window.Dispatcher.InvokeAsync(() =>
                {
                    Assert.Equal("/originalabcd", Glfw.Glfw.glfwGetClipboardString(null));
                    Assert.Equal(1, probe.Model.Calls);
                });
                await Capture(window, x, $"callback-{backend}-four-characters-selection", 900, 500);
            }
            finally { burstSent.Set(); x.CloseNormally(); }
        });
        window.Run(); input.GetAwaiter().GetResult(); Assert.True(window.IsDisposed);
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
    internal static async Task Click(Window w, C07X11 x, Func<Element> get)
    {
        await Frame(w);
        var rect = await w.Dispatcher.InvokeAsync(() =>
        {
            var e = get(); e.WindowToLocal(0, 0, out var px, out var py);
            return new RectF(-px, -py, e.Bounds.W, e.Bounds.H);
        });
        x.Move((int)rect.X + Math.Min(12, (int)rect.W / 2), (int)rect.Y + Math.Min(12, (int)rect.H / 2)); x.Click(); await Task.Delay(40);
    }
    internal static async Task Type(Window w, C07X11 x, Func<TextBox> get, string text)
    {
        await Click(w, x, () => get()); x.Chord(0xffe3, 'a');
        foreach (var c in text) { if (char.IsUpper(c)) x.Chord(0xffe1, char.ToLowerInvariant(c)); else x.Key(c); }
        await Task.Delay(60);
    }
    internal static async Task Capture(Window w, C07X11 x, string name, int width, int height)
    { await Frame(w); x.Capture(name, width, height); }
}
