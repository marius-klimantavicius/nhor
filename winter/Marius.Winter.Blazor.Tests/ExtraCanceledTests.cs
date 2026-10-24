using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
namespace Marius.Winter.Blazor.Tests;
[Collection("Native rendering")]
public class ExtraCanceledTests
{
    [Theory]
    [InlineData("reset", false)] [InlineData("provider", false)] [InlineData("target", false)] [InlineData("unmount", false)]
    [InlineData("reset", true)] [InlineData("provider", true)] [InlineData("target", true)] [InlineData("unmount", true)]
    public void QueuedExplicitCanceledIdentitySurvivesRenderThenAwaitCannotRetarget(string transition, bool unrelatedRender)
    {
        Exception? error = null;
        var owner = new Thread(() =>
        {
            try
            {
                using var window = new Window(640, 480, "C07 canceled callback identity", backend: RenderBackend.SW);
                using var renderer = new Renderer(window); var root = new Panel(); window.AddChild(root);
                var probe = renderer.AddComponent<CancelAwaitProbe>(TestRenderer.CreateRootHandler(renderer, root)).GetAwaiter().GetResult();
                var picker = root.Children.OfType<FilePicker>().Single(); var model = probe.Model;
                Task.Run(() => picker.Controller!.Cancel()).GetAwaiter().GetResult();
                Console.WriteLine($"After controller cancel render={unrelatedRender}: session={picker.Controller!.State.SessionId}; revision={picker.Controller.Revision}; open={picker.Controller.State.IsOpen}; queue={window.DispatcherQueue.Count}");
                if (unrelatedRender) renderer.Dispatcher.InvokeAsync(probe.Refresh).GetAwaiter().GetResult();
                Console.WriteLine($"After parent render={unrelatedRender}: session={picker.Controller!.State.SessionId}; revision={picker.Controller.Revision}; open={picker.Controller.State.IsOpen}; queue={window.DispatcherQueue.Count}");
                Drain(); Assert.Equal(new[] { "open", "cancel" }, model.Order); Assert.False(model.Open); Assert.Equal(0, probe.LegacyChanges);
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
                while (!model.Finished.Task.IsCompleted && DateTime.UtcNow < until) { Drain(); Thread.Yield(); }
                Assert.True(model.Finished.Task.IsCompletedSuccessfully); Drain(); Assert.Equal(new[] { "open", "cancel" }, model.Order);
                if (transition == "unmount") Assert.Empty(root.Children);
                else { Assert.True(probe.Model.Open); Assert.True(picker.Dialog.IsOpen); Assert.True(picker.Controller.State.SessionId > session); if (transition == "target") Assert.Empty(probe.Model.Order); }
                renderer.Dispatcher.InvokeAsync(() => { probe.Show = false; probe.Refresh(); }).GetAwaiter().GetResult();
                Console.WriteLine("Explicit canceled callback: ordinary render preserved queued identity, open-before-cancel once; awaited " + transition + " safe.");
                void Drain() { while (window.DispatcherQueue.TryDequeue(out var action)) action(); }
            }
            catch (Exception e) { error = e; }
        }) { IsBackground = true };
        owner.Start(); Assert.True(owner.Join(TimeSpan.FromSeconds(12))); if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
    sealed class Renderer(Window w) : TestRenderer { public override Microsoft.AspNetCore.Components.Dispatcher Dispatcher { get; } = new Dispatcher(w); }
    sealed class Dispatcher(Window w) : Microsoft.AspNetCore.Components.Dispatcher
    {
        public override bool CheckAccess() => w.Dispatcher.CheckAccess();
        public override Task InvokeAsync(Action a) => w.Dispatcher.InvokeAsync(a);
        public override Task InvokeAsync(Func<Task> a) => w.Dispatcher.InvokeAsync(a);
        public override Task<T> InvokeAsync<T>(Func<T> a) => w.Dispatcher.InvokeAsync(a);
        public override Task<T> InvokeAsync<T>(Func<Task<T>> a) => w.Dispatcher.InvokeAsync(a);
    }
}
