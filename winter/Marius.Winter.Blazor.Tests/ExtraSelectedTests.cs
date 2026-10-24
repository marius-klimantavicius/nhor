using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
namespace Marius.Winter.Blazor.Tests;
public sealed class SelectGateProvider : IFilePickerProvider
{
    public TaskCompletionSource<FilePickerEntry?> Stat = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public PickerPath Normalize(string p) => new(p);
    public PickerPath? Parent(PickerPath p) => null;
    public PickerPath Child(PickerPath p, string n) => new("/" + n);
    public Task<IReadOnlyList<FilePickerEntry>> ListAsync(PickerPath p, CancellationToken token) => Task.FromResult<IReadOnlyList<FilePickerEntry>>([new(new("/image.svg"), "image.svg", false)]);
    public Task<FilePickerEntry?> GetEntryAsync(PickerPath p, CancellationToken token) => Stat.Task;
}
[Collection("Native rendering")]
public class ExtraSelectedTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public void AsyncProviderSelectionMustSurviveUnrelatedRender(bool render)
    {
        Exception? error = null;
        var owner = new Thread(() =>
        {
            try
            {
                using var window = new Window(640, 480, "C07 async result render", backend: RenderBackend.SW);
                using var renderer = new Renderer(window); var root = new Panel(); window.AddChild(root);
                var probe = renderer.AddComponent<SelectAwaitProbe>(TestRenderer.CreateRootHandler(renderer, root)).GetAwaiter().GetResult();
                var picker = root.Children.OfType<FilePicker>().Single();
                picker.Controller!.Select(new("/image.svg")); var pending = picker.Controller.AcceptAsync();
                Task.Run(() => probe.Provider.Stat.SetResult(new(new("/image.svg"), "image.svg", false))).GetAwaiter().GetResult();
                Assert.True(pending.Wait(TimeSpan.FromSeconds(3)));
                var oldSession = picker.Controller.State.SessionId;
                Console.WriteLine($"Accepted provider result before parent render={render}: open={picker.Controller.State.IsOpen}; session={oldSession}; queued={window.DispatcherQueue.Count}");
                if (render) renderer.Dispatcher.InvokeAsync(probe.Refresh).GetAwaiter().GetResult();
                while (window.DispatcherQueue.TryDequeue(out var action)) action();
                Console.WriteLine($"After drain render={render}: model_open={probe.Model.Open}; changes={probe.Model.Changes}; selected={probe.Model.Results}; session={picker.Controller.State.SessionId}; diffs={renderer.AttributeUpdates.Count}");
                Assert.Equal(1, probe.Model.Changes); Assert.Equal(1, probe.Model.Results); Assert.False(probe.Model.Open);
                if (render) Assert.Contains(renderer.AttributeUpdates, a => a.Name == "Text");
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
