using System;
using System.Reflection;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Marius.Winter.OperationFeedback.Tests;

public class FeedbackTests
{
    // Before Append exists this exercises the compatible whole-string append
    // used by consumers. The assertion captures the required inspection behavior.
    [Fact]
    public void AppendPreservesSelectionWhileInspectingEarlierText()
    {
        using var log = new LogView { Text = "first line\nsecond line\n", };
        Set(log, "_selAnchor", 1);
        Set(log, "_selEnd", 5);
        typeof(LogView).GetProperty("FollowTail")?.SetValue(log, false);
        var append = typeof(LogView).GetMethod("Append", new[] { typeof(string) });
        if (append == null) log.Text += "third line\n";
        else append.Invoke(log, new object[] { "third line\n" });
        Assert.Equal("first line\nsecond line\nthird line\n", log.Text);
        Assert.Equal(1, Get<int>(log, "_selAnchor"));
        Assert.Equal(5, Get<int>(log, "_selEnd"));
    }

    [Fact]
    public void LogThemeRecomputesInheritedFontMetrics()
    {
        OnOwner(() =>
        {
            using var window = new Window(400, 240, "C08 font contract", Theme.Light, RenderBackend.SW);
            using var log = new LogView { Text = "Legitimate é 👩‍💻 log\nnext" };
            window.AddChild(log);
            float before = Get<float>(log, "_lineHeight");
            float original = window.Theme.TextBox.FontSize;
            try
            {
                window.Theme.TextBox.FontSize = original + 8;
                Assert.True(Get<float>(log, "_lineHeight") > before, "Inherited log metrics must follow a larger theme font.");
            }
            finally { window.Theme.TextBox.FontSize = original; }
        });
    }

    internal static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
    internal static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    internal static void OnOwner(Action action)
    {
        Exception? failure = null;
        var thread = new System.Threading.Thread(() => { try { action(); } catch (Exception e) { failure = e; } }) { IsBackground = true };
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Owned Window test timed out (setup, not behavioral RED).");
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
