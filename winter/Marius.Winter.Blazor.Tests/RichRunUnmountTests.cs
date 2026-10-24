using System;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using Marius.Winter.Blazor.Core;
using ThorVG;
using Xunit;

namespace Marius.Winter.Blazor.Tests;

[Collection("Native rendering")]
public class RichRunUnmountTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CompiledBindingCanUnmountNewRunsBeforeNextDraw(bool production, bool restore) => OnOwner(() =>
    {
        using var window = new Window(500, 300, "Bound rich same-turn unmount", Theme.Light, RenderBackend.SW);
        using var root = new Panel(); if (!production) window.AddChild(root);
        using var test = production ? null : new TestRenderer();
        var host = production ? window.UseBlazor() : null;
        NativeComponentRenderer renderer = production ? host!.Renderer : test!;
        var probe = production ? host!.AddComponent<RichDetachProbe>().GetAwaiter().GetResult() :
            test!.AddComponent<RichDetachProbe>(TestRenderer.CreateRootHandler(test, root)).GetAwaiter().GetResult();
        var editor = production ? window.Children.OfType<RichTextBox>().Single() : (RichTextBox)root.Children.Single();
        editor.Arrange(new RectF(10, 10, 480, 220));
        var canvas = (Canvas)typeof(Window).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        Draw(canvas);
        renderer.Dispatcher.InvokeAsync(() =>
        {
            if (restore) editor.Document = RichDocument.FromJson(new RichDocument(editor.Text)
                .WithStyle(11, 12, new(TextFormat.Bold, new Color4(.1f, .2f, .8f))).ToJson());
            else { editor.Select(11, 12); editor.ToggleBold(); }
        }).GetAwaiter().GetResult();
        var error = Record.Exception(() => renderer.Dispatcher.InvokeAsync(probe.Remove).GetAwaiter().GetResult());
        Console.WriteLine($"compiled rich production={production} restore={restore} unmount: {error?.ToString() ?? "ok"}");
        Assert.Null(error);
        Assert.True(editor.IsDisposed);
        Assert.DoesNotContain(production ? window.Children : root.Children, child => child is RichTextBox);
        // Pending formatting/edit deliveries must not revive the removed binding.
        renderer.Dispatcher.InvokeAsync(() => { while (window.DispatcherQueue.TryDequeue(out var action)) action(); }).GetAwaiter().GetResult();
        Assert.Equal(new RichDocument("Review the blue horizon."), probe.Document);
        Draw(canvas);
    });

    private static void Draw(Canvas canvas)
    {
        Assert.Equal(Result.Success, canvas.Update()); Assert.Equal(Result.Success, canvas.Draw(true)); Assert.Equal(Result.Success, canvas.Sync());
    }
    internal static void OnOwner(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } }) { IsBackground = true };
        thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Owned Window prerequisite timeout.");
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
