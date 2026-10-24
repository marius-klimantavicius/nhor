using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Marius.Winter.Blazor.Tests;

[Collection("Native rendering")]
public class TreeDeliveryLifetimeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void QueuedSelectionDropsExplicitlyReplacedOrClearedSubscription(bool productionHost, bool clear)
        => WithHost(productionHost, (window, probe, tree) =>
        {
            tree.Controller!.Select("a");
            probe.ReplaceSelection(clear);
            Drain(window);
            Assert.Equal(0, probe.OldCalls);
            Assert.Equal(0, probe.NewCalls); // Old action must not be retargeted.
            Assert.Equal(0, probe.Requests);
            tree.Controller.Select("b");
            Drain(window);
            Assert.Equal(clear ? 0 : 1, probe.NewCalls);
            Assert.Equal(1, probe.Requests);
        });

    [Theory]
    [InlineData(false, "reset", TreeRequestKind.Select)]
    [InlineData(false, "unmount", TreeRequestKind.Select)]
    [InlineData(false, "data", TreeRequestKind.Expand)]
    [InlineData(false, "reset", TreeRequestKind.Activate)]
    [InlineData(true, "reset", TreeRequestKind.Select)]
    [InlineData(true, "unmount", TreeRequestKind.Select)]
    [InlineData(true, "data", TreeRequestKind.Expand)]
    [InlineData(true, "reset", TreeRequestKind.Activate)]
    public void AwaitedActionDoesNotStartLateRequestAfterInvalidation(bool productionHost, string reason, TreeRequestKind kind)
        => WithHost(productionHost, (window, probe, tree) =>
        {
            probe.Wait = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Request(tree, kind);
            Drain(window);
            Assert.Equal(1, probe.OldCalls + probe.Expansions + probe.Activations);
            Assert.Equal(0, probe.Requests);
            probe.Invalidate(reason);
            probe.Wait.SetResult();
            // A UI-dispatcher barrier after resuming the renderer ensures that
            // the awaited wrapper continuation has run before checking absence.
            Settle(window);
            Assert.Equal(0, probe.Requests);
            Assert.Null(probe.Selected);
            Assert.Equal(reason == "unmount", tree.IsDisposed);
        });

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void AwaitedActionKeepsUnchangedSubscriptionsButDropsChangedRequest(bool productionHost, bool replaceRequest)
        => WithHost(productionHost, (window, probe, tree) =>
        {
            probe.Wait = new(TaskCreationOptions.RunContinuationsAsynchronously);
            tree.Controller!.Select("a"); Drain(window);
            if (replaceRequest) probe.ReplaceRequest(); else probe.RenderAgain();
            probe.Wait.SetResult(); Settle(window);
            Assert.Equal(1, probe.OldCalls);
            Assert.Equal(replaceRequest ? 0 : 1, probe.Requests);
            Assert.Equal(0, probe.NewRequests);
            Assert.Equal("a", probe.Selected);
        });

    private static void Request(DataTree<TreeBindingItem> tree, TreeRequestKind kind)
    {
        if (kind == TreeRequestKind.Select) tree.Controller!.Select("a");
        else if (kind == TreeRequestKind.Expand) _ = tree.ExpandAsync("a");
        else tree.Controller!.Activate("a");
    }

    private static void WithHost(bool production, Action<Window, TreeDeliveryProbe, DataTree<TreeBindingItem>> action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var window = new Window(500, 320, "Tree delivery lifetime", Theme.Light, RenderBackend.SW);
                if (production)
                {
                    var host = window.UseBlazor();
                    var probe = host.AddComponent<TreeDeliveryProbe>().GetAwaiter().GetResult();
                    action(window, probe, Assert.IsType<DataTree<TreeBindingItem>>(window.Children[0]));
                }
                else
                {
                    using var root = new Panel(); window.AddChild(root);
                    using var renderer = new TestRenderer();
                    var probe = renderer.AddComponent<TreeDeliveryProbe>(TestRenderer.CreateRootHandler(renderer, root)).GetAwaiter().GetResult();
                    action(window, probe, Assert.IsType<DataTree<TreeBindingItem>>(root.Children.Single()));
                }
            }
            catch (Exception ex) { failure = ex; }
        }) { IsBackground = true };
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(12)), "Owned Window prerequisite timeout.");
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void Drain(Window window)
    {
        int count = 0;
        while (window.DispatcherQueue.TryDequeue(out var work)) { Assert.True(++count < 1000); work(); }
    }
    private static void Settle(Window window)
    {
        // Renderer continuations resume on its synchronization context, which
        // may enqueue back onto the owning Window. Give both queues a barrier.
        for (int i = 0; i < 40; i++) { Drain(window); Thread.Sleep(5); }
        Drain(window);
    }
}
