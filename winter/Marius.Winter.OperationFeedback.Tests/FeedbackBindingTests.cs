using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;
using B = Marius.Winter.Blazor.Elements;

namespace Marius.Winter.Blazor.Tests;

public class FeedbackBindingTests
{
    [Fact]
    public async Task ActualRazorBindingDiffsResetAndReplaceWithoutEchoOrLostIdentity()
    {
        using var renderer = new TestRenderer();
        using var root = new Panel();
        var host = await renderer.AddComponent<BoundFeedbackHost>(TestRenderer.CreateRootHandler(renderer, root));
        var log = Assert.IsType<LogView>(root.Children[0]);
        var bar = Assert.IsType<ProgressBar>(root.Children[1]);
        var status = Assert.IsType<StatusPanel>(root.Children[2]);
        await renderer.Dispatcher.InvokeAsync(() => log.OnKeyDown(Glfw.GLFW.GLFW_KEY_HOME, 0, false));
        Assert.False(host._follow);
        Assert.False(log.FollowTail);
        await renderer.Dispatcher.InvokeAsync(() => { host._follow = true; host._busy = true; host._status = new("failed", OperationState.Failed); host.Render(); });
        Assert.True(log.FollowTail);
        Assert.True(bar.IsIndeterminate);
        await renderer.Dispatcher.InvokeAsync(status.RetryAsync);
        Assert.Equal(1, host._retries);
        var replacement = new LogBuffer(); replacement.Append("replacement");
        await renderer.Dispatcher.InvokeAsync(() => { host._buffer = replacement; host._busy = false; host._progress = .7f; host._paused = true; host._showProgress = false; host.Render(); });
        Assert.Same(log, root.Children[0]);
        Assert.Equal("replacement", log.Text);
        Assert.False(bar.IsIndeterminate);
        Assert.True(bar.IsPaused);
        Assert.True(status.IsProgressPaused);
        Assert.False(status.ShowProgress);
        Assert.Equal(.7f, bar.Value);
        Assert.True(renderer.ConfigurationDiffs >= 3);
        await renderer.Dispatcher.InvokeAsync(() => { host._key++; host.Render(); });
        Assert.True(log.IsDisposed);
        Assert.NotSame(log, root.Children[0]);
        Assert.Equal("replacement", Assert.IsType<LogView>(root.Children[0]).Text);
    }

    [Fact]
    public async Task ConventionalCallbackWinsLegacyFallsBackAndParentRejectionSurvivesAwait()
    {
        using var renderer = new TestRenderer();
        using var root = new Panel();
        var host = await renderer.AddComponent<CallbackHost>(TestRenderer.CreateRootHandler(renderer, root));
        var log = Assert.IsType<LogView>(Assert.Single(root.Children));
        await renderer.Dispatcher.InvokeAsync(() => log.OnKeyDown(Glfw.GLFW.GLFW_KEY_HOME, 0, false));
        Assert.Equal(1, host.Changed);
        Assert.Equal(0, host.Legacy);
        // The parent deliberately rejects the edit with its original true value.
        await renderer.Dispatcher.InvokeAsync(host.Render);
        Assert.True(log.FollowTail);
        host.Completion.SetResult();
        await Task.Yield();
        Assert.True(log.FollowTail);
        await renderer.Dispatcher.InvokeAsync(() => { host.UseChanged = false; host.Render(); });
        await renderer.Dispatcher.InvokeAsync(() => log.OnKeyDown(Glfw.GLFW.GLFW_KEY_HOME, 0, false));
        Assert.Equal(1, host.Changed);
        Assert.Equal(1, host.Legacy);
        await renderer.Dispatcher.InvokeAsync(() => { host.UseLegacy = false; host.Render(); });
        await renderer.Dispatcher.InvokeAsync(log.JumpToLatest);
        Assert.Equal(1, host.Legacy);
    }

    [Fact]
    public void ClearingOptionalFontAndColorResumesOwningThemeMetrics()
    {
        Marius.Winter.OperationFeedback.Tests.FeedbackTests.OnOwner(() =>
        {
            using var window = new Window(500, 300, "C08 renderer font reset", Theme.Light);
            var host = window.UseBlazor().AddComponent<BoundFeedbackHost>().GetAwaiter().GetResult();
            var log = Assert.IsType<LogView>(window.Children[0]);
            window.Dispatcher.InvokeAsync(() => { host._font = 30; host._color = new Color4(1f, 0f, 0f, 1f); host.Render(); }).GetAwaiter().GetResult();
            Assert.Equal(30, log.FontSize);
            Assert.True(log.Color.HasValue);
            window.Theme.TextBox.FontSize = 22;
            window.Dispatcher.InvokeAsync(() => { host._font = null; host._color = null; host.Render(); }).GetAwaiter().GetResult();
            Assert.Equal(22, log.FontSize);
            Assert.Null(log.Color);
            window.Theme.TextBox.FontSize = 26;
            Assert.Equal(26, log.FontSize);
        });
    }

    public sealed class CallbackHost : ComponentBase
    {
        internal int Changed, Legacy;
        internal bool UseChanged = true, UseLegacy = true;
        internal readonly TaskCompletionSource Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Render() => StateHasChanged();
        protected override void BuildRenderTree(RenderTreeBuilder b)
        {
            b.OpenComponent<B.LogView>(0);
            b.AddAttribute(1, "FollowTail", true);
            b.AddAttribute(2, "FollowTailChanged", UseChanged ? EventCallback.Factory.Create<bool>(this, async value => { Changed++; await Completion.Task; }) : default);
            b.AddAttribute(3, "OnFollowTailChanged", UseLegacy ? EventCallback.Factory.Create<bool>(this, value => Legacy++) : default);
            b.CloseComponent();
        }
    }
}
