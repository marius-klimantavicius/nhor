using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using Xunit;

namespace Marius.Winter.Tests;

internal static class WindowRegressionOwner
{
    internal static void Run(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } }) { IsBackground = true };
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(12)), "Window owner timed out; prerequisite failure, not a product assertion RED.");
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    internal static void Flush(Window window) => typeof(Window)
        .GetMethod("FlushLayout", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
}
