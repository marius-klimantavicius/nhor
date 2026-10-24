using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Marius.Winter.Tests;

public class TreeRetryReentrancyTests
{
    private sealed record Item(string Key, bool Lazy = true);

    [Theory]
    [InlineData("collapse", false)]
    [InlineData("synchronize", false)]
    [InlineData("replace", false)]
    [InlineData("dispose", false)]
    [InlineData("collapse", true)]
    [InlineData("replace", true)]
    [InlineData("dispose", true)]
    public void NativeRetryDoesNotReviveWorkRejectedDuringNotification(string change, bool duringLoading)
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(500, 320, "Retry application reentrancy", Theme.Light, RenderBackend.SW);
            int loads = 0;
            using var controller = new TreeController<Item>(i => i.Key, i => i.Key, mayHaveChildren: i => i.Lazy,
                loadChildren: (_, _) => ++loads == 1
                    ? Task.FromException<IReadOnlyList<Item>>(new InvalidOperationException("Folder temporarily unavailable"))
                    : Task.FromResult<IReadOnlyList<Item>>([new("child", false)]));
            controller.SetItems([new("p")]);
            using var tree = new DataTree<Item>(controller);
            window.AddChild(tree);
            tree.ExpandAsync("p").GetAwaiter().GetResult();
            Assert.Equal(TreeLoadState.Error, controller.GetNode("p")!.LoadState);
            bool rejecting = false;
            void Reject()
            {
                if (rejecting) return;
                rejecting = true;
                if (change == "collapse") tree.Collapse("p");
                else if (change == "synchronize") tree.Synchronize(null, []);
                else if (change == "replace") controller.SetItems([new("replacement", false)], null, []);
                else controller.Dispose();
            }
            void Requested(TreeRequest<Item> request) { if (request.Kind == TreeRequestKind.Retry) Reject(); }
            void Changed() { if (controller.GetNode("p")?.LoadState == TreeLoadState.Loading) Reject(); }
            if (duringLoading) controller.Changed += Changed;
            else tree.Requested += Requested;
            tree.RetryAsync("p").GetAwaiter().GetResult();
            tree.Requested -= Requested; controller.Changed -= Changed;
            Assert.True(rejecting);
            Assert.Equal(1, loads); // No application loader begins after rejection.
            Assert.DoesNotContain(controller.Roots, n => n.LoadState == TreeLoadState.Loading);
            Assert.Null(controller.GetNode("child"));
            if (change is "collapse" or "synchronize")
            {
                Assert.DoesNotContain("p", controller.ExpandedKeys);
                tree.ExpandAsync("p").GetAwaiter().GetResult();
                // A rejected Retry keeps its ordinary error; retrying explicitly
                // remains usable and must not encounter an already disposed CTS.
                tree.RetryAsync("p").GetAwaiter().GetResult();
                Assert.Equal(2, loads);
                Assert.Equal(TreeLoadState.Loaded, controller.GetNode("p")!.LoadState);
            }
            Assert.Null(Record.Exception(controller.Dispose));
        });
    }
}
