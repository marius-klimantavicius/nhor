using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThorVG;
using Task = System.Threading.Tasks.Task;
using Xunit;
using static Marius.Winter.OperationFeedback.Tests.FeedbackTests;
using static Glfw.GLFW;

namespace Marius.Winter.OperationFeedback.Tests;

public class NativeFeedbackTests
{
    [Fact]
    public void LegacyTextSnapshotRemainsExactAndAppendActivatesDefaultRetention()
    {
        string text = new string('x', 110_000);
        using var log = new LogView { Text = text, FollowTail = false };
        Assert.Equal(text, log.Text);
        log.Append("y");
        Assert.Equal(100_000, log.Text.Length);
        Assert.EndsWith("y", log.Text);
    }

    [Fact]
    public void BufferRetentionHasStableOffsetsAndClearStartsNewDocument()
    {
        var buffer = new LogBuffer(maxLines: 3, maxCharacters: 80);
        int changes = 0;
        buffer.Changed += () => changes++;
        buffer.Append("one\ntwo\nthree\n");
        Assert.Equal("two\nthree\n", buffer.Snapshot.Text); // trailing empty line counts
        Assert.Equal(4, buffer.Snapshot.StartOffset);
        buffer.AppendLine("four");
        Assert.Equal("three\nfour\n", buffer.Snapshot.Text);
        Assert.Equal(8, buffer.Snapshot.StartOffset);
        var old = buffer.Snapshot;
        buffer.Clear();
        Assert.Empty(buffer.Snapshot.Text);
        Assert.True(buffer.Snapshot.ResetId > old.ResetId);
        Assert.Equal(0, buffer.Snapshot.StartOffset);
        buffer.Append("");
        Assert.Equal(3, changes);
    }

    [Theory]
    [InlineData("prefix-éabc", 4, "abc")]
    [InlineData("prefix-👩‍💻ok", 5, "ok")]
    [InlineData("very-long-legitimate-line", 4, "line")]
    public void CharacterRetentionNeverSplitsGraphemes(string text, int limit, string expected)
    {
        var buffer = new LogBuffer(10, limit);
        buffer.Append(text);
        Assert.Equal(expected, buffer.Snapshot.Text);
        Assert.Equal(text.Length - expected.Length, buffer.Snapshot.StartOffset);
    }

    [Fact]
    public void AppendShiftsRetainedSelectionAndCollapsesDiscardedSelection()
    {
        var buffer = new LogBuffer(3, 100);
        buffer.Append("aaa\nbbb\nccc");
        using var log = new LogView { Buffer = buffer, FollowTail = false };
        Set(log, "_selAnchor", 4);
        Set(log, "_selEnd", 7);
        log.Append("\nddd");
        Assert.Equal("bbb\nccc\nddd", log.Text);
        Assert.Equal(0, log.SelectionStart);
        Assert.Equal("bbb", log.SelectedText);
        log.Append("\neee");
        Assert.Empty(log.SelectedText);
        Assert.Equal(0, log.SelectionLength);
        log.Clear();
        Assert.Equal(0, log.SelectionLength);
    }

    [Fact]
    public void BufferWorkerNotificationsDispatchAndObsoleteSubscriptionsCannotOverwriteReplacement()
    {
        OnOwner(() =>
        {
            using var window = new Window(500, 260, "C08 subscriptions");
            var a = new LogBuffer(); var b = new LogBuffer();
            a.Append("a"); b.Append("b");
            using var log = new LogView { Buffer = a };
            window.AddChild(log);
            Task.Run(() => a.Append(" worker")).GetAwaiter().GetResult();
            Assert.Equal("a", log.Text); // no worker-side paint/state mutation
            log.Buffer = b;
            Pump(window);
            Assert.Equal("b", log.Text);
            window.RemoveChild(log);
            Task.Run(() => b.Append(" detached")).GetAwaiter().GetResult();
            Pump(window);
            Assert.Equal("b", log.Text);
            window.AddChild(log);
            Assert.Equal("b detached", log.Text);
            log.Dispose();
            b.Append(" survives");
            Assert.Equal("b detached survives", b.Snapshot.Text);
        });
    }

    [Fact]
    public void TailFollowInspectionRetentionAndResizeKeepTheirPolicy()
    {
        OnOwner(() =>
        {
            using var window = new Window(500, 300, "C08 tail");
            var buffer = new LogBuffer(100, 6000);
            buffer.Append(string.Join("\n", Enumerable.Range(1, 50).Select(i => $"line {i}")));
            using var log = new LogView { Buffer = buffer };
            window.AddChild(log);
            log.Arrange(new RectF(0, 0, 450, 180));
            Assert.True(log.ScrollOffset > 0);
            int changes = 0;
            log.FollowTailChanged = _ => changes++;
            log.OnKeyDown(GLFW_KEY_HOME, 0, false);
            Assert.False(log.FollowTail);
            Assert.Equal(0, log.ScrollOffset);
            log.Append("\nnew line");
            Assert.Equal(0, log.ScrollOffset);
            log.OnKeyDown(GLFW_KEY_PAGE_DOWN, 0, false);
            float earlier = log.ScrollOffset;
            log.Append("\nnewer line");
            Assert.Equal(earlier, log.ScrollOffset);
            log.JumpToLatest();
            Assert.True(log.FollowTail);
            Assert.True(log.ScrollOffset > earlier);
            float beforeResize = log.ScrollOffset;
            log.Arrange(new RectF(0, 0, 450, 100));
            Assert.True(log.ScrollOffset > beforeResize);
            log.FollowTail = false; // synchronization must not echo
            Assert.Equal(2, changes);
        });
    }

    [Fact]
    public void BusyAnimationIsBoundedAndStopsForPauseAncestorHideDetachAndDispose()
    {
        OnOwner(() =>
        {
            using var window = new Window(500, 250, "C08 progress");
            using var parent = new Panel();
            using var bar = new ProgressBar { IsIndeterminate = true };
            parent.AddChild(bar);
            window.AddChild(parent);
            bar.Arrange(new RectF(0, 0, 240, 18));
            Assert.True(window.Animator.HasActiveAnimations);
            for (int i = 0; i < 40; i++)
            {
                window.Animator.Tick(.1f);
                var shape = Get<Shape>(bar, "_fillShape");
                shape.Bounds(out float x, out _, out float w, out _);
                Assert.InRange(x, 1.99f, 240);
                Assert.InRange(x + w, 2, 238.01f);
            }
            bar.IsPaused = true;
            Assert.False(window.Animator.HasActiveAnimations);
            float pausedPhase = Get<float>(bar, "_phase");
            bar.IsPaused = false;
            window.Animator.Tick(0);
            Assert.Equal(pausedPhase, Get<float>(bar, "_phase"));
            parent.Visible = false;
            Assert.False(window.Animator.HasActiveAnimations);
            parent.Visible = true;
            Assert.True(window.Animator.HasActiveAnimations);
            window.RemoveChild(parent);
            Assert.False(window.Animator.HasActiveAnimations);
            window.AddChild(parent);
            Assert.True(window.Animator.HasActiveAnimations);
            bar.IsIndeterminate = false;
            Assert.False(window.Animator.HasActiveAnimations);
            bar.Value = .5f;
            Get<Shape>(bar, "_fillShape").Bounds(out _, out _, out float width, out _);
            Assert.Equal(118, width, 2);
            bar.IsIndeterminate = true;
            bar.Dispose();
            Assert.False(window.Animator.HasActiveAnimations);
        });
    }

    [Fact]
    public void StatusActionsStayVisibleGuardDuplicatesAndReturnTheRealTask()
    {
        OnOwner(() =>
        {
            using var window = new Window(500, 300, "C08 actions");
            using var panel = new StatusPanel { Status = new("Failed", OperationState.Failed, StatusSeverity.Error) };
            window.AddChild(panel);
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int calls = 0;
            panel.Retry = () => { calls++; return completion.Task; };
            var task = panel.RetryAsync();
            Assert.False(task.IsCompleted);
            panel.RetryAsync().GetAwaiter().GetResult();
            Assert.Equal(1, calls);
            var actions = panel.Children.OfType<Panel>().Single().Children.OfType<Button>().ToArray();
            Assert.All(actions, button => { Assert.True(button.Visible); Assert.False(button.Enabled); });
            panel.Status = new("New run", OperationState.Working);
            completion.SetResult();
            PumpUntil(window, task);
            Assert.Equal("New run", panel.Status.Message); // task completion cannot replace app status
            Assert.True(actions.Single(b => b.Text == "Cancel").Visible);
            panel.Cancel = () => Task.FromException(new InvalidOperationException("ordinary action failure"));
            var error = panel.CancelAsync();
            PumpUntil(window, error, expectFailure: true);
            Assert.IsType<InvalidOperationException>(panel.ActionError);
            Assert.Contains(panel.Children.OfType<Label>(), l => l.Text.Contains("ordinary action failure"));
        });
    }

    [Fact]
    public void AwaitingRetryKeepsCooperativeCancelAvailable()
    {
        OnOwner(() =>
        {
            using var window = new Window(500, 300, "C08 retry cancellation");
            using var panel = new StatusPanel { Status = new("Failed", OperationState.Failed) };
            window.AddChild(panel);
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            panel.Retry = () => { panel.Status = new("Working", OperationState.Working); return completion.Task; };
            int cancels = 0;
            panel.Cancel = () => { cancels++; completion.SetResult(); return Task.CompletedTask; };
            var retry = panel.RetryAsync();
            Assert.False(retry.IsCompleted);
            var cancel = panel.Children.OfType<Panel>().Single().Children.OfType<Button>().Single(b => b.Text == "Cancel");
            Assert.True(cancel.Enabled);
            PumpUntil(window, panel.CancelAsync());
            PumpUntil(window, retry);
            Assert.Equal(1, cancels);
        });
    }

    [Fact]
    public void StaleActionFailureCannotReplaceNewerStatusOrCallback()
    {
        OnOwner(() =>
        {
            using var window = new Window(500, 300, "C08 action generation");
            using var panel = new StatusPanel { Status = new("Failed", OperationState.Failed) };
            window.AddChild(panel);
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            panel.Retry = () => completion.Task;
            var retry = panel.RetryAsync();
            panel.Status = new("Newer completed result", OperationState.Completed, StatusSeverity.Success, 1);
            panel.Retry = () => Task.CompletedTask;
            completion.SetException(new InvalidOperationException("obsolete failure"));
            PumpUntil(window, retry, expectFailure: true);
            Assert.Null(panel.ActionError);
            Assert.Contains(panel.Children.OfType<Label>(), l => l.Text == "Success: Newer completed result");
        });
    }

    internal static void Pump(Window window) { while (window.DispatcherQueue.TryDequeue(out var action)) action(); }
    internal static void PumpUntil(Window window, Task task, bool expectFailure = false)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!task.IsCompleted && DateTime.UtcNow < deadline) { Pump(window); Thread.Sleep(1); }
        Assert.True(task.IsCompleted, "Cooperative task did not settle.");
        if (expectFailure) Assert.True(task.IsFaulted); else task.GetAwaiter().GetResult();
    }
}
